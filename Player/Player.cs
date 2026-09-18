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
        _stats = new PlayerStats();
AddChild(_stats);

InitializeDeathSystem();
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

   private Texture2D _unknownItemIconTex;

private Texture2D GetItemIcon(string itemId)
{
    if (string.IsNullOrEmpty(itemId))
        return null;

    if (_iconCache.TryGetValue(itemId, out var cached))
        return cached;

    // ------------------------------------------------------------
    // 1. Prefer the icon assigned directly to the ItemResource.
    // ------------------------------------------------------------
    // Important for tools and crafted items that generate or assign
    // their own icons.

    var item =
        ItemRegistry.Instance?.GetItem(itemId);

    Texture2D tex =
        item?.Icon;


    // ------------------------------------------------------------
    // 2. Recursively search Assets/Textures/Items
    // ------------------------------------------------------------
    // This allows any item texture to live in any subfolder:
    //
    // Items/apple.png
    // Items/food/apple.png
    // Items/food/fruit/apple.png
    // Items/ore/sun_shard.png
    // Items/tool/chalk/chalk.png
    //
    // No folder list needs to be maintained here.

    if (tex == null)
    {
        string itemTexturePath =
            FindItemTexturePath(
                "res://Assets/Textures/Items",
                itemId
            );

        if (!string.IsNullOrEmpty(itemTexturePath) &&
            ResourceLoader.Exists(itemTexturePath))
        {
            tex =
                ResourceLoader.Load<Texture2D>(
                    itemTexturePath
                );
        }
    }


    // ------------------------------------------------------------
    // 3. Block texture fallback
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
    // 4. Unknown item placeholder
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
    // 5. Cache the result
    // ------------------------------------------------------------

    _iconCache[itemId] =
        tex;

    return tex;
}


// =========================================================================
// RECURSIVE ITEM TEXTURE SEARCH
// =========================================================================

private string FindItemTexturePath(
    string folderPath,
    string itemId)
{
    if (string.IsNullOrWhiteSpace(itemId))
        return "";

    DirAccess dir =
        DirAccess.Open(folderPath);

    if (dir == null)
        return "";


    // ------------------------------------------------------------
    // Check files in this folder
    // ------------------------------------------------------------

    string[] files =
        dir.GetFiles();

    foreach (string fileName in files)
    {
        if (!fileName.EndsWith(
                ".png",
                StringComparison.OrdinalIgnoreCase))
            continue;

        string baseName =
            System.IO.Path.GetFileNameWithoutExtension(
                fileName
            );

        if (!string.Equals(
                baseName,
                itemId,
                StringComparison.OrdinalIgnoreCase))
            continue;

        string fullPath =
            $"{folderPath}/{fileName}";

        if (ResourceLoader.Exists(fullPath))
            return fullPath;
    }


    // ------------------------------------------------------------
    // Search every subfolder
    // ------------------------------------------------------------

    string[] directories =
        dir.GetDirectories();

    foreach (string directoryName in directories)
    {
        string childPath =
            $"{folderPath}/{directoryName}";

        string result =
            FindItemTexturePath(
                childPath,
                itemId
            );

        if (!string.IsNullOrWhiteSpace(result))
            return result;
    }

    return "";
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
            if (mb.ButtonIndex == MouseButton.Middle && mb.Pressed)
{
    PickBlock();
    return;
}
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
        // -------------------------------------------------
        // MOB INTERACTION
        // -------------------------------------------------

        if (TryInteractWithMob())
        {
            _isPlacing = false;
            return;
        }

        // -------------------------------------------------
        // EXISTING BLOCK INTERACTION
        // -------------------------------------------------

        if (TryOpenCraftingTable())
        {
            _isPlacing = false;
        }
        else
        {
            TryPlaceBlock();
            _placeTimer = 0f;
        }
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
                cm.SavePlayerPosition(
    GlobalPosition,
    Rotation.Y,
    _playerCamera?.GetPitch() ?? 0f
);
                GD.Print("World saved!");
            }
        }
    }
    private bool TryInteractWithMob()
{
    Camera3D camera = GetViewport().GetCamera3D();

    if (camera == null)
        return false;

    Vector2 screenCenter =
        GetViewport().GetVisibleRect().Size / 2f;

    Vector3 rayOrigin =
        camera.ProjectRayOrigin(screenCenter);

    Vector3 rayDirection =
        camera.ProjectRayNormal(screenCenter);

    Vector3 rayEnd =
        rayOrigin + rayDirection * 6f;

    var spaceState =
        GetWorld3D().DirectSpaceState;

    var query =
        PhysicsRayQueryParameters3D.Create(
            rayOrigin,
            rayEnd
        );

    query.CollideWithBodies = true;
    query.CollideWithAreas = true;

    var result =
        spaceState.IntersectRay(query);

    if (result.Count == 0)
        return false;

    if (!result.ContainsKey("collider"))
        return false;

    Node collider =
        result["collider"].As<Node>();

    if (collider == null)
        return false;

    // The collision shape is normally a child of the Mob,
    // so check both the collider itself and its parent.
   Mob mob = collider as Mob;

Node current = collider;

while (mob == null && current != null)
{
    current = current.GetParent();
    mob = current as Mob;
}
    if (mob == null)
        return false;

    // -----------------------------------------------------
    // GET CURRENTLY HELD ITEM
    // -----------------------------------------------------

    int inventoryIndex =
        MainInvSize + _selectedSlot;

    if (_inventory == null ||
        _inventory.Slots == null ||
        inventoryIndex < 0 ||
        inventoryIndex >= _inventory.Slots.Length)
    {
        return true;
    }

    InventorySlot slot =
        _inventory.Slots[inventoryIndex];

    if (slot == null ||
        slot.IsEmpty)
    {
        GD.Print(
            $"[Player] Looking at {mob.Name}, but holding nothing."
        );

        return true;
    }

    string itemId =
        slot.ItemId;

    // -----------------------------------------------------
    // TRY FEEDING
    // -----------------------------------------------------

    if (!mob.CanEat(itemId))
    {
        GD.Print(
            $"[Player] {mob.Name} cannot eat {itemId}."
        );

        // We still return true because the player is
        // interacting with a mob, so don't place a block
        // through it.
        return true;
    }

    if (!mob.Feed(itemId))
    {
        return true;
    }

    // -----------------------------------------------------
    // CONSUME ONE FOOD ITEM
    // -----------------------------------------------------

    int removed =
        _inventory.RemoveItem(
            itemId,
            1
        );

    if (removed > 0)
    {
        GD.Print(
            $"[Player] Fed {mob.Name} {itemId}."
        );
    }

    return true;
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

// =========================================================
// DAMAGE
// =========================================================
//
// Generic player damage entry point.
//
// Damage is intentionally separate from knockback/effects.
// External effect sources can deal damage and/or apply
// knockback independently.
//
// =========================================================

public void TakeDamage(float amount)
{
    if (_stats == null ||
        _stats.IsDead)
    {
        return;
    }

    if (amount <= 0f)
    {
        return;
    }

    _stats.TakeDamage(amount);
}
    public PlayerStats  GetStats()        => _stats;
    public PlayerCamera GetPlayerCamera() => _playerCamera;

    public void SaveInventoryFromPauseMenu(ChunkManager cm)
{
    cm.SaveInventory(_inventory);

    cm.SavePlayerPosition(
        GlobalPosition,
        Rotation.Y,
        _playerCamera?.GetPitch() ?? 0f
    );
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