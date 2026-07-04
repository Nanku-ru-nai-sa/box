using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
    [Export] public float WalkSpeed      { get; set; } = 5f;
    [Export] public float SprintSpeed    { get; set; } = 8f;
    [Export] public float CrouchSpeed    { get; set; } = 2.5f;
    [Export] public float JumpVelocity   { get; set; } = 6f;
    [Export] public float SprintStaminaCost { get; set; } = 10f;
    [Export] public float JumpStaminaCost   { get; set; } = 10f;

    private float _gravity = 20f;
    private PlayerStats   _stats;
    private PlayerCamera  _playerCamera;
    private RayCast3D     _rayCast;
    private bool _isSprinting = false;
    private bool _isCrouching = false;
    private bool _hasDoubleJumped = false;
    private bool _isGliding  = false;
    private bool _isPlacing  = false;
    private bool _isInWater  = false;
    private float _placeTimer = 0f;
    private const float PlaceInterval = 0.15f;
    private bool _isBreaking  = false;
    private float _breakTimer = 0f;
    private const float BreakInterval = 0.15f;
    private string _selectedBlockId = "";
    private MeshInstance3D _blockOutline;

    // ── Hotbar ──────────────────────────────────────────────────────────────
    private const int HotbarSize = 12;
    private Panel[] _hotbarSlots     = new Panel[HotbarSize];
    private Panel[] _invHotbarSlots  = new Panel[HotbarSize];
    private Label[] _hotbarLabels    = new Label[HotbarSize];
    private Label[] _invHotbarLabels = new Label[HotbarSize];
    private int _selectedSlot = 0;

    // ── Inventory ────────────────────────────────────────────────────────────
    // Slots  0-35  = main inventory (3 rows × 12)
    // Slots 36-47  = hotbar         (1 row  × 12)
    private const int MainInvSize = 36;
    private const int TotalSlots  = MainInvSize + HotbarSize; // 48
    private Inventory _inventory;

    private Panel[] _invSlotPanels = new Panel[MainInvSize];
    private Label[] _invSlotLabels = new Label[MainInvSize];

    // ── Held / cursor item ───────────────────────────────────────────────────
    private InventorySlot _heldSlot     = new InventorySlot();
    private int           _heldFromSlot = -1;
    private Panel         _cursorPanel;
    private Label         _cursorLabel;
    private TextureRect   _cursorTex;

    // ── Drag state ───────────────────────────────────────────────────────────
    //
    //  LMB WITH ITEM (even-split drag)
    //   • Pick up a stack → start dragging.
    //   • Every NEW slot the cursor enters gets a share of the original stack.
    //   • Share = floor(originalCount / numSlotsVisited).
    //   • The source slot is slot[0] in the visited list; it gets redistributed
    //     just like all others so the visual is consistent.
    //   • Remainder stays on cursor until mouse is released.
    //   • On release the redistribution is finalised.
    //
    //  RMB DRAG (place-one-per-slot)
    //   • Split stack → start dragging.
    //   • Each NEW slot the cursor enters receives exactly 1 item from cursor.
    //   • Re-entering a slot that was already visited does NOT place another.
    //   • On release nothing extra happens.
    //
    //  LMB NO ITEM (collect same type)
    //   • Hold LMB on empty space / begin moving over slots with nothing held.
    //   • Every slot visited of the same item type is picked up.
    //
    //  SHIFT-LMB NO ITEM
    //   • Shift + drag over slots → each visited slot is shift-clicked.

    private enum DragMode { None, LmbWithItem, LmbNoItem, RmbDrag, ShiftLmbNoItem }
    private DragMode     _dragMode      = DragMode.None;
    private List<int>    _dragVisited   = new List<int>(); // ordered list of unique slots entered
    private int          _dragOrigCount = 0;               // count at the moment drag started
    private int          _dragLastSlot  = -1;              // last slot seen (any mode)

    // ── UI layers ────────────────────────────────────────────────────────────
    private Control     _inventoryScreen;
    private bool        _inventoryOpen = false;
    private bool        _showChunkBorders = false;
    private bool        _hudVisible       = true;
    private CanvasLayer _hotbarLayer;
    private CanvasLayer _crosshairLayer;
    private CanvasLayer _inventoryLayer;
    private CanvasLayer _cursorLayer;
    private PauseMenu    _pauseMenu;

    // ── Gamemode / fly / chat ────────────────────────────────────────────────
    private bool         _isFlying        = false;
    private float        _flySpeed        = 12f;
    private float        _flyVertSpeed    = 8f;
    private bool         _chatOpen        = false;
    private CanvasLayer  _chatLayer;
    private LineEdit     _chatInput;
    private Label        _chatFeedback;
    private float        _feedbackTimer   = 0f;
    private const float  FeedbackDuration = 3f;
    private double       _lastJumpTime    = 0.0;
    private const double DoubleJumpWindow = 0.35;
    private readonly List<MeshInstance3D> _chunkBorderMeshes = new();

    public bool CanDoubleJump { get; set; } = false;
    public bool CanWallClimb  { get; set; } = false;
    public bool CanGlide      { get; set; } = false;
    public bool CanGrapple    { get; set; } = false;

    private const int SlotSize = 64;
    private const int SlotGap  = 5;

    // Icon texture cache  (res://items/<blockId>.png)
    private Dictionary<string, Texture2D> _iconCache = new();

    // =========================================================================
    // READY
    // =========================================================================

    public override void _Ready()
    {
        _stats        = GetNodeOrNull<PlayerStats>("PlayerStats");
        _playerCamera = GetNodeOrNull<PlayerCamera>("PlayerCamera");
        _rayCast      = GetNode<RayCast3D>("PlayerCamera/Camera3D/RayCast3D");
        _rayCast.AddException(this);

        _inventory           = new Inventory();
        _inventory.SlotCount = TotalSlots;
        AddChild(_inventory);

        CallDeferred(nameof(LoadInventoryFromSave));

        // Crosshair
        _crosshairLayer = new CanvasLayer();
        GetTree().Root.CallDeferred("add_child", _crosshairLayer);
        var crosshair = new ColorRect();
        crosshair.Color       = new Color(1, 1, 1);
        crosshair.Size        = new Vector2(2, 2);
        crosshair.PivotOffset = new Vector2(1, 1);
        crosshair.MouseFilter = Control.MouseFilterEnum.Ignore;
        crosshair.AnchorLeft  = 0.5f; crosshair.AnchorRight  = 0.5f;
        crosshair.AnchorTop   = 0.5f; crosshair.AnchorBottom = 0.5f;
        crosshair.OffsetLeft  = -1;   crosshair.OffsetTop    = -1;
        crosshair.OffsetRight =  1;   crosshair.OffsetBottom =  1;
        _crosshairLayer.CallDeferred("add_child", crosshair);

        // Block outline
        _blockOutline = new MeshInstance3D();
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Lines);
        Vector3[] corners = { new(0,0,0),new(1,0,0),new(1,0,1),new(0,0,1),
                               new(0,1,0),new(1,1,0),new(1,1,1),new(0,1,1) };
        int[][] edges = { new[]{0,1},new[]{1,2},new[]{2,3},new[]{3,0},
                          new[]{4,5},new[]{5,6},new[]{6,7},new[]{7,4},
                          new[]{0,4},new[]{1,5},new[]{2,6},new[]{3,7} };
        foreach (var e in edges) { st.AddVertex(corners[e[0]]); st.AddVertex(corners[e[1]]); }
        _blockOutline.Mesh = st.Commit();
        var outlineMat = new StandardMaterial3D();
        outlineMat.AlbedoColor = new Color(0, 0, 0);
        outlineMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _blockOutline.MaterialOverride = outlineMat;
        _blockOutline.Visible = false;
        GetTree().Root.CallDeferred("add_child", _blockOutline);

        BuildHotbarHUD();
        BuildInventoryScreen();
        BuildCursorPanel();

        if (_stats == null) { _stats = new PlayerStats(); AddChild(_stats); }
        if (_playerCamera == null)
        {
            _playerCamera          = new PlayerCamera();
            _playerCamera.Position = new Vector3(0, 1.6f, 0);
            AddChild(_playerCamera);
        }

        Input.MouseMode = Input.MouseModeEnum.Captured;
        _inventory.OnInventoryChanged += RefreshAllSlotVisuals;
        RefreshAllSlotVisuals();
        SelectHotbarSlot(0);

        // Pause menu
        _pauseMenu = new PauseMenu();
        _pauseMenu.Init(this);
        AddChild(_pauseMenu);

        BuildChatBar();

        // Hook gamemode changes
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.OnGameModeChanged += OnGameModeChanged;

        GD.Print("Player ready.");
    }

    // =========================================================================
    // TEXTURE LOADING
    // =========================================================================

    // Place item textures at:  res://items/<blockId>.png
    // If missing, the slot TextureRect stays empty (no texture shown).
    private Texture2D GetItemIcon(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (_iconCache.TryGetValue(itemId, out var cached)) return cached;

        string path = $"res://Assets/Textures/Items/{itemId}.png";
        Texture2D tex = null;
        if (ResourceLoader.Exists(path))
            tex = ResourceLoader.Load<Texture2D>(path);

        _iconCache[itemId] = tex; // cache even if null so we don't retry every frame
        return tex;
    }

    // =========================================================================
    // UI BUILDERS
    // =========================================================================

    private void BuildHotbarHUD()
    {
        _hotbarLayer = new CanvasLayer();
        GetTree().Root.CallDeferred("add_child", _hotbarLayer);

        float totalWidth = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;
        var container = new HBoxContainer();
        container.AnchorLeft   = 0.5f; container.AnchorRight  = 0.5f;
        container.AnchorTop    = 1.0f; container.AnchorBottom = 1.0f;
        container.OffsetLeft   = -totalWidth / 2f;
        container.OffsetRight  =  totalWidth / 2f;
        container.OffsetTop    = -(SlotSize + 10);
        container.OffsetBottom = -10;
        container.AddThemeConstantOverride("separation", SlotGap);

        for (int i = 0; i < HotbarSize; i++)
        {
            var slot = MakeSlotPanel(SlotSize);
            slot.AddChild(MakeSlotTexRect());
            slot.AddChild(MakeCountLabel(9));
            slot.AddChild(MakeKeyHintLabel(i));
            _hotbarLabels[i] = slot.GetChild<Label>(1);
            container.AddChild(slot);
            _hotbarSlots[i] = slot;
        }
        _hotbarLayer.CallDeferred("add_child", container);
    }

    private void BuildInventoryScreen()
    {
        _inventoryLayer       = new CanvasLayer();
        _inventoryLayer.Layer = 10;
        GetTree().Root.CallDeferred("add_child", _inventoryLayer);

        float gridW      = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;
        float mainH      = 3 * SlotSize + 2 * SlotGap;
        float sectionGap = 14f;
        float titleH     = 30f;
        float pad        = 16f;
        float totalW     = gridW + pad * 2f;
        float totalH     = titleH + mainH + sectionGap + SlotSize + pad * 2f;

        _inventoryScreen              = new Panel();
        _inventoryScreen.AnchorLeft   = 0.5f; _inventoryScreen.AnchorRight  = 0.5f;
        _inventoryScreen.AnchorTop    = 0.5f; _inventoryScreen.AnchorBottom = 0.5f;
        _inventoryScreen.OffsetLeft   = -totalW / 2f;
        _inventoryScreen.OffsetRight  =  totalW / 2f;
        _inventoryScreen.OffsetTop    = -totalH / 2f;
        _inventoryScreen.OffsetBottom =  totalH / 2f;
        _inventoryScreen.AddThemeStyleboxOverride("panel",
            MakePanelStyle(new Color(0.08f, 0.08f, 0.08f, 0.95f), new Color(0.5f, 0.5f, 0.5f)));

        var title = new Label();
        title.Text                = "Inventory";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AnchorRight         = 1.0f;
        title.OffsetTop           = pad / 2f;
        title.OffsetBottom        = pad / 2f + titleH;
        _inventoryScreen.AddChild(title);

        // Main 3×10 grid — slots 0..29
        var mainGrid = new GridContainer();
        mainGrid.Columns  = HotbarSize;
        mainGrid.Position = new Vector2(pad, titleH + pad);
        mainGrid.AddThemeConstantOverride("h_separation", SlotGap);
        mainGrid.AddThemeConstantOverride("v_separation", SlotGap);

        for (int i = 0; i < MainInvSize; i++)
        {
            var slot = MakeSlotPanel(SlotSize);
            slot.AddChild(MakeSlotTexRect());
            slot.AddChild(MakeCountLabel(10));
            _invSlotPanels[i] = slot;
            _invSlotLabels[i] = slot.GetChild<Label>(1);
            int idx = i;
            slot.GuiInput    += (InputEvent ev) => OnInvSlotInput(ev, idx);
            slot.MouseEntered += ()             => OnSlotMouseEntered(idx);
            slot.MouseFilter   = Control.MouseFilterEnum.Stop;
            mainGrid.AddChild(slot);
        }
        _inventoryScreen.AddChild(mainGrid);

        // Divider
        var divider = new ColorRect();
        divider.Color    = new Color(0.35f, 0.35f, 0.35f);
        divider.Position = new Vector2(pad, titleH + pad + mainH + sectionGap / 2f - 1f);
        divider.Size     = new Vector2(gridW, 2f);
        _inventoryScreen.AddChild(divider);

        // Hotbar row — slots 30..39
        var hotbarRow = new HBoxContainer();
        hotbarRow.Position = new Vector2(pad, titleH + pad + mainH + sectionGap);
        hotbarRow.AddThemeConstantOverride("separation", SlotGap);

        for (int i = 0; i < HotbarSize; i++)
        {
            var slot = MakeSlotPanel(SlotSize);
            slot.AddChild(MakeSlotTexRect());
            slot.AddChild(MakeCountLabel(10));
            slot.AddChild(MakeKeyHintLabel(i));
            _invHotbarSlots[i]  = slot;
            _invHotbarLabels[i] = slot.GetChild<Label>(1);
            int idx = i;
            slot.GuiInput    += (InputEvent ev) => OnHotbarInvSlotInput(ev, idx);
            slot.MouseEntered += ()             => OnSlotMouseEntered(MainInvSize + idx);
            slot.MouseFilter   = Control.MouseFilterEnum.Stop;
            hotbarRow.AddChild(slot);
        }
        _inventoryScreen.AddChild(hotbarRow);

        _inventoryScreen.Visible = false;
        _inventoryLayer.CallDeferred("add_child", _inventoryScreen);
    }

    private void BuildCursorPanel()
    {
        _cursorLayer       = new CanvasLayer();
        _cursorLayer.Layer = 20;
        GetTree().Root.CallDeferred("add_child", _cursorLayer);

        _cursorPanel = MakeSlotPanel(SlotSize);
        _cursorPanel.AddThemeStyleboxOverride("panel",
            MakePanelStyle(new Color(0.25f, 0.25f, 0.25f, 0.9f), new Color(1f, 0.9f, 0.2f), 2));
        _cursorPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _cursorPanel.Visible     = false;

        _cursorTex   = MakeSlotTexRect();
        _cursorLabel = MakeCountLabel(11);
        _cursorPanel.AddChild(_cursorTex);
        _cursorPanel.AddChild(_cursorLabel);

        _cursorLayer.CallDeferred("add_child", _cursorPanel);
    }

    // ── Slot factory helpers ─────────────────────────────────────────────────

    private Panel MakeSlotPanel(int size)
    {
        var slot = new Panel();
        slot.CustomMinimumSize = new Vector2(size, size);
        slot.AddThemeStyleboxOverride("panel",
            MakePanelStyle(new Color(0.15f, 0.15f, 0.15f, 0.85f), new Color(0.4f, 0.4f, 0.4f)));
        return slot;
    }

    // Child index 0 in every slot panel: the item icon TextureRect
    private TextureRect MakeSlotTexRect()
    {
        var tex = new TextureRect();
        tex.ExpandMode    = TextureRect.ExpandModeEnum.IgnoreSize;
        tex.StretchMode   = TextureRect.StretchModeEnum.KeepAspectCentered;
        tex.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        tex.AnchorRight  = 1f;  tex.AnchorBottom = 1f;
        tex.OffsetLeft   = 6;   tex.OffsetTop    = 6;
        tex.OffsetRight  = -6;  tex.OffsetBottom = -6;
        tex.MouseFilter  = Control.MouseFilterEnum.Ignore;
        return tex;
    }

    // Child index 1 in every slot panel: the stack-count label (bottom-right)
    private Label MakeCountLabel(int fontSize)
    {
        var lbl = new Label();
        lbl.HorizontalAlignment = HorizontalAlignment.Right;
        lbl.VerticalAlignment   = VerticalAlignment.Bottom;
        lbl.AnchorRight         = 1.0f;
        lbl.AnchorBottom        = 1.0f;
        lbl.OffsetRight         = -3f;
        lbl.OffsetBottom        = -3f;
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AutowrapMode        = TextServer.AutowrapMode.Off;
        lbl.MouseFilter         = Control.MouseFilterEnum.Ignore;
        return lbl;
    }

    // Key hint — top-right corner (only used in hotbar slots)
    private Label MakeKeyHintLabel(int hotbarIndex)
    {
        var lbl = new Label();
        lbl.Text = hotbarIndex < 9 ? (hotbarIndex + 1).ToString() : hotbarIndex == 9 ? "0" : hotbarIndex == 10 ? "-" : "+";
        lbl.HorizontalAlignment = HorizontalAlignment.Right;
        lbl.AnchorRight         = 1.0f;
        lbl.OffsetRight         = -3f;
        lbl.OffsetTop           = 2f;
        lbl.AddThemeFontSizeOverride("font_size", 8);
        lbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        lbl.MouseFilter         = Control.MouseFilterEnum.Ignore;
        return lbl;
    }

    private StyleBoxFlat MakePanelStyle(Color bg, Color border, int bw = 2)
    {
        var s = new StyleBoxFlat();
        s.BgColor           = bg;
        s.BorderColor       = border;
        s.BorderWidthTop    = bw; s.BorderWidthBottom = bw;
        s.BorderWidthLeft   = bw; s.BorderWidthRight  = bw;
        return s;
    }

    // =========================================================================
    // SLOT INPUT ROUTING
    // =========================================================================

    private void OnInvSlotInput(InputEvent ev, int slotIndex)
    {
        if (!_inventoryOpen) return;
        HandleSlotInput(ev, slotIndex);
    }

    private void OnHotbarInvSlotInput(InputEvent ev, int hotbarIndex)
    {
        if (!_inventoryOpen) return;
        HandleSlotInput(ev, MainInvSize + hotbarIndex);
    }

    private void HandleSlotInput(InputEvent ev, int slotIndex)
    {
        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            bool shift = Input.IsKeyPressed(Key.Shift);

            switch (mb.ButtonIndex)
            {
                case MouseButton.Left when shift:
                    ShiftClick(slotIndex);
                    break;
                case MouseButton.Left when mb.DoubleClick:
                    DoubleClickCollect(slotIndex);
                    break;
                case MouseButton.Left:
                    HandleLeftClick(slotIndex);
                    break;
                case MouseButton.Right:
                    HandleRightClick(slotIndex);
                    break;
                case MouseButton.WheelUp:
                    ScrollSlot(slotIndex, up: true);
                    break;
                case MouseButton.WheelDown:
                    ScrollSlot(slotIndex, up: false);
                    break;
            }
        }
    }

    // =========================================================================
    // LEFT CLICK  (pick up / place / swap)
    // =========================================================================

    private void HandleLeftClick(int slotIndex)
    {
        var slot = _inventory.Slots[slotIndex];

        if (_heldSlot.IsEmpty)
        {
            if (slot.IsEmpty) return;

            // Pick up the whole stack and start an LmbWithItem drag
            _heldFromSlot    = slotIndex;
            _heldSlot.ItemId = slot.ItemId;
            _heldSlot.Count  = slot.Count;
            _dragOrigCount   = slot.Count;
            slot.Clear();

            _dragMode = DragMode.LmbWithItem;
            _dragVisited.Clear();
            _dragVisited.Add(slotIndex);   // source slot is visit[0]
            _dragLastSlot = slotIndex;
        }
        else
        {
            // Placing / merging / swapping
            if (slot.IsEmpty)
            {
                slot.ItemId = _heldSlot.ItemId;
                slot.Count  = _heldSlot.Count;
                _heldSlot.Clear();
            }
            else if (slot.ItemId == _heldSlot.ItemId)
            {
                int space    = _inventory.MaxStackSize - slot.Count;
                int transfer = Mathf.Min(space, _heldSlot.Count);
                slot.Count      += transfer;
                _heldSlot.Count -= transfer;
                if (_heldSlot.Count <= 0) _heldSlot.Clear();
            }
            else
            {
                // Swap
                (slot.ItemId, _heldSlot.ItemId) = (_heldSlot.ItemId, slot.ItemId);
                (slot.Count,  _heldSlot.Count)  = (_heldSlot.Count,  slot.Count);
            }

            EndDrag();
        }

        FireChanged();
    }

    // =========================================================================
    // RIGHT CLICK  (split / place-one)
    // =========================================================================

    private void HandleRightClick(int slotIndex)
    {
        var slot = _inventory.Slots[slotIndex];

        if (_heldSlot.IsEmpty)
        {
            if (slot.IsEmpty) return;

            // Pick up the ceiling half and start an RmbDrag
            int half         = Mathf.CeilToInt(slot.Count / 2f);
            _heldSlot.ItemId = slot.ItemId;
            _heldSlot.Count  = half;
            _heldFromSlot    = slotIndex;
            slot.Count      -= half;
            if (slot.Count <= 0) slot.Clear();

            _dragMode = DragMode.RmbDrag;
            _dragVisited.Clear();
            _dragVisited.Add(slotIndex); // mark source so we don't place back immediately
            _dragLastSlot = slotIndex;
        }
        else
        {
            // Place exactly one item into this slot
            PlaceOneIntoSlot(slotIndex);
        }

        FireChanged();
    }

    // Places one item from _heldSlot into the given inventory slot.
    // Returns true if an item was placed.
    private bool PlaceOneIntoSlot(int slotIndex)
    {
        if (_heldSlot.IsEmpty) return false;
        var slot = _inventory.Slots[slotIndex];

        if (!slot.IsEmpty && slot.ItemId != _heldSlot.ItemId) return false;
        if (!slot.IsEmpty && slot.Count >= _inventory.MaxStackSize) return false;

        if (slot.IsEmpty) slot.ItemId = _heldSlot.ItemId;
        slot.Count++;
        _heldSlot.Count--;
        if (_heldSlot.Count <= 0) _heldSlot.Clear();
        return true;
    }

    // =========================================================================
    // DRAG — driven from _Input via mouse-motion
    // =========================================================================

    // Called whenever the cursor moves into a new slot during a drag.
    private void OnDragEnterSlot(int slotIndex)
    {
        switch (_dragMode)
        {
            // ── LMB WITH ITEM: even-split across all visited slots ────────────
            case DragMode.LmbWithItem:
            {
                if (_heldSlot.IsEmpty) return;
                var slot = _inventory.Slots[slotIndex];

                // Only accept empty slots or matching-item slots that aren't full
                if (!slot.IsEmpty && slot.ItemId != _heldSlot.ItemId) return;
                if (!slot.IsEmpty && slot.Count >= _inventory.MaxStackSize) return;

                if (!_dragVisited.Contains(slotIndex))
                    _dragVisited.Add(slotIndex);

                // Redistribute the original count evenly across all visited slots.
                // Each slot gets floor(total / numSlots); the first slot absorbs any remainder.
                int n       = _dragVisited.Count;
                int perSlot = _dragOrigCount / n;          // floor division
                int leftover = _dragOrigCount - perSlot * n;

                // First clear all visited slots so we can re-write cleanly
                foreach (int idx in _dragVisited)
                {
                    var s = _inventory.Slots[idx];
                    s.ItemId = _heldSlot.ItemId;
                    s.Count  = 0;
                }

                int remaining = _dragOrigCount;
                for (int i = 0; i < _dragVisited.Count; i++)
                {
                    int give = perSlot + (i == 0 ? leftover : 0);
                    give     = Mathf.Min(give, _inventory.MaxStackSize);
                    _inventory.Slots[_dragVisited[i]].Count = give;
                    remaining -= give;
                }

                // Whatever didn't fit stays on cursor
                _heldSlot.Count = Mathf.Max(0, remaining);
                if (_heldSlot.Count <= 0) _heldSlot.Clear();
                break;
            }

            // ── RMB DRAG: place exactly one item per NEW slot entered ─────────
            case DragMode.RmbDrag:
            {
                if (_heldSlot.IsEmpty) return;
                if (_dragVisited.Contains(slotIndex)) return; // already visited → skip

                if (PlaceOneIntoSlot(slotIndex))
                    _dragVisited.Add(slotIndex);
                break;
            }

            // ── LMB NO ITEM: collect same-type items ─────────────────────────
            case DragMode.LmbNoItem:
            {
                if (_dragVisited.Contains(slotIndex)) return;
                var slot = _inventory.Slots[slotIndex];
                if (slot.IsEmpty) return;
                if (!_heldSlot.IsEmpty && slot.ItemId != _heldSlot.ItemId) return;

                int space = _inventory.MaxStackSize - (_heldSlot.IsEmpty ? 0 : _heldSlot.Count);
                if (space <= 0) return;

                _dragVisited.Add(slotIndex);
                if (_heldSlot.IsEmpty) _heldSlot.ItemId = slot.ItemId;
                int take        = Mathf.Min(slot.Count, space);
                _heldSlot.Count += take;
                slot.Count      -= take;
                if (slot.Count <= 0) slot.Clear();
                break;
            }

            // ── SHIFT-LMB NO ITEM: shift-click each slot entered ─────────────
            case DragMode.ShiftLmbNoItem:
            {
                if (_dragVisited.Contains(slotIndex)) return;
                _dragVisited.Add(slotIndex);
                ShiftMoveSlot(slotIndex);
                break;
            }
        }

        FireChanged();
        UpdateCursorVisual();
    }

    private void EndDrag()
    {
        _dragMode = DragMode.None;
        _dragVisited.Clear();
        _dragOrigCount = 0;
        _dragLastSlot  = -1;
        _heldFromSlot  = -1;
    }

    // =========================================================================
    // SHIFT CLICK  (move item to other section)
    // =========================================================================

    private void ShiftClick(int slotIndex)
    {
        ShiftMoveSlot(slotIndex);
        FireChanged();
    }

    private void ShiftMoveSlot(int slotIndex)
    {
        var src = _inventory.Slots[slotIndex];
        if (src.IsEmpty) return;

        bool isHotbar = slotIndex >= MainInvSize;
        int  destStart = isHotbar ? 0          : MainInvSize;
        int  destEnd   = isHotbar ? MainInvSize : TotalSlots;

        // Stack onto existing stacks first
        for (int i = destStart; i < destEnd && src.Count > 0; i++)
        {
            var dst = _inventory.Slots[i];
            if (dst.IsEmpty || dst.ItemId != src.ItemId) continue;
            int transfer = Mathf.Min(_inventory.MaxStackSize - dst.Count, src.Count);
            dst.Count   += transfer;
            src.Count   -= transfer;
        }

        // Then fill empty slots
        for (int i = destStart; i < destEnd && src.Count > 0; i++)
        {
            var dst = _inventory.Slots[i];
            if (!dst.IsEmpty) continue;
            dst.ItemId = src.ItemId;
            dst.Count  = src.Count;
            src.Clear();
        }

        if (src.Count <= 0) src.Clear();
    }

    // =========================================================================
    // DOUBLE-CLICK  (collect all matching into cursor)
    // =========================================================================

    private void DoubleClickCollect(int slotIndex)
    {
        if (_heldSlot.IsEmpty)
        {
            var s = _inventory.Slots[slotIndex];
            if (s.IsEmpty) return;
            _heldSlot.ItemId = s.ItemId;
            _heldSlot.Count  = s.Count;
            s.Clear();
        }

        if (_heldSlot.Count >= _inventory.MaxStackSize) { FireChanged(); UpdateCursorVisual(); return; }
        string id = _heldSlot.ItemId;

        for (int i = 0; i < TotalSlots && _heldSlot.Count < _inventory.MaxStackSize; i++)
        {
            if (i == slotIndex) continue;
            var s = _inventory.Slots[i];
            if (s.IsEmpty || s.ItemId != id) continue;
            int take         = Mathf.Min(s.Count, _inventory.MaxStackSize - _heldSlot.Count);
            _heldSlot.Count += take;
            s.Count         -= take;
            if (s.Count <= 0) s.Clear();
        }

        FireChanged();
        UpdateCursorVisual();
    }

    // =========================================================================
    // SCROLL WHEEL IN INVENTORY  (move one item between main ↔ hotbar)
    // =========================================================================

    private void ScrollSlot(int slotIndex, bool up)
    {
        bool isHotbar = slotIndex >= MainInvSize;
        var  src      = _inventory.Slots[slotIndex];

        if (up)
        {
            // Pull one item from the OTHER section into this slot
            int fromStart = isHotbar ? 0 : MainInvSize;
            int fromEnd   = isHotbar ? MainInvSize : TotalSlots;

            for (int i = fromStart; i < fromEnd; i++)
            {
                var other = _inventory.Slots[i];
                if (other.IsEmpty) continue;
                if (!src.IsEmpty && other.ItemId != src.ItemId) continue;
                if (src.Count >= _inventory.MaxStackSize) continue;

                if (src.IsEmpty) src.ItemId = other.ItemId;
                src.Count++;
                other.Count--;
                if (other.Count <= 0) other.Clear();
                FireChanged();
                return;
            }
        }
        else
        {
            // Push one item from this slot to the OTHER section
            if (src.IsEmpty) return;
            int toStart = isHotbar ? 0 : MainInvSize;
            int toEnd   = isHotbar ? MainInvSize : TotalSlots;

            // Stack first
            for (int i = toStart; i < toEnd; i++)
            {
                var dst = _inventory.Slots[i];
                if (dst.IsEmpty || dst.ItemId != src.ItemId || dst.Count >= _inventory.MaxStackSize) continue;
                dst.Count++;
                src.Count--;
                if (src.Count <= 0) src.Clear();
                FireChanged();
                return;
            }
            // Then empty slot
            for (int i = toStart; i < toEnd; i++)
            {
                var dst = _inventory.Slots[i];
                if (!dst.IsEmpty) continue;
                dst.ItemId = src.ItemId;
                dst.Count  = 1;
                src.Count--;
                if (src.Count <= 0) src.Clear();
                FireChanged();
                return;
            }
        }
    }

    // =========================================================================
    // ADD ITEM  (hotbar first, then main inv)
    // =========================================================================

    private void AddItemToInventory(string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return;
        int rem = count;

        void TryAdd(int start, int end, bool stackOnly)
        {
            for (int i = start; i < end && rem > 0; i++)
            {
                var s = _inventory.Slots[i];
                if (stackOnly)
                {
                    if (s.IsEmpty || s.ItemId != itemId) continue;
                    int add = Mathf.Min(_inventory.MaxStackSize - s.Count, rem);
                    s.Count += add; rem -= add;
                }
                else
                {
                    if (!s.IsEmpty) continue;
                    int add  = Mathf.Min(_inventory.MaxStackSize, rem);
                    s.ItemId = itemId; s.Count = add; rem -= add;
                }
            }
        }

        TryAdd(MainInvSize, TotalSlots, true);   // stack onto hotbar
        TryAdd(MainInvSize, TotalSlots, false);  // empty hotbar slots
        TryAdd(0, MainInvSize, true);            // stack onto main inv
        TryAdd(0, MainInvSize, false);           // empty main inv slots

        _inventory.OnInventoryChanged?.Invoke();
    }

    // =========================================================================
    // GLOBAL INPUT  (drag tracking + LmbNoItem start)
    // =========================================================================

    // Unused — kept so MouseEntered wiring compiles; actual drag is polled in _Process
    private void OnSlotMouseEntered(int slotIndex) { }

    public override void _Input(InputEvent @event)
    {
        if (!_inventoryOpen) return;

        // Mouse button released: end drag
        if (@event is InputEventMouseButton mb && !mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right)
            {
                EndDrag();
                FireChanged();
                UpdateCursorVisual();
            }
        }

        // Start LmbNoItem drag on mouse-down with nothing held
        if (@event is InputEventMouseButton startMb && startMb.Pressed
            && startMb.ButtonIndex == MouseButton.Left
            && _heldSlot.IsEmpty && _dragMode == DragMode.None)
        {
            _dragMode = Input.IsKeyPressed(Key.Shift)
                ? DragMode.ShiftLmbNoItem
                : DragMode.LmbNoItem;
            _dragVisited.Clear();
            _dragLastSlot = -1;
        }
    }

    private int GetSlotUnderMouse()
    {
        var mouse = GetViewport().GetMousePosition();
        for (int i = 0; i < MainInvSize; i++)
        {
            if (_invSlotPanels[i] == null) continue;
            if (new Rect2(_invSlotPanels[i].GlobalPosition, _invSlotPanels[i].Size).HasPoint(mouse))
                return i;
        }
        for (int i = 0; i < HotbarSize; i++)
        {
            if (_invHotbarSlots[i] == null) continue;
            if (new Rect2(_invHotbarSlots[i].GlobalPosition, _invHotbarSlots[i].Size).HasPoint(mouse))
                return MainInvSize + i;
        }
        return -1;
    }

    // =========================================================================
    // VISUALS
    // =========================================================================

    private void RefreshAllSlotVisuals()
    {
        // Main inventory grid
        for (int i = 0; i < MainInvSize; i++)
        {
            var s    = _inventory.Slots[i];
            var tex  = _invSlotPanels[i].GetChild<TextureRect>(0);
            var lbl  = _invSlotLabels[i];
            tex.Texture = s.IsEmpty ? null : GetItemIcon(s.ItemId);
            lbl.Text    = (!s.IsEmpty && s.Count > 1) ? s.Count.ToString() : "";
        }

        // Hotbar — HUD and inventory-screen row
        for (int i = 0; i < HotbarSize; i++)
        {
            var s    = _inventory.Slots[MainInvSize + i];
            var tex1 = _hotbarSlots[i].GetChild<TextureRect>(0);
            var tex2 = _invHotbarSlots[i].GetChild<TextureRect>(0);
            var icon = s.IsEmpty ? null : GetItemIcon(s.ItemId);
            tex1.Texture = icon;
            tex2.Texture = icon;
            string countTxt = (!s.IsEmpty && s.Count > 1) ? s.Count.ToString() : "";
            _hotbarLabels[i].Text    = countTxt;
            _invHotbarLabels[i].Text = countTxt;
        }

        // Sync selected block from hotbar
        var sel = _inventory.Slots[MainInvSize + _selectedSlot];
        _selectedBlockId = sel.IsEmpty ? "" : sel.ItemId;

        UpdateHotbarSelectionBorder();
        UpdateCursorVisual();
    }

    private void UpdateHotbarSelectionBorder()
    {
        for (int i = 0; i < HotbarSize; i++)
        {
            bool sel    = i == _selectedSlot;
            var  border = sel ? new Color(1f, 1f, 1f) : new Color(0.3f, 0.3f, 0.3f);
            int  bw     = sel ? 3 : 2;
            _hotbarSlots[i].AddThemeStyleboxOverride("panel",
                MakePanelStyle(new Color(0.15f, 0.15f, 0.15f, 0.85f), border, bw));
            _invHotbarSlots[i].AddThemeStyleboxOverride("panel",
                MakePanelStyle(new Color(0.15f, 0.15f, 0.15f, 0.85f), border, bw));
        }
    }

    private void UpdateCursorVisual()
    {
        if (_heldSlot.IsEmpty)
        {
            _cursorPanel.Visible = false;
            return;
        }
        _cursorTex.Texture   = GetItemIcon(_heldSlot.ItemId);
        _cursorLabel.Text    = _heldSlot.Count > 1 ? _heldSlot.Count.ToString() : "";
        _cursorPanel.Visible = _inventoryOpen;
    }

    private void FireChanged() => _inventory.OnInventoryChanged?.Invoke();

    // =========================================================================
    // PROCESS  (cursor follows mouse)
    // =========================================================================

    public override void _Process(double delta)
    {
        // Feedback message timer
        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= (float)delta;
            if (_feedbackTimer <= 0f && _chatFeedback != null)
                _chatFeedback.Visible = false;
        }

        if (!_inventoryOpen) return;

        var mouse = GetViewport().GetMousePosition();

        // Move cursor panel
        if (!_heldSlot.IsEmpty && _cursorPanel != null)
            _cursorPanel.GlobalPosition = mouse - new Vector2(SlotSize / 2f, SlotSize / 2f);

        // Poll slot under mouse every frame during a drag.
        // We do this in _Process because Godot captures GuiInput and MouseEntered
        // to the original pressed control during a mouse-button-held drag, so
        // neither signal reaches other panels. Polling GlobalPosition rects is
        // the only reliable cross-slot drag detection in Godot's UI system.
        if (_dragMode != DragMode.None)
        {
            int under = GetSlotUnderMouse();
            if (under >= 0 && under != _dragLastSlot)
            {
                _dragLastSlot = under;
                OnDragEnterSlot(under);
            }
        }
    }

    // =========================================================================
    // SAVE / LOAD
    // =========================================================================

    private void LoadInventoryFromSave()
    {
        var cm = GetTree().Root.FindChild("ChunkManager", true, false) as ChunkManager;
        if (cm == null) return;
        cm.LoadInventory(_inventory);
        RefreshAllSlotVisuals();

    }

    // =========================================================================
    // PHYSICS
    // =========================================================================

    public override void _PhysicsProcess(double delta)
    {
        UpdateBlockOutline();
        if (_stats == null || _stats.IsDead) return;
        if (_chatOpen) return; // freeze all movement while chat is open
        CheckWaterStatus();

        float dt         = (float)delta;
        Vector3 velocity = Velocity;

        // ── Fly mode (Creative only) ──────────────────────────────────────────
        if (_isFlying && GameModeManager.Instance?.IsCreate == true)
        {
            HandleFlyMovement(dt, ref velocity);
            Velocity = velocity;
            MoveAndSlide();
            return;
        }

        if (_isInWater)
        {
            velocity.Y -= (_gravity * 0.2f) * dt;
            velocity.Y  = Mathf.Clamp(velocity.Y, -2f, 8f);
        }
        else if (!IsOnFloor()) velocity.Y -= _gravity * dt;
        else { _hasDoubleJumped = false; _isGliding = false; }

        if (_isInWater)
        { if (Input.IsActionPressed("jump")) velocity.Y = Mathf.MoveToward(velocity.Y, 6f, 12f * dt); }
        else if (Input.IsActionPressed("jump") && IsOnFloor()) velocity.Y = JumpVelocity;

        if (Input.IsActionJustReleased("ui_cancel"))
        {
            if (_inventoryOpen)
                ToggleInventory();
            else if (_pauseMenu.IsOpen)
                _pauseMenu.Close();
            else
                _pauseMenu.Open();
        }

        _isCrouching = Input.IsActionPressed("crouch");
        bool wantsSprint = Input.IsActionPressed("sprint");
        if (wantsSprint && _stats.Stamina > 0 && !_isCrouching)
        { _isSprinting = true; _stats.UseStamina(SprintStaminaCost * dt); }
        else _isSprinting = false;

        Vector2 inputDir  = Input.GetVector("move_left","move_right","move_forward","move_back");
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        if (_isBreaking) { _breakTimer += dt; if (_breakTimer >= BreakInterval) { TryBreakBlock(); _breakTimer = 0f; } }
        if (_isPlacing)  { _placeTimer += dt; if (_placeTimer >= PlaceInterval) { TryPlaceBlock();  _placeTimer = 0f; } }

        float speed = _isCrouching ? CrouchSpeed : _isSprinting ? SprintSpeed : WalkSpeed;
        if (_isInWater) speed *= 0.5f;

        if (direction != Vector3.Zero)
        { velocity.X = direction.X * speed; velocity.Z = direction.Z * speed; }
        else
        { velocity.X = Mathf.MoveToward(velocity.X, 0, speed * dt * 10f);
          velocity.Z = Mathf.MoveToward(velocity.Z, 0, speed * dt * 10f); }

        Velocity = velocity;
        MoveAndSlide();
    }

    // =========================================================================
    // INPUT
    // =========================================================================

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (!_inventoryOpen && !_chatOpen)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                { _isBreaking = mb.Pressed; if (mb.Pressed) { TryBreakBlock(); _breakTimer = 0f; } }
                if (mb.ButtonIndex == MouseButton.Right)
                { _isPlacing = mb.Pressed; if (mb.Pressed) { TryPlaceBlock(); _placeTimer = 0f; } }

                // Scroll to cycle hotbar
                if (mb.ButtonIndex == MouseButton.WheelDown)
                    SelectHotbarSlot((_selectedSlot + 1) % HotbarSize);
                else if (mb.ButtonIndex == MouseButton.WheelUp)
                    SelectHotbarSlot((_selectedSlot - 1 + HotbarSize) % HotbarSize);
            }
        }

        if (@event is InputEventKey key && key.Pressed)
        {
            // Escape closes chat first, nothing else fires
            if (key.Keycode == Key.Escape && _chatOpen)
            { CloseChat(); return; }

            // Block all gameplay keys while chat is open (LineEdit handles typing)
            if (_chatOpen) return;

            if (key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9)
                SelectHotbarSlot((int)key.Keycode - (int)Key.Key1);
            else if (key.Keycode == Key.Key0)
                SelectHotbarSlot(9);
            else if (key.Keycode == Key.Minus)
                SelectHotbarSlot(10);
            else if (key.Keycode == Key.Equal)
                SelectHotbarSlot(11);

            if (key.Keycode == Key.Tab) ToggleInventory();

            // F4 — cycle gamemode
            if (key.Keycode == Key.F4)
            {
                GameModeManager.Instance?.CycleNext();
                var mode = GameModeManager.Instance?.Current;
                if (mode.HasValue)
                    ShowFeedback($"Gamemode: {mode.Value}");
            }

            // T — open chat/command bar
            if (key.Keycode == Key.T && !_chatOpen && !_inventoryOpen && !_pauseMenu.IsOpen)
                OpenChat();

            // Double-jump to toggle fly in Create mode
            if (key.Keycode == Key.Space && GameModeManager.Instance?.IsCreate == true)
            {
                double now = Time.GetTicksMsec() / 1000.0;
                if (now - _lastJumpTime < DoubleJumpWindow)
                    ToggleFly();
                _lastJumpTime = now;
            }

            if (key.Keycode == Key.F1)
            {
                _hudVisible = !_hudVisible;
                if (_hotbarLayer    != null) _hotbarLayer.Visible    = _hudVisible && !_inventoryOpen;
                if (_crosshairLayer != null) _crosshairLayer.Visible = _hudVisible;
            }
            if (key.Keycode == Key.F2)
            {
                var img  = GetViewport().GetTexture().GetImage();
                string p = $"user://screenshot_{Time.GetDatetimeStringFromSystem().Replace(":","-")}.png";
                img.SavePng(p);
                GD.Print($"Screenshot saved: {p}");
            }
            if (key.Keycode == Key.F3) { _showChunkBorders = !_showChunkBorders; ToggleChunkBorders(); }
            if (key.Keycode == Key.F5)
            {
                var cm = GetTree().Root.FindChild("ChunkManager", true, false) as ChunkManager;
                cm.Call("SaveModifiedChunks");
                cm.SaveInventory(_inventory);
                cm.SavePlayerPosition(GlobalPosition);
                GD.Print("World saved!");
            }
        }
    }

    private void SelectHotbarSlot(int slot)
    {
        if (slot < 0 || slot >= HotbarSize) return;
        _selectedSlot    = slot;
        var s            = _inventory.Slots[MainInvSize + slot];
        _selectedBlockId = s.IsEmpty ? "" : s.ItemId;
        UpdateHotbarSelectionBorder();
    }

    private void ToggleInventory()
    {
        _inventoryOpen           = !_inventoryOpen;
        _inventoryScreen.Visible = _inventoryOpen;

        if (_inventoryOpen)
        {
            _hotbarLayer.Visible = false;
            RefreshAllSlotVisuals();
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else
        {
            // Return held item to original slot if possible, else any free slot
            if (!_heldSlot.IsEmpty)
            {
                bool placed = false;
                if (_heldFromSlot >= 0 && _heldFromSlot < TotalSlots)
                {
                    var orig = _inventory.Slots[_heldFromSlot];
                    if (orig.IsEmpty)
                    {
                        orig.ItemId = _heldSlot.ItemId;
                        orig.Count  = _heldSlot.Count;
                        placed      = true;
                    }
                    else if (orig.ItemId == _heldSlot.ItemId && orig.Count < _inventory.MaxStackSize)
                    {
                        int fit      = Mathf.Min(_inventory.MaxStackSize - orig.Count, _heldSlot.Count);
                        orig.Count  += fit;
                        _heldSlot.Count -= fit;
                        placed = _heldSlot.Count <= 0;
                    }
                }
                if (!placed && _heldSlot.Count > 0)
                    AddItemToInventory(_heldSlot.ItemId, _heldSlot.Count);

                _heldSlot.Clear();
                UpdateCursorVisual();
            }

            EndDrag();
            _hotbarLayer.Visible = _hudVisible;
            Input.MouseMode      = Input.MouseModeEnum.Captured;
        }
    }

    // =========================================================================
    // BLOCK BREAK / PLACE
    // =========================================================================

    private void CheckWaterStatus()
    {
        var cm = GetTree().Root.FindChild("ChunkManager", true, false) as ChunkManager;
        if (cm == null) { _isInWater = false; return; }
        _isInWater = IsBlockWaterAt(cm, GlobalPosition + new Vector3(0, -0.9f, 0));
    }

    private bool IsBlockWaterAt(ChunkManager cm, Vector3 worldPos)
    {
        Chunk chunk = cm.GetChunk(cm.WorldToChunk(worldPos));
        if (chunk == null) return false;
        Vector3 lp = worldPos - chunk.GlobalPosition;
        return chunk.GetBlock(Mathf.FloorToInt(lp.X), Mathf.FloorToInt(lp.Y), Mathf.FloorToInt(lp.Z)).BlockId == "water";
    }

    private void UpdateBlockOutline()
    {
        if (!_rayCast.IsColliding()) { _blockOutline.Visible = false; return; }
        var col = _rayCast.GetCollider() as Node;
        if (col == null || !col.HasMeta("chunk")) { _blockOutline.Visible = false; return; }
        Vector3 inside = _rayCast.GetCollisionPoint() - _rayCast.GetCollisionNormal() * 0.5f;
        _blockOutline.GlobalPosition = new Vector3(Mathf.Floor(inside.X), Mathf.Floor(inside.Y), Mathf.Floor(inside.Z));
        _blockOutline.Visible = true;
    }

    private void TryBreakBlock()
    {
        if (!_rayCast.IsColliding()) return;

        var gm = GameModeManager.Instance;

        // Story mode: only break if holding a story tool (item with "storytool" tag — future)
        // For now, block breaking entirely in story mode
        if (gm != null && gm.IsStory) return;
        var col = _rayCast.GetCollider() as Node;
        if (col is Melon melon) { melon.Break(_inventory); return; }
        if (col == null || !col.HasMeta("chunk")) return;

        Chunk chunk  = (Chunk)col.GetMeta("chunk").AsGodotObject();
        Vector3 tPos = _rayCast.GetCollisionPoint() - _rayCast.GetCollisionNormal() * 0.5f;
        Vector3 lPos = tPos - chunk.GlobalPosition;
        int bx = Mathf.FloorToInt(lPos.X), by = Mathf.FloorToInt(lPos.Y), bz = Mathf.FloorToInt(lPos.Z);

        BlockState above = chunk.GetBlock(bx, by + 1, bz);
        if (above.BlockId is "rose" or "clover" or "dandelion")
        {
            if (above.BlockId == "rose")       AddItemToInventory("rose", 1);
            if (above.BlockId == "dandelion")  AddItemToInventory("dandelion", 1);
            chunk.SetBlock(bx, by + 1, bz, BlockState.Air);
            return;
        }

        BlockState b = chunk.GetBlock(bx, by, bz);
        if (!b.IsAir())
        {
            string drop = b.BlockId == "grass_block" ? "dirt" : b.BlockId;
            if (drop is not ("rose" or "dandelion" or "clover"))
                AddItemToInventory(drop, 1);
            chunk.SetBlock(bx, by, bz, BlockState.Air);
        }
    }

    private void TryPlaceBlock()
    {
        if (!_rayCast.IsColliding() || string.IsNullOrEmpty(_selectedBlockId)) return;

        var gm = GameModeManager.Instance;

        // Story mode: no placing
        if (gm != null && gm.IsStory) return;

        // Creative: don't consume inventory
        bool consume = gm == null || !gm.IsCreate;
        if (consume && !_inventory.HasItem(_selectedBlockId, 1)) { GD.Print($"No {_selectedBlockId}"); return; }

        var col = _rayCast.GetCollider() as Node;
        if (col == null || !col.HasMeta("chunk")) return;

        Chunk hitChunk      = (Chunk)col.GetMeta("chunk").AsGodotObject();
        Vector3 worldTarget = _rayCast.GetCollisionPoint() + _rayCast.GetCollisionNormal() * 0.5f;
        Vector3 center      = new(Mathf.Floor(worldTarget.X)+0.5f, Mathf.Floor(worldTarget.Y)+0.5f, Mathf.Floor(worldTarget.Z)+0.5f);
        if (center.DistanceTo(GlobalPosition) < 0.9f) return;

        var cm = hitChunk.GetParent() as ChunkManager;
        if (cm == null) return;
        Chunk tc = cm.GetChunk(cm.WorldToChunk(worldTarget));
        if (tc == null) return;

        Vector3 lp = worldTarget - tc.GlobalPosition;
        tc.SetBlock(Mathf.FloorToInt(lp.X), Mathf.FloorToInt(lp.Y), Mathf.FloorToInt(lp.Z),
            new BlockState { BlockId = _selectedBlockId, BitMask = 0xFF });
        if (consume) _inventory.RemoveItem(_selectedBlockId, 1);
    }

    // =========================================================================
    // CHUNK BORDERS
    // =========================================================================

    private void ToggleChunkBorders()
    {
        foreach (var m in _chunkBorderMeshes) m.QueueFree();
        _chunkBorderMeshes.Clear();
        if (!_showChunkBorders) return;

        var cm = GetTree().Root.FindChild("ChunkManager", true, false) as ChunkManager;
        if (cm == null) return;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1, 1, 0);
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

        foreach (var cp in cm.GetLoadedChunkPositions())
        {
            float x0=cp.X*Chunk.SIZE,x1=x0+Chunk.SIZE;
            float y0=cp.Y*Chunk.HEIGHT,y1=y0+Chunk.HEIGHT;
            float z0=cp.Z*Chunk.SIZE,z1=z0+Chunk.SIZE;
            var bst = new SurfaceTool();
            bst.Begin(Mesh.PrimitiveType.Lines);
            bst.AddVertex(new(x0,y0,z0)); bst.AddVertex(new(x1,y0,z0));
            bst.AddVertex(new(x1,y0,z0)); bst.AddVertex(new(x1,y0,z1));
            bst.AddVertex(new(x1,y0,z1)); bst.AddVertex(new(x0,y0,z1));
            bst.AddVertex(new(x0,y0,z1)); bst.AddVertex(new(x0,y0,z0));
            bst.AddVertex(new(x0,y1,z0)); bst.AddVertex(new(x1,y1,z0));
            bst.AddVertex(new(x1,y1,z0)); bst.AddVertex(new(x1,y1,z1));
            bst.AddVertex(new(x1,y1,z1)); bst.AddVertex(new(x0,y1,z1));
            bst.AddVertex(new(x0,y1,z1)); bst.AddVertex(new(x0,y1,z0));
            bst.AddVertex(new(x0,y0,z0)); bst.AddVertex(new(x0,y1,z0));
            bst.AddVertex(new(x1,y0,z0)); bst.AddVertex(new(x1,y1,z0));
            bst.AddVertex(new(x1,y0,z1)); bst.AddVertex(new(x1,y1,z1));
            bst.AddVertex(new(x0,y0,z1)); bst.AddVertex(new(x0,y1,z1));
            var brd = new MeshInstance3D { Mesh = bst.Commit(), MaterialOverride = mat };
            GetTree().Root.AddChild(brd);
            _chunkBorderMeshes.Add(brd);
        }
    }

    // =========================================================================
    // MISC
    // =========================================================================


    // Clean up all root-level nodes when player leaves the scene.
    // These were added directly to GetTree().Root so they survive scene changes
    // unless we manually free them here.
    public override void _ExitTree()
    {
        _hotbarLayer?.QueueFree();
        _chatLayer?.QueueFree();
        _crosshairLayer?.QueueFree();
        _inventoryLayer?.QueueFree();
        _cursorLayer?.QueueFree();
        _blockOutline?.QueueFree();

        foreach (var m in _chunkBorderMeshes)
            m?.QueueFree();
        _chunkBorderMeshes.Clear();
    }

    // =========================================================================
    // CHAT BAR + COMMAND SYSTEM
    // =========================================================================

    private void BuildChatBar()
    {
        _chatLayer             = new CanvasLayer();
        _chatLayer.Layer       = 15;
        _chatLayer.ProcessMode = ProcessModeEnum.Always;
        GetTree().Root.CallDeferred("add_child", _chatLayer);

        var bg       = new ColorRect();
        bg.Color     = new Color(0f, 0f, 0f, 0.55f);
        bg.AnchorLeft = 0f; bg.AnchorRight  = 0.6f;
        bg.AnchorTop  = 1f; bg.AnchorBottom = 1f;
        bg.OffsetTop  = -44f; bg.OffsetBottom = 0f;
        bg.Visible    = false;
        _chatLayer.CallDeferred("add_child", bg);

        _chatInput = new LineEdit();
        _chatInput.PlaceholderText = "Type a command... (e.g. /gamemode creative)";
        _chatInput.AnchorLeft      = 0f; _chatInput.AnchorRight  = 0.6f;
        _chatInput.AnchorTop       = 1f; _chatInput.AnchorBottom = 1f;
        _chatInput.OffsetTop       = -40f; _chatInput.OffsetBottom = -4f;
        _chatInput.OffsetLeft      = 8f;  _chatInput.OffsetRight  = -8f;
        _chatInput.Visible         = false;
        _chatInput.TextSubmitted   += OnChatSubmit;
        _chatLayer.CallDeferred("add_child", _chatInput);

        _chatFeedback = new Label();
        _chatFeedback.AnchorLeft   = 0f; _chatFeedback.AnchorRight  = 0.6f;
        _chatFeedback.AnchorTop    = 1f; _chatFeedback.AnchorBottom = 1f;
        _chatFeedback.OffsetTop    = -70f; _chatFeedback.OffsetBottom = -46f;
        _chatFeedback.OffsetLeft   = 8f;
        _chatFeedback.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.6f));
        _chatFeedback.AddThemeFontSizeOverride("font_size", 13);
        _chatFeedback.Visible      = false;
        _chatLayer.CallDeferred("add_child", _chatFeedback);
    }

    private void OpenChat()
    {
        _chatOpen          = true;
        _chatInput.Visible = true;
        _chatInput.Clear();
        _chatInput.GrabFocus();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void CloseChat()
    {
        _chatOpen          = false;
        _chatInput.Visible = false;
        _chatInput.ReleaseFocus();
        if (!_inventoryOpen && !_pauseMenu.IsOpen)
            Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OnChatSubmit(string text)
    {
        CloseChat();
        text = text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        ParseCommand(text);
    }

    private void ShowFeedback(string msg)
    {
        _chatFeedback.Text    = msg;
        _chatFeedback.Visible = true;
        _feedbackTimer        = FeedbackDuration;
    }

    private void ParseCommand(string input)
    {
        if (!input.StartsWith("/")) return;
        string[] parts = input.Substring(1).Split(' ');
        string cmd = parts[0].ToLower();

        switch (cmd)
        {
            case "admin":
                if (parts.Length < 2) { ShowFeedback("Usage: /admin <password>"); return; }
                if (SettingsManager.Instance.TryUnlockAdmin(parts[1]))
                    ShowFeedback("Admin access granted.");
                else
                    ShowFeedback("Incorrect password.");
                break;

            case "gamemode":
            case "gm":
                if (!SettingsManager.Instance.IsAdmin)
                { ShowFeedback("You need admin to use this command."); return; }
                if (parts.Length < 2) { ShowFeedback("Usage: /gamemode <creative|survival|story>"); return; }
                switch (parts[1].ToLower())
                {
                    case "create": case "creative": case "c": case "1":
                        GameModeManager.Instance.SetMode(GameModeManager.GameMode.Create);
                        ShowFeedback("Switched to Create mode."); break;
                    case "survival": case "s": case "0":
                        GameModeManager.Instance.SetMode(GameModeManager.GameMode.Survival);
                        ShowFeedback("Switched to Survival mode."); break;
                    case "story": case "st": case "2":
                        GameModeManager.Instance.SetMode(GameModeManager.GameMode.Story);
                        ShowFeedback("Switched to Story mode."); break;
                    default:
                        ShowFeedback($"Unknown gamemode: {parts[1]}"); break;
                }
                break;

            case "fly":
                if (!SettingsManager.Instance.IsAdmin && !GameModeManager.Instance.IsCreate)
                { ShowFeedback("Fly is only available in Create mode."); return; }
                ToggleFly();
                ShowFeedback(_isFlying ? "Flying: ON" : "Flying: OFF");
                break;

            default:
                ShowFeedback($"Unknown command: /{cmd}");
                break;
        }
    }

    // =========================================================================
    // GAMEMODE CHANGED
    // =========================================================================

    private void OnGameModeChanged(GameModeManager.GameMode mode)
    {
        if (mode != GameModeManager.GameMode.Create && _isFlying)
            SetFlying(false);
    }

    // =========================================================================
    // FLY MODE
    // =========================================================================

    private void ToggleFly() => SetFlying(!_isFlying);

    private void SetFlying(bool fly)
    {
        _isFlying = fly;
        if (!fly) { var v = Velocity; v.Y = 0f; Velocity = v; }
    }

    private void HandleFlyMovement(float dt, ref Vector3 velocity)
    {
        Vector2 inputDir  = Input.GetVector("move_left","move_right","move_forward","move_back");
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        float   speed     = _isSprinting ? _flySpeed * 2f : _flySpeed;

        velocity.X = direction.X * speed;
        velocity.Z = direction.Z * speed;

        if (Input.IsActionPressed("jump"))
            velocity.Y = _flyVertSpeed;
        else if (Input.IsActionPressed("crouch"))
            velocity.Y = -_flyVertSpeed;
        else
            velocity.Y = Mathf.MoveToward(velocity.Y, 0f, _flyVertSpeed * dt * 10f);
    }

    public PlayerStats  GetStats()        => _stats;

    // Called by PauseMenu when saving before quitting to main menu
    public void SaveInventoryFromPauseMenu(ChunkManager cm)
    {
        cm.SaveInventory(_inventory);
        cm.SavePlayerPosition(GlobalPosition);
    }
    public PlayerCamera GetPlayerCamera() => _playerCamera;

    public void ApplyGearMovement(ItemResource gear)
    {
        if (gear.GrantsDoubleJump) CanDoubleJump = true;
        if (gear.GrantsWallClimb) CanWallClimb  = true;
        if (gear.GrantsGliding)   CanGlide       = true;
        if (gear.GrantsGrapple)   CanGrapple     = true;
        WalkSpeed += gear.BonusMovementSpeed;
    }

    public void RemoveGearMovement(ItemResource gear)
    {
        if (gear.GrantsDoubleJump) CanDoubleJump = false;
        if (gear.GrantsWallClimb) CanWallClimb  = false;
        if (gear.GrantsGliding)   CanGlide       = false;
        if (gear.GrantsGrapple)   CanGrapple     = false;
        WalkSpeed -= gear.BonusMovementSpeed;
    }
}