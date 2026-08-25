using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
    [Export] public float WalkSpeed         { get; set; } = 5f;
    [Export] public float SprintSpeed       { get; set; } = 8f;
    [Export] public float CrouchSpeed       { get; set; } = 2.5f;
    [Export] public float JumpVelocity      { get; set; } = 6f;
    [Export] public float SprintStaminaCost { get; set; } = 1f;
    [Export] public float JumpStaminaCost   { get; set; } = 10f;

    private float _gravity = 20f;
    private PlayerStats  _stats;
    private PlayerCamera _playerCamera;
    private RayCast3D    _rayCast;
    private RandomNumberGenerator _dropRng = new RandomNumberGenerator();
    private bool  _isSprinting = false;
    private bool  _isCrouching = false;
    private bool  _hasDoubleJumped = false;
    private bool  _isGliding   = false;
    private bool  _isPlacing   = false;
    private bool  _isInWater   = false;
    private float _placeTimer  = 0f;
    private const float PlaceInterval = 0.15f;
    private bool  _isBreaking  = false;
    
    private float _breakTimer  = 0f;
    private const float BreakInterval = 0.4f;
    private bool  _isDropping  = false;
    private float _dropTimer   = 0f;
    private const float DropInterval    = 0.25f; // how often it drops again while Q is held
    private const float DropForwardSpeed = 7.5f;  // pushed out further
    private const float DropUpSpeed      = 1.0f;  // less of an upward pop now
    private const float DropSpawnHeight  = 1.0f;  // roughly chest/face height instead of over the top of the head
    private string         _selectedBlockId    = "";
    private MeshInstance3D _blockOutline;

    // ── Break overlay ─────────────────────────────────────────────────────────
    private BlockBreakOverlay _breakOverlay;
    private int               _breakHitCount      = 0;
    private int               _breakMiningPower   = 1;
    private Vector3I          _breakTargetBlock   = new Vector3I(int.MinValue, 0, 0);
    private string            _breakTargetBlockId = "";

    // ── Combat ───────────────────────────────────────────────────────────────
    private const float UnarmedAttackDamage = 2f; // TODO: replace with per-weapon damage once weapon stats exist

    // ── Hotbar ───────────────────────────────────────────────────────────────
    private const int HotbarSize = 12;
    private Panel[]       _hotbarSlots     = new Panel[HotbarSize];
    private Panel[]       _invHotbarSlots  = new Panel[HotbarSize];
    private Label[]       _hotbarLabels    = new Label[HotbarSize];
    private Label[]       _invHotbarLabels = new Label[HotbarSize];
    private int           _selectedSlot    = 0;

    // ── Inventory ─────────────────────────────────────────────────────────────
    // Slots 0-35  = main inventory (3×12)
    // Slots 36-47 = hotbar (1×12)
    private const int MainInvSize = 48;
    private const int TotalSlots  = MainInvSize + HotbarSize; // 48
    private Inventory _inventory;
    private Panel[]       _invSlotPanels = new Panel[MainInvSize];
    private ItemTooltip   _invTooltip;
    private ItemTooltip   _hotbarTooltip; // separate instance for the always-on-screen HUD hotbar, so it works even when the full inventory isn't open
    private Label[]       _invSlotLabels = new Label[MainInvSize];

    // ── Held / cursor ─────────────────────────────────────────────────────────
    private InventorySlot _heldSlot     = new InventorySlot();
    private int           _heldFromSlot = -1;
    private Panel         _cursorPanel;
    private Label         _cursorLabel;
    private TextureRect   _cursorTex;

    // ── Drag ─────────────────────────────────────────────────────────────────
    private enum DragMode { None, LmbWithItem, LmbNoItem, RmbDrag, ShiftLmbNoItem }
    private DragMode  _dragMode      = DragMode.None;
    private List<int> _dragVisited   = new List<int>();
    private int       _dragOrigCount = 0;
    private int       _dragLastSlot  = -1;

    // ── UI layers ─────────────────────────────────────────────────────────────
    private Control     _inventoryScreen;
    private bool        _inventoryOpen    = false;
    private bool        _showChunkBorders = false;
    private bool        _hudVisible       = true;
    private CanvasLayer _hotbarLayer;
    private CanvasLayer _crosshairLayer;
    private CanvasLayer _inventoryLayer;
    private CanvasLayer _cursorLayer;
    private PauseMenu   _pauseMenu;
    // Calendar HUD
    private CalendarPanel _calendarPanel;
    private CanvasLayer _calendarLayer;

    // ── Crafting ──────────────────────────────────────────────────────────────
    private CraftingPanel _craftingPanel;
    private CanvasLayer   _craftingLayer;
    private const float   CraftingTableRange = 5f;
    // ── Equipment ─────────────────────────────────────────────────────────────
    private EquipmentPanel _equipmentPanel;
    private CanvasLayer    _equipmentLayer;

    // ── Tool Bench ────────────────────────────────────────────────────────────
    private ToolBenchPanel _toolBenchPanel;

    // ── Station tab bar (Crafter / Tool Bench / others in range) ────────────────
    private Control _stationTabBar;
    private Panel   _tabCrafterStation;
    private Panel   _tabToolBenchStation;
    private Label   _tabCrafterStationLbl;
    private Label   _tabToolBenchStationLbl;
    private string  _activeStation = "crafter"; // "crafter" or "tool_bench"

    // ── Creative menu ─────────────────────────────────────────────────────────
    // Item browser for Create mode — see CreativeMenu.cs / ItemCatalog.cs.
    // Opened/closed with V (see _UnhandledInput), independent of Tab/inventory.
    private CreativeMenu _creativeMenu;
    private CanvasLayer  _creativeMenuLayer;
    private bool         _creativeMenuOpen = false;

    // ── Stats HUD ─────────────────────────────────────────────────────────────
    private StatsHud    _statsHud;
    private CanvasLayer _statsHudLayer;

    // ── Gamemode / fly / chat ─────────────────────────────────────────────────
    private bool        _isFlying      = false;
    private float       _flySpeed      = 12f;
    private float       _flyVertSpeed  = 8f;
    private bool        _chatOpen      = false;
    private CanvasLayer _chatLayer;
    private LineEdit    _chatInput;
    private Label       _chatFeedback;
    private float       _feedbackTimer  = 0f;
    private const float FeedbackDuration = 3f;
    private double      _lastJumpTime   = 0.0;
    private const double DoubleJumpWindow = 0.35;

    private readonly List<MeshInstance3D> _chunkBorderMeshes = new();

    public bool CanDoubleJump { get; set; } = false;
    public bool CanWallClimb  { get; set; } = false;
    public bool CanGlide      { get; set; } = false;
    public bool CanGrapple    { get; set; } = false;

    private const int SlotSize = 56;
    private const int SlotGap  = 5;

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
        _dropRng.Randomize();

        // Lets ItemPickup (and anything else) reliably find the player node
        // regardless of what it's actually named in the scene tree.
        AddToGroup("player");

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

        // Break overlay
        _breakOverlay = new BlockBreakOverlay();
        GetTree().Root.CallDeferred("add_child", _breakOverlay);

        BuildHotbarHUD();
        BuildInventoryScreen();
        BuildCursorPanel();
        BuildCraftingPanel();
        BuildToolBenchPanel();
        BuildStationTabBar();
        BuildEquipmentPanel();
        BuildCreativeMenu();

        if (_stats == null) { _stats = new PlayerStats(); AddChild(_stats); }
        if (_playerCamera == null)
        {
            _playerCamera          = new PlayerCamera();
            _playerCamera.Position = new Vector3(0, 1.6f, 0);
            AddChild(_playerCamera);
        }

        BuildStatsHud();
        BuildCalendarPanel();
        _equipmentPanel.Init(_stats);

        Input.MouseMode = Input.MouseModeEnum.Captured;
        _inventory.OnInventoryChanged += RefreshAllSlotVisuals;
        RefreshAllSlotVisuals();
        SelectHotbarSlot(0);

        _pauseMenu = new PauseMenu();
        _pauseMenu.Init(this);
        AddChild(_pauseMenu);

        BuildChatBar();

        if (GameModeManager.Instance != null)
            GameModeManager.Instance.OnGameModeChanged += OnGameModeChanged;
        else
            GD.PrintErr("GameModeManager not found — add to Autoload.");

        GD.Print("Player ready.");
    }

    // =========================================================================
    // TEXTURE LOADING
    // =========================================================================

    private Texture2D _unknownItemIconTex; // fallback for any item whose real icon can't be found - loaded once, lazily

   private Texture2D GetItemIcon(string itemId)
{
    if (string.IsNullOrEmpty(itemId))
        return null;

    if (_iconCache.TryGetValue(itemId, out var cached))
        return cached;

    // ------------------------------------------------------------
    // 1. Prefer the icon already assigned to the ItemResource.
    // ------------------------------------------------------------
    // This is important for tools and crafted items that generate
    // their icons dynamically.

    var item =
        ItemRegistry.Instance?.GetItem(itemId);

    Texture2D tex =
        item?.Icon;


    // ------------------------------------------------------------
    // 2. Normal item texture
    // ------------------------------------------------------------

    if (tex == null)
    {
        string path =
            $"res://Assets/Textures/Items/{itemId}.png";

        if (ResourceLoader.Exists(path))
        {
            tex =
                ResourceLoader.Load<Texture2D>(
                    path
                );
        }
    }


    // ------------------------------------------------------------
    // 3. Ore texture
    // ------------------------------------------------------------
    // Sun and Moon shards are stored here:
    //
    // Assets/Textures/Items/ore/sun_shard.png
    // Assets/Textures/Items/ore/moon_shard.png

    if (tex == null)
    {
        string orePath =
            $"res://Assets/Textures/Items/ore/{itemId}.png";

        if (ResourceLoader.Exists(orePath))
        {
            tex =
                ResourceLoader.Load<Texture2D>(
                    orePath
                );
        }
    }


    // ------------------------------------------------------------
    // 4. Block texture fallback
    // ------------------------------------------------------------

    if (tex == null)
    {
        string blockPath =
            $"res://Assets/Textures/Blocks/{itemId}.png";

        if (ResourceLoader.Exists(blockPath))
        {
            tex =
                ResourceLoader.Load<Texture2D>(
                    blockPath
                );
        }
    }


    // ------------------------------------------------------------
    // 5. Unknown item placeholder
    // ------------------------------------------------------------

    if (tex == null)
    {
        if (_unknownItemIconTex == null)
        {
            _unknownItemIconTex =
                ResourceLoader.Load<Texture2D>(
                    "res://Assets/Textures/Items/tool/chalk/unknown.png"
                );
        }

        tex =
            _unknownItemIconTex;
    }


    // ------------------------------------------------------------
    // 6. Cache the result
    // ------------------------------------------------------------

    _iconCache[itemId] =
        tex;

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

            // Hover tooltip - this is the persistent HUD hotbar, not the
            // one inside the full inventory screen, so it needs its own
            // wiring (was missing entirely before, which is why hovering
            // it while just playing showed nothing).
            slot.MouseFilter = Control.MouseFilterEnum.Stop;
            int idx = i;
            slot.MouseEntered += () => OnHudHotbarSlotMouseEntered(idx);
            slot.MouseExited  += () => _hotbarTooltip?.HideTooltip();
        }
        _hotbarLayer.CallDeferred("add_child", container);

        _hotbarTooltip = new ItemTooltip();
        _hotbarLayer.CallDeferred("add_child", _hotbarTooltip); // added after container = renders above the slots
    }

    private void BuildInventoryScreen()
    {
        _inventoryLayer       = new CanvasLayer();
        _inventoryLayer.Layer = 10;
        GetTree().Root.CallDeferred("add_child", _inventoryLayer);

        float gridW      = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;
        float mainH      = 4 * SlotSize + 3 * SlotGap;
        float sectionGap = 14f;
        float topPad     = 14f;  // matches CraftingPanel/EquipmentPanel so slot rows align
        float pad        = 16f;
        float totalW     = gridW + pad * 2f;
        float totalH     = topPad + mainH + sectionGap + SlotSize + pad;

        _inventoryScreen              = new Panel();
        _inventoryScreen.AnchorLeft   = 0.5f; _inventoryScreen.AnchorRight  = 0.5f;
        _inventoryScreen.AnchorTop    = 0.5f; _inventoryScreen.AnchorBottom = 0.5f;
        _inventoryScreen.OffsetLeft   = -totalW / 2f;
        _inventoryScreen.OffsetRight  =  totalW / 2f;
        _inventoryScreen.OffsetTop    = -180f;
        _inventoryScreen.OffsetBottom = -180f + totalH;
        _inventoryScreen.AddThemeStyleboxOverride("panel",
            MakePanelStyle(new Color(0.08f, 0.08f, 0.08f, 0.95f), new Color(0.5f, 0.5f, 0.5f)));

        var mainGrid = new GridContainer();
        mainGrid.Columns  = HotbarSize;
        mainGrid.Position = new Vector2(pad, topPad);
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
            slot.MouseEntered += () => slot.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0.15f,0.15f,0.15f,0.85f), new Color(0.75f,0.75f,0.75f)));
            slot.MouseExited  += () => slot.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0.15f,0.15f,0.15f,0.85f), new Color(0.4f,0.4f,0.4f)));
            slot.MouseExited  += () => _invTooltip?.HideTooltip();
            mainGrid.AddChild(slot);
        }
        _inventoryScreen.AddChild(mainGrid);

        var divider = new ColorRect();
        divider.Color    = new Color(0.35f, 0.35f, 0.35f);
        divider.Position = new Vector2(pad, topPad + mainH + sectionGap / 2f - 1f);
        divider.Size     = new Vector2(gridW, 2f);
        _inventoryScreen.AddChild(divider);

        var hotbarRow = new HBoxContainer();
        hotbarRow.Position = new Vector2(pad, topPad + mainH + sectionGap);
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
            slot.MouseExited  += ()             => _invTooltip?.HideTooltip();
            slot.MouseFilter   = Control.MouseFilterEnum.Stop;
            hotbarRow.AddChild(slot);
        }
        _inventoryScreen.AddChild(hotbarRow);

        _invTooltip = new ItemTooltip();
        _inventoryScreen.AddChild(_invTooltip); // added last = renders above every slot

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

    private void BuildCalendarPanel()
    {
        _calendarLayer = new CanvasLayer();
        _calendarLayer.Layer = 12;
        GetTree().Root.CallDeferred("add_child", _calendarLayer);

        _calendarPanel = new CalendarPanel();
        _calendarLayer.CallDeferred("add_child", _calendarPanel);
    }

    private void BuildCraftingPanel()
    {
        _craftingLayer       = new CanvasLayer();
        _craftingLayer.Layer = 11;
        GetTree().Root.CallDeferred("add_child", _craftingLayer);

        _craftingPanel = new CraftingPanel();
        _craftingPanel.Init(_inventory);
        _craftingPanel.OnSlotClicked += HandleCraftSlotClicked;
        _craftingPanel.OnOutputClicked += HandleOutputClicked;
        _craftingPanel.OnLearnedCraftClicked += HandleLearnedCraftClicked;

        float gridW    = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;
        float pad      = 16f;
        float totalW   = gridW + pad * 2f;
        float invHalfW = totalW / 2f;

        _craftingPanel.AnchorLeft   = 0.5f;
        _craftingPanel.AnchorRight  = 0.5f;
        _craftingPanel.AnchorTop    = 0.5f;
        _craftingPanel.AnchorBottom = 0.5f;
        _craftingPanel.OffsetLeft   = -invHalfW - 10f - 300f;
        _craftingPanel.OffsetRight  = -invHalfW - 10f;
        _craftingPanel.OffsetTop    = -180f;
        _craftingPanel.OffsetBottom =  159f;
        _craftingPanel.Visible      = false;

        _craftingLayer.CallDeferred("add_child", _craftingPanel);
    }

    private void BuildToolBenchPanel()
    {
        // Shares _craftingLayer (built in BuildCraftingPanel, which always
        // runs first) so it sorts on the same layer as the Crafter panel.
        _toolBenchPanel = new ToolBenchPanel();
        _toolBenchPanel.Init(_inventory);
        _toolBenchPanel.OnSlotClicked   += HandleToolBenchSlotClicked;
        _toolBenchPanel.OnCenterClicked += HandleToolBenchCenterClicked;
        _toolBenchPanel.OnOutputClicked += HandleToolBenchOutputClicked;

        float gridW    = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;
        float pad      = 16f;
        float totalW   = gridW + pad * 2f;
        float invHalfW = totalW / 2f;

        // Identical anchors/offsets to _craftingPanel so the two panels sit
        // in exactly the same screen position and overlap when swapped.
        _toolBenchPanel.AnchorLeft   = 0.5f;
        _toolBenchPanel.AnchorRight  = 0.5f;
        _toolBenchPanel.AnchorTop    = 0.5f;
        _toolBenchPanel.AnchorBottom = 0.5f;
        _toolBenchPanel.OffsetLeft   = -invHalfW - 10f - 300f;
        _toolBenchPanel.OffsetRight  = -invHalfW - 10f;
        _toolBenchPanel.OffsetTop    = -180f;
        _toolBenchPanel.OffsetBottom =  159f;
        _toolBenchPanel.Visible      = false;

        _craftingLayer.CallDeferred("add_child", _toolBenchPanel);
    }

    private void BuildStationTabBar()
    {
        float gridW    = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;
        float pad      = 16f;
        float totalW   = gridW + pad * 2f;
        float invHalfW = totalW / 2f;

        _stationTabBar = new Control();
        _stationTabBar.AnchorLeft   = 0.5f;
        _stationTabBar.AnchorRight  = 0.5f;
        _stationTabBar.AnchorTop    = 0.5f;
        _stationTabBar.AnchorBottom = 0.5f;
        _stationTabBar.OffsetLeft   = -invHalfW - 10f - 300f;
        _stationTabBar.OffsetRight  = -invHalfW - 10f;
        _stationTabBar.OffsetTop    = -180f - 28f;
        _stationTabBar.OffsetBottom = -180f - 4f;
        _stationTabBar.Visible      = false;

        _tabCrafterStation    = MakeStationTab("Crafter",    0f,   out _tabCrafterStationLbl);
        _tabToolBenchStation  = MakeStationTab("Tool Bench", 96f,  out _tabToolBenchStationLbl);

        _tabCrafterStation.GuiInput   += (InputEvent ev) => { if (IsStationTabClick(ev)) SwitchStation("crafter"); };
        _tabToolBenchStation.GuiInput += (InputEvent ev) => { if (IsStationTabClick(ev)) SwitchStation("tool_bench"); };

        _stationTabBar.AddChild(_tabCrafterStation);
        _stationTabBar.AddChild(_tabToolBenchStation);

        _craftingLayer.CallDeferred("add_child", _stationTabBar);
    }

    private Panel MakeStationTab(string text, float x, out Label label)
    {
        var tab = new Panel();
        tab.Position          = new Vector2(x, 0f);
        tab.CustomMinimumSize = new Vector2(90f, 24f);
        tab.MouseFilter       = Control.MouseFilterEnum.Stop;

        label = new Label();
        label.Text                = text;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment   = VerticalAlignment.Center;
        label.AnchorRight         = 1f; label.AnchorBottom = 1f;
        label.AddThemeFontSizeOverride("font_size", 11);
        label.MouseFilter         = Control.MouseFilterEnum.Ignore;
        tab.AddChild(label);

        return tab;
    }

    private bool IsStationTabClick(InputEvent ev) =>
        ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;

    private void SwitchStation(string stationId)
    {
        _activeStation = stationId;
        if (_craftingPanel  != null) _craftingPanel.Visible  = (stationId == "crafter");
        if (_toolBenchPanel != null) _toolBenchPanel.Visible = (stationId == "tool_bench");
        SetStationTabStyle(_tabCrafterStation,   _tabCrafterStationLbl,   stationId == "crafter");
        SetStationTabStyle(_tabToolBenchStation, _tabToolBenchStationLbl, stationId == "tool_bench");
    }

    private void SetStationTabStyle(Panel tab, Label label, bool active)
    {
        var s = new StyleBoxFlat();
        s.BgColor         = active ? new Color(0.2f, 0.2f, 0.25f) : new Color(0.12f, 0.12f, 0.15f);
        s.BorderColor     = active ? new Color(0.6f, 0.6f, 0.7f)  : new Color(0.3f, 0.3f, 0.35f);
        s.BorderWidthTop  = 2; s.BorderWidthBottom = 2;
        s.BorderWidthLeft = 2; s.BorderWidthRight  = 2;
        tab.AddThemeStyleboxOverride("panel", s);
        label.AddThemeColorOverride("font_color", active ? new Color(1f,1f,1f) : new Color(0.55f,0.55f,0.55f));
    }

    private void BuildEquipmentPanel()
    {
        _equipmentLayer       = new CanvasLayer();
        _equipmentLayer.Layer = 11;
        GetTree().Root.CallDeferred("add_child", _equipmentLayer);

        _equipmentPanel = new EquipmentPanel();

        float gridW    = HotbarSize * SlotSize + (HotbarSize - 1) * SlotGap;
        float pad      = 16f;
        float totalW   = gridW + pad * 2f;
        float invHalfW = totalW / 2f;

        // Mirrors the crafting panel, but on the right (crafting is on the left).
        _equipmentPanel.AnchorLeft   = 0.5f;
        _equipmentPanel.AnchorRight  = 0.5f;
        _equipmentPanel.AnchorTop    = 0.5f;
        _equipmentPanel.AnchorBottom = 0.5f;
        _equipmentPanel.OffsetLeft   = invHalfW + 10f;
        _equipmentPanel.OffsetRight  = invHalfW + 10f + 300f;
        _equipmentPanel.OffsetTop    = -180f;
        _equipmentPanel.OffsetBottom =  159f;
        _equipmentPanel.Visible      = false;

        _equipmentLayer.CallDeferred("add_child", _equipmentPanel);
    }

    private void BuildCreativeMenu()
    {
        _creativeMenuLayer       = new CanvasLayer();
        _creativeMenuLayer.Layer = 12;
        GetTree().Root.CallDeferred("add_child", _creativeMenuLayer);

        _creativeMenu = new CreativeMenu();

        float w = CreativeMenu.TotalWidth;
        float h = CreativeMenu.TotalHeight;
        _creativeMenu.AnchorLeft   = 0.5f; _creativeMenu.AnchorRight  = 0.5f;
        _creativeMenu.AnchorTop    = 0.5f; _creativeMenu.AnchorBottom = 0.5f;
        _creativeMenu.OffsetLeft   = -w / 2f;
        _creativeMenu.OffsetRight  =  w / 2f;
        _creativeMenu.OffsetTop    = -h / 2f;
        _creativeMenu.OffsetBottom =  h / 2f;
        _creativeMenu.Visible      = false;
        _creativeMenu.OnItemChosen += OnCreativeItemChosen;

        _creativeMenuLayer.CallDeferred("add_child", _creativeMenu);
        _creativeMenu.CallDeferred(nameof(CreativeMenu.Init));
    }

    private void BuildStatsHud()
    {
        _statsHudLayer = new CanvasLayer();
        GetTree().Root.CallDeferred("add_child", _statsHudLayer);

        _statsHud = new StatsHud();
        _statsHud.AnchorLeft     = 0.5f; _statsHud.AnchorRight  = 0.5f;
        _statsHud.AnchorTop      = 1f;   _statsHud.AnchorBottom = 1f;
        _statsHud.OffsetLeft     = 0f;   _statsHud.OffsetRight  = 0f;
        _statsHud.OffsetTop      = -74f; _statsHud.OffsetBottom = -74f;
        _statsHud.GrowHorizontal = Control.GrowDirection.Both;
        _statsHud.GrowVertical   = Control.GrowDirection.Begin;
        _statsHud.MouseFilter    = Control.MouseFilterEnum.Ignore;
        _statsHud.Init(_stats);

        _statsHudLayer.CallDeferred("add_child", _statsHud);
    }

    // ── Slot factory ─────────────────────────────────────────────────────────

    private Panel MakeSlotPanel(int size)
    {
        var slot = new Panel();
        slot.CustomMinimumSize = new Vector2(size, size);
        slot.AddThemeStyleboxOverride("panel",
            MakePanelStyle(new Color(0.15f, 0.15f, 0.15f, 0.85f), new Color(0.4f, 0.4f, 0.4f)));
        return slot;
    }

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

    private Label MakeKeyHintLabel(int i)
    {
        var lbl = new Label();
        lbl.Text = i < 9 ? (i + 1).ToString() : i == 9 ? "0" : i == 10 ? "-" : "+";
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
        s.BgColor        = bg;      s.BorderColor      = border;
        s.BorderWidthTop = bw;      s.BorderWidthBottom = bw;
        s.BorderWidthLeft = bw;     s.BorderWidthRight  = bw;
        s.CornerRadiusTopLeft     = 3; s.CornerRadiusTopRight    = 3;
        s.CornerRadiusBottomLeft  = 3; s.CornerRadiusBottomRight = 3;
        return s;
    }

    // =========================================================================
    // CRAFTING PROXIMITY
    // =========================================================================

    private static readonly string[] StationBlockIds = { "crafter", "tool_bench" };

    private HashSet<string> GetNearbyStationTypes()
    {
        var found = new HashSet<string>();
        var cm = GetTree().Root.FindChild("ChunkManager", true, false) as ChunkManager;
        if (cm == null) return found;

        Vector3 pos = GlobalPosition;
        for (int dx = -5; dx <= 5; dx++)
        for (int dy = -3; dy <= 3; dy++)
        for (int dz = -5; dz <= 5; dz++)
        {
            if (new Vector3(dx, dy, dz).Length() > CraftingTableRange) continue;
            var block = cm.GetBlockAtWorld(new Vector3I(
                Mathf.FloorToInt(pos.X) + dx,
                Mathf.FloorToInt(pos.Y) + dy,
                Mathf.FloorToInt(pos.Z) + dz));
            if (System.Array.IndexOf(StationBlockIds, block.BlockId) >= 0)
                found.Add(block.BlockId);
        }
        return found;
    }

    private void UpdateCraftingProximity()
    {
        if (!_inventoryOpen || RecipeManager.Instance == null) return;

        var nearby = GetNearbyStationTypes();
        bool nearTable = nearby.Contains("crafter");
        _craftingPanel?.SetGridSize(nearTable ? 3 : 2, nearTable);

        bool showBar = nearby.Count > 0;
        if (_stationTabBar != null) _stationTabBar.Visible = showBar;
        if (_tabCrafterStation   != null) _tabCrafterStation.Visible   = nearby.Contains("crafter");
        if (_tabToolBenchStation != null) _tabToolBenchStation.Visible = nearby.Contains("tool_bench");

        // If the currently active station walked out of range, fall back to
        // whichever tab is still available (or hide the tab bar entirely).
        if (_activeStation == "tool_bench" && !nearby.Contains("tool_bench") && nearby.Contains("crafter"))
            SwitchStation("crafter");
        else if (_activeStation == "crafter" && !nearby.Contains("crafter") && nearby.Contains("tool_bench"))
            SwitchStation("tool_bench");
    }

    private bool TryOpenCraftingTable()
    {
        if (!_rayCast.IsColliding()) return false;
        var col = _rayCast.GetCollider() as Node;
        if (col == null || !col.HasMeta("chunk")) return false;
        Chunk chunk  = (Chunk)col.GetMeta("chunk").AsGodotObject();
        Vector3 tPos = _rayCast.GetCollisionPoint() - _rayCast.GetCollisionNormal() * 0.5f;
        Vector3 lPos = tPos - chunk.GlobalPosition;
        var b = chunk.GetBlock(Mathf.FloorToInt(lPos.X), Mathf.FloorToInt(lPos.Y), Mathf.FloorToInt(lPos.Z));

        if (b.BlockId != "crafter" && b.BlockId != "tool_bench") return false;

        if (!_inventoryOpen) ToggleInventory();
        _hotbarLayer.Visible = false;
        if (_craftingPanel  != null) { _craftingPanel.SetGridSize(3, true); UpdateCraftingProximity(); }
        if (_equipmentPanel != null) _equipmentPanel.Visible = true;

        SwitchStation(b.BlockId);
        return true;
    }

    // =========================================================================
    // SLOT INPUT ROUTING
    // =========================================================================

    private void OnInvSlotInput(InputEvent ev, int slotIndex)
    {
        if (!_inventoryOpen) return;
        HandleSlotInput(ev, slotIndex);
    }

    // Is this item allowed to stack by Count? False for crafted tools
    // (IsStackable=false on their ItemResource) - unknown/unregistered
    // item ids default to stackable, matching the old blanket behavior.
    private bool IsStackableItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return true;
        var item = ItemRegistry.Instance?.GetItem(itemId);
        return item == null || item.IsStackable;
    }

    // Copies EVERYTHING about a slot's contents - ItemId, Count, and (unlike
    // the old plain ItemId/Count-only copies scattered through this file)
    // CurrentDurability and CustomName too. Use this instead of manually
    // copying ItemId+Count anywhere a tool might be involved.
    private void CopySlot(InventorySlot from, InventorySlot to)
    {
        to.ItemId             = from.ItemId;
        to.Count               = from.Count;
        to.CurrentDurability   = from.CurrentDurability;
        to.CustomName          = from.CustomName;
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
                case MouseButton.Left when shift:           ShiftClick(slotIndex); break;
                case MouseButton.Left when mb.DoubleClick:  DoubleClickCollect(slotIndex); break;
                case MouseButton.Left:                      HandleLeftClick(slotIndex); break;
                case MouseButton.Right:                     HandleRightClick(slotIndex); break;
                case MouseButton.WheelUp:                   ScrollSlot(slotIndex, up: true); break;
                case MouseButton.WheelDown:                 ScrollSlot(slotIndex, up: false); break;
            }
        }
    }

    // =========================================================================
    // LEFT CLICK
    // =========================================================================

    private void HandleLeftClick(int slotIndex)
    {
        var slot = _inventory.Slots[slotIndex];

        if (_heldSlot.IsEmpty)
        {
            if (slot.IsEmpty) return;
            _heldFromSlot  = slotIndex;
            CopySlot(slot, _heldSlot);
            _dragOrigCount = slot.Count;
            slot.Clear();
            _dragMode = DragMode.LmbWithItem;
            _dragVisited.Clear();
            _dragVisited.Add(slotIndex);
            _dragLastSlot = slotIndex;
        }
        else
        {
            if (slot.IsEmpty)
            {
                CopySlot(_heldSlot, slot);
                _heldSlot.Clear();
            }
            else if (slot.ItemId == _heldSlot.ItemId && IsStackableItem(slot.ItemId))
            {
                int space    = _inventory.MaxStackSize - slot.Count;
                int transfer = Mathf.Min(space, _heldSlot.Count);
                slot.Count      += transfer;
                _heldSlot.Count -= transfer;
                if (_heldSlot.Count <= 0) _heldSlot.Clear();
            }
            else if (slot.ItemId == _heldSlot.ItemId && !IsStackableItem(slot.ItemId))
            {
                // Non-stackable (e.g. a crafted tool) - merge durability
                // instead of stacking Count, same idea as Inventory.TryMergeTools
                // (allowed to exceed max), only if the names also match.
                if (slot.CustomName == _heldSlot.CustomName)
                {
                    slot.CurrentDurability += _heldSlot.CurrentDurability;
                    _heldSlot.Clear();
                }
                // Names differ - refuse the merge, leave both as-is (held item stays on cursor).
            }
            else
            {
                (slot.ItemId, _heldSlot.ItemId)                       = (_heldSlot.ItemId, slot.ItemId);
                (slot.Count,  _heldSlot.Count)                        = (_heldSlot.Count,  slot.Count);
                (slot.CurrentDurability, _heldSlot.CurrentDurability) = (_heldSlot.CurrentDurability, slot.CurrentDurability);
                (slot.CustomName, _heldSlot.CustomName)               = (_heldSlot.CustomName, slot.CustomName);
            }
            EndDrag();
        }
        FireChanged();
    }

    // =========================================================================
    // RIGHT CLICK
    // =========================================================================

    private void HandleRightClick(int slotIndex)
    {
        var slot = _inventory.Slots[slotIndex];
        if (_heldSlot.IsEmpty)
        {
            if (slot.IsEmpty) return;
            int half         = Mathf.CeilToInt(slot.Count / 2f);
            _heldSlot.ItemId = slot.ItemId;
            _heldSlot.Count  = half;
            _heldSlot.CurrentDurability = slot.CurrentDurability;
            _heldSlot.CustomName        = slot.CustomName;
            _heldFromSlot    = slotIndex;
            slot.Count      -= half;
            if (slot.Count <= 0) slot.Clear();
            _dragMode = DragMode.RmbDrag;
            _dragVisited.Clear();
            _dragVisited.Add(slotIndex);
            _dragLastSlot = slotIndex;
        }
        else PlaceOneIntoSlot(slotIndex);
        FireChanged();
    }

    private bool PlaceOneIntoSlot(int slotIndex)
    {
        if (_heldSlot.IsEmpty) return false;
        var slot = _inventory.Slots[slotIndex];
        if (!slot.IsEmpty && slot.ItemId != _heldSlot.ItemId) return false;

        if (!IsStackableItem(_heldSlot.ItemId))
        {
            // Non-stackable (e.g. a tool) - there's only ever 1 of it, so
            // "place one" only makes sense into an empty slot, taking the
            // whole held item at once.
            if (!slot.IsEmpty) return false;
            CopySlot(_heldSlot, slot);
            _heldSlot.Clear();
            return true;
        }

        if (!slot.IsEmpty && slot.Count >= _inventory.MaxStackSize) return false;
        if (slot.IsEmpty) slot.ItemId = _heldSlot.ItemId;
        slot.Count++;
        _heldSlot.Count--;
        if (_heldSlot.Count <= 0) _heldSlot.Clear();
        return true;
    }

    // =========================================================================
    // DRAG
    // =========================================================================

    private void OnDragEnterSlot(int slotIndex)
    {
        // Check if dragging over a crafting panel slot
        if (_craftingPanel != null && _inventoryOpen)
        {
            int count = _craftingPanel.GetActiveSlotCount();
            for (int i = 0; i < count; i++)
            {
                var panel = _craftingPanel.GetSlotPanel(i);
                if (panel == null) continue;
                var mouse = GetViewport().GetMousePosition();
                if (new Rect2(panel.GlobalPosition, panel.Size).HasPoint(mouse))
                {
                    OnDragEnterCraftSlot(i);
                    return;
                }
            }
        }

        switch (_dragMode)
        {
            case DragMode.LmbWithItem:
            {
                if (_heldSlot.IsEmpty) return;
                var slot = _inventory.Slots[slotIndex];

                if (!IsStackableItem(_heldSlot.ItemId))
                {
                    // Non-stackable (e.g. a tool) - dragging just moves the
                    // whole thing into whichever slot you're over, no
                    // spreading a single item across multiple slots.
                    if (!slot.IsEmpty) return;
                    CopySlot(_heldSlot, slot);
                    _heldSlot.Clear();
                    _dragVisited.Clear();
                    _dragVisited.Add(slotIndex);
                    break;
                }

                if (!slot.IsEmpty && slot.ItemId != _heldSlot.ItemId) return;
                if (!slot.IsEmpty && slot.Count >= _inventory.MaxStackSize) return;
                if (!_dragVisited.Contains(slotIndex)) _dragVisited.Add(slotIndex);
                int n        = _dragVisited.Count;
                int perSlot  = _dragOrigCount / n;
                int leftover = _dragOrigCount - perSlot * n;
                foreach (int idx in _dragVisited) { var s = _inventory.Slots[idx]; s.ItemId = _heldSlot.ItemId; s.Count = 0; }
                int remaining = _dragOrigCount;
                for (int i = 0; i < _dragVisited.Count; i++)
                {
                    int give = perSlot + (i == 0 ? leftover : 0);
                    give = Mathf.Min(give, _inventory.MaxStackSize);
                    _inventory.Slots[_dragVisited[i]].Count = give;
                    remaining -= give;
                }
                _heldSlot.Count = Mathf.Max(0, remaining);
                if (_heldSlot.Count <= 0) _heldSlot.Clear();
                break;
            }
            case DragMode.RmbDrag:
            {
                if (_heldSlot.IsEmpty) return;
                if (_dragVisited.Contains(slotIndex)) return;
                if (PlaceOneIntoSlot(slotIndex)) _dragVisited.Add(slotIndex);
                break;
            }
            case DragMode.LmbNoItem:
            {
                if (_dragVisited.Contains(slotIndex)) return;
                var slot = _inventory.Slots[slotIndex];
                if (slot.IsEmpty) return;
                if (!_heldSlot.IsEmpty && slot.ItemId != _heldSlot.ItemId) return;

                if (!IsStackableItem(slot.ItemId))
                {
                    if (!_heldSlot.IsEmpty) return; // already holding one - can't collect a second
                    CopySlot(slot, _heldSlot);
                    slot.Clear();
                    _dragVisited.Add(slotIndex);
                    break;
                }

                int space = _inventory.MaxStackSize - (_heldSlot.IsEmpty ? 0 : _heldSlot.Count);
                if (space <= 0) return;
                _dragVisited.Add(slotIndex);
                if (_heldSlot.IsEmpty) _heldSlot.ItemId = slot.ItemId;
                int take = Mathf.Min(slot.Count, space);
                _heldSlot.Count += take; slot.Count -= take;
                if (slot.Count <= 0) slot.Clear();
                break;
            }
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

    // Drag held item into a crafting slot
    // Drag held item into a crafting slot (stacks if same item, same as inventory drag)
    private void OnDragEnterCraftSlot(int craftIdx)
    {
        if (_dragMode != DragMode.LmbWithItem && _dragMode != DragMode.RmbDrag) return;
        if (_heldSlot.IsEmpty) return;

        if (_craftingPanel.TryPlaceHeldItem(craftIdx, _heldSlot.ItemId))
        {
            _heldSlot.Count--;
            if (_heldSlot.Count <= 0) _heldSlot.Clear();
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
    // SHIFT CLICK
    // =========================================================================

    private void ShiftClick(int slotIndex) { ShiftMoveSlot(slotIndex); FireChanged(); }

    private void ShiftMoveSlot(int slotIndex)
    {
        var src = _inventory.Slots[slotIndex];
        if (src.IsEmpty) return;
        bool isHotbar  = slotIndex >= MainInvSize;
        int  destStart = isHotbar ? 0 : MainInvSize;
        int  destEnd   = isHotbar ? MainInvSize : TotalSlots;

        if (IsStackableItem(src.ItemId))
        {
            for (int i = destStart; i < destEnd && src.Count > 0; i++)
            {
                var dst = _inventory.Slots[i];
                if (dst.IsEmpty || dst.ItemId != src.ItemId) continue;
                int t = Mathf.Min(_inventory.MaxStackSize - dst.Count, src.Count);
                dst.Count += t; src.Count -= t;
            }
        }
        for (int i = destStart; i < destEnd && src.Count > 0; i++)
        {
            var dst = _inventory.Slots[i];
            if (!dst.IsEmpty) continue;
            CopySlot(src, dst);
            src.Clear();
        }
        if (src.Count <= 0) src.Clear();
    }

    // =========================================================================
    // DOUBLE CLICK
    // =========================================================================

    private void DoubleClickCollect(int slotIndex)
    {
        if (_heldSlot.IsEmpty)
        {
            var s = _inventory.Slots[slotIndex];
            if (s.IsEmpty) return;
            CopySlot(s, _heldSlot);
            s.Clear();
        }
        if (!IsStackableItem(_heldSlot.ItemId)) { FireChanged(); UpdateCursorVisual(); return; } // no "collect more" for a tool - there's only ever 1
        if (_heldSlot.Count >= _inventory.MaxStackSize) { FireChanged(); UpdateCursorVisual(); return; }
        string id = _heldSlot.ItemId;
        for (int i = 0; i < TotalSlots && _heldSlot.Count < _inventory.MaxStackSize; i++)
        {
            if (i == slotIndex) continue;
            var s = _inventory.Slots[i];
            if (s.IsEmpty || s.ItemId != id) continue;
            int take = Mathf.Min(s.Count, _inventory.MaxStackSize - _heldSlot.Count);
            _heldSlot.Count += take; s.Count -= take;
            if (s.Count <= 0) s.Clear();
        }
        FireChanged(); UpdateCursorVisual();
    }

    // =========================================================================
    // SCROLL WHEEL IN INVENTORY
    // =========================================================================

    private void ScrollSlot(int slotIndex, bool up)
    {
        bool isHotbar = slotIndex >= MainInvSize;
        var  src      = _inventory.Slots[slotIndex];

        if (up)
        {
            int fromStart = isHotbar ? 0 : MainInvSize;
            int fromEnd   = isHotbar ? MainInvSize : TotalSlots;
            for (int i = fromStart; i < fromEnd; i++)
            {
                var other = _inventory.Slots[i];
                if (other.IsEmpty) continue;
                if (!src.IsEmpty && other.ItemId != src.ItemId) continue;

                if (!IsStackableItem(other.ItemId))
                {
                    // Non-stackable - only moves whole, and only into an
                    // empty slot (can't "add 1 more" to an existing tool).
                    if (!src.IsEmpty) continue;
                    CopySlot(other, src);
                    other.Clear();
                    FireChanged(); return;
                }

                if (src.Count >= _inventory.MaxStackSize) continue;
                if (src.IsEmpty) src.ItemId = other.ItemId;
                src.Count++; other.Count--;
                if (other.Count <= 0) other.Clear();
                FireChanged(); return;
            }
        }
        else
        {
            if (src.IsEmpty) return;

            if (!IsStackableItem(src.ItemId))
            {
                // Non-stackable - move the whole thing into the first empty
                // slot, don't try to merge 1 unit into a matching stack.
                int toStart = isHotbar ? 0 : MainInvSize;
                int toEnd   = isHotbar ? MainInvSize : TotalSlots;
                for (int i = toStart; i < toEnd; i++)
                {
                    var dst = _inventory.Slots[i];
                    if (!dst.IsEmpty) continue;
                    CopySlot(src, dst);
                    src.Clear();
                    FireChanged(); return;
                }
                return;
            }

            int stackToStart = isHotbar ? 0 : MainInvSize;
            int stackToEnd   = isHotbar ? MainInvSize : TotalSlots;
            for (int i = stackToStart; i < stackToEnd; i++)
            {
                var dst = _inventory.Slots[i];
                if (dst.IsEmpty || dst.ItemId != src.ItemId || dst.Count >= _inventory.MaxStackSize) continue;
                dst.Count++; src.Count--;
                if (src.Count <= 0) src.Clear();
                FireChanged(); return;
            }
            for (int i = stackToStart; i < stackToEnd; i++)
            {
                var dst = _inventory.Slots[i];
                if (!dst.IsEmpty) continue;
                dst.ItemId = src.ItemId; dst.Count = 1; src.Count--;
                if (src.Count <= 0) src.Clear();
                FireChanged(); return;
            }
        }
    }

    // =========================================================================
    // ADD ITEM (hotbar first)
    // =========================================================================

    private int AddItemToInventory(string itemId, int count, int? durability = null, string customName = "")
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return count;
        int rem = count;
        bool stackable = IsStackableItem(itemId);

        void TryAdd(int start, int end, bool stackOnly)
        {
            for (int i = start; i < end && rem > 0; i++)
            {
                var s = _inventory.Slots[i];
                if (stackOnly)
                {
                    if (!stackable) continue; // non-stackable items never merge into an existing slot
                    if (s.IsEmpty || s.ItemId != itemId) continue;
                    int add = Mathf.Min(_inventory.MaxStackSize - s.Count, rem);
                    s.Count += add; rem -= add;
                }
                else
                {
                    if (!s.IsEmpty) continue;
                    int add = stackable ? Mathf.Min(_inventory.MaxStackSize, rem) : 1;
                    s.ItemId = itemId; s.Count = add; rem -= add;
                    if (!stackable)
                    {
                        // Defaults to full durability if the caller doesn't know the
                        // specific instance's actual remaining durability (better
                        // than silently defaulting to 0). Pass durability explicitly
                        // once whatever spawns the physical drop tracks it.
                        var item = ItemRegistry.Instance?.GetItem(itemId);
                        s.CurrentDurability = durability ?? item?.MaxDurability ?? 0;
                        s.CustomName        = customName;
                    }
                }
            }
        }
        TryAdd(MainInvSize, TotalSlots, true);
        TryAdd(MainInvSize, TotalSlots, false);
        TryAdd(0, MainInvSize, true);
        TryAdd(0, MainInvSize, false);
        _inventory.OnInventoryChanged?.Invoke();
        return rem;
    }

    // Called by ItemPickup when the player walks close enough to collect it.
    // Returns how many items didn't fit anywhere (0 means it was fully collected).
    // NOTE: durability/customName aren't yet threaded through from whatever
    // spawns physical drops (ItemPickup.cs) - a dropped tool currently comes
    // back at full durability rather than whatever it actually had when
    // dropped. Wire real values through here once that script tracks them.
    public int CollectPickup(string itemId, int count, int? durability = null, string customName = "")
    {
        return AddItemToInventory(itemId, count, durability, customName);
    }

    // Spawns a physical item drop in the world at worldPosition instead of
    // adding the item straight to the inventory. Used by block breaking and
    // by manually dropping items with Q. If tossVelocity is given (Q-drops),
    // the pickup uses that instead of its default random "popped out of a
    // broken block" velocity.
    private void SpawnItemDrop(string itemId, int count, Vector3 worldPosition, Vector3? tossVelocity = null)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return;
        var pickup = new ItemPickup();
        pickup.ItemId = itemId;
        pickup.Count  = count;
        pickup.TossVelocity = tossVelocity;
        GetTree().Root.AddChild(pickup);
        pickup.GlobalPosition = worldPosition;
    }

    // Drops one item from the currently selected hotbar slot, tossed a
    // little way in front of the player. Called once on Q press, and
    // repeatedly while Q is held (see _PhysicsProcess / _UnhandledInput).
    private void DropOneItem()
    {
        var gm = GameModeManager.Instance;
        if (gm != null && gm.IsStory) return;

        var slot = _inventory.Slots[MainInvSize + _selectedSlot];
        if (slot.IsEmpty) return;

        string itemId = slot.ItemId;
        slot.Count--;
        if (slot.Count <= 0) slot.Clear();
        FireChanged();

        Vector3 forward  = -_rayCast.GlobalTransform.Basis.Z;
        Vector3 spawnPos = GlobalPosition + new Vector3(0, DropSpawnHeight, 0) + forward * 0.6f;
        Vector3 tossVel  = forward * DropForwardSpeed + Vector3.Up * DropUpSpeed;
        SpawnItemDrop(itemId, 1, spawnPos, tossVel);
    }

    // =========================================================================
    // GLOBAL INPUT
    // =========================================================================

    private void OnSlotMouseEntered(int slotIndex)
    {
        if (_invTooltip == null) return;
        if (slotIndex < 0 || slotIndex >= _inventory.Slots.Length) { _invTooltip.HideTooltip(); return; }

        var slot = _inventory.Slots[slotIndex];
        if (slot.IsEmpty) { _invTooltip.HideTooltip(); return; }

        var item = ItemRegistry.Instance.GetItem(slot.ItemId);
        _invTooltip.ShowFor(item, GetSlotControl(slotIndex), slot.CurrentDurability);
    }

    // slotIndex < MainInvSize is the main grid, the rest is the hotbar row
    // inside the full inventory screen - mirrors how RefreshAllSlotVisuals
    // addresses the same two arrays.
    private Control GetSlotControl(int slotIndex)
    {
        return slotIndex < MainInvSize
            ? _invSlotPanels[slotIndex]
            : _invHotbarSlots[slotIndex - MainInvSize];
    }

    // Same idea as OnSlotMouseEntered, but for the persistent HUD hotbar
    // (hotbarIdx is 0-based within the hotbar, not a full inventory index).
    private void OnHudHotbarSlotMouseEntered(int hotbarIdx)
    {
        if (_hotbarTooltip == null) return;
        int slotIndex = MainInvSize + hotbarIdx;
        if (slotIndex < 0 || slotIndex >= _inventory.Slots.Length) { _hotbarTooltip.HideTooltip(); return; }

        var slot = _inventory.Slots[slotIndex];
        if (slot.IsEmpty) { _hotbarTooltip.HideTooltip(); return; }

        var item = ItemRegistry.Instance.GetItem(slot.ItemId);
        _hotbarTooltip.ShowFor(item, _hotbarSlots[hotbarIdx], slot.CurrentDurability);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_inventoryOpen) return;
        if (@event is InputEventMouseButton mb && !mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right)
            { EndDrag(); FireChanged(); UpdateCursorVisual(); }
        }
        if (@event is InputEventMouseButton startMb && startMb.Pressed
            && startMb.ButtonIndex == MouseButton.Left
            && _heldSlot.IsEmpty && _dragMode == DragMode.None)
        {
            _dragMode = Input.IsKeyPressed(Key.Shift) ? DragMode.ShiftLmbNoItem : DragMode.LmbNoItem;
            _dragVisited.Clear(); _dragLastSlot = -1;
        }
    }

    private int GetSlotUnderMouse()
    {
        var mouse = GetViewport().GetMousePosition();
        for (int i = 0; i < MainInvSize; i++)
        {
            if (_invSlotPanels[i] == null) continue;
            if (new Rect2(_invSlotPanels[i].GlobalPosition, _invSlotPanels[i].Size).HasPoint(mouse)) return i;
        }
        for (int i = 0; i < HotbarSize; i++)
        {
            if (_invHotbarSlots[i] == null) continue;
            if (new Rect2(_invHotbarSlots[i].GlobalPosition, _invHotbarSlots[i].Size).HasPoint(mouse)) return MainInvSize + i;
        }
        return -1;
    }

    // =========================================================================
    // CRAFTING SLOT CLICK (routed from CraftingPanel)
    // =========================================================================

    private void HandleCraftSlotClicked(int idx, MouseButton button, bool shift)
    {
        if (_craftingPanel == null) return;
        var slot = _craftingPanel.GetSlot(idx);
        if (slot == null) return;

        if (shift && button == MouseButton.Left)
        {
            if (slot.IsEmpty) return;
            int leftover = AddItemToInventory(slot.ItemId, slot.Count);
            int moved    = slot.Count - leftover;
            slot.Count -= moved;
            if (slot.Count <= 0) slot.Clear();
            _craftingPanel.NotifyGridChanged();
            FireChanged();
            UpdateCursorVisual();
            return;
        }

        if (button == MouseButton.Left)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty) return;
                _heldSlot.ItemId = slot.ItemId;
                _heldSlot.Count  = slot.Count;
                slot.Clear();
            }
            else
            {
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
                    (slot.ItemId, _heldSlot.ItemId) = (_heldSlot.ItemId, slot.ItemId);
                    (slot.Count,  _heldSlot.Count)  = (_heldSlot.Count,  slot.Count);
                }
            }
        }
        else if (button == MouseButton.Right)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty) return;
                int half         = Mathf.CeilToInt(slot.Count / 2f);
                _heldSlot.ItemId = slot.ItemId;
                _heldSlot.Count  = half;
                slot.Count      -= half;
                if (slot.Count <= 0) slot.Clear();
            }
            else
            {
                if (!slot.IsEmpty && slot.ItemId != _heldSlot.ItemId) return;
                if (!slot.IsEmpty && slot.Count >= _inventory.MaxStackSize) return;
                if (slot.IsEmpty) slot.ItemId = _heldSlot.ItemId;
                slot.Count++;
                _heldSlot.Count--;
                if (_heldSlot.Count <= 0) _heldSlot.Clear();
            }
        }

        _craftingPanel.NotifyGridChanged();
        FireChanged();
        UpdateCursorVisual();
    }

    private void HandleToolBenchCenterClicked()
    {
        if (_toolBenchPanel == null) return;

        // Holding something → only act if it's an already-crafted tool
        // (recognized by having recipe tags), in which case load it in for
        // modification. Holding anything else, ignore the click.
        if (!_heldSlot.IsEmpty)
        {
            var item = ItemRegistry.Instance.GetItem(_heldSlot.ItemId);
            if (item != null && item.HasDurability &&
                ToolCrafting.TryGetRecipe(item, out _, out _, out _))
            {
                if (_toolBenchPanel.LoadExistingTool(_heldSlot))
                {
                    FireChanged();
                    UpdateCursorVisual();
                }
            }
            return;
        }

        // Empty cursor → open/close the tool-type picker.
        _toolBenchPanel.ToggleFamilyPicker();
    }

    private void HandleToolBenchSlotClicked(int idx, MouseButton button, bool shift)
    {
        if (_toolBenchPanel == null) return;
        var slot = _toolBenchPanel.GetSlot(idx);
        if (slot == null) return;

        if (shift && button == MouseButton.Left)
        {
            if (slot.IsEmpty) return;
            int leftover = AddItemToInventory(slot.ItemId, slot.Count);
            int moved    = slot.Count - leftover;
            slot.Count -= moved;
            if (slot.Count <= 0) slot.Clear();
            _toolBenchPanel.RefreshAllVisuals();
            FireChanged();
            UpdateCursorVisual();
            return;
        }

        if (button == MouseButton.Left)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty) return;
                _heldSlot.ItemId = slot.ItemId;
                _heldSlot.Count  = slot.Count;
                slot.Clear();
            }
            else
            {
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
                    (slot.ItemId, _heldSlot.ItemId) = (_heldSlot.ItemId, slot.ItemId);
                    (slot.Count,  _heldSlot.Count)  = (_heldSlot.Count,  slot.Count);
                }
            }
        }
        else if (button == MouseButton.Right)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty) return;
                int half         = Mathf.CeilToInt(slot.Count / 2f);
                _heldSlot.ItemId = slot.ItemId;
                _heldSlot.Count  = half;
                slot.Count      -= half;
                if (slot.Count <= 0) slot.Clear();
            }
            else
            {
                if (!slot.IsEmpty && slot.ItemId != _heldSlot.ItemId) return;
                if (!slot.IsEmpty && slot.Count >= _inventory.MaxStackSize) return;
                if (slot.IsEmpty) slot.ItemId = _heldSlot.ItemId;
                slot.Count++;
                _heldSlot.Count--;
                if (_heldSlot.Count <= 0) _heldSlot.Clear();
            }
        }

        _toolBenchPanel.RefreshAllVisuals();
        FireChanged();
        UpdateCursorVisual();
    }

    private void HandleToolBenchOutputClicked(MouseButton button, bool shift)
    {
        if (_toolBenchPanel == null) return;
        if (button != MouseButton.Left) return;

        if (!_toolBenchPanel.PeekResult(out string peekId, out int _)) return;

        // Cursor must be empty - crafted tools are unique/durable items,
        // not simple stackable counts, so we don't try to merge onto
        // whatever's already held (unlike the Crafter's grid output).
        if (!_heldSlot.IsEmpty) return;

        if (_toolBenchPanel.TryConsumeOneCraft(out string resultId, out int resultDurability))
        {
            _heldSlot.ItemId            = resultId;
            _heldSlot.Count             = 1;
            _heldSlot.CurrentDurability = resultDurability;
            FireChanged();
            UpdateCursorVisual();
        }
    }

private void HandleOutputClicked(MouseButton button, bool shift)
    {
        if (_craftingPanel == null) return;

        if (shift && button == MouseButton.Left)
        {
            // Craft repeatedly straight into inventory until out of ingredients
            // or the inventory has no room left.
            int safety = 64;
            while (safety-- > 0)
            {
                if (!_craftingPanel.TryConsumeOneCraft(out string rid, out int rcount)) break;
                int leftover = AddItemToInventory(rid, rcount);
                if (leftover > 0) break; // inventory full — stop here
            }
            FireChanged();
            return;
        }

        if (button == MouseButton.Left)
        {
            if (!_craftingPanel.PeekResult(out string peekId, out int _)) return;

            // If cursor already holds something, it must match the result
            // and have room, same as picking items into a normal slot.
            if (!_heldSlot.IsEmpty)
            {
                if (_heldSlot.ItemId != peekId) return;
                if (_heldSlot.Count >= _inventory.MaxStackSize) return;
            }

            if (_craftingPanel.TryConsumeOneCraft(out string resultId, out int resultCount))
            {
                if (_heldSlot.IsEmpty)
                {
                    _heldSlot.ItemId = resultId;
                    _heldSlot.Count  = resultCount;
                }
                else
                {
                    int space = _inventory.MaxStackSize - _heldSlot.Count;
                    _heldSlot.Count += Mathf.Min(space, resultCount);
                }
                FireChanged();
                UpdateCursorVisual();
            }
        }
    }

        private void HandleLearnedCraftClicked(string resultId, int resultCount)
            {
                if (!_heldSlot.IsEmpty)
                {
                    if (_heldSlot.ItemId != resultId || _heldSlot.Count >= _inventory.MaxStackSize)
                    {
                        // Cursor's holding something incompatible — send crafted item to inventory instead
                        AddItemToInventory(resultId, resultCount);
                    }
                    else
                    {
                        int space = _inventory.MaxStackSize - _heldSlot.Count;
                        _heldSlot.Count += Mathf.Min(space, resultCount);
                    }
                }
                else
                {
                    _heldSlot.ItemId = resultId;
                    _heldSlot.Count  = resultCount;
                }
                FireChanged();
                UpdateCursorVisual();
            }

    // =========================================================================
    // CREATIVE MENU (routed from CreativeMenu)
    // =========================================================================

    // Called whenever an item is clicked in the creative menu — left-click
    // gives a full stack, right-click gives one (CreativeMenu decides which
    // and just hands us the count). Goes straight into inventory, same path
    // as picking up a world drop.
    private void OnCreativeItemChosen(string itemId, int count)
    {
        AddItemToInventory(itemId, count);
        FireChanged();
    }

    private void ToggleCreativeMenu()
    {
        _creativeMenuOpen     = !_creativeMenuOpen;
        _creativeMenu.Visible = _creativeMenuOpen;

        if (_creativeMenuOpen)
        {
            _hotbarLayer.Visible = false;
            if (_statsHudLayer != null) _statsHudLayer.Visible = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else
        {
            _hotbarLayer.Visible = _hudVisible;
            if (_statsHudLayer != null) _statsHudLayer.Visible = _hudVisible;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    // =========================================================================
    // VISUALS
    // =========================================================================

    private void RefreshAllSlotVisuals()
    {
        for (int i = 0; i < MainInvSize; i++)
        {
            var s = _inventory.Slots[i];
            _invSlotPanels[i].GetChild<TextureRect>(0).Texture = s.IsEmpty ? null : GetItemIcon(s.ItemId);
            _invSlotLabels[i].Text = (!s.IsEmpty && s.Count > 1) ? s.Count.ToString() : "";
        }
        for (int i = 0; i < HotbarSize; i++)
        {
            var s    = _inventory.Slots[MainInvSize + i];
            var icon = s.IsEmpty ? null : GetItemIcon(s.ItemId);
            _hotbarSlots[i].GetChild<TextureRect>(0).Texture    = icon;
            _invHotbarSlots[i].GetChild<TextureRect>(0).Texture = icon;
            string t = (!s.IsEmpty && s.Count > 1) ? s.Count.ToString() : "";
            _hotbarLabels[i].Text    = t;
            _invHotbarLabels[i].Text = t;
        }
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
            _hotbarSlots[i].AddThemeStyleboxOverride("panel",   MakePanelStyle(new Color(0.15f,0.15f,0.15f,0.85f), border, bw));
            _invHotbarSlots[i].AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0.15f,0.15f,0.15f,0.85f), border, bw));
        }
    }

    private void UpdateCursorVisual()
    {
        if (_heldSlot.IsEmpty) { _cursorPanel.Visible = false; return; }
        _cursorTex.Texture   = GetItemIcon(_heldSlot.ItemId);
        _cursorLabel.Text    = _heldSlot.Count > 1 ? _heldSlot.Count.ToString() : "";
        _cursorPanel.Visible = _inventoryOpen;
    }

    private void FireChanged() => _inventory.OnInventoryChanged?.Invoke();

    // =========================================================================
    // PROCESS
    // =========================================================================

    public override void _Process(double delta)
    {
        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= (float)delta;
            if (_feedbackTimer <= 0f && _chatFeedback != null) _chatFeedback.Visible = false;
        }

        if (_inventoryOpen) UpdateCraftingProximity();
        if (!_inventoryOpen) return;

        var mouse = GetViewport().GetMousePosition();
        if (!_heldSlot.IsEmpty && _cursorPanel != null)
            _cursorPanel.GlobalPosition = mouse - new Vector2(SlotSize / 2f, SlotSize / 2f);

        if (_dragMode != DragMode.None)
        {
            int under = GetSlotUnderMouse();
            if (under >= 0 && under != _dragLastSlot)
            { _dragLastSlot = under; OnDragEnterSlot(under); }
            else if (under < 0 && _craftingPanel != null)
            {
                // Check crafting panel slots separately since they're outside inventory slot range
                int count = _craftingPanel.GetActiveSlotCount();
                for (int i = 0; i < count; i++)
                {
                    var panel = _craftingPanel.GetSlotPanel(i);
                    if (panel == null) continue;
                    if (new Rect2(panel.GlobalPosition, panel.Size).HasPoint(mouse))
                    {
                        int craftId = -(i + 1); // negative = crafting slot
                        if (craftId != _dragLastSlot)
                        { _dragLastSlot = craftId; OnDragEnterCraftSlot(i); }
                        break;
                    }
                }
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
        if (_isBreaking) SyncBreakOverlayPosition();
        if (_stats == null || _stats.IsDead) return;
        if (_chatOpen) return;
        CheckWaterStatus();

        float dt         = (float)delta;
        Vector3 velocity = Velocity;

        if (_isFlying && GameModeManager.Instance?.IsCreate == true)
        {
            HandleFlyMovement(dt, ref velocity);
            Velocity = velocity;
            MoveAndSlide();
            return;
        }

        if (_isInWater)
        { velocity.Y -= (_gravity * 0.2f) * dt; velocity.Y = Mathf.Clamp(velocity.Y, -2f, 8f); }
        else if (!IsOnFloor()) velocity.Y -= _gravity * dt;
        else { _hasDoubleJumped = false; _isGliding = false; }

        if (_isInWater)
        { if (Input.IsActionPressed("jump")) velocity.Y = Mathf.MoveToward(velocity.Y, 6f, 12f * dt); }
        else if (Input.IsActionPressed("jump") && IsOnFloor()) velocity.Y = JumpVelocity;

        if (Input.IsActionJustReleased("ui_cancel"))
        {
            if (_creativeMenuOpen)      ToggleCreativeMenu();
            else if (_inventoryOpen)    ToggleInventory();
            else if (_pauseMenu.IsOpen) _pauseMenu.Close();
            else                        _pauseMenu.Open();
        }

        _isCrouching = Input.IsActionPressed("crouch");
        bool wantsSprint = Input.IsActionPressed("sprint");
        if (wantsSprint && _stats.Stamina > 0 && !_isCrouching)
        { _isSprinting = true; _stats.UseStamina(SprintStaminaCost * dt); }
        else _isSprinting = false;

        Vector2 inputDir  = Input.GetVector("move_left","move_right","move_forward","move_back");
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        if (_isBreaking)
        {
            _breakTimer += dt;
            if (_breakTimer >= BreakInterval) { TryBreakBlock(); _breakTimer = 0f; }
        }
        else if (_breakHitCount > 0) ResetBreak();

        if (_isPlacing) { _placeTimer += dt; if (_placeTimer >= PlaceInterval) { TryPlaceBlock(); _placeTimer = 0f; } }

        if (_isDropping) { _dropTimer += dt; if (_dropTimer >= DropInterval) { DropOneItem(); _dropTimer = 0f; } }

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
        if (@event is InputEventKey qUp && !qUp.Pressed && qUp.Keycode == Key.Q)
            _isDropping = false;

        if (@event is InputEventMouseButton mb)
        {
            if (!_inventoryOpen && !_chatOpen && !_creativeMenuOpen)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    _isBreaking = mb.Pressed;
                    if (mb.Pressed) { TryBreakBlock(); _breakTimer = 0f; }
                    else ResetBreak();
                }
                if (mb.ButtonIndex == MouseButton.Right)
                {
                    _isPlacing = mb.Pressed;
                    if (mb.Pressed)
                    {
                        if (TryOpenCraftingTable()) _isPlacing = false;
                        else { TryPlaceBlock(); _placeTimer = 0f; }
                    }
                }
                if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) SelectHotbarSlot((_selectedSlot + 1) % HotbarSize);
                else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)  SelectHotbarSlot((_selectedSlot - 1 + HotbarSize) % HotbarSize);
            }
        }

        if (@event is InputEventKey key && key.Pressed)
        {
            if (key.Keycode == Key.Escape && _chatOpen) { CloseChat(); return; }
            if (_chatOpen) return;

            if (key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9) SelectHotbarSlot((int)key.Keycode - (int)Key.Key1);
            else if (key.Keycode == Key.Key0)  SelectHotbarSlot(9);
            else if (key.Keycode == Key.Minus) SelectHotbarSlot(10);
            else if (key.Keycode == Key.Equal) SelectHotbarSlot(11);

            if (key.Keycode == Key.Tab && !_creativeMenuOpen) ToggleInventory();

            // Creative item browser — only meaningful in Create mode, but if
            // it's somehow already open (e.g. gamemode changed mid-browse)
            // still let it close.
            if (key.Keycode == Key.V && !_inventoryOpen &&
                (_creativeMenuOpen || GameModeManager.Instance?.IsCreate == true))
                ToggleCreativeMenu();

            if (key.Keycode == Key.Q && !_isDropping)
            {
                _isDropping = true;
                _dropTimer  = 0f;
                DropOneItem();
            }

            if (key.Keycode == Key.F4)
            {
                if (GameModeManager.Instance == null) ShowFeedback("GameModeManager not loaded.");
                else { GameModeManager.Instance.CycleNext(); ShowFeedback($"Gamemode: {GameModeManager.Instance.Current}"); }
            }

            if (key.Keycode == Key.T && !_chatOpen && !_inventoryOpen && !_pauseMenu.IsOpen) OpenChat();

            if (key.Keycode == Key.Space && GameModeManager.Instance?.IsCreate == true && !key.Echo)
            {
                // !key.Echo matters here: holding Space to fly upward (see
                // HandleFlyMovement) makes the OS fire repeat "pressed"
                // events for as long as it's held. Without this check,
                // those repeats kept re-running the double-tap check below
                // and could re-toggle flying mid-ascent. Only a genuine
                // fresh key-down counts as a "press" for the double-tap now.
                double now = Time.GetTicksMsec() / 1000.0;
                if (now - _lastJumpTime < DoubleJumpWindow) ToggleFly();
                _lastJumpTime = now;
            }

            if (key.Keycode == Key.F1)
            {
                _hudVisible = !_hudVisible;
                if (_hotbarLayer    != null) _hotbarLayer.Visible    = _hudVisible && !_inventoryOpen;
                if (_crosshairLayer != null) _crosshairLayer.Visible = _hudVisible;
                if (_statsHudLayer  != null) _statsHudLayer.Visible  = _hudVisible && !_inventoryOpen;
            }
            if (key.Keycode == Key.F2)
            {
                var img = GetViewport().GetTexture().GetImage();
                string p = $"user://screenshot_{Time.GetDatetimeStringFromSystem().Replace(":","-")}.png";
                img.SavePng(p); GD.Print($"Screenshot saved: {p}");
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
            if (_statsHudLayer != null) _statsHudLayer.Visible = false;
            if (_craftingPanel != null) { _craftingPanel.Visible = true; UpdateCraftingProximity(); }
            if (_equipmentPanel != null) _equipmentPanel.Visible = true;
            RefreshAllSlotVisuals();
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else
        {
            // Return held item
            if (!_heldSlot.IsEmpty)
            {
                bool placed = false;
                if (_heldFromSlot >= 0 && _heldFromSlot < TotalSlots)
                {
                    var orig = _inventory.Slots[_heldFromSlot];
                    if (orig.IsEmpty) { orig.ItemId = _heldSlot.ItemId; orig.Count = _heldSlot.Count; placed = true; }
                    else if (orig.ItemId == _heldSlot.ItemId && orig.Count < _inventory.MaxStackSize)
                    { int fit = Mathf.Min(_inventory.MaxStackSize - orig.Count, _heldSlot.Count); orig.Count += fit; _heldSlot.Count -= fit; placed = _heldSlot.Count <= 0; }
                }
                if (!placed && _heldSlot.Count > 0) AddItemToInventory(_heldSlot.ItemId, _heldSlot.Count);
                _heldSlot.Clear(); UpdateCursorVisual();
            }

            // 3x3 closes → return items to inventory, revert to 2x2
            _craftingPanel?.OnInventoryClose();
            if (_craftingPanel != null) _craftingPanel.Visible = false;
            if (_equipmentPanel != null) _equipmentPanel.Visible = false;

            _toolBenchPanel?.ReturnSocketsToInventory();
            if (_toolBenchPanel != null) _toolBenchPanel.Visible = false;
            if (_stationTabBar != null) _stationTabBar.Visible = false;
            _activeStation = "crafter";
            EndDrag();
            _hotbarLayer.Visible = _hudVisible;
            if (_statsHudLayer != null) _statsHudLayer.Visible = _hudVisible;
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

    // Maps a broken block's BlockId to the item(s) it should drop.
    // Checks BlockDropManager first — custom, JSON-editable drop rules
    // (e.g. gravel -> chance of flint) live there, same pattern as
    // RecipeManager. If there's no custom rule, or the roll didn't hit one,
    // falls back to these built-in special cases:
    // grass_block always drops dirt (breaking the grass "layer" leaves
    // the dirt underneath). wet_sand1 and wet_sand2 both collapse into a
    // single obtainable item, "wet_sand2" (displayed in-game as "Wet
    // Sand") - wet_sand1 is purely a transitional block state on its way
    // to becoming wet_sand2, so it never drops as itself. Otherwise the
    // block just drops itself, count 1.
    private (string ItemId, int Count) GetDrop(string blockId)
    {
        if (BlockDropManager.Instance != null &&
            BlockDropManager.Instance.TryRollDrop(blockId, _dropRng, out string customId, out int customCount))
        {
            return (customId, customCount);
        }

        if (blockId == "grass_block") return ("dirt", 1);
        if (blockId == "wet_sand1" || blockId == "wet_sand2") return ("wet_sand2", 1);
        return (blockId, 1);
    }

    private void TryBreakBlock()
{
    if (!_rayCast.IsColliding()) { ResetBreak(); return; }

    var gm = GameModeManager.Instance;

    if (gm != null && gm.IsStory)
    {
        ResetBreak();
        return;
    }

    var col = _rayCast.GetCollider() as Node;

    if (col is Melon melon)
    {
        melon.Break(_inventory);
        ResetBreak();
        return;
    }

        // ---------------------------------------------------------
// SUN / MOON
// ---------------------------------------------------------

if (col != null &&
    (col.Name == "SunCollision" ||
     col.Name == "MoonCollision"))
{
    string celestialId =
        col.Name == "SunCollision"
            ? "sun"
            : "moon";

    // Start a new celestial break.
    if (_breakTargetBlockId != celestialId)
    {
        _breakOverlay?.ResetTarget();

        _breakHitCount = 0;
        _breakTargetBlockId = celestialId;

        // Celestial bodies use the normal block breaking speed.
        _breakMiningPower = 1;
    }

    _breakHitCount++;

    // Get the CURRENT position of the moving celestial body.
    Vector3 celestialPosition =
        ((Node3D)col).GlobalPosition;

    Vector3I animationPosition =
        new Vector3I(
            Mathf.FloorToInt(celestialPosition.X),
            Mathf.FloorToInt(celestialPosition.Y),
            Mathf.FloorToInt(celestialPosition.Z)
        );

    // Keep the break overlay attached to the moving
    // Sun/Moon every single hit.
    float celestialSize =
    celestialId == "sun"
        ? 16f
        : 10f;

_breakOverlay?.SetCelestialMode(
    celestialPosition,
    new Vector3(
        celestialSize,
        celestialSize,
        celestialSize
    ),
    new Vector3(
        0f,
        Mathf.Pi / 4f,
        0f
    )
);

    // Normal block-breaking animation.
    bool celestialShouldBreak =
        _breakOverlay?.UpdateBreak(
            animationPosition,
            _breakHitCount,
            _breakMiningPower
        )
        ??
        (
            _breakHitCount *
            _breakMiningPower >=
            BlockBreakOverlay.TotalStages
        );

    // -----------------------------------------------------
    // BREAK THE CELESTIAL BODY
    // -----------------------------------------------------

    if (celestialShouldBreak)
{
    GD.Print(
        $"[Player] BROKE CELESTIAL BODY: {celestialId}"
    );

    var dayNight =
        GetTree().GetFirstNodeInGroup(
            "day_night_cycle"
        );

    if (dayNight is DayNightCycle cycle)
    {
        // Break the Sun / Moon.
        cycle.BreakCelestialBody(
            celestialId
        );

        // ---------------------------------------------------------
        // CELESTIAL DROP
        // ---------------------------------------------------------

        string defaultDropItem =
            celestialId == "sun"
                ? "sun_shard"
                : "moon_shard";

        int dropCount = 1;
        string dropItem = defaultDropItem;

        var dropManager =
            BlockDropManager.Instance;

        if (dropManager != null)
        {
            var rng =
                new RandomNumberGenerator();

            rng.Randomize();

            if (dropManager.TryRollDrop(
                celestialId,
                rng,
                out string jsonDropItem,
                out int jsonDropCount))
            {
                dropItem = jsonDropItem;
                dropCount = jsonDropCount;

                GD.Print(
                    $"[Player] Celestial JSON drop: " +
                    $"{dropCount}x {dropItem}"
                );
            }
            else
            {
                // JSON chance failed.
                dropCount = 0;

                GD.Print(
                    $"[Player] Celestial drop roll failed: " +
                    $"{celestialId}"
                );
            }
        }
        else
        {
            // Safety fallback.
            dropCount = 1;

            GD.PrintErr(
                "[Player] BlockDropManager unavailable. " +
                $"Using fallback drop: 1x {dropItem}"
            );
        }

        if (dropCount > 0)
        {
            Vector3 dropPosition =
                ((Node3D)col).GlobalPosition;

            SpawnItemDrop(
                dropItem,
                dropCount,
                dropPosition
            );
        }
    }

    ResetBreak();
}

return;

}

    if (col is Mob mob)
    {
        mob.TakeDamage(UnarmedAttackDamage, GlobalPosition);
        ResetBreak();
        return;
    }

    if (col == null || !col.HasMeta("chunk"))
    {
        ResetBreak();
        return;
    }

    Chunk chunk = (Chunk)col.GetMeta("chunk").AsGodotObject();

    Vector3 tPos =
        _rayCast.GetCollisionPoint()
        - _rayCast.GetCollisionNormal() * 0.5f;

    Vector3 lPos = tPos - chunk.GlobalPosition;

    int bx = Mathf.FloorToInt(lPos.X);
    int by = Mathf.FloorToInt(lPos.Y);
    int bz = Mathf.FloorToInt(lPos.Z);

    // ---------------------------------------------------------
    // FLOWERS / ROCKS ABOVE THE BLOCK
    // ---------------------------------------------------------

    BlockState above = chunk.GetBlock(bx, by + 1, bz);

    if (above.BlockId is
        "rose" or
        "clover" or
        "dandelion" or
        "rock_flint" or
        "rock_coal" or
        "rock_iron" or
        "rock_tin" or
        "rock_copper")
    {
        Vector3 aboveWorldPos =
            chunk.GlobalPosition +
            new Vector3(bx + 0.5f, by + 1.5f, bz + 0.5f);

        if (above.BlockId == "rose")
            SpawnItemDrop("rose", 1, aboveWorldPos);

        if (above.BlockId == "dandelion")
            SpawnItemDrop("dandelion", 1, aboveWorldPos);

        if (above.BlockId.StartsWith("rock_"))
            SpawnItemDrop(above.BlockId, 1, aboveWorldPos);

        chunk.SetBlock(
            bx,
            by + 1,
            bz,
            BlockState.Air
        );

        ResetBreak();
        return;
    }

    // ---------------------------------------------------------
    // GET TARGET BLOCK
    // ---------------------------------------------------------

    BlockState b = chunk.GetBlock(bx, by, bz);

    if (b.IsAir())
    {
        ResetBreak();
        return;
    }

    // ---------------------------------------------------------
    // BEDROCK
    // ---------------------------------------------------------

    if (b.BlockId == "bedrock" &&
        gm != null &&
        gm.IsSurvival)
    {
        ResetBreak();
        return;
    }

    // ---------------------------------------------------------
    // BOMB
    // ---------------------------------------------------------

    if (b.BlockId == "bomb")
    {
        Vector3I bombWorldPosition = new Vector3I(
            Mathf.FloorToInt(chunk.GlobalPosition.X) + bx,
            Mathf.FloorToInt(chunk.GlobalPosition.Y) + by,
            Mathf.FloorToInt(chunk.GlobalPosition.Z) + bz
        );

        // Remove the bomb first.
        chunk.SetBlock(
            bx,
            by,
            bz,
            BlockState.Air
        );

        // Then explode.
        ExplodeBomb(
            bombWorldPosition,
            3
        );

        ResetBreak();
        return;
    }

    // ---------------------------------------------------------
    // CREATIVE
    // ---------------------------------------------------------

    if (gm != null && gm.IsCreate)
    {
        var (dropIdC, dropCountC) = GetDrop(b.BlockId);

        if (dropIdC is not ("rose" or "dandelion" or "clover"))
            AddItemToInventory(dropIdC, dropCountC);

        var oreC =
            OreRegistry.Instance?.GetOreFromBlockState(b);

        if (oreC != null)
            AddItemToInventory(oreC.ItemId, 1);

        chunk.SetBlock(
            bx,
            by,
            bz,
            BlockState.Air
        );

        ResetBreak();
        return;
    }

    // ---------------------------------------------------------
    // NORMAL BREAKING / MINING
    // ---------------------------------------------------------

    var blockWorldPos = new Vector3I(
        Mathf.FloorToInt(tPos.X),
        Mathf.FloorToInt(tPos.Y),
        Mathf.FloorToInt(tPos.Z)
    );

    if (blockWorldPos != _breakTargetBlock ||
        b.BlockId != _breakTargetBlockId)
    {
        _breakOverlay?.ResetTarget();

        _breakHitCount = 0;

        _breakTargetBlock = blockWorldPos;
        _breakTargetBlockId = b.BlockId;

        string heldItem =
            _inventory.Slots[MainInvSize + _selectedSlot].IsEmpty
                ? ""
                : _inventory.Slots[MainInvSize + _selectedSlot].ItemId;

        _breakMiningPower =
            ToolDefinition.GetEffectiveMiningPower(
                b.BlockId,
                heldItem
            );
    }

    _breakHitCount++;

    bool shouldBreak =
        _breakOverlay?.UpdateBreak(
            blockWorldPos,
            _breakHitCount,
            _breakMiningPower
        )
        ??
        (
            _breakHitCount *
            _breakMiningPower >=
            BlockBreakOverlay.TotalStages
        );

    if (shouldBreak)
    {
        var (drop, dropCount) = GetDrop(b.BlockId);

        if (drop is not ("rose" or "dandelion" or "clover"))
        {
            Vector3 dropWorldPos =
                chunk.GlobalPosition +
                new Vector3(
                    bx + 0.5f,
                    by + 0.5f,
                    bz + 0.5f
                );

            SpawnItemDrop(
                drop,
                dropCount,
                dropWorldPos
            );

            var ore =
                OreRegistry.Instance?.GetOreFromBlockState(b);

            if (ore != null)
            {
                SpawnItemDrop(
                    ore.ItemId,
                    1,
                    dropWorldPos
                );
            }
        }

        chunk.SetBlock(
            bx,
            by,
            bz,
            BlockState.Air
        );

        DamageHeldToolDurability();

        ResetBreak();
    }
}

    private void ExplodeBomb(Vector3I center, int radius = 3)
{
    ChunkManager chunkManager = GetTree().GetFirstNodeInGroup("chunk_manager") as ChunkManager;

    if (chunkManager == null)
    {
        GD.PrintErr("[Bomb] Could not find ChunkManager.");
        return;
    }

    int radiusSquared = radius * radius;

    for (int x = center.X - radius; x <= center.X + radius; x++)
    {
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (int z = center.Z - radius; z <= center.Z + radius; z++)
            {
                int dx = x - center.X;
                int dy = y - center.Y;
                int dz = z - center.Z;

                // Sphere-shaped explosion.
                if ((dx * dx) + (dy * dy) + (dz * dz) > radiusSquared)
                    continue;

                Vector3I worldPos = new Vector3I(x, y, z);
                BlockState block = chunkManager.GetBlockAtWorld(worldPos);

                if (block.IsAir())
                    continue;

                // Bombs cannot destroy bedrock.
                if (block.BlockId == "bedrock")
                    continue;

                chunkManager.SetBlockAtWorld(worldPos, BlockState.Air);
            }
        }
    }

    GD.Print($"[Bomb] Explosion at {center} radius {radius}");
}

    // Loses 1 durability on the currently equipped hotbar item, if it's a
    // tool with durability. Breaks (clears the slot) at 0.
    private void DamageHeldToolDurability()
    {
        var slot = _inventory.Slots[MainInvSize + _selectedSlot];
        if (slot.IsEmpty) return;

        var item = ItemRegistry.Instance.GetItem(slot.ItemId);
        if (item == null || !item.HasDurability) return;

        slot.CurrentDurability--;
        if (slot.CurrentDurability <= 0)
            slot.Clear(); // tool breaks

        FireChanged();
        RefreshAllSlotVisuals();
    }

    private void ResetBreak()
    {
        _breakOverlay?.HideOverlay();
        _breakHitCount      = 0;
        _breakTargetBlock   = new Vector3I(int.MinValue, 0, 0);
        _breakTargetBlockId = "";
    }

    private void SyncBreakOverlayPosition()
    {
        if (!_rayCast.IsColliding()) return;
        var col = _rayCast.GetCollider() as Node;
        if (col == null || !col.HasMeta("chunk")) return;
        Vector3 tPos = _rayCast.GetCollisionPoint() - _rayCast.GetCollisionNormal() * 0.5f;
        _breakOverlay?.SyncPosition(new Vector3I(Mathf.FloorToInt(tPos.X), Mathf.FloorToInt(tPos.Y), Mathf.FloorToInt(tPos.Z)));
    }

    private void TryPlaceBlock()
    {
        if (!_rayCast.IsColliding() || string.IsNullOrEmpty(_selectedBlockId)) return;
        var gm = GameModeManager.Instance;
        if (gm != null && gm.IsStory) return;
        bool consume = gm == null || !gm.IsCreate;
        if (consume && !_inventory.HasItem(_selectedBlockId, 1)) return;

        // Guards against items with no real matching block (e.g. stray
        // icon-only items like "grass"/"box" that were never wired to a
        // BlockResource) - without this, the block would still get written
        // into the chunk data, and the mesh builder would spam
        // "Block not found" every time that chunk rebuilds since it can
        // never actually resolve a texture for it.
        if (!BlockRegistry.Instance.BlockExists(_selectedBlockId))
        {
            ShowFeedback($"{_selectedBlockId} can't be placed");
            return;
        }

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
    // EXIT TREE
    // =========================================================================

    public override void _ExitTree()
    {
        _hotbarLayer?.QueueFree();
        _chatLayer?.QueueFree();
        _craftingLayer?.QueueFree();
        _equipmentLayer?.QueueFree();
        _creativeMenuLayer?.QueueFree();
        _statsHudLayer?.QueueFree();
        _breakOverlay?.QueueFree();
        _crosshairLayer?.QueueFree();
        _inventoryLayer?.QueueFree();
        _cursorLayer?.QueueFree();
        _blockOutline?.QueueFree();
        foreach (var m in _chunkBorderMeshes) m?.QueueFree();
        _chunkBorderMeshes.Clear();
    }

    // =========================================================================
    // CHAT BAR + COMMANDS
    // =========================================================================

    private void BuildChatBar()
    {
        _chatLayer             = new CanvasLayer();
        _chatLayer.Layer       = 15;
        _chatLayer.ProcessMode = ProcessModeEnum.Always;
        GetTree().Root.CallDeferred("add_child", _chatLayer);

        var bg = new ColorRect();
        bg.Color = new Color(0f,0f,0f,0.55f);
        bg.AnchorLeft=0f; bg.AnchorRight=0.6f; bg.AnchorTop=1f; bg.AnchorBottom=1f;
        bg.OffsetTop=-44f; bg.OffsetBottom=0f; bg.Visible=false;
        _chatLayer.CallDeferred("add_child", bg);

        _chatInput = new LineEdit();
        _chatInput.PlaceholderText="Type a command... (e.g. /gamemode creative)";
        _chatInput.AnchorLeft=0f; _chatInput.AnchorRight=0.6f;
        _chatInput.AnchorTop=1f; _chatInput.AnchorBottom=1f;
        _chatInput.OffsetTop=-40f; _chatInput.OffsetBottom=-4f;
        _chatInput.OffsetLeft=8f; _chatInput.OffsetRight=-8f;
        _chatInput.Visible=false;
        _chatInput.TextSubmitted+=OnChatSubmit;
        _chatLayer.CallDeferred("add_child", _chatInput);

        _chatFeedback = new Label();
        _chatFeedback.AnchorLeft=0f; _chatFeedback.AnchorRight=0.6f;
        _chatFeedback.AnchorTop=1f; _chatFeedback.AnchorBottom=1f;
        _chatFeedback.OffsetTop=-70f; _chatFeedback.OffsetBottom=-46f;
        _chatFeedback.OffsetLeft=8f;
        _chatFeedback.AddThemeColorOverride("font_color", new Color(1f,1f,0.6f));
        _chatFeedback.AddThemeFontSizeOverride("font_size", 13);
        _chatFeedback.Visible=false;
        _chatLayer.CallDeferred("add_child", _chatFeedback);
    }

    private void OpenChat()
    {
        _chatOpen=true; _chatInput.Visible=true; _chatInput.Clear();
        _chatInput.GrabFocus(); Input.MouseMode=Input.MouseModeEnum.Visible;
    }

    private void CloseChat()
    {
        _chatOpen=false; _chatInput.Visible=false; _chatInput.ReleaseFocus();
        if (!_inventoryOpen && !_pauseMenu.IsOpen) Input.MouseMode=Input.MouseModeEnum.Captured;
    }

    private void OnChatSubmit(string text)
    {
        CloseChat(); text=text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        ParseCommand(text);
    }

    private void ShowFeedback(string msg)
    {
        _chatFeedback.Text=msg; _chatFeedback.Visible=true; _feedbackTimer=FeedbackDuration;
    }

    private void ParseCommand(string input)
    {
        if (!input.StartsWith("/")) return;
        string[] parts=input.Substring(1).Split(' ');
        string cmd=parts[0].ToLower();
        switch (cmd)
        {
            case "admin":
                if (parts.Length<2){ShowFeedback("Usage: /admin <password>");return;}
                if (SettingsManager.Instance.TryUnlockAdmin(parts[1])) ShowFeedback("Admin access granted.");
                else ShowFeedback("Incorrect password.");
                break;
            case "gamemode": case "gm":
                if (GameModeManager.Instance==null){ShowFeedback("GameModeManager not loaded.");return;}
                if (!SettingsManager.Instance.IsAdmin){ShowFeedback("You need admin to use this command.");return;}
                if (parts.Length<2){ShowFeedback("Usage: /gamemode <create|survival|story>");return;}
                switch (parts[1].ToLower())
                {
                    case "create": case "creative": case "c": case "1":
                        GameModeManager.Instance.SetMode(GameModeManager.GameMode.Create); ShowFeedback("Switched to Create mode."); break;
                    case "survival": case "s": case "0":
                        GameModeManager.Instance.SetMode(GameModeManager.GameMode.Survival); ShowFeedback("Switched to Survival mode."); break;
                    case "story": case "st": case "2":
                        GameModeManager.Instance.SetMode(GameModeManager.GameMode.Story); ShowFeedback("Switched to Story mode."); break;
                    default: ShowFeedback($"Unknown gamemode: {parts[1]}"); break;
                }
                break;
            case "fly":
                if (GameModeManager.Instance==null){ShowFeedback("GameModeManager not loaded.");return;}
                if (!SettingsManager.Instance.IsAdmin&&!GameModeManager.Instance.IsCreate)
                {ShowFeedback("Fly is only available in Create mode.");return;}
                ToggleFly(); ShowFeedback(_isFlying?"Flying: ON":"Flying: OFF");
                break;
            default: ShowFeedback($"Unknown command: /{cmd}"); break;
        }
    }

    // =========================================================================
    // GAMEMODE + FLY
    // =========================================================================

    private void OnGameModeChanged(GameModeManager.GameMode mode)
    {
        if (mode != GameModeManager.GameMode.Create && _isFlying) SetFlying(false);
    }

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
        if (Input.IsActionPressed("jump"))        velocity.Y = _flyVertSpeed;
        else if (Input.IsActionPressed("crouch")) velocity.Y = -_flyVertSpeed;
        else velocity.Y = Mathf.MoveToward(velocity.Y, 0f, _flyVertSpeed * dt * 10f);
    }

    // =========================================================================
    // MISC
    // =========================================================================

    public PlayerStats  GetStats()        => _stats;
    public PlayerCamera GetPlayerCamera() => _playerCamera;

    public void SaveInventoryFromPauseMenu(ChunkManager cm)
    {
        cm.SaveInventory(_inventory);
        cm.SavePlayerPosition(GlobalPosition);
    }

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