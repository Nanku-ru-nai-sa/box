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
    private PlayerStats _stats;
    private PlayerCamera _playerCamera;
    private RayCast3D _rayCast;
    private RandomNumberGenerator _dropRng = new RandomNumberGenerator();
    private bool _isSprinting = false;
    private bool _isCrouching = false;
    private bool _hasDoubleJumped = false;
    private bool _isGliding = false;
    private bool _isPlacing = false;
    private bool _isInWater = false;
    private float _placeTimer = 0f;
    private const float PlaceInterval = 0.15f;
    private bool _isBreaking = false;

    private float _breakTimer = 0f;
    private const float BreakInterval = 0.4f;
    private bool _isDropping = false;
    private float _dropTimer = 0f;
    private const float DropInterval = 0.25f;
    private const float DropForwardSpeed = 7.5f;
    private const float DropUpSpeed = 1.0f;
    private const float DropSpawnHeight = 1.0f;
    private string _selectedBlockId = "";
    private MeshInstance3D _blockOutline;

    private BlockBreakOverlay _breakOverlay;
    private int _breakHitCount = 0;
    private int _breakMiningPower = 1;
    private Vector3I _breakTargetBlock = new Vector3I(int.MinValue, 0, 0);
    private string _breakTargetBlockId = "";

    private const float UnarmedAttackDamage = 2f;

    private const int HotbarSize = 12;
    private Panel[] _hotbarSlots = new Panel[HotbarSize];
    private Panel[] _invHotbarSlots = new Panel[HotbarSize];
    private Label[] _hotbarLabels = new Label[HotbarSize];
    private Label[] _invHotbarLabels = new Label[HotbarSize];
    private int _selectedSlot = 0;

    private const int MainInvSize = 48;
    private const int TotalSlots = MainInvSize + HotbarSize;
    private Inventory _inventory;
    private Panel[] _invSlotPanels = new Panel[MainInvSize];
    private ItemTooltip _invTooltip;
    private ItemTooltip _hotbarTooltip;
    private Label[] _invSlotLabels = new Label[MainInvSize];

    private InventorySlot _heldSlot = new InventorySlot();
    private int _heldFromSlot = -1;
    private Panel _cursorPanel;
    private Label _cursorLabel;
    private TextureRect _cursorTex;

    private enum DragMode { None, LmbWithItem, LmbNoItem, RmbDrag, ShiftLmbNoItem }
    private DragMode _dragMode = DragMode.None;
    private List<int> _dragVisited = new List<int>();
    private int _dragOrigCount = 0;
    private int _dragLastSlot = -1;
    private CollisionShape3D _playerCollision;

    private Control _inventoryScreen;
    private bool _inventoryOpen = false;
    private bool _showChunkBorders = false;
    private bool _hudVisible = true;
    private CanvasLayer _hotbarLayer;
    private CanvasLayer _crosshairLayer;
    private CanvasLayer _inventoryLayer;
    private CanvasLayer _cursorLayer;
    private PauseMenu _pauseMenu;

    private CalendarPanel _calendarPanel;
    private CanvasLayer _calendarLayer;

    private CraftingPanel _craftingPanel;
    private CanvasLayer _craftingLayer;
    private const float CraftingTableRange = 5f;

    private EquipmentPanel _equipmentPanel;
    private CanvasLayer _equipmentLayer;

    private ToolBenchPanel _toolBenchPanel;
    private KilnPanel _kilnPanel;

    private Control _stationTabBar;
    private readonly Panel[] _stationSlots = new Panel[4];
    private readonly TextureRect[] _stationSlotIcons = new TextureRect[4];
    private readonly string[] _stationSlotIds = new string[4];
    private string _activeStation = "default";
    private Vector3I _nearbyKilnPosition = Vector3I.Zero;
    private bool _hasNearbyKiln = false;
    private bool _kilnPanelHasPosition = false;

    private CreativeMenu _creativeMenu;
    private CanvasLayer _creativeMenuLayer;
    private bool _creativeMenuOpen = false;

    private StatsHud _statsHud;
    private CanvasLayer _statsHudLayer;

    private bool _isFlying = false;
    private float _flySpeed = 12f;
    private float _flyVertSpeed = 8f;
    private bool _chatOpen = false;
    private readonly List<string> _chatHistory = new();
    private int _chatHistoryIndex = -1;
    private const int MaxChatHistory = 50;
    private CanvasLayer _chatLayer;
    private LineEdit _chatInput;
    private Label _chatFeedback;
    private float _feedbackTimer = 0f;
    private const float FeedbackDuration = 3f;
    private double _lastJumpTime = 0.0;
    private const double DoubleJumpWindow = 0.35;

    private readonly List<MeshInstance3D> _chunkBorderMeshes = new();
    private Mob _mountedMob;

public bool IsMounted =>
    _mountedMob != null &&
    IsInstanceValid(_mountedMob);
    public Mob MountedMob => _mountedMob;
    

    public bool CanDoubleJump { get; set; } = false;
    public bool CanWallClimb { get; set; } = false;
    public bool CanGlide { get; set; } = false;
    public bool CanGrapple { get; set; } = false;

    private const int SlotSize = 56;
    private const int SlotGap = 5;

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
        _rayCast = GetNode<RayCast3D>("PlayerCamera/Camera3D/RayCast3D");
        _rayCast.AddException(this);
        _dropRng.Randomize();
        _playerCollision = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");

        AddToGroup("player");

        _inventory = new Inventory();
        _inventory.SlotCount = TotalSlots;
        AddChild(_inventory);

        CallDeferred(nameof(LoadInventoryFromSave));

        _crosshairLayer = new CanvasLayer();
        GetTree().Root.CallDeferred("add_child", _crosshairLayer);

        var crosshair = new ColorRect();
        crosshair.Color = new Color(1, 1, 1);
        crosshair.Size = new Vector2(2, 2);
        crosshair.PivotOffset = new Vector2(1, 1);
        crosshair.MouseFilter = Control.MouseFilterEnum.Ignore;
        crosshair.AnchorLeft = 0.5f;
        crosshair.AnchorRight = 0.5f;
        crosshair.AnchorTop = 0.5f;
        crosshair.AnchorBottom = 0.5f;
        crosshair.OffsetLeft = -1;
        crosshair.OffsetTop = -1;
        crosshair.OffsetRight = 1;
        crosshair.OffsetBottom = 1;
        _crosshairLayer.CallDeferred("add_child", crosshair);

        _blockOutline = new MeshInstance3D();
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Lines);

        Vector3[] corners =
        {
            new(0,0,0), new(1,0,0), new(1,0,1), new(0,0,1),
            new(0,1,0), new(1,1,0), new(1,1,1), new(0,1,1)
        };

        int[][] edges =
        {
            new[]{0,1}, new[]{1,2}, new[]{2,3}, new[]{3,0},
            new[]{4,5}, new[]{5,6}, new[]{6,7}, new[]{7,4},
            new[]{0,4}, new[]{1,5}, new[]{2,6}, new[]{3,7}
        };

        foreach (var e in edges)
        {
            st.AddVertex(corners[e[0]]);
            st.AddVertex(corners[e[1]]);
        }

        _blockOutline.Mesh = st.Commit();

        var outlineMat = new StandardMaterial3D();
        outlineMat.AlbedoColor = new Color(0, 0, 0);
        outlineMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _blockOutline.MaterialOverride = outlineMat;
        _blockOutline.Visible = false;

        GetTree().Root.CallDeferred("add_child", _blockOutline);

        _breakOverlay = new BlockBreakOverlay();
        GetTree().Root.CallDeferred("add_child", _breakOverlay);

        BuildHotbarHUD();
        BuildInventoryScreen();
        BuildCursorPanel();
        BuildCraftingPanel();
        BuildToolBenchPanel();
        BuildKilnPanel();
        BuildStationTabBar();
        BuildEquipmentPanel();
        BuildCreativeMenu();

        if (_stats == null)
        {
            _stats = new PlayerStats();
            AddChild(_stats);
        }

        if (_playerCamera == null)
        {
            _playerCamera = new PlayerCamera();
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

        var item =
            ItemRegistry.Instance?.GetItem(itemId);

        Texture2D tex =
            item?.Icon;

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

    private static readonly string[] StationBlockIds =
    {
        "crafter",
        "tool_bench",
        "kiln"
    };

    private List<string> GetNearbyStationTypes()
    {
        var nearestByType = new Dictionary<string, (float Distance, Vector3I Position)>();
        _hasNearbyKiln = false;

        var cm =
            GetTree().Root.FindChild(
                "ChunkManager",
                true,
                false
            ) as ChunkManager;

        if (cm == null)
            return new List<string>();

        Vector3 pos = GlobalPosition;
        float nearestKilnDistance = float.MaxValue;

        for (int dx = -5; dx <= 5; dx++)
        for (int dy = -3; dy <= 3; dy++)
        for (int dz = -5; dz <= 5; dz++)
        {
            float distance = new Vector3(dx, dy, dz).Length();
            if (distance > CraftingTableRange)
                continue;

            Vector3I blockPosition = new Vector3I(
                Mathf.FloorToInt(pos.X) + dx,
                Mathf.FloorToInt(pos.Y) + dy,
                Mathf.FloorToInt(pos.Z) + dz
            );

            var block = cm.GetBlockAtWorld(blockPosition);
            string id = block.BlockId;

            if (System.Array.IndexOf(StationBlockIds, id) < 0)
                continue;

            if (!nearestByType.TryGetValue(id, out var existing) ||
                distance < existing.Distance)
            {
                nearestByType[id] = (distance, blockPosition);
            }

            if (id == "kiln" && distance < nearestKilnDistance)
            {
                nearestKilnDistance = distance;
                _nearbyKilnPosition = blockPosition;
                _hasNearbyKiln = true;
            }
        }

        var candidates = new List<(string Id, float Distance)>();
        foreach (var pair in nearestByType)
            candidates.Add((pair.Key, pair.Value.Distance));

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        var result = new List<string>();
        for (int i = 0; i < candidates.Count && i < 3; i++)
            result.Add(candidates[i].Id);

        return result;
    }

    private void UpdateCraftingProximity()
    {
        if (!_inventoryOpen ||
            RecipeManager.Instance == null)
            return;

        var nearby = GetNearbyStationTypes();

        if (_hasNearbyKiln && _kilnPanel != null &&
            (!_kilnPanelHasPosition ||
             _kilnPanel.GetKilnPosition() != _nearbyKilnPosition))
        {
            _kilnPanel.SetKiln(
                _nearbyKilnPosition,
                KilnManager.Instance?.GetSlots(_nearbyKilnPosition)
            );
            _kilnPanelHasPosition = true;
        }

        UpdateStationSelector(nearby);

        if (_activeStation != "default" && !nearby.Contains(_activeStation))
            SwitchStation("default");

        if (_activeStation == "crafter")
            _craftingPanel?.SetGridSize(3, true);
        else if (_activeStation == "default")
            _craftingPanel?.SetGridSize(2, false);
    }

    private bool TryOpenCraftingTable()
    {
        if (!_rayCast.IsColliding())
            return false;

        var col =
            _rayCast.GetCollider() as Node;

        if (col == null ||
            !col.HasMeta("chunk"))
            return false;

        Chunk chunk =
            (Chunk)col.GetMeta("chunk").AsGodotObject();

        Vector3 tPos =
            _rayCast.GetCollisionPoint() -
            _rayCast.GetCollisionNormal() * 0.5f;

        Vector3 lPos =
            tPos - chunk.GlobalPosition;

        var b =
            chunk.GetBlock(
                Mathf.FloorToInt(lPos.X),
                Mathf.FloorToInt(lPos.Y),
                Mathf.FloorToInt(lPos.Z)
            );

        if (b.BlockId != "crafter" &&
            b.BlockId != "tool_bench" &&
            b.BlockId != "kiln")
            return false;

        if (!_inventoryOpen)
            ToggleInventory();

        _hotbarLayer.Visible = false;

        if (_craftingPanel != null)
        {
            _craftingPanel.SetGridSize(3, true);
            UpdateCraftingProximity();
        }

        if (_equipmentPanel != null)
            _equipmentPanel.Visible = true;

        if (b.BlockId == "kiln")
        {
            Vector3I kilnPosition = new Vector3I(
                Mathf.FloorToInt(tPos.X),
                Mathf.FloorToInt(tPos.Y),
                Mathf.FloorToInt(tPos.Z));
            _kilnPanel?.SetKiln(kilnPosition, KilnManager.Instance?.GetSlots(kilnPosition));
            _kilnPanelHasPosition = true;
        }

        SwitchStation(b.BlockId);

        return true;
    }

    // =========================================================================
    // CRAFTING SLOT CLICK
    // =========================================================================

    private void HandleCraftSlotClicked(
        int idx,
        MouseButton button,
        bool shift)
    {
        if (_craftingPanel == null)
            return;

        var slot =
            _craftingPanel.GetSlot(idx);

        if (slot == null)
            return;

        if (shift &&
            button == MouseButton.Left)
        {
            if (slot.IsEmpty)
                return;

            int leftover =
                AddItemToInventory(
                    slot.ItemId,
                    slot.Count
                );

            int moved =
                slot.Count - leftover;

            slot.Count -= moved;

            if (slot.Count <= 0)
                slot.Clear();

            _craftingPanel.NotifyGridChanged();
            FireChanged();
            UpdateCursorVisual();

            return;
        }

        if (button == MouseButton.Left)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty)
                    return;

                _heldSlot.ItemId =
                    slot.ItemId;

                _heldSlot.Count =
                    slot.Count;

                slot.Clear();
            }
            else
            {
                if (slot.IsEmpty)
                {
                    slot.ItemId =
                        _heldSlot.ItemId;

                    slot.Count =
                        _heldSlot.Count;

                    _heldSlot.Clear();
                }
                else if (slot.ItemId ==
                         _heldSlot.ItemId)
                {
                    int space =
                        _inventory.MaxStackSize -
                        slot.Count;

                    int transfer =
                        Mathf.Min(
                            space,
                            _heldSlot.Count
                        );

                    slot.Count +=
                        transfer;

                    _heldSlot.Count -=
                        transfer;

                    if (_heldSlot.Count <= 0)
                        _heldSlot.Clear();
                }
                else
                {
                    (slot.ItemId, _heldSlot.ItemId) =
                        (_heldSlot.ItemId, slot.ItemId);

                    (slot.Count, _heldSlot.Count) =
                        (_heldSlot.Count, slot.Count);
                }
            }
        }
        else if (button == MouseButton.Right)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty)
                    return;

                int half =
                    Mathf.CeilToInt(
                        slot.Count / 2f
                    );

                _heldSlot.ItemId =
                    slot.ItemId;

                _heldSlot.Count =
                    half;

                slot.Count -= half;

                if (slot.Count <= 0)
                    slot.Clear();
            }
            else
            {
                if (!slot.IsEmpty &&
                    slot.ItemId != _heldSlot.ItemId)
                    return;

                if (!slot.IsEmpty &&
                    slot.Count >= _inventory.MaxStackSize)
                    return;

                if (slot.IsEmpty)
                    slot.ItemId =
                        _heldSlot.ItemId;

                slot.Count++;

                _heldSlot.Count--;

                if (_heldSlot.Count <= 0)
                    _heldSlot.Clear();
            }
        }

        _craftingPanel.NotifyGridChanged();
        FireChanged();
        UpdateCursorVisual();
    }

    private void HandleKilnSlotClicked(int index, MouseButton button, bool shift)
    {
        if (_kilnPanel == null) return;
        InventorySlot slot = _kilnPanel.GetSlot(index);
        if (slot == null) return;

        if (shift && button == MouseButton.Left)
        {
            if (slot.IsEmpty) return;
            int remaining = AddItemToInventory(slot.ItemId, slot.Count);
            slot.Count = remaining;
            if (slot.Count <= 0) slot.Clear();
        }
        else if (button == MouseButton.Left)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty) return;
                _heldSlot.ItemId = slot.ItemId;
                _heldSlot.Count = slot.Count;
                slot.Clear();
            }
            else
            {
                if (index == 1 &&
                    (ItemRegistry.Instance?.GetItem(_heldSlot.ItemId)?.FuelBurnSeconds ?? 0f) <= 0f)
                {
                    ShowFeedback("That item cannot be used as fuel.");
                    return;
                }

                if (slot.IsEmpty)
                {
                    slot.ItemId = _heldSlot.ItemId;
                    slot.Count = _heldSlot.Count;
                    _heldSlot.Clear();
                }
                else if (slot.ItemId == _heldSlot.ItemId)
                {
                    int maxStack = ItemRegistry.Instance?.GetItem(slot.ItemId)?.MaxStackSize ?? 64;
                    int amount = Mathf.Min(maxStack - slot.Count, _heldSlot.Count);
                    if (amount > 0)
                    {
                        slot.Count += amount;
                        _heldSlot.Count -= amount;
                        if (_heldSlot.Count <= 0) _heldSlot.Clear();
                    }
                }
                else
                {
                    (slot.ItemId, _heldSlot.ItemId) = (_heldSlot.ItemId, slot.ItemId);
                    (slot.Count, _heldSlot.Count) = (_heldSlot.Count, slot.Count);
                }
            }
        }
        else if (button == MouseButton.Right)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty) return;
                int take = Mathf.CeilToInt(slot.Count / 2f);
                _heldSlot.ItemId = slot.ItemId;
                _heldSlot.Count = take;
                slot.Count -= take;
                if (slot.Count <= 0) slot.Clear();
            }
            else
            {
                if (index == 1 &&
                    (ItemRegistry.Instance?.GetItem(_heldSlot.ItemId)?.FuelBurnSeconds ?? 0f) <= 0f)
                {
                    ShowFeedback("That item cannot be used as fuel.");
                    return;
                }

                if (slot.IsEmpty)
                {
                    slot.ItemId = _heldSlot.ItemId;
                    slot.Count = 1;
                    _heldSlot.Count--;
                    if (_heldSlot.Count <= 0) _heldSlot.Clear();
                }
                else if (slot.ItemId == _heldSlot.ItemId)
                {
                    int maxStack = ItemRegistry.Instance?.GetItem(slot.ItemId)?.MaxStackSize ?? 64;
                    if (slot.Count < maxStack)
                    {
                        slot.Count++;
                        _heldSlot.Count--;
                        if (_heldSlot.Count <= 0) _heldSlot.Clear();
                    }
                }
            }
        }

        KilnManager.Instance?.NotifyChanged();
        _kilnPanel.Refresh();
        FireChanged();
        UpdateCursorVisual();
    }

    private void HandleKilnOutputClicked()
    {
        if (_kilnPanel == null || !_heldSlot.IsEmpty) return;
        var slots = KilnManager.Instance?.GetSlots(_kilnPanel.GetKilnPosition());
        if (slots == null || slots.Count < 3 || slots[2].IsEmpty) return;

        _heldSlot.ItemId = slots[2].ItemId;
        _heldSlot.Count = slots[2].Count;
        slots[2].Clear();

        KilnManager.Instance?.NotifyChanged();
        _kilnPanel.Refresh();
        FireChanged();
        UpdateCursorVisual();
    }

    private void HandleToolBenchCenterClicked()
    {
        if (_toolBenchPanel == null)
            return;

        if (!_heldSlot.IsEmpty)
        {
            var item =
                ItemRegistry.Instance.GetItem(
                    _heldSlot.ItemId
                );

            if (item != null &&
                item.HasDurability &&
                ToolCrafting.TryGetRecipe(
                    item,
                    out _,
                    out _,
                    out _))
            {
                if (_toolBenchPanel.LoadExistingTool(
                        _heldSlot))
                {
                    FireChanged();
                    UpdateCursorVisual();
                }
            }

            return;
        }

        _toolBenchPanel.ToggleFamilyPicker();
    }

    private void HandleToolBenchSlotClicked(
        int idx,
        MouseButton button,
        bool shift)
    {
        if (_toolBenchPanel == null)
            return;

        var slot =
            _toolBenchPanel.GetSlot(idx);

        if (slot == null)
            return;

        if (shift &&
            button == MouseButton.Left)
        {
            if (slot.IsEmpty)
                return;

            int leftover =
                AddItemToInventory(
                    slot.ItemId,
                    slot.Count
                );

            int moved =
                slot.Count - leftover;

            slot.Count -= moved;

            if (slot.Count <= 0)
                slot.Clear();

            _toolBenchPanel.RefreshAllVisuals();
            FireChanged();
            UpdateCursorVisual();

            return;
        }

        if (button == MouseButton.Left)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty)
                    return;

                _heldSlot.ItemId =
                    slot.ItemId;

                _heldSlot.Count =
                    slot.Count;

                slot.Clear();
            }
            else
            {
                if (slot.IsEmpty)
                {
                    slot.ItemId =
                        _heldSlot.ItemId;

                    slot.Count =
                        _heldSlot.Count;

                    _heldSlot.Clear();
                }
                else if (slot.ItemId ==
                         _heldSlot.ItemId)
                {
                    int space =
                        _inventory.MaxStackSize -
                        slot.Count;

                    int transfer =
                        Mathf.Min(
                            space,
                            _heldSlot.Count
                        );

                    slot.Count +=
                        transfer;

                    _heldSlot.Count -=
                        transfer;

                    if (_heldSlot.Count <= 0)
                        _heldSlot.Clear();
                }
                else
                {
                    (slot.ItemId, _heldSlot.ItemId) =
                        (_heldSlot.ItemId, slot.ItemId);

                    (slot.Count, _heldSlot.Count) =
                        (_heldSlot.Count, slot.Count);
                }
            }
        }
        else if (button == MouseButton.Right)
        {
            if (_heldSlot.IsEmpty)
            {
                if (slot.IsEmpty)
                    return;

                int half =
                    Mathf.CeilToInt(
                        slot.Count / 2f
                    );

                _heldSlot.ItemId =
                    slot.ItemId;

                _heldSlot.Count =
                    half;

                slot.Count -= half;

                if (slot.Count <= 0)
                    slot.Clear();
            }
            else
            {
                if (!slot.IsEmpty &&
                    slot.ItemId != _heldSlot.ItemId)
                    return;

                if (!slot.IsEmpty &&
                    slot.Count >= _inventory.MaxStackSize)
                    return;

                if (slot.IsEmpty)
                    slot.ItemId =
                        _heldSlot.ItemId;

                slot.Count++;

                _heldSlot.Count--;

                if (_heldSlot.Count <= 0)
                    _heldSlot.Clear();
            }
        }

        _toolBenchPanel.RefreshAllVisuals();
        FireChanged();
        UpdateCursorVisual();
    }

    private void HandleToolBenchOutputClicked(
        MouseButton button,
        bool shift)
    {
        if (_toolBenchPanel == null)
            return;

        if (button != MouseButton.Left)
            return;

        if (!_toolBenchPanel.PeekResult(
                out string peekId,
                out int _))
            return;

        if (!_heldSlot.IsEmpty)
            return;

        if (_toolBenchPanel.TryConsumeOneCraft(
                out string resultId,
                out int resultDurability))
        {
            _heldSlot.ItemId =
                resultId;

            _heldSlot.Count =
                1;

            _heldSlot.CurrentDurability =
                resultDurability;

            FireChanged();
            UpdateCursorVisual();
        }
    }

    private void HandleOutputClicked(
        MouseButton button,
        bool shift)
    {
        if (_craftingPanel == null)
            return;

        if (shift &&
            button == MouseButton.Left)
        {
            int safety = 64;

            while (safety-- > 0)
            {
                if (!_craftingPanel.TryConsumeOneCraft(
                        out string rid,
                        out int rcount))
                    break;

                int leftover =
                    AddItemToInventory(
                        rid,
                        rcount
                    );

                if (leftover > 0)
                    break;
            }

            FireChanged();
            return;
        }

        if (button == MouseButton.Left)
        {
            if (!_craftingPanel.PeekResult(
                    out string peekId,
                    out int _))
                return;

            if (!_heldSlot.IsEmpty)
            {
                if (_heldSlot.ItemId != peekId ||
                    _heldSlot.Count >=
                    _inventory.MaxStackSize)
                    return;
            }

            if (_craftingPanel.TryConsumeOneCraft(
                    out string resultId,
                    out int resultCount))
            {
                if (_heldSlot.IsEmpty)
                {
                    _heldSlot.ItemId =
                        resultId;

                    _heldSlot.Count =
                        resultCount;
                }
                else
                {
                    int space =
                        _inventory.MaxStackSize -
                        _heldSlot.Count;

                    _heldSlot.Count +=
                        Mathf.Min(
                            space,
                            resultCount
                        );
                }

                FireChanged();
                UpdateCursorVisual();
            }
        }
    }

    private void HandleLearnedCraftClicked(
        string resultId,
        int resultCount)
    {
        if (!_heldSlot.IsEmpty)
        {
            if (_heldSlot.ItemId != resultId ||
                _heldSlot.Count >=
                _inventory.MaxStackSize)
            {
                AddItemToInventory(
                    resultId,
                    resultCount
                );
            }
            else
            {
                int space =
                    _inventory.MaxStackSize -
                    _heldSlot.Count;

                _heldSlot.Count +=
                    Mathf.Min(
                        space,
                        resultCount
                    );
            }
        }
        else
        {
            _heldSlot.ItemId =
                resultId;

            _heldSlot.Count =
                resultCount;
        }

        FireChanged();
        UpdateCursorVisual();
    }

    // =========================================================================
    // CREATIVE MENU
    // =========================================================================

    private void OnCreativeItemChosen(
        string itemId,
        int count)
    {
        AddItemToInventory(
            itemId,
            count
        );

        FireChanged();
    }

    // =========================================================================
    // PROCESS
    // =========================================================================

    public override void _Process(double delta)
{
    if (_feedbackTimer > 0f)
    {
        _feedbackTimer -= (float)delta;

        if (_feedbackTimer <= 0f &&
            _chatFeedback != null)
        {
            _chatFeedback.Visible = false;
        }
    }

    if (_inventoryOpen)
        UpdateCraftingProximity();

    if (_kilnPanel != null && _kilnPanel.Visible)
        _kilnPanel.Refresh();

    // Automatically close the crate when the player gets too far away.
    if (_containerOpen)
    {
        Vector3 crateCenter =
            (Vector3)_openedCratePosition +
            new Vector3(0.5f, 0.5f, 0.5f);

        if (GlobalPosition.DistanceTo(crateCenter) > CrateCloseRange)
        {
            CloseCrateOnly();
        }
    }

    if (!_inventoryOpen)
        return;

    var mouse =
        GetViewport().GetMousePosition();

    if (!_heldSlot.IsEmpty &&
        _cursorPanel != null)
    {
        _cursorPanel.GlobalPosition =
            mouse -
            new Vector2(
                SlotSize / 2f,
                SlotSize / 2f
            );
    }

    if (_dragMode != DragMode.None)
    {
        int under =
            GetSlotUnderMouse();

        if (under >= 0 &&
            under != _dragLastSlot)
        {
            _dragLastSlot = under;
            OnDragEnterSlot(under);
        }
        else if (under < 0 &&
                 _craftingPanel != null)
        {
            int count =
                _craftingPanel.GetActiveSlotCount();

            for (int i = 0; i < count; i++)
            {
                var panel =
                    _craftingPanel.GetSlotPanel(i);

                if (panel == null)
                    continue;

                if (new Rect2(
                        panel.GlobalPosition,
                        panel.Size
                    ).HasPoint(mouse))
                {
                    int craftId =
                        -(i + 1);

                    if (craftId != _dragLastSlot)
                    {
                        _dragLastSlot = craftId;
                        OnDragEnterCraftSlot(i);
                    }

                    break;
                }
            }
        }
    }
}

    // =========================================================================
    // PHYSICS
    // =========================================================================

    public override void _PhysicsProcess(double delta)
{
    float dt = (float)delta;

    UpdateBlockOutline();

    if (_isBreaking)
        SyncBreakOverlayPosition();

    if (_stats == null ||
        _stats.IsDead)
        return;

    if (_chatOpen)
    return;

// =========================================================
// CONTINUOUS BLOCK MINING
// =========================================================

if (_isBreaking)
{
    if (_inventoryOpen ||
        _creativeMenuOpen ||
        IsMounted)
    {
        _isBreaking = false;
        ResetBreak();
    }
    else
    {
        _breakTimer += dt;

        if (_breakTimer >= BreakInterval)
        {
            _breakTimer -= BreakInterval;
            TryBreakBlock();
        }
    }
}

// =========================================================
// MOUNTED
// =========================================================
    if (IsMounted)
    {
        if (Input.IsActionJustPressed("crouch"))
        {
            Mob mob = _mountedMob;

            _mountedMob = null;

            if (mob != null &&
                IsInstanceValid(mob))
            {
                Vector3 mobPosition =
                    mob.GlobalPosition;

                Vector3 dismountRight =
                    mob.GlobalTransform.Basis.X;

                dismountRight.Y = 0f;

                if (dismountRight.LengthSquared() > 0.0001f)
                    dismountRight = dismountRight.Normalized();
                else
                    dismountRight = Vector3.Right;

                mob.StopRiding();

                Vector3 safePosition;

                if (TryGetSafeDismountPosition(
                        mob,
                        mobPosition,
                        dismountRight,
                        out safePosition))
                {
                    GlobalPosition = safePosition;
                }
                else
                {
                    GlobalPosition =
                        mobPosition +
                        new Vector3(0f, 0.5f, 0f);
                }
            }

            if (_playerCollision != null)
                _playerCollision.Disabled = false;

            Velocity = Vector3.Zero;
            return;
        }

        if (Input.IsActionJustPressed("jump"))
        {
            _mountedMob.JumpFromPlayer(
                JumpVelocity
            );
        }

        Vector2 mountInput =
            Input.GetVector(
                "move_left",
                "move_right",
                "move_forward",
                "move_back"
            );

        Vector3 forward =
            -_playerCamera.GlobalTransform.Basis.Z;

        Vector3 right =
            _playerCamera.GlobalTransform.Basis.X;

        forward.Y = 0f;
        right.Y = 0f;

        if (forward.LengthSquared() > 0.0001f)
            forward = forward.Normalized();

        if (right.LengthSquared() > 0.0001f)
            right = right.Normalized();

        Vector3 mountDirection =
            (
                right * mountInput.X +
                forward * -mountInput.Y
            );

        if (mountDirection.LengthSquared() > 0.0001f)
            mountDirection = mountDirection.Normalized();
        else
            mountDirection = Vector3.Zero;

        bool mountSprinting =
            Input.IsActionPressed("sprint");

        _mountedMob.DriveFromPlayer(
            mountDirection,
            mountSprinting
        );

        // Keep player attached to the mount.
        GlobalPosition =
            _mountedMob.GetMountPosition();

        Velocity = Vector3.Zero;

        return;
    }

    // =========================================================
    // NORMAL PLAYER MOVEMENT
    // =========================================================

    CheckWaterStatus();

    Vector3 velocity = Velocity;

    // ---------------------------------------------------------
    // FLYING
    // ---------------------------------------------------------

    if (_isFlying)
    {
        _isSprinting =
            Input.IsActionPressed("sprint");

        _isCrouching =
            Input.IsActionPressed("crouch");

        HandleFlyMovement(
            dt,
            ref velocity
        );

        Velocity = velocity;

        MoveAndSlide();

        return;
    }

    // ---------------------------------------------------------
    // GRAVITY
    // ---------------------------------------------------------

    if (!IsOnFloor())
    {
        if (_isInWater)
        {
            velocity.Y =
                Mathf.MoveToward(
                    velocity.Y,
                    -2f,
                    _gravity * 0.25f * dt
                );
        }
        else
        {
            velocity.Y -=
                _gravity * dt;
        }
    }
    else
    {
        _hasDoubleJumped = false;
    }

    // ---------------------------------------------------------
    // MOVEMENT INPUT
    // ---------------------------------------------------------

    Vector2 inputDir =
        Input.GetVector(
            "move_left",
            "move_right",
            "move_forward",
            "move_back"
        );

    Vector3 direction =
        (
            Transform.Basis *
            new Vector3(
                inputDir.X,
                0f,
                inputDir.Y
            )
        );

    if (direction.LengthSquared() > 0.0001f)
        direction = direction.Normalized();
    else
        direction = Vector3.Zero;

    // ---------------------------------------------------------
    // CROUCH / SPRINT
    // ---------------------------------------------------------

    _isCrouching =
        Input.IsActionPressed("crouch");

    _isSprinting =
        Input.IsActionPressed("sprint") &&
        !_isCrouching &&
        direction.LengthSquared() > 0.0001f;

    float moveSpeed;

    if (_isCrouching)
    {
        moveSpeed = CrouchSpeed;
    }
    else if (_isSprinting)
    {
        moveSpeed = SprintSpeed;
    }
    else
    {
        moveSpeed = WalkSpeed;
    }

    // ---------------------------------------------------------
    // HORIZONTAL MOVEMENT
    // ---------------------------------------------------------

    if (direction != Vector3.Zero)
    {
        velocity.X =
            direction.X * moveSpeed;

        velocity.Z =
            direction.Z * moveSpeed;
    }
    else
    {
        velocity.X =
            Mathf.MoveToward(
                velocity.X,
                0f,
                moveSpeed * 10f * dt
            );

        velocity.Z =
            Mathf.MoveToward(
                velocity.Z,
                0f,
                moveSpeed * 10f * dt
            );
    }

    // ---------------------------------------------------------
    // JUMP
    // ---------------------------------------------------------

    if (Input.IsActionJustPressed("jump"))
    {
        if (IsOnFloor())
        {
            velocity.Y =
                JumpVelocity;

            _hasDoubleJumped = false;
        }
        else if (CanDoubleJump &&
                 !_hasDoubleJumped)
        {
            velocity.Y =
                JumpVelocity;

            _hasDoubleJumped = true;
        }
        else if (_isInWater)
        {
            velocity.Y =
                JumpVelocity;
        }
    }

    // ---------------------------------------------------------
    // GLIDING
    // ---------------------------------------------------------

    _isGliding =
        CanGlide &&
        !_isInWater &&
        !IsOnFloor() &&
        Input.IsActionPressed("jump") &&
        velocity.Y < 0f;

    if (_isGliding)
    {
        velocity.Y =
            Mathf.Max(
                velocity.Y,
                -2f
            );
    }

    // ---------------------------------------------------------
    // WATER MOVEMENT
    // ---------------------------------------------------------

    if (_isInWater)
    {
        velocity.Y =
            Mathf.MoveToward(
                velocity.Y,
                0f,
                _gravity * 0.5f * dt
            );

        if (Input.IsActionPressed("jump"))
        {
            velocity.Y =
                JumpVelocity * 0.75f;
        }
    }

    Velocity = velocity;

    MoveAndSlide();
}

private bool TryGetSafeDismountPosition(
    Mob mob,
    Vector3 mobPosition,
    Vector3 dismountRight,
    out Vector3 safePosition)
{
    safePosition = mobPosition;

    if (_playerCollision == null ||
        _playerCollision.Shape == null)
    {
        safePosition =
            mobPosition +
            dismountRight * 1.5f +
            new Vector3(0f, 0.5f, 0f);

        return true;
    }

    Vector3 upOffset =
        new Vector3(0f, 0.5f, 0f);

    Vector3 forward =
        -mob.GlobalTransform.Basis.Z;

    forward.Y = 0f;

    if (forward.LengthSquared() > 0.0001f)
        forward = forward.Normalized();
    else
        forward = Vector3.Forward;

    // Try several positions in order.
    Vector3[] candidates =
    {
        // Normal dismount.
        mobPosition +
            dismountRight * 1.5f +
            upOffset,

        // Other side of the mob.
        mobPosition -
            dismountRight * 1.5f +
            upOffset,

        // Slightly farther right.
        mobPosition +
            dismountRight * 2.0f +
            upOffset,

        // Slightly farther left.
        mobPosition -
            dismountRight * 2.0f +
            upOffset,

        // Front.
        mobPosition +
            forward * 1.5f +
            upOffset,

        // Behind.
        mobPosition -
            forward * 1.5f +
            upOffset
    };

    PhysicsDirectSpaceState3D spaceState =
        GetWorld3D().DirectSpaceState;

    foreach (Vector3 candidate in candidates)
    {
        PhysicsShapeQueryParameters3D query =
            new PhysicsShapeQueryParameters3D();

        query.Shape =
            _playerCollision.Shape;

        query.Transform =
            new Transform3D(
                GlobalTransform.Basis,
                candidate
            ) *
            _playerCollision.Transform;

        query.CollisionMask =
            CollisionMask;

        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        // Ignore the player itself.
        query.Exclude.Add(
            GetRid()
        );

        // Ignore the mount itself.
        if (mob != null &&
            IsInstanceValid(mob))
        {
            query.Exclude.Add(
                mob.GetRid()
            );
        }

        var hits =
            spaceState.IntersectShape(
                query,
                1
            );

        if (hits.Count == 0)
        {
            safePosition = candidate;
            return true;
        }
    }

    return false;
}
// =========================================================================
// MOUNTING
// =========================================================================

private Mob GetMobUnderCrosshair()
{
    if (_rayCast == null ||
        !_rayCast.IsColliding())
    {
        return null;
    }

    Node node =
        _rayCast.GetCollider() as Node;

    while (node != null)
    {
        if (node is Mob mob)
            return mob;

        node = node.GetParent();
    }

    return null;
}
    // =========================================================================
    // INPUT
    // =========================================================================

   
public override void _UnhandledInput(InputEvent @event)
{
    // Stop continuous dropping when Q is released.
    if (@event is InputEventKey qUp &&
        !qUp.Pressed &&
        qUp.Keycode == Key.Q)
    {
        _isDropping = false;
    }

    // =========================================================
    // MOUSE INPUT
    // =========================================================

    if (@event is InputEventMouseButton mb)
    {
        // Middle mouse: pick block.
        if (mb.ButtonIndex == MouseButton.Middle && mb.Pressed)
        {
            PickBlock();
            return;
        }

        // Mouse wheel: cycle hotbar.
        if (!_inventoryOpen &&
            !_chatOpen &&
            !_creativeMenuOpen)
        {
            if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                SelectHotbarSlot(
                    (_selectedSlot + 1) % HotbarSize
                );
                return;
            }

            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            {
                SelectHotbarSlot(
                    (_selectedSlot - 1 + HotbarSize) % HotbarSize
                );
                return;
            }
        }

        // Normal gameplay mouse controls.
        if (!_inventoryOpen &&
            !_chatOpen &&
            !_creativeMenuOpen)
        {
            // Left mouse: break blocks.
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _isBreaking = mb.Pressed;

                if (mb.Pressed)
                {
                    TryBreakBlock();
                    _breakTimer = 0f;
                }
                else
                {
                    ResetBreak();
                }
            }

            // Right mouse: mount, interact, or place.
            if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                // Mount mob with empty hand.
                if (_heldSlot.IsEmpty)
                {
                    Mob mob = GetMobUnderCrosshair();

                    if (mob != null && mob.StartRiding(this))
                    {
                        _mountedMob = mob;

                        if (_playerCollision != null)
                            _playerCollision.Disabled = true;

                        _isPlacing = false;
                        return;
                    }
                }

                // Interact with blocks.
                _isPlacing = true;

                if (TryOpenCrate())
                {
                    _isPlacing = false;
                }
                else if (TryOpenCraftingTable())
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
    }

    // =========================================================
    // KEYBOARD INPUT
    // =========================================================

    if (@event is InputEventKey key && key.Pressed)
    {
        // Chat escape.
        if (key.Keycode == Key.Escape && _chatOpen)
        {
            CloseChat();
            return;
        }

        if (_chatOpen)
            return;

        // Prevent held keys from repeatedly toggling menus.
        if (key.Echo)
            return;

        
// =====================================================
// ESCAPE / PAUSE
// =====================================================
if (key.Keycode == Key.Escape)
{
    // If pause menu is already open, close it.
    if (_pauseMenu != null && _pauseMenu.IsOpen)
    {
        _pauseMenu.Close();
        return;
    }

    // Close crate inventory first.
    if (_containerOpen)
    {
        CloseContainerWindow();
        return;
    }

    // Otherwise close normal inventory.
    if (_inventoryOpen)
    {
        ToggleInventory();
        return;
    }

    // Otherwise close creative menu.
    if (_creativeMenuOpen)
    {
        ToggleCreativeMenu();
        return;
    }

    // Nothing else is open, so open pause menu.
    if (_pauseMenu != null)
        _pauseMenu.Open();

    return;
}


        // =====================================================
        // HOTBAR
        // =====================================================

        if (key.Keycode >= Key.Key1 &&
            key.Keycode <= Key.Key9)
        {
            SelectHotbarSlot(
                (int)key.Keycode - (int)Key.Key1
            );
        }
        else if (key.Keycode == Key.Key0)
        {
            SelectHotbarSlot(9);
        }
        else if (key.Keycode == Key.Minus)
        {
            SelectHotbarSlot(10);
        }
        else if (key.Keycode == Key.Equal)
        {
            SelectHotbarSlot(11);
        }

        // =====================================================
        // INVENTORY: E OR TAB
        // =====================================================

        if ((key.Keycode == Key.E ||
             key.Keycode == Key.Tab) &&
            !_creativeMenuOpen)
        {
            if (_containerOpen)
                CloseContainerWindow();
            else
                ToggleInventory();

            return;
        }

        // =====================================================
        // CREATIVE MENU
        // =====================================================

        if (key.Keycode == Key.V &&
            !_inventoryOpen &&
            (_creativeMenuOpen ||
             GameModeManager.Instance?.IsCreate == true))
        {
            ToggleCreativeMenu();
        }

        // =====================================================
        // DROP ITEM
        // =====================================================

        if (key.Keycode == Key.Q && !_isDropping)
        {
            _isDropping = true;
            _dropTimer = 0f;
            DropOneItem();
        }

        // =====================================================
        // GAMEMODE
        // =====================================================

        if (key.Keycode == Key.F4)
        {
            if (GameModeManager.Instance == null)
            {
                ShowFeedback("GameModeManager not loaded.");
            }
            else
            {
                GameModeManager.Instance.CycleNext();

                ShowFeedback(
                    $"Gamemode: {GameModeManager.Instance.Current}"
                );
            }
        }

        // =====================================================
        // OPEN CHAT
        // =====================================================

        if (key.Keycode == Key.T &&
            !_chatOpen &&
            !_inventoryOpen &&
            !_pauseMenu.IsOpen)
        {
            OpenChat();
        }

        // =====================================================
        // JUMP / FLY
        // =====================================================

        if (key.Keycode == Key.Space &&
            GameModeManager.Instance?.IsCreate == true)
        {
            double now = Time.GetTicksMsec() / 1000.0;

            if (now - _lastJumpTime < DoubleJumpWindow)
                ToggleFly();

            _lastJumpTime = now;
        }

        // =====================================================
        // HUD
        // =====================================================

        if (key.Keycode == Key.F1)
        {
            _hudVisible = !_hudVisible;

            if (_hotbarLayer != null)
                _hotbarLayer.Visible = _hudVisible && !_inventoryOpen;

            if (_crosshairLayer != null)
                _crosshairLayer.Visible = _hudVisible;

            if (_statsHudLayer != null)
                _statsHudLayer.Visible = _hudVisible && !_inventoryOpen;
        }

        // =====================================================
        // SCREENSHOT
        // =====================================================

        if (key.Keycode == Key.F2)
        {
            var img = GetViewport().GetTexture().GetImage();

            string p =
                $"user://screenshot_{Time.GetDatetimeStringFromSystem().Replace(":", "-")}.png";

            img.SavePng(p);
            GD.Print($"Screenshot saved: {p}");
        }

        // =====================================================
        // CHUNK BORDERS
        // =====================================================

        if (key.Keycode == Key.F3)
        {
            _showChunkBorders = !_showChunkBorders;
            ToggleChunkBorders();
        }

        // =====================================================
        // SAVE WORLD
        // =====================================================

        if (key.Keycode == Key.F5)
        {
            var cm = GetTree().Root.FindChild(
                "ChunkManager",
                true,
                false
            ) as ChunkManager;

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


    private void SelectHotbarSlot(int slot)
    {
        if (slot < 0 ||
            slot >= HotbarSize)
            return;

        _selectedSlot =
            slot;

        var s =
            _inventory.Slots[
                MainInvSize + slot
            ];

        _selectedBlockId =
            s.IsEmpty
                ? ""
                : s.ItemId;

        UpdateHotbarSelectionBorder();
    }

    private void ToggleInventory()
    {
        _inventoryOpen =
            !_inventoryOpen;

        if (_stationTabBar != null)
            _stationTabBar.Visible = _inventoryOpen;

        _inventoryScreen.Visible =
            _inventoryOpen;

        if (_inventoryOpen)
        {
            _hotbarLayer.Visible = false;

            if (_statsHudLayer != null)
                _statsHudLayer.Visible = false;

            if (_craftingPanel != null)
            {
                _craftingPanel.Visible = true;
                UpdateCraftingProximity();
            }

            if (_equipmentPanel != null)
                _equipmentPanel.Visible = true;

            RefreshAllSlotVisuals();

            Input.MouseMode =
                Input.MouseModeEnum.Visible;
        }
        else
        {
            if (!_heldSlot.IsEmpty)
            {
                bool placed = false;

                if (_heldFromSlot >= 0 &&
                    _heldFromSlot < TotalSlots)
                {
                    var orig =
                        _inventory.Slots[
                            _heldFromSlot
                        ];

                    if (orig.IsEmpty)
                    {
                        orig.ItemId =
                            _heldSlot.ItemId;

                        orig.Count =
                            _heldSlot.Count;

                        placed = true;
                    }
                    else if (
                        orig.ItemId ==
                        _heldSlot.ItemId &&
                        orig.Count <
                        _inventory.MaxStackSize)
                    {
                        int fit =
                            Mathf.Min(
                                _inventory.MaxStackSize -
                                orig.Count,
                                _heldSlot.Count
                            );

                        orig.Count += fit;
                        _heldSlot.Count -= fit;

                        placed =
                            _heldSlot.Count <= 0;
                    }
                }

                if (!placed &&
                    _heldSlot.Count > 0)
                {
                    AddItemToInventory(
                        _heldSlot.ItemId,
                        _heldSlot.Count
                    );
                }

                _heldSlot.Clear();
                UpdateCursorVisual();
            }

            _craftingPanel?.OnInventoryClose();

            if (_craftingPanel != null)
                _craftingPanel.Visible = false;

            if (_equipmentPanel != null)
                _equipmentPanel.Visible = false;

            _toolBenchPanel?.ReturnSocketsToInventory();
            KilnManager.Instance?.SaveKilns();

            if (_toolBenchPanel != null)
                _toolBenchPanel.Visible = false;

            if (_kilnPanel != null)
                _kilnPanel.Visible = false;

            if (_stationTabBar != null)
                _stationTabBar.Visible = false;

            _activeStation = "crafter";

            EndDrag();

            _hotbarLayer.Visible =
                _hudVisible;

            if (_statsHudLayer != null)
                _statsHudLayer.Visible =
                    _hudVisible;

            Input.MouseMode =
                Input.MouseModeEnum.Captured;
        }
    }

    private void CheckWaterStatus()
    {
        var cm =
            GetTree().Root.FindChild(
                "ChunkManager",
                true,
                false
            ) as ChunkManager;

        if (cm == null)
        {
            _isInWater = false;
            return;
        }

        _isInWater =
            IsBlockWaterAt(
                cm,
                GlobalPosition +
                new Vector3(0, -0.9f, 0)
            );
    }

    private bool IsBlockWaterAt(
        ChunkManager cm,
        Vector3 worldPos)
    {
        Chunk chunk =
            cm.GetChunk(
                cm.WorldToChunk(worldPos)
            );

        if (chunk == null)
            return false;

        Vector3 lp =
            worldPos -
            chunk.GlobalPosition;

        return chunk.GetBlock(
            Mathf.FloorToInt(lp.X),
            Mathf.FloorToInt(lp.Y),
            Mathf.FloorToInt(lp.Z)
        ).BlockId == "water";
    }

    // =========================================================================
    // CHUNK BORDERS
    // =========================================================================

    private void ToggleChunkBorders()
    {
        foreach (var m in _chunkBorderMeshes)
            m.QueueFree();

        _chunkBorderMeshes.Clear();

        if (!_showChunkBorders)
            return;

        var cm =
            GetTree().Root.FindChild(
                "ChunkManager",
                true,
                false
            ) as ChunkManager;

        if (cm == null)
            return;

        var mat =
            new StandardMaterial3D();

        mat.AlbedoColor =
            new Color(1, 1, 0);

        mat.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.Unshaded;

        foreach (var cp in
                 cm.GetLoadedChunkPositions())
        {
            float x0 = cp.X * Chunk.SIZE;
            float x1 = x0 + Chunk.SIZE;

            float y0 = cp.Y * Chunk.HEIGHT;
            float y1 = y0 + Chunk.HEIGHT;

            float z0 = cp.Z * Chunk.SIZE;
            float z1 = z0 + Chunk.SIZE;

            var bst =
                new SurfaceTool();

            bst.Begin(
                Mesh.PrimitiveType.Lines
            );

            bst.AddVertex(new(x0,y0,z0));
            bst.AddVertex(new(x1,y0,z0));

            bst.AddVertex(new(x1,y0,z0));
            bst.AddVertex(new(x1,y0,z1));

            bst.AddVertex(new(x1,y0,z1));
            bst.AddVertex(new(x0,y0,z1));

            bst.AddVertex(new(x0,y0,z1));
            bst.AddVertex(new(x0,y0,z0));

            bst.AddVertex(new(x0,y1,z0));
            bst.AddVertex(new(x1,y1,z0));

            bst.AddVertex(new(x1,y1,z0));
            bst.AddVertex(new(x1,y1,z1));

            bst.AddVertex(new(x1,y1,z1));
            bst.AddVertex(new(x0,y1,z1));

            bst.AddVertex(new(x0,y1,z1));
            bst.AddVertex(new(x0,y1,z0));

            bst.AddVertex(new(x0,y0,z0));
            bst.AddVertex(new(x0,y1,z0));

            bst.AddVertex(new(x1,y0,z0));
            bst.AddVertex(new(x1,y1,z0));

            bst.AddVertex(new(x1,y0,z1));
            bst.AddVertex(new(x1,y1,z1));

            bst.AddVertex(new(x0,y0,z1));
            bst.AddVertex(new(x0,y1,z1));

            var brd =
                new MeshInstance3D
                {
                    Mesh = bst.Commit(),
                    MaterialOverride = mat
                };

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

        foreach (var m in _chunkBorderMeshes)
            m?.QueueFree();

        _chunkBorderMeshes.Clear();
    }

    // =========================================================================
    // CHAT BAR + COMMANDS
    // =========================================================================

    private void BuildChatBar()
    {
        _chatLayer =
            new CanvasLayer();

        _chatLayer.Layer = 15;
        _chatLayer.ProcessMode =
            ProcessModeEnum.Always;

        GetTree().Root.CallDeferred(
            "add_child",
            _chatLayer
        );

        var bg =
            new ColorRect();

        bg.Color =
            new Color(0f, 0f, 0f, 0.55f);

        bg.AnchorLeft = 0f;
        bg.AnchorRight = 0.6f;
        bg.AnchorTop = 1f;
        bg.AnchorBottom = 1f;

        bg.OffsetTop = -44f;
        bg.OffsetBottom = 0f;
        bg.Visible = false;

        _chatLayer.CallDeferred(
            "add_child",
            bg
        );

        _chatInput =
            new LineEdit();

        _chatInput.PlaceholderText =
            "Type a command... (e.g. /gamemode creative)";

        _chatInput.AnchorLeft = 0f;
        _chatInput.AnchorRight = 0.6f;
        _chatInput.AnchorTop = 1f;
        _chatInput.AnchorBottom = 1f;

        _chatInput.OffsetTop = -40f;
        _chatInput.OffsetBottom = -4f;
        _chatInput.OffsetLeft = 8f;
        _chatInput.OffsetRight = -8f;

        _chatInput.Visible = false;

        _chatInput.TextSubmitted +=
            OnChatSubmit;

        _chatInput.GuiInput +=
            OnChatInputGuiInput;

        _chatLayer.CallDeferred(
            "add_child",
            _chatInput
        );

        _chatFeedback =
            new Label();

        _chatFeedback.AnchorLeft = 0f;
        _chatFeedback.AnchorRight = 0.6f;
        _chatFeedback.AnchorTop = 1f;
        _chatFeedback.AnchorBottom = 1f;

        _chatFeedback.OffsetTop = -70f;
        _chatFeedback.OffsetBottom = -46f;
        _chatFeedback.OffsetLeft = 8f;

        _chatFeedback.AddThemeColorOverride(
            "font_color",
            new Color(1f, 1f, 0.6f)
        );

        _chatFeedback.AddThemeFontSizeOverride(
            "font_size",
            13
        );

        _chatFeedback.Visible = false;

        _chatLayer.CallDeferred(
            "add_child",
            _chatFeedback
        );
    }

    private void OnChatInputGuiInput(
        InputEvent @event)
    {
        if (!_chatOpen)
            return;

        if (@event is not InputEventKey key)
            return;

        if (!key.Pressed ||
            key.Echo)
            return;

        if (key.Keycode == Key.Up)
        {
            NavigateChatHistory(-1);
            _chatInput.AcceptEvent();
            return;
        }

        if (key.Keycode == Key.Down)
        {
            NavigateChatHistory(1);
            _chatInput.AcceptEvent();
            return;
        }
    }

    private void OpenChat()
    {
        _chatOpen = true;
        _chatInput.Visible = true;
        _chatInput.Clear();

        _chatHistoryIndex =
            _chatHistory.Count;

        _chatInput.GrabFocus();
        Input.MouseMode =
            Input.MouseModeEnum.Visible;
    }

    private void CloseChat()
    {
        _chatOpen = false;
        _chatInput.Visible = false;
        _chatInput.ReleaseFocus();

        if (!_inventoryOpen &&
            !_pauseMenu.IsOpen)
        {
            Input.MouseMode =
                Input.MouseModeEnum.Captured;
        }
    }

    private void OnChatSubmit(string text)
    {
        CloseChat();

        text = text.Trim();

        if (string.IsNullOrEmpty(text))
            return;

        _chatHistory.Add(text);

        if (_chatHistory.Count > MaxChatHistory)
            _chatHistory.RemoveAt(0);

        _chatHistoryIndex =
            _chatHistory.Count;

        ParseCommand(text);
    }

    private void NavigateChatHistory(int direction)
    {
        if (!_chatOpen ||
            _chatInput == null)
            return;

        if (_chatHistory.Count == 0)
            return;

        _chatHistoryIndex +=
            direction;

        if (_chatHistoryIndex < 0)
            _chatHistoryIndex = 0;

        if (_chatHistoryIndex >= _chatHistory.Count)
        {
            _chatHistoryIndex =
                _chatHistory.Count;

            _chatInput.Text = "";
            _chatInput.CaretColumn = 0;

            return;
        }

        _chatInput.Text =
            _chatHistory[
                _chatHistoryIndex
            ];

        _chatInput.CaretColumn =
            _chatInput.Text.Length;
    }

    private void ShowFeedback(string msg)
    {
        _chatFeedback.Text = msg;
        _chatFeedback.Visible = true;
        _feedbackTimer =
            FeedbackDuration;
    }

    private void ParseCommand(string input)
    {
        if (!input.StartsWith("/"))
            return;

        string[] parts =
            input.Substring(1).Split(' ');

        string cmd =
            parts[0].ToLower();

        switch (cmd)
        {
            case "admin":
                if (parts.Length < 2)
                {
                    ShowFeedback(
                        "Usage: /admin <password>"
                    );

                    return;
                }

                if (SettingsManager.Instance.TryUnlockAdmin(
                        parts[1]))
                {
                    ShowFeedback(
                        "Admin access granted."
                    );
                }
                else
                {
                    ShowFeedback(
                        "Incorrect password."
                    );
                }

                break;

            // =================================================================
            // /gamemode
            // =================================================================

            case "gamemode":
            case "gm":
                if (GameModeManager.Instance == null)
                {
                    ShowFeedback(
                        "GameModeManager not loaded."
                    );

                    return;
                }

                if (!SettingsManager.Instance.IsAdmin)
                {
                    ShowFeedback(
                        "You need admin to use this command."
                    );

                    return;
                }

                if (parts.Length < 2)
                {
                    ShowFeedback(
                        "Usage: /gamemode <create|survival|story>"
                    );

                    return;
                }

                switch (parts[1].ToLower())
                {
                    case "create":
                    case "creative":
                    case "c":
                    case "1":
                        GameModeManager.Instance.SetMode(
                            GameModeManager.GameMode.Create
                        );

                        ShowFeedback(
                            "Switched to Create mode."
                        );

                        break;

                    case "survival":
                    case "s":
                    case "0":
                        GameModeManager.Instance.SetMode(
                            GameModeManager.GameMode.Survival
                        );

                        ShowFeedback(
                            "Switched to Survival mode."
                        );

                        break;

                    case "story":
                    case "st":
                    case "2":
                        GameModeManager.Instance.SetMode(
                            GameModeManager.GameMode.Story
                        );

                        ShowFeedback(
                            "Switched to Story mode."
                        );

                        break;

                    default:
                        ShowFeedback(
                            $"Unknown gamemode: {parts[1]}"
                        );

                        break;
                }

                break;

            // =================================================================
            // /spawn
            // =================================================================

            case "spawn":
{
    if (parts.Length < 2)
    {
        ShowFeedback(
            "Usage: /spawn <mob> [male/m/female/f]"
        );

        return;
    }

    if (_rayCast == null ||
        !_rayCast.IsColliding())
    {
        ShowFeedback(
            "Look at a block or surface to spawn the mob."
        );

        return;
    }

    string mobId =
        parts[1].Trim();

    string gender =
        parts.Length >= 3
            ? parts[2].Trim()
            : "";

    if (parts.Length > 3)
    {
        ShowFeedback(
            "Usage: /spawn <mob> [male/m/female/f]"
        );

        return;
    }

    if (!string.IsNullOrEmpty(gender) &&
        !gender.Equals(
            "male",
            StringComparison.OrdinalIgnoreCase) &&
        !gender.Equals(
            "m",
            StringComparison.OrdinalIgnoreCase) &&
        !gender.Equals(
            "female",
            StringComparison.OrdinalIgnoreCase) &&
        !gender.Equals(
            "f",
            StringComparison.OrdinalIgnoreCase))
    {
        ShowFeedback(
            "Gender must be male/m or female/f."
        );

        return;
    }

    MobSpawner spawner =
        GetTree()
            .GetFirstNodeInGroup("mob_spawner")
            as MobSpawner;

    if (spawner == null)
    {
        spawner =
            GetTree()
                .Root
                .FindChild(
                    "MobSpawner",
                    true,
                    false
                ) as MobSpawner;
    }

    if (spawner == null)
    {
        ShowFeedback(
            "MobSpawner not found."
        );

        return;
    }

    Vector3 spawnPosition =
        _rayCast.GetCollisionPoint();

    spawnPosition +=
        _rayCast.GetCollisionNormal() * 0.1f;

    bool spawned =
        spawner.TrySpawnMob(
            mobId,
            gender,
            spawnPosition
        );

    if (spawned)
    {
        string genderText =
            string.IsNullOrEmpty(gender)
                ? "random gender"
                : gender;

        ShowFeedback(
            $"Spawned {mobId} ({genderText})."
        );
    }
    else
    {
        ShowFeedback(
            $"Could not find or spawn mob: {mobId}"
        );
    }

    break;
}

            // =================================================================
            // /kill
            // =================================================================

            case "kill":
                if (!SettingsManager.Instance.IsAdmin)
                {
                    ShowFeedback(
                        "You need admin to use this command."
                    );

                    return;
                }

                if (parts.Length < 2)
                {
                    ShowFeedback(
                        "Usage: /kill all"
                    );

                    return;
                }

                if (parts[1].ToLower() == "all")
                {
                    int killed = 0;

                    foreach (Node node in
                             GetTree().GetNodesInGroup("mobs"))
                    {
                        if (node is Mob mob)
                        {
                            mob.TakeDamage(
                                999999f
                            );

                            killed++;
                        }
                    }

                    ShowFeedback(
                        $"Killed {killed} mobs."
                    );
                }
                else
                {
                    ShowFeedback(
                        $"Unknown kill target: {parts[1]}"
                    );
                }

                break;

            // =================================================================
            // /fly
            // =================================================================

            case "fly":
                if (GameModeManager.Instance == null)
                {
                    ShowFeedback(
                        "GameModeManager not loaded."
                    );

                    return;
                }

                if (!SettingsManager.Instance.IsAdmin &&
                    !GameModeManager.Instance.IsCreate)
                {
                    ShowFeedback(
                        "Fly is only available in Create mode."
                    );

                    return;
                }

                ToggleFly();

                ShowFeedback(
                    _isFlying
                        ? "Flying: ON"
                        : "Flying: OFF"
                );

                break;

            default:
                ShowFeedback(
                    $"Unknown command: /{cmd}"
                );

                break;
        }
    }

    // =========================================================================
    // GAMEMODE + FLY
    // =========================================================================

    private void OnGameModeChanged(
        GameModeManager.GameMode mode)
    {
        if (mode !=
            GameModeManager.GameMode.Create &&
            _isFlying)
        {
            SetFlying(false);
        }
    }

    private void ToggleFly()
    {
        SetFlying(!_isFlying);
    }

    private void SetFlying(bool fly)
    {
        _isFlying = fly;

        if (!fly)
        {
            var v = Velocity;
            v.Y = 0f;
            Velocity = v;
        }
    }

    private void HandleFlyMovement(
        float dt,
        ref Vector3 velocity)
    {
        Vector2 inputDir =
            Input.GetVector(
                "move_left",
                "move_right",
                "move_forward",
                "move_back"
            );

        Vector3 direction =
            (
                Transform.Basis *
                new Vector3(
                    inputDir.X,
                    0,
                    inputDir.Y
                )
            ).Normalized();

        float speed =
            _isSprinting
                ? _flySpeed * 2f
                : _flySpeed;

        velocity.X =
            direction.X * speed;

        velocity.Z =
            direction.Z * speed;

        if (Input.IsActionPressed("jump"))
        {
            velocity.Y =
                _flyVertSpeed;
        }
        else if (Input.IsActionPressed("crouch"))
        {
            velocity.Y =
                -_flyVertSpeed;
        }
        else
        {
            velocity.Y =
                Mathf.MoveToward(
                    velocity.Y,
                    0f,
                    _flyVertSpeed *
                    dt *
                    10f
                );
        }
    }

    // =========================================================================
    // DAMAGE
    // =========================================================================

    public void TakeDamage(float amount)
    {
        if (_stats == null ||
            _stats.IsDead)
        {
            return;
        }

        if (amount <= 0f)
            return;

        _stats.TakeDamage(amount);
    }

    public PlayerStats GetStats()
        => _stats;

    public PlayerCamera GetPlayerCamera()
        => _playerCamera;

    public void SaveInventoryFromPauseMenu(
        ChunkManager cm)
    {
        cm.SaveInventory(
            _inventory
        );

        cm.SavePlayerPosition(
            GlobalPosition,
            Rotation.Y,
            _playerCamera?.GetPitch() ?? 0f
        );
    }

    public void ApplyGearMovement(
        ItemResource gear)
    {
        if (gear.GrantsDoubleJump)
            CanDoubleJump = true;

        if (gear.GrantsWallClimb)
            CanWallClimb = true;

        if (gear.GrantsGliding)
            CanGlide = true;

        if (gear.GrantsGrapple)
            CanGrapple = true;

        WalkSpeed +=
            gear.BonusMovementSpeed;
    }

    public void RemoveGearMovement(
        ItemResource gear)
    {
        if (gear.GrantsDoubleJump)
            CanDoubleJump = false;

        if (gear.GrantsWallClimb)
            CanWallClimb = false;

        if (gear.GrantsGliding)
            CanGlide = false;

        if (gear.GrantsGrapple)
            CanGrapple = false;

        WalkSpeed -=
            gear.BonusMovementSpeed;
    }
}