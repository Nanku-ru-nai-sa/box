using Godot;
using System;
using System.Collections.Generic;

// ToolBenchPanel — Tetra-style X-layout tool assembly UI.
//
// 4 sockets, all rotated 45° into diamonds, arranged in an X around a
// decorative rotated center piece:
//   Binding (top-left)     Head A (top-right)
//              [ center diamond ]
//   Head B (bottom-left)   Handle/Rod (bottom-right)
//
// The 4 corner positions are the same 4 corner cells as the Crafter's 3x3
// grid (columns 0/2, rows 0/2), and the output slot sits at the same
// position as the Crafter's output slot, so the two panels line up when
// the station tab bar swaps between them.
//
// No Craft button — like the Crafter, grabbing the output slot performs
// the craft. UpdateOutputPreview() runs live any time a socket changes,
// so the output highlights the moment a valid recipe is socketed.
//
// Internal slot INDEX order is unchanged (0=HeadA, 1=HeadB, 2=Handle,
// 3=Binding) - only where they're drawn on screen changed.
//
// NOTE: item -> (family/material/slot) recognition is driven by ItemResource.Tags,
// using this convention (add these tags when you create shaped part items):
//   "slot:head"    - this item can go in HeadA or HeadB
//   "slot:handle"  - this item can go in Handle
//   "slot:binding" - this item can go in Binding
//   "family:pickaxe" (etc) - only present on head items, sets the tool's family
//   "material:flint" (etc) - present on every part item, drives stats via MaterialStatsDb

public partial class ToolBenchPanel : Control
{
    private Inventory _inventory;

    // 0 = HeadA, 1 = HeadB, 2 = Handle, 3 = Binding — same order as before
    private InventorySlot[] _sockets = new InventorySlot[4];
    public static readonly string[] SocketLabels = { "Head A", "Head B", "Handle", "Binding" };

    public Action<int, MouseButton, bool> OnSlotClicked;
    public Action<MouseButton, bool> OnOutputClicked;

    // ── UI ────────────────────────────────────────────────────────────────────
    private Panel   _bg;

    private Control       _socketRow;
    private Panel[]       _socketPanels = new Panel[4];
    private TextureRect[] _socketTex    = new TextureRect[4];
    private Label[]       _socketCount  = new Label[4];
    private Panel         _centerPiece;

    private Panel        _outputSlot;
    private TextureRect  _outputTex;
    private Label        _outputLabel;
    private Label        _statusLabel;

    // Live "does the current socket state make a craftable tool" check —
    // recomputed by UpdateOutputPreview() after every socket change.
    private ToolBenchCraftResolver.ResolveResult _currentResult;

    // Same constants as CraftingPanel so the two panels' grids line up exactly.
    private const int SlotSz  = 56;
    private const int SlotGap = 5;
    private const int PanelW  = 300;

    // =========================================================================
    // INIT
    // =========================================================================

    public void Init(Inventory inventory)
    {
        _inventory = inventory;
        for (int i = 0; i < 4; i++) _sockets[i] = new InventorySlot();
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(PanelW, 339f);
        MouseFilter       = MouseFilterEnum.Stop;

        BuildBackground();
        BuildTitle();
        BuildSockets();
        BuildOutputArea();
        UpdateOutputPreview();
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
        s.BgColor                = new Color(0.08f, 0.08f, 0.10f, 0.97f);
        s.BorderColor             = new Color(0.35f, 0.35f, 0.40f);
        s.BorderWidthTop         = 2; s.BorderWidthBottom = 2;
        s.BorderWidthLeft        = 2; s.BorderWidthRight  = 2;
        s.CornerRadiusTopLeft    = 4; s.CornerRadiusTopRight    = 4;
        s.CornerRadiusBottomLeft = 4; s.CornerRadiusBottomRight = 4;
        _bg.AddThemeStyleboxOverride("panel", s);
        AddChild(_bg);
    }

    private void BuildTitle()
    {
        var title = new Label();
        title.Text = "Tool Bench";
        title.Position = new Vector2(0f, -24f); // matches CraftingPanel's external title
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
        title.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(title);
    }

    private void BuildSockets()
    {
        _socketRow = new Control();
        _socketRow.AnchorRight  = 1f;
        _socketRow.AnchorBottom = 1f;
        _socketRow.OffsetTop    = 14f; // matches CraftingPanel's _gridView offset
        AddChild(_socketRow);

        // Same math CraftingPanel uses for its 3x3 grid: gridX is the left
        // edge of column 0, columns/rows are SlotSz+SlotGap apart.
        float gridPx = 3 * SlotSz + 2 * SlotGap;      // 178
        float gridX  = (PanelW - gridPx) / 2f;         // 61 — column 0 x
        float rowH   = SlotSz + SlotGap;                // 61
        float col0X  = gridX;                            // 61  (left column)
        float col2X  = gridX + 2 * rowH;                 // 183 (right column)
        float row0Y  = 0f;                                // top row   (matches Crafter row 1)
        float row2Y  = 2 * rowH;                          // 122 - bottom row (matches Crafter row 3)
        float centerX = gridX + rowH;                     // 122 - middle column
        float centerY = rowH;                             // 61  - middle row

        // Corner sockets are smaller diamonds (75% of the full slot size),
        // but their CENTERS still sit on the exact same alignment points as
        // before (same row/column lines as the Crafter's grid).
        int cornerSz = Mathf.RoundToInt(SlotSz * 0.75f); // 42

        // idx 0=HeadA(bottom-left), 1=HeadB(top-right), 2=Handle(bottom-right), 3=Binding(top-left)
        // (HeadA/HeadB swapped from their original top-right/bottom-left spots.)
        Vector2[] centers =
        {
            new Vector2(col0X + SlotSz / 2f, row2Y + SlotSz / 2f), // 0 HeadA    - bottom left
            new Vector2(col2X + SlotSz / 2f, row0Y + SlotSz / 2f), // 1 HeadB    - top right
            new Vector2(col2X + SlotSz / 2f, row2Y + SlotSz / 2f), // 2 Handle   - bottom right
            new Vector2(col0X + SlotSz / 2f, row0Y + SlotSz / 2f), // 3 Binding  - top left
        };

        // Decorative center piece — rotated 45°, sits behind the 4 corner
        // sockets as the "X" hub. Not interactive, not a real slot.
        _centerPiece = new Panel();
        _centerPiece.Position          = new Vector2(centerX, centerY);
        _centerPiece.CustomMinimumSize = new Vector2(SlotSz, SlotSz);
        _centerPiece.PivotOffset       = new Vector2(SlotSz / 2f, SlotSz / 2f);
        _centerPiece.Rotation          = Mathf.Pi / 4f;
        _centerPiece.MouseFilter       = MouseFilterEnum.Ignore;
        var centerStyle = new StyleBoxFlat();
        centerStyle.BgColor     = new Color(0.15f, 0.15f, 0.18f, 0.9f);
        centerStyle.BorderColor = new Color(0.4f, 0.4f, 0.46f);
        centerStyle.BorderWidthTop = 2; centerStyle.BorderWidthBottom = 2;
        centerStyle.BorderWidthLeft = 2; centerStyle.BorderWidthRight = 2;
        _centerPiece.AddThemeStyleboxOverride("panel", centerStyle);
        _socketRow.AddChild(_centerPiece);

        // Icon size stays fixed regardless of the box size around it — only
        // the diamond frame shrinks, the item art doesn't. Same 44px the
        // full-size 56px slot used (6px margin on each side).
        const float iconSz = SlotSz - 12f; // 44

        for (int i = 0; i < 4; i++)
        {
            // The socket frame itself is rotated 45° into a diamond, sized
            // smaller but centered on the same alignment point as before.
            Vector2 topLeft = centers[i] - new Vector2(cornerSz / 2f, cornerSz / 2f);

            var slot = MakePartSlot(cornerSz);
            slot.Position    = topLeft;
            slot.PivotOffset = new Vector2(cornerSz / 2f, cornerSz / 2f);
            slot.Rotation    = Mathf.Pi / 4f;

            // ...but the icon and count label inside it are counter-rotated
            // back to upright, so items still look normal, just framed in a diamond.
            // NOTE: these use fixed Position/Size (anchors disabled) rather than
            // anchor-fill, because the pivot for rotation must match the
            // control's ACTUAL size exactly. Icon is centered on the box's
            // center point even though it's a different (fixed) size than
            // the box itself, so it can overhang the frame slightly — intentional.
            float iconMargin = (cornerSz - iconSz) / 2f; // can go negative — that's fine, icon just overhangs

            var tex = MakeTexRect();
            tex.AnchorLeft = tex.AnchorTop = tex.AnchorRight = tex.AnchorBottom = 0f;
            tex.Position    = new Vector2(iconMargin, iconMargin);
            tex.Size        = new Vector2(iconSz, iconSz);
            tex.PivotOffset = new Vector2(iconSz / 2f, iconSz / 2f);
            tex.Rotation    = -Mathf.Pi / 4f;

            var lbl = MakeCountLbl();
            lbl.AnchorLeft = lbl.AnchorTop = lbl.AnchorRight = lbl.AnchorBottom = 0f;
            lbl.Position    = new Vector2(iconMargin, iconMargin);
            lbl.Size        = new Vector2(iconSz, iconSz);
            lbl.PivotOffset = new Vector2(iconSz / 2f, iconSz / 2f);
            lbl.Rotation    = -Mathf.Pi / 4f;

            slot.AddChild(tex);
            slot.AddChild(lbl);

            int idx = i;
            slot.GuiInput     += (InputEvent ev) => OnSocketInput(ev, idx);
            slot.MouseFilter   = MouseFilterEnum.Stop;
            slot.MouseEntered += () => slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.7f, 0.7f, 0.75f)));
            slot.MouseExited  += () => slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.3f, 0.3f, 0.35f)));

            _socketRow.AddChild(slot);
            _socketPanels[i] = slot;
            _socketTex[i]    = tex;
            _socketCount[i]  = lbl;

            bool isTopRow = centers[i].Y < centerY; // centers[i] is at row0Y+28 or row2Y+28
            var caption = new Label();
            caption.Text                = SocketLabels[i];
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.Position            = new Vector2(centers[i].X - (SlotSz + 16f) / 2f, isTopRow ? topLeft.Y - 14f : topLeft.Y + cornerSz + 3f);
            caption.CustomMinimumSize   = new Vector2(SlotSz + 16f, 14f);
            caption.AddThemeFontSizeOverride("font_size", 9);
            caption.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
            caption.MouseFilter         = MouseFilterEnum.Ignore;
            _socketRow.AddChild(caption);
        }
    }

    private void BuildOutputArea()
    {
        // Same numbers CraftingPanel uses for its output slot, so both
        // panels' outputs sit in the exact same spot. Output stays square
        // (not rotated) — same visual language as the Crafter's output.
        int   outSz      = SlotSz;
        float gridX       = (PanelW - (3 * SlotSz + 2 * SlotGap)) / 2f;
        float outX        = (PanelW - outSz) / 2f;
        float outputRowY  = 253f;
        float gridBottom  = 2 * (SlotSz + SlotGap) + SlotSz; // bottom edge of bottom row

        var arrow = new Label();
        arrow.Text                = "↓";
        arrow.HorizontalAlignment = HorizontalAlignment.Center;
        arrow.Position            = new Vector2(gridX, gridBottom + (outputRowY - gridBottom) / 2f - 8f);
        arrow.CustomMinimumSize   = new Vector2(3 * SlotSz + 2 * SlotGap, 16f);
        arrow.AddThemeFontSizeOverride("font_size", 14);
        arrow.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        _socketRow.AddChild(arrow);

        _outputSlot           = MakePartSlot(outSz);
        _outputSlot.Position  = new Vector2(outX, outputRowY);
        _outputTex            = MakeTexRect();
        _outputSlot.AddChild(_outputTex);
        _outputSlot.GuiInput   += OnOutputInput;
        _outputSlot.MouseFilter  = MouseFilterEnum.Stop;
        _socketRow.AddChild(_outputSlot);

        _outputLabel = new Label();
        _outputLabel.Text                = "";
        _outputLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _outputLabel.Position            = new Vector2(0f, outputRowY + outSz + 4f);
        _outputLabel.CustomMinimumSize   = new Vector2(PanelW, 16f);
        _outputLabel.AddThemeFontSizeOverride("font_size", 11);
        _outputLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        _outputLabel.MouseFilter         = MouseFilterEnum.Ignore;
        _socketRow.AddChild(_outputLabel);

        _statusLabel = new Label();
        _statusLabel.Text                = "";
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _statusLabel.Position            = new Vector2(0f, outputRowY + outSz + 22f);
        _statusLabel.CustomMinimumSize   = new Vector2(PanelW, 32f);
        _statusLabel.AutowrapMode        = TextServer.AutowrapMode.Word;
        _statusLabel.AddThemeFontSizeOverride("font_size", 10);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.6f, 0.4f));
        _statusLabel.MouseFilter         = MouseFilterEnum.Ignore;
        _socketRow.AddChild(_statusLabel);
    }

    // =========================================================================
    // SOCKET ACCESS — used by Player for click/drag placement, and by
    // ToolBenchCraftResolver to read what's socketed.
    // =========================================================================

    public InventorySlot GetSlot(int idx) => (idx >= 0 && idx < 4) ? _sockets[idx] : null;
    public int GetActiveSlotCount() => 4;
    public Panel GetSlotPanel(int i) => (i >= 0 && i < 4) ? _socketPanels[i] : null;

    // Places 1 unit of itemId into socket idx. Used for drag-across placement,
    // same behavior as CraftingPanel.TryPlaceHeldItem.
    public bool TryPlaceHeldItem(int idx, string itemId)
    {
        var slot = GetSlot(idx);
        if (slot == null) return false;

        if (slot.IsEmpty)
        {
            var item = ItemRegistry.Instance.GetItem(itemId);
            int max = item?.MaxStackSize ?? 1;
            slot.ItemId = itemId;
            slot.Count  = Mathf.Min(1, max);
            RefreshAllVisuals();
            return true;
        }

        if (slot.ItemId == itemId)
        {
            var item = ItemRegistry.Instance.GetItem(itemId);
            int max = item?.MaxStackSize ?? 1;
            if (slot.Count >= max) return false;
            slot.Count++;
            RefreshAllVisuals();
            return true;
        }

        return false;
    }

    // Returns all 4 sockets keyed by PartSlot, skipping empty ones.
    // Used by the resolver both for live preview and for the actual craft.
    public Dictionary<PartSlot, InventorySlot> GetFilledSockets()
    {
        var result = new Dictionary<PartSlot, InventorySlot>();
        var order = new[] { PartSlot.HeadA, PartSlot.HeadB, PartSlot.Handle, PartSlot.Binding };
        for (int i = 0; i < 4; i++)
            if (!_sockets[i].IsEmpty)
                result[order[i]] = _sockets[i];
        return result;
    }

    public void ClearSocket(int idx)
    {
        var slot = GetSlot(idx);
        slot?.Clear();
        RefreshAllVisuals();
    }

    // Called when closing the panel to return any leftover socketed items
    // to the inventory. Same idea as CraftingPanel.Return3x3ToInventory.
    public void ReturnSocketsToInventory()
    {
        for (int i = 0; i < 4; i++)
        {
            if (!_sockets[i].IsEmpty)
            {
                _inventory.AddItem(_sockets[i].ItemId, _sockets[i].Count);
                _sockets[i].Clear();
            }
        }
        _inventory.OnInventoryChanged?.Invoke();
        RefreshAllVisuals();
    }

    // =========================================================================
    // OUTPUT — auto-craft-on-grab, same pattern as CraftingPanel's output.
    // =========================================================================

    // Live check: does the current socket state resolve to a craftable tool?
    // Does NOT consume anything. Called after every socket change.
    private void UpdateOutputPreview()
    {
        _currentResult = ToolBenchCraftResolver.Resolve(this);

        if (_currentResult.Success)
        {
            _outputTex.Texture = null; // no composited icon yet - see note in ToolBenchCraftResolver
            _outputLabel.Text  = ItemRegistry.Instance.GetItem(_currentResult.ItemId)?.DisplayName ?? _currentResult.ItemId;
            _statusLabel.Text  = "";

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
            _outputLabel.Text  = "";
            _statusLabel.Text  = GetFilledSockets().Count > 0 ? _currentResult.FailReason : "";
            _outputSlot.AddThemeStyleboxOverride("panel", MakeSlotStyle(new Color(0.3f, 0.3f, 0.35f)));
        }
    }

    // For Player: check what grabbing the output would give, without taking it.
    public bool PeekResult(out string itemId, out int durability)
    {
        if (_currentResult != null && _currentResult.Success)
        {
            itemId     = _currentResult.ItemId;
            durability = _currentResult.Durability;
            return true;
        }
        itemId = null; durability = 0;
        return false;
    }

    // For Player: actually perform the craft — consumes ingredients from the
    // sockets and returns the crafted tool's id + durability. Mirrors
    // CraftingPanel.TryConsumeOneCraft.
    public bool TryConsumeOneCraft(out string resultId, out int resultDurability)
    {
        if (_currentResult == null || !_currentResult.Success)
        {
            resultId = null; resultDurability = 0;
            return false;
        }

        var item = ItemRegistry.Instance.GetItem(_currentResult.ItemId);
        ToolFamily primaryFamily = System.Enum.Parse<ToolFamily>(item.ToolType);
        ToolFamily? secondaryFamily = GetFilledSockets().ContainsKey(PartSlot.HeadB)
            ? ReadSecondaryFamily() : null;

        resultId         = _currentResult.ItemId;
        resultDurability = _currentResult.Durability;

        ToolBenchCraftResolver.ConsumeIngredients(this, primaryFamily, secondaryFamily);
        RefreshAllVisuals(); // also re-runs UpdateOutputPreview for the (now likely empty) sockets
        return true;
    }

    private ToolFamily? ReadSecondaryFamily()
    {
        // HeadB's family isn't stored anywhere else once we're mid-consume,
        // so re-derive it the same way the resolver does.
        if (!GetFilledSockets().TryGetValue(PartSlot.HeadB, out var headB)) return null;
        var item = ItemRegistry.Instance.GetItem(headB.ItemId);
        if (item?.Tags == null) return null;
        foreach (var tag in item.Tags)
            if (tag.StartsWith("family:") && System.Enum.TryParse<ToolFamily>(tag.Substring(7), true, out var fam))
                return fam;
        return null;
    }

    // =========================================================================
    // VISUAL REFRESH
    // =========================================================================

    public void RefreshAllVisuals()
    {
        for (int i = 0; i < 4; i++) RefreshSocketVisual(i);
        UpdateOutputPreview();
    }

    private void RefreshSocketVisual(int idx)
    {
        var slot = _sockets[idx];
        _socketTex[idx].Texture = GetIcon(slot.IsEmpty ? "" : slot.ItemId);
        _socketCount[idx].Text  = (!slot.IsEmpty && slot.Count > 1) ? slot.Count.ToString() : "";
    }

    // =========================================================================
    // INPUT — forwards to Player, which owns cursor state (mirrors CraftingPanel)
    // =========================================================================

    private void OnSocketInput(InputEvent ev, int idx)
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
    // HELPERS — same visual style as CraftingPanel's
    // =========================================================================

    private Panel MakePartSlot(int size)
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