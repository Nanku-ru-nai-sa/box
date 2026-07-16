using Godot;
using System;
using System.Collections.Generic;

// CraftingPanel — integrated crafting UI.
// 2x2 is always available in inventory.
// 3x3 overlays when near/right-clicking a crafting table.
// Grid cells are real InventorySlot objects (item + count), so they behave
// like normal inventory slots: stacking, splitting, shift-click, etc.
// On close: 3x3 items return to inventory, 2x2 items stay for next open.
//
// NOTE: Clicks are NOT handled locally. Player owns the cursor/held-item
// state, so clicks are routed to Player via OnSlotClicked, and Player
// manipulates the slot directly via GetSlot(idx).

public partial class CraftingPanel : Control
{
    private Inventory _inventory;

    // Grid storage — items stay here between opens (2x2), cleared on 3x3 close
    private InventorySlot[] _grid2 = new InventorySlot[4];  // 2x2 = 4 slots
    private InventorySlot[] _grid3 = new InventorySlot[9];  // 3x3 = 9 slots

    public int GridSize { get; private set; } = 2;
    private bool _atTable = false;

    private RecipeManager.Recipe _currentMatch = null;

    // Fired when a crafting grid cell is clicked. Player subscribes and
    // handles the actual cursor/held-item interaction.
    // Params: slot index, mouse button, shift held
    public Action<int, MouseButton, bool> OnSlotClicked;
    // Fired after a learned-recipe click successfully consumes ingredients.
    // Params: resultId, resultCount
    public Action<string, int> OnLearnedCraftClicked;
    // Fired when the output slot is clicked. Params: button, shift held
    public Action<MouseButton, bool> OnOutputClicked;

    // ── UI ────────────────────────────────────────────────────────────────────
    private Panel   _bg;

    // Tab buttons
    private Panel   _tabGrid;
    private Panel   _tabLearned;
    private Label   _tabGridIcon;
    private Label   _tabLearnedIcon;

    // Grid view
    private Control       _gridView;
    private Panel[]       _gridSlotPanels  = new Panel[9];
    private TextureRect[] _gridSlotTex     = new TextureRect[9];
    private Label[]       _gridSlotCount   = new Label[9];
    private Panel         _outputSlot;
    private TextureRect   _outputTex;
    private Label         _outputCount;

    // Learned view
    private Control       _learnedView;
    private GridContainer _learnedGrid;

    // Tooltip
    private Panel _tooltip;
    private Label _tooltipTitle;
    private Label _tooltipBody;

    private const int SlotSz  = 56;
    private const int SlotGap = 5;
    private const int PanelW  = 300;

    // =========================================================================
    // INIT
    // =========================================================================

    public void Init(Inventory inventory)
    {
        _inventory = inventory;
        for (int i = 0; i < 4; i++) _grid2[i] = new InventorySlot();
        for (int i = 0; i < 9; i++) _grid3[i] = new InventorySlot();
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(PanelW, 339f);
        MouseFilter       = MouseFilterEnum.Stop;

        BuildBackground();
        BuildTabButtons();
        BuildGridView();
        BuildLearnedView();
        BuildTooltip();
        ShowTab(true);

        if (RecipeManager.Instance != null)
            RecipeManager.Instance.OnLearnedChanged += RefreshLearnedTab;
    }

    // =========================================================================
    // GRID SIZE — called by Player on proximity/table right-click
    // =========================================================================

    public void SetGridSize(int size, bool atTable)
    {
        if (GridSize == size && _atTable == atTable) return;

        // Switching from 3x3 back to 2x2 → return 3x3 items to inventory
        if (GridSize == 3 && size == 2)
            Return3x3ToInventory();

        GridSize  = size;
        _atTable  = atTable;
        _currentMatch = null;

        RebuildGridSlots();
        UpdateOutput();

    }

    // Called when inventory closes while 3x3 is active — return items
    public void OnInventoryClose()
    {
        if (GridSize == 3)
        {
            Return3x3ToInventory();
            GridSize = 2;
            _atTable = false;
            RebuildGridSlots();
            UpdateOutput();
        }
        // 2x2 items stay in grid intentionally
    }

    private void Return3x3ToInventory()
    {
        for (int i = 0; i < 9; i++)
        {
            if (!_grid3[i].IsEmpty)
            {
                _inventory.AddItem(_grid3[i].ItemId, _grid3[i].Count);
                _grid3[i].Clear();
            }
        }
        _inventory.OnInventoryChanged?.Invoke();
    }

    // =========================================================================
    // BUILD UI
    // =========================================================================

    private void BuildBackground()
    {
        _bg = new Panel();
        _bg.AnchorRight  = 1f;
        _bg.AnchorBottom = 1f;
        var s = new StyleBoxFlat();
        s.BgColor              = new Color(0.08f, 0.08f, 0.10f, 0.97f);
        s.BorderColor          = new Color(0.35f, 0.35f, 0.40f);
        s.BorderWidthTop       = 2; s.BorderWidthBottom = 2;
        s.BorderWidthLeft      = 2; s.BorderWidthRight  = 2;
        s.CornerRadiusTopLeft  = 4; s.CornerRadiusTopRight    = 4;
        s.CornerRadiusBottomLeft = 4; s.CornerRadiusBottomRight = 4;
        _bg.AddThemeStyleboxOverride("panel", s);
        AddChild(_bg);
    }

    private void BuildTabButtons()
    {
        float btnSz = 24f;
        float y     = -24f; // sits above the panel, level with the external title

        _tabGrid            = MakeTabPanel();
        _tabGrid.Position   = new Vector2(PanelW - btnSz * 2 - 4f, y);
        _tabGrid.CustomMinimumSize = new Vector2(btnSz, btnSz);
        _tabGridIcon        = MakeTabIcon("⊞");
        _tabGrid.AddChild(_tabGridIcon);
        _tabGrid.GuiInput  += (InputEvent ev) => { if (IsLClick(ev)) ShowTab(true); };
        AddChild(_tabGrid);

        _tabLearned           = MakeTabPanel();
        _tabLearned.Position  = new Vector2(PanelW - btnSz, y);
        _tabLearned.CustomMinimumSize = new Vector2(btnSz, btnSz);
        _tabLearnedIcon       = MakeTabIcon("☰");
        _tabLearned.AddChild(_tabLearnedIcon);
        _tabLearned.GuiInput += (InputEvent ev) => { if (IsLClick(ev)) ShowTab(false); };
        AddChild(_tabLearned);

        var externalTitle = new Label();
        externalTitle.Text = "Craft";
        externalTitle.Position = new Vector2(0f, y);
        externalTitle.AddThemeFontSizeOverride("font_size", 13);
        externalTitle.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
        externalTitle.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(externalTitle);
    }

    private void BuildGridView()
    {
        _gridView           = new Control();
        _gridView.AnchorRight  = 1f;
        _gridView.AnchorBottom = 1f;
        _gridView.OffsetTop    = 14f;
        AddChild(_gridView);

        RebuildGridSlots();
    }

    private void RebuildGridSlots()
    {
        if (_gridView == null) return;
        foreach (Node child in _gridView.GetChildren()) child.QueueFree();

        int   size   = GridSize;
        int   count  = size * size;
        float gridPx = size * SlotSz + (size - 1) * SlotGap;
        float gridX  = (PanelW - gridPx) / 2f;
        float rowH   = SlotSz + SlotGap; // 61 — one inventory row's height

        // 2x2 top row lines up with Armor/Belt (inventory row 1).
        // 3x3 takes the top 3 rows (inventory rows 0-2), so its top row is row 0.
        float startY = (size == 3) ? 0f : rowH;

        var gridContainer = new GridContainer();
        gridContainer.Columns  = size;
        gridContainer.Position = new Vector2(gridX, startY);
        gridContainer.AddThemeConstantOverride("h_separation", SlotGap);
        gridContainer.AddThemeConstantOverride("v_separation", SlotGap);
        _gridView.AddChild(gridContainer);

        for (int i = 0; i < count; i++)
        {
            var slot = MakeCraftSlot(SlotSz);
            var tex  = MakeTexRect();
            var lbl  = MakeCountLbl();
            slot.AddChild(tex); slot.AddChild(lbl);
            _gridSlotPanels[i] = slot;
            _gridSlotTex[i]    = tex;
            _gridSlotCount[i]  = lbl;
            int idx = i;
            slot.GuiInput   += (InputEvent ev) => OnGridSlotInput(ev, idx);
            slot.MouseFilter  = MouseFilterEnum.Stop;
            slot.MouseEntered += () => slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.7f,0.7f,0.75f)));
            slot.MouseExited  += () => slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.3f,0.3f,0.35f)));
            gridContainer.AddChild(slot);
        }

        for (int i = count; i < 9; i++)
        {
            _gridSlotPanels[i] = null;
            _gridSlotTex[i]    = null;
            _gridSlotCount[i]  = null;
        }

        float gridH       = size * SlotSz + (size - 1) * SlotGap;
        float gridBottom  = startY + gridH;
        int   outSz       = SlotSz;
        float outputRowY  = 253f; // hotbar row, relative to _gridView's own offset
        float outX        = (PanelW - outSz) / 2f;

        var arrow = new Label();
        arrow.Text                = "↓";
        arrow.HorizontalAlignment = HorizontalAlignment.Center;
        arrow.Position            = new Vector2(gridX, gridBottom + (outputRowY - gridBottom) / 2f - 8f);
        arrow.CustomMinimumSize   = new Vector2(gridPx, 16f);
        arrow.AddThemeFontSizeOverride("font_size", 14);
        arrow.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        _gridView.AddChild(arrow);

        _outputSlot           = MakeCraftSlot(outSz);
        _outputSlot.Position  = new Vector2(outX, outputRowY);
        _outputTex            = MakeTexRect();
        _outputCount          = MakeCountLbl();
        _outputSlot.AddChild(_outputTex);
        _outputSlot.AddChild(_outputCount);
        _outputSlot.GuiInput   += OnOutputInput;
        _outputSlot.MouseFilter  = MouseFilterEnum.Stop;
        _gridView.AddChild(_outputSlot);

        // Clear button — bottom-left corner of the panel, for now.
        float clearW = 50f, clearH = 22f;
        float panelBottomLocal = 339f - 14f; // panel height minus _gridView's own top offset
        var clearBtn = new Button();
        clearBtn.Text              = "Clear";
        clearBtn.Position          = new Vector2(4f, panelBottomLocal - clearH - 4f);
        clearBtn.CustomMinimumSize = new Vector2(clearW, clearH);
        clearBtn.AddThemeFontSizeOverride("font_size", 10);
        clearBtn.Pressed          += ClearGrid;
        _gridView.AddChild(clearBtn);

        RefreshGridVisuals();
        UpdateOutput();
    }

    private void BuildLearnedView()
    {
        _learnedView              = new Control();
        _learnedView.AnchorRight  = 1f;
        _learnedView.AnchorBottom = 1f;
        _learnedView.OffsetTop    = 14f;
        _learnedView.Visible      = false;

        var scroll               = new ScrollContainer();
        scroll.AnchorRight       = 1f; scroll.AnchorBottom = 1f;
        scroll.OffsetLeft        = 6f; scroll.OffsetRight  = -6f;
        scroll.OffsetTop         = 0f; scroll.OffsetBottom = -4f;
        _learnedView.AddChild(scroll);

        _learnedGrid         = new GridContainer();
        _learnedGrid.Columns = 5;
        _learnedGrid.AddThemeConstantOverride("h_separation", 2);
        _learnedGrid.AddThemeConstantOverride("v_separation", SlotGap);
        scroll.AddChild(_learnedGrid);

        AddChild(_learnedView);
        RefreshLearnedTab();
    }

    private void BuildTooltip()
    {
        _tooltip = new Panel();
        var ts = new StyleBoxFlat();
        ts.BgColor         = new Color(0.08f, 0.08f, 0.12f, 0.97f);
        ts.BorderColor     = new Color(0.45f, 0.45f, 0.55f);
        ts.BorderWidthTop  = 1; ts.BorderWidthBottom = 1;
        ts.BorderWidthLeft = 1; ts.BorderWidthRight  = 1;
        _tooltip.AddThemeStyleboxOverride("panel", ts);
        _tooltip.Visible     = false;
        _tooltip.MouseFilter = MouseFilterEnum.Ignore;
        _tooltip.ZIndex      = 100;

        var vb = new VBoxContainer();
        vb.AnchorRight  = 1f; vb.AnchorBottom = 1f;
        vb.OffsetLeft   = 6f; vb.OffsetRight  = -6f;
        vb.OffsetTop    = 4f; vb.OffsetBottom = -4f;
        vb.AddThemeConstantOverride("separation", 2);
        _tooltip.AddChild(vb);

        _tooltipTitle = new Label();
        _tooltipTitle.AddThemeFontSizeOverride("font_size", 12);
        _tooltipTitle.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.5f));
        vb.AddChild(_tooltipTitle);

        _tooltipBody = new Label();
        _tooltipBody.AddThemeFontSizeOverride("font_size", 11);
        _tooltipBody.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        _tooltipBody.AutowrapMode = TextServer.AutowrapMode.Word;
        vb.AddChild(_tooltipBody);

        AddChild(_tooltip);
    }

    // =========================================================================
    // TABS
    // =========================================================================

    private void ShowTab(bool grid)
    {
        _gridView.Visible    = grid;
        _learnedView.Visible = !grid;
        SetTabActive(_tabGrid,    _tabGridIcon,    grid);
        SetTabActive(_tabLearned, _tabLearnedIcon, !grid);
    }

    private void SetTabActive(Panel tab, Label icon, bool active)
    {
        var s = new StyleBoxFlat();
        s.BgColor         = active ? new Color(0.2f, 0.2f, 0.25f) : new Color(0.12f, 0.12f, 0.15f);
        s.BorderColor     = active ? new Color(0.6f, 0.6f, 0.7f)  : new Color(0.3f, 0.3f, 0.35f);
        s.BorderWidthTop  = 2; s.BorderWidthBottom = 2;
        s.BorderWidthLeft = 2; s.BorderWidthRight  = 2;
        tab.AddThemeStyleboxOverride("panel", s);
        icon.AddThemeColorOverride("font_color",
            active ? new Color(1f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f));
    }

    // =========================================================================
    // GRID ACCESS — used by Player for click/drag placement
    // =========================================================================

    // Direct reference to a grid cell's slot. Player mutates it in place,
    // same as it does with _inventory.Slots[i].
    public InventorySlot GetSlot(int idx)
    {
        var grid = GridSize == 3 ? _grid3 : _grid2;
        if (idx < 0 || idx >= grid.Length) return null;
        return grid[idx];
    }

    public string GetGridItem(int slotIndex)
    {
        var s = GetSlot(slotIndex);
        return (s == null || s.IsEmpty) ? "" : s.ItemId;
    }

    public int GetActiveSlotCount() => GridSize * GridSize;

    public Panel GetSlotPanel(int i) => i < GetActiveSlotCount() ? _gridSlotPanels[i] : null;

    // Places 1 unit of itemId into slotIdx — works whether the cell is empty
    // or already holds a matching stack with room. Used for drag-across placement.
    public bool TryPlaceHeldItem(int slotIdx, string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        var slot = GetSlot(slotIdx);
        if (slot == null) return false;
        if (!slot.IsEmpty && slot.ItemId != itemId) return false;
        if (!slot.IsEmpty && slot.Count >= _inventory.MaxStackSize) return false;
        if (slot.IsEmpty) slot.ItemId = itemId;
        slot.Count++;
        RefreshGridVisuals();
        UpdateOutput();
        return true;
    }

    // Called after Player directly mutates a slot (e.g. via GetSlot) so the
    // visuals/recipe match refresh.
    public void NotifyGridChanged()
    {
        RefreshGridVisuals();
        UpdateOutput();
    }

    // =========================================================================
    // GRID INPUT — just forwards the click to Player, which owns cursor state
    // =========================================================================

    private void OnGridSlotInput(InputEvent ev, int idx)
    {
        if (!(ev is InputEventMouseButton mb && mb.Pressed)) return;
        if (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right)
            OnSlotClicked?.Invoke(idx, mb.ButtonIndex, Input.IsKeyPressed(Key.Shift));
    }

    private void OnOutputInput(InputEvent ev)
    {
        if (!(ev is InputEventMouseButton mb && mb.Pressed)) return;
        if (mb.ButtonIndex == MouseButton.Left)
            OnOutputClicked?.Invoke(mb.ButtonIndex, Input.IsKeyPressed(Key.Shift));
    }

    // =========================================================================
    // CRAFTING LOGIC
    // =========================================================================

    // Peek at the current match's result without consuming anything.
    public bool PeekResult(out string resultId, out int resultCount)
    {
        if (_currentMatch == null) { resultId = ""; resultCount = 0; return false; }
        resultId    = _currentMatch.ResultId;
        resultCount = _currentMatch.ResultCount;
        return true;
    }

    // Consumes one craft's worth of ingredients from the grid and learns the
    // recipe. Does NOT touch the inventory or cursor — caller (Player) decides
    // where the result goes.
    public bool TryConsumeOneCraft(out string resultId, out int resultCount)
    {
        if (_currentMatch == null) { resultId = ""; resultCount = 0; return false; }
        resultId    = _currentMatch.ResultId;
        resultCount = _currentMatch.ResultCount;
        string learnId = _currentMatch.Id;

        ConsumeGrid();
        RecipeManager.Instance?.LearnRecipe(learnId);

        return true;
    }

    // Consumes exactly 1 from each occupied cell (not the whole stack) —
    // matches Minecraft: a stack of ingredients in a cell lasts multiple crafts.
    private void ConsumeGrid()
    {
        var grid = GridSize == 3 ? _grid3 : _grid2;
        foreach (var s in grid)
        {
            if (s.IsEmpty) continue;
            s.Count--;
            if (s.Count <= 0) s.Clear();
        }
        RefreshGridVisuals();
        UpdateOutput();
    }

    private void ClearGrid()
    {
        var grid = GridSize == 3 ? _grid3 : _grid2;
        foreach (var s in grid)
        {
            if (!s.IsEmpty)
            {
                _inventory.AddItem(s.ItemId, s.Count);
                s.Clear();
            }
        }
        _inventory.OnInventoryChanged?.Invoke();
        RefreshGridVisuals();
        UpdateOutput();
    }

    // =========================================================================
    // LEARNED TAB
    // =========================================================================

    private void RefreshLearnedTab()
    {
        if (_learnedGrid == null) return;
        foreach (Node child in _learnedGrid.GetChildren()) child.QueueFree();
        if (RecipeManager.Instance == null) return;

        foreach (var recipe in RecipeManager.Instance.GetLearnedRecipes())
        {
            var btn = MakeCraftSlot(SlotSz);
            btn.MouseFilter = MouseFilterEnum.Stop;

            var tex = MakeTexRect();
            tex.Texture = GetIcon(recipe.ResultId);
            btn.AddChild(tex);

            var lbl = MakeCountLbl();
            lbl.Text = recipe.ResultCount > 1 ? recipe.ResultCount.ToString() : "";
            btn.AddChild(lbl);

            var cap = recipe;
            btn.GuiInput     += (InputEvent ev) => { if (IsLClick(ev)) OnLearnedSlotClicked(cap); };
            btn.MouseEntered += () => ShowRecipeTooltip(cap, btn);
            btn.MouseExited  += HideTooltip;
            _learnedGrid.AddChild(btn);
        }
    }

    private void OnLearnedSlotClicked(RecipeManager.Recipe recipe)
    {
        if (!TryConsumeIngredientsFromInventory(recipe)) return;
        OnLearnedCraftClicked?.Invoke(recipe.ResultId, recipe.ResultCount);
    }

    private bool TryConsumeIngredientsFromInventory(RecipeManager.Recipe recipe)
    {
        var needed = new Dictionary<string, int>();
        if (recipe.Shaped)
        {
            for (int r = 0; r < recipe.Pattern.GetLength(0); r++)
                for (int c = 0; c < recipe.Pattern.GetLength(1); c++)
                {
                    string item = recipe.Pattern[r, c];
                    if (!string.IsNullOrEmpty(item) && item != " ")
                        needed[item] = needed.TryGetValue(item, out int n) ? n + 1 : 1;
                }
        }
        else
        {
            foreach (var item in recipe.Ingredients)
                needed[item] = needed.TryGetValue(item, out int n) ? n + 1 : 1;
        }

        foreach (var kvp in needed)
            if (_inventory.GetItemCount(kvp.Key) < kvp.Value) return false;

        foreach (var kvp in needed)
            _inventory.RemoveItem(kvp.Key, kvp.Value);

        _inventory.OnInventoryChanged?.Invoke();
        return true;
    }

    // =========================================================================
    // VISUALS
    // =========================================================================

    private void RefreshGridVisuals()
    {
        if (_gridSlotPanels == null) return;
        var grid  = GridSize == 3 ? _grid3 : _grid2;
        int count = GridSize * GridSize;
        for (int i = 0; i < count; i++)
        {
            if (_gridSlotTex[i] == null) continue;
            var s = grid[i];
            _gridSlotTex[i].Texture  = s.IsEmpty ? null : GetIcon(s.ItemId);
            _gridSlotCount[i].Text   = (!s.IsEmpty && s.Count > 1) ? s.Count.ToString() : "";
        }
    }

    private string[,] BuildGridArray()
    {
        var grid = GridSize == 3 ? _grid3 : _grid2;
        var arr = new string[GridSize, GridSize];
        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
            {
                var s = grid[r * GridSize + c];
                arr[r, c] = s.IsEmpty ? "" : s.ItemId;
            }
        return arr;
    }

    private void UpdateOutput()
    {
        if (_outputSlot == null) return;
        _currentMatch = RecipeManager.Instance?.FindMatch(BuildGridArray(), GridSize);

        if (_currentMatch != null)
        {
            _outputTex.Texture   = GetIcon(_currentMatch.ResultId);
            _outputCount.Text    = _currentMatch.ResultCount > 1 ? _currentMatch.ResultCount.ToString() : "";
            var hs = new StyleBoxFlat();
            hs.BgColor         = new Color(0.15f, 0.22f, 0.15f, 0.95f);
            hs.BorderColor     = new Color(0.35f, 0.7f, 0.35f);
            hs.BorderWidthTop  = 2; hs.BorderWidthBottom = 2;
            hs.BorderWidthLeft = 2; hs.BorderWidthRight  = 2;
            _outputSlot.AddThemeStyleboxOverride("panel", hs);
        }
        else
        {
            _outputTex.Texture = null;
            _outputCount.Text  = "";
            _outputSlot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.3f, 0.3f, 0.35f)));
        }
    }

    // =========================================================================
    // TOOLTIP
    // =========================================================================

    private void ShowRecipeTooltip(RecipeManager.Recipe recipe, Control anchor)
    {
        _tooltipTitle.Text = recipe.ResultId;
        var ingredients    = RecipeManager.Instance?.GetIngredientDisplay(recipe);
        _tooltipBody.Text  = ingredients != null ? string.Join("\n", ingredients) : "";
        _tooltip.Size      = new Vector2(150f, 0f);
        _tooltip.Visible   = true;
        Vector2 pos        = anchor.GlobalPosition - GlobalPosition;
        pos.X -= 155f;
        pos.Y  = Mathf.Max(0f, pos.Y);
        _tooltip.Position  = pos;
    }

    public void ShowItemTooltip(string itemId, Vector2 screenPos)
    {
        if (string.IsNullOrEmpty(itemId)) { HideTooltip(); return; }
        _tooltipTitle.Text      = itemId;
        _tooltipBody.Text       = "";
        _tooltip.Size           = new Vector2(120f, 0f);
        _tooltip.Visible        = true;
        _tooltip.GlobalPosition = screenPos + new Vector2(10f, -30f);
    }

    public void HideTooltip() => _tooltip.Visible = false;

    // =========================================================================
    // HELPERS
    // =========================================================================

    private Panel MakeTabPanel()
    {
        var p = new Panel(); p.MouseFilter = MouseFilterEnum.Stop; return p;
    }

    private Label MakeTabIcon(string icon)
    {
        var lbl = new Label();
        lbl.Text                = icon;
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        lbl.VerticalAlignment   = VerticalAlignment.Center;
        lbl.AnchorRight         = 1f; lbl.AnchorBottom = 1f;
        lbl.AddThemeFontSizeOverride("font_size", 16);
        lbl.MouseFilter         = MouseFilterEnum.Ignore;
        return lbl;
    }

    private Panel MakeCraftSlot(int size)
    {
        var slot = new Panel();
        slot.CustomMinimumSize = new Vector2(size, size);
        slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.3f, 0.3f, 0.35f)));
        return slot;
    }

    private StyleBoxFlat MakeSlotStyle(Color border)
    {
        var s = new StyleBoxFlat();
        s.BgColor         = new Color(0.12f, 0.12f, 0.15f, 0.95f);
        s.BorderColor     = border;
        s.BorderWidthTop  = 2; s.BorderWidthBottom = 2;
        s.BorderWidthLeft = 2; s.BorderWidthRight  = 2;
        s.CornerRadiusTopLeft     = 3; s.CornerRadiusTopRight    = 3;
        s.CornerRadiusBottomLeft  = 3; s.CornerRadiusBottomRight = 3;
        return s;
    }

   private TextureRect MakeTexRect()
    {
        var tex = new TextureRect();
        tex.ExpandMode    = TextureRect.ExpandModeEnum.IgnoreSize;
        tex.StretchMode   = TextureRect.StretchModeEnum.KeepAspectCentered;
        tex.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        tex.AnchorRight   = 1f; tex.AnchorBottom = 1f;
        tex.OffsetLeft    = 6f; tex.OffsetTop    = 6f;
        tex.OffsetRight   = -6f; tex.OffsetBottom = -6f;
        tex.MouseFilter   = MouseFilterEnum.Ignore;
        return tex;
    }

    private Label MakeCountLbl()
    {
        var lbl = new Label();
        lbl.HorizontalAlignment = HorizontalAlignment.Right;
        lbl.VerticalAlignment   = VerticalAlignment.Bottom;
        lbl.AnchorRight         = 1f; lbl.AnchorBottom = 1f;
        lbl.OffsetRight         = -3f; lbl.OffsetBottom = -3f;
        lbl.AddThemeFontSizeOverride("font_size", 10);
        lbl.MouseFilter         = MouseFilterEnum.Ignore;
        return lbl;
    }

    private bool IsLClick(InputEvent ev) =>
        ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;

    private Dictionary<string, Texture2D> _iconCache = new();
    private Texture2D GetIcon(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (_iconCache.TryGetValue(itemId, out var cached)) return cached;
        string path = $"res://Assets/Textures/Items/{itemId}.png";
        var tex = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        _iconCache[itemId] = tex;
        return tex;
    }
}