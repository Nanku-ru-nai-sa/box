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
    private Node3D _playerModelRoot;
    private Node3D _playerModelParts;
    private Node3D _bonesModel;
    private Node3D _playerHeadPivot;
    private Node3D _playerUpperBodyPivot;
    private Vector3 _upperBodyBasePosition;
    private Vector3 _upperBodyBaseRotation;
    private float _sneakBlend = 0f;
    private CapsuleShape3D _playerCapsule;
    private float _standingCapsuleHeight = 1.8f;
    private Vector3 _standingCollisionPosition = Vector3.Zero;
    private float _playerHeadHalfDepth = 0.25f;
    private Vector3 _defaultPlayerCameraPosition = new Vector3(0f, 0.6f, 0f);
    private Node3D _leftBicep;
    private Node3D _rightBicep;
    private Node3D _leftHand;
    private Node3D _rightHand;
    // 0 = first-person, 1 = third-person, 2 = front-facing third-person.
    private int _cameraMode = 0;
    private bool _thirdPersonView = false;
    private float _cameraZoomDistance = 4.5f;
    private const float CameraZoomMin = 1.5f;
    private const float CameraZoomMax = 8.0f;
    private const float CameraZoomStep = 0.5f;
    private float _armAnimationTime = 0f;
    private readonly List<(Node3D Node, Vector3 RestRotation)> _leftArmParts = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _rightArmParts = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _leftForearmParts = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _rightForearmParts = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _leftLegParts = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _rightLegParts = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _leftLowerLegParts = new();
    private readonly List<(Node3D Node, Vector3 RestRotation)> _rightLowerLegParts = new();
    private RayCast3D _rayCast;
    private RandomNumberGenerator _dropRng = new RandomNumberGenerator();
    private bool _isSprinting = false;
    private bool _isCrouching = false;
    private bool _isCrouchJumping = false;
    private float _crouchChargeTime = 0f;
    private const float CrouchJumpChargeDuration = 0.15f;
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
    private readonly Vector3I[] _stationSlotPositions = new Vector3I[4];
    private string _activeStation = "default";
    private Vector3I _activeStationPosition = Vector3I.Zero;
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
        _defaultPlayerCameraPosition = _playerCamera?.Position ?? new Vector3(0f, 0.6f, 0f);
        _rayCast = GetNode<RayCast3D>("PlayerCamera/Camera3D/RayCast3D");
        _rayCast.AddException(this);
        _dropRng.Randomize();
        _playerCollision = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        _playerCapsule = _playerCollision?.Shape as CapsuleShape3D;
        if (_playerCapsule != null)
        {
            _standingCapsuleHeight = _playerCapsule.Height;
            _standingCollisionPosition = _playerCollision.Position;
        }

        SetupPlayerModel();

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
    // PLAYER MODEL AND CAMERA MODES
    // =========================================================================

    private void SetupPlayerModel()
    {
        _playerModelRoot = GetNodeOrNull<Node3D>("PlayerModel");

        if (_playerModelRoot == null)
        {
            GD.PrintErr("Player model scene is missing.");
            return;
        }

        _playerModelParts =
            _playerModelRoot.FindChild("player", true, false) as Node3D;

        _bonesModel =
            _playerModelRoot.FindChild("bones", true, false) as Node3D;

        if (_playerModelParts == null)
        {
            GD.PrintErr("Player model is missing its player mesh group.");
            return;
        }

        // Keep references to the visible arm meshes for first-person view.
        _leftBicep =
            _playerModelParts.FindChild("left bicep", true, false) as Node3D;
        _rightBicep =
            _playerModelParts.FindChild("right bicep", true, false) as Node3D;
        _leftHand =
            _playerModelParts.FindChild("left hand", true, false) as Node3D;
        _rightHand =
            _playerModelParts.FindChild("right hand", true, false) as Node3D;

        // The skeleton is a separate display group. Hide the whole group so
        // none of its bone meshes can accidentally become visible.
        if (_bonesModel != null)
            _bonesModel.Visible = false;

        List<Node3D> leftUpperLegMeshes = new();
        List<Node3D> rightUpperLegMeshes = new();
        Node3D leftLowerLegMesh = null;
        Node3D rightLowerLegMesh = null;

        foreach (Node node in _playerModelParts.FindChildren("*", "Node3D", true, false))
        {
            if (node is not Node3D part)
                continue;

            string partName = part.Name.ToString().ToLowerInvariant();

            if (partName.Contains("left") && partName.Contains("thigh"))
                leftUpperLegMeshes.Add(part);
            else if (partName.Contains("right") && partName.Contains("thigh"))
                rightUpperLegMeshes.Add(part);
            else if (partName == "lower left leg")
                leftLowerLegMesh = part;
            else if (partName == "lower right leg")
                rightLowerLegMesh = part;
        }

        BuildPlayerHeadPivot();

        // Build a parent/child joint hierarchy so lower limbs follow the
        // upper limbs while still bending independently at elbows and knees.
        Node3D leftShoulder = null;
        Node3D rightShoulder = null;
        Node3D leftHip = null;
        Node3D rightHip = null;

        if (_leftBicep != null)
            leftShoulder = CreateLimbPivot(
                "LeftShoulderPivot", new Vector3(-0.25f, 1.5f, 0f),
                _leftArmParts, null, _leftBicep);
        if (_rightBicep != null)
            rightShoulder = CreateLimbPivot(
                "RightShoulderPivot", new Vector3(0.25f, 1.5f, 0f),
                _rightArmParts, null, _rightBicep);

        if (_leftHand != null && leftShoulder != null)
            CreateLimbPivot(
                "LeftElbowPivot", new Vector3(-0.34375f, 1.125f, 0f),
                _leftForearmParts, leftShoulder, _leftHand);
        if (_rightHand != null && rightShoulder != null)
            CreateLimbPivot(
                "RightElbowPivot", new Vector3(0.34375f, 1.125f, 0f),
                _rightForearmParts, rightShoulder, _rightHand);

        if (leftUpperLegMeshes.Count > 0)
            leftHip = CreateLimbPivot(
                "LeftHipPivot", new Vector3(-0.125f, 0.75f, 0f),
                _leftLegParts, null, leftUpperLegMeshes.ToArray());
        if (rightUpperLegMeshes.Count > 0)
            rightHip = CreateLimbPivot(
                "RightHipPivot", new Vector3(0.125f, 0.75f, 0f),
                _rightLegParts, null, rightUpperLegMeshes.ToArray());

        if (leftLowerLegMesh != null && leftHip != null)
            CreateLimbPivot(
                "LeftKneePivot", new Vector3(-0.125f, 0.375f, 0f),
                _leftLowerLegParts, leftHip, leftLowerLegMesh);
        if (rightLowerLegMesh != null && rightHip != null)
            CreateLimbPivot(
                "RightKneePivot", new Vector3(0.125f, 0.375f, 0f),
                _rightLowerLegParts, rightHip, rightLowerLegMesh);

        // Group the torso, head and shoulders so sneaking can lower the
        // upper body without moving the feet into the ground or scaling meshes.
        Node3D torsoMesh = _playerModelParts.FindChild("body", true, false) as Node3D;
        if (torsoMesh != null)
        {
            Vector3 torsoCenter = Vector3.Zero;
            if (torsoMesh is MeshInstance3D torsoVisual && torsoVisual.Mesh != null)
                torsoCenter = _playerModelParts.ToLocal(
                    torsoMesh.ToGlobal(torsoVisual.GetAabb().GetCenter()));

            _playerUpperBodyPivot = new Node3D { Name = "PlayerUpperBodyPivot" };
            _playerModelParts.AddChild(_playerUpperBodyPivot);
            _playerUpperBodyPivot.Position = torsoCenter;

            torsoMesh.Reparent(_playerUpperBodyPivot, true);
            if (_playerHeadPivot != null)
                _playerHeadPivot.Reparent(_playerUpperBodyPivot, true);
            if (leftShoulder != null)
                leftShoulder.Reparent(_playerUpperBodyPivot, true);
            if (rightShoulder != null)
                rightShoulder.Reparent(_playerUpperBodyPivot, true);
            // Keep the thigh roots attached to the pelvis as the whole
            // upper-body group lowers and leans into the sneak pose.
            if (leftHip != null)
                leftHip.Reparent(_playerUpperBodyPivot, true);
            if (rightHip != null)
                rightHip.Reparent(_playerUpperBodyPivot, true);

            _upperBodyBasePosition = _playerUpperBodyPivot.Position;
            _upperBodyBaseRotation = _playerUpperBodyPivot.Rotation;
        }

        // The imported model is authored with its feet at local Y=0,
        // while the player's capsule is centred on the CharacterBody.
        _playerModelRoot.Position = new Vector3(0f, -0.9f, 0f);

        SetCameraMode(0);
    }

    private void BuildPlayerHeadPivot()
    {
        Node3D headNode = _playerModelParts.FindChild("head", true, false) as Node3D;
        if (headNode == null)
            return;

        // Eyes and pupils are separate imported meshes, so attach all five
        // pieces to the same pivot to keep the face together while looking.
        Node3D[] faceParts =
        {
            headNode,
            _playerModelParts.FindChild("left eye", true, false) as Node3D,
            _playerModelParts.FindChild("left pupil", true, false) as Node3D,
            _playerModelParts.FindChild("right eye", true, false) as Node3D,
            _playerModelParts.FindChild("right pupil", true, false) as Node3D
        };

        Vector3 headCenter = Vector3.Zero;
        if (headNode is MeshInstance3D headMesh && headMesh.Mesh != null)
        {
            headCenter = headMesh.GetAabb().GetCenter();
            _playerHeadHalfDepth = headMesh.GetAabb().Size.Z * 0.5f;
        }

        // Place the pivot at the head centre, then preserve each imported
        // mesh's world transform as it is parented beneath the pivot.
        Vector3 headCenterInModel = _playerModelParts.ToLocal(headNode.ToGlobal(headCenter));
        _playerHeadPivot = new Node3D { Name = "PlayerHeadPivot" };
        _playerModelParts.AddChild(_playerHeadPivot);
        _playerHeadPivot.Position = headCenterInModel;

        foreach (Node3D facePart in faceParts)
        {
            if (facePart != null && IsInstanceValid(facePart))
                facePart.Reparent(_playerHeadPivot, true);
        }
    }

    private void UpdatePlayerHeadLook()
    {
        if (_playerHeadPivot == null || !IsInstanceValid(_playerHeadPivot) || _playerCamera == null)
            return;

        // Match the camera's world pitch, compensating for the model's own
        // forward lean and the upper-body sneak bend so the head does not
        // appear to aim above or below the camera cursor.
        Vector3 headRotation = _playerHeadPivot.Rotation;
        float parentPitch = 0f;
        if (_playerHeadPivot.GetParent() is Node3D headParent)
            parentPitch = headParent.GlobalRotation.X;
        headRotation.X = _playerCamera.GetPitch() - parentPitch;
        _playerHeadPivot.Rotation = headRotation;
    }

    private Node3D CreateLimbPivot(
        string pivotName,
        Vector3 jointPosition,
        List<(Node3D Node, Vector3 RestRotation)> animationParts,
        Node3D parentPivot,
        params Node3D[] meshes)
    {
        Node3D pivot = new Node3D
        {
            Name = pivotName
        };

        Node3D pivotParent = parentPivot ?? _playerModelParts;
        pivotParent.AddChild(pivot);

        // jointPosition is authored in player-model local coordinates.
        // Convert it into the selected parent's local space so nested joints
        // still sit exactly at the elbow or knee in the untouched rest pose.
        Vector3 jointWorldPosition = _playerModelParts.ToGlobal(jointPosition);
        pivot.Position = pivotParent.ToLocal(jointWorldPosition);

        foreach (Node3D mesh in meshes)
        {
            if (mesh == null || !IsInstanceValid(mesh))
                continue;

            mesh.Reparent(pivot, true);
        }

        animationParts.Add((pivot, pivot.Rotation));
        return pivot;
    }

    private void SetCameraMode(int mode)
    {
        _cameraMode = ((mode % 3) + 3) % 3;
        _thirdPersonView = _cameraMode != 0;

        if (_playerModelParts != null)
        {
            // Keep the complete model visible in every camera mode for now.
            // This also lets us verify head tracking in first-person.
            SetModelMeshesVisible(_playerModelParts, true);
        }

        if (_bonesModel != null)
            _bonesModel.Visible = false;

        // In first-person, never render the player's own head or face.
        // The camera sits just in front of the head, so extreme pitch
        // angles could otherwise reveal the face from above or below.
        if (_playerHeadPivot != null && IsInstanceValid(_playerHeadPivot))
            _playerHeadPivot.Visible = _cameraMode != 0;

        UpdateCameraModeTransform();
    }

    private void UpdateCameraModeTransform()
    {
        Camera3D camera = _playerCamera?.GetCamera();
        if (camera == null)
            return;

        if (_cameraMode == 0)
        {
            // Put the camera just in front of the head, rather than at the
            // character body's origin. This keeps first-person view out of
            // the head and torso while preserving the camera's mouse pitch.
            if (_playerHeadPivot != null && _playerModelRoot != null)
            {
                Vector3 faceFront = _playerHeadPivot.GlobalPosition +
                    _playerModelRoot.GlobalBasis *
                    new Vector3(0f, 0f, -(_playerHeadHalfDepth + 0.12f));

                _playerCamera.Position = ToLocal(faceFront);
            }
            else
            {
                _playerCamera.Position = _defaultPlayerCameraPosition;
            }

            camera.Position = Vector3.Zero;
            camera.Rotation = Vector3.Zero;
        }
        else
        {
            _playerCamera.Position = _defaultPlayerCameraPosition;

            float direction = _cameraMode == 2 ? -1f : 1f;
            camera.Position = new Vector3(
                0f,
                0f,
                direction * _cameraZoomDistance
            );

            // The front camera sits in front of the character and looks back
            // toward them; the rear camera keeps the normal forward view.
            camera.Rotation = new Vector3(
                0f,
                _cameraMode == 2 ? Mathf.Pi : 0f,
                0f
            );
        }
    }

    private void SetModelMeshesVisible(Node parent, bool visible)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is GeometryInstance3D visual)
                visual.Visible = visible;

            SetModelMeshesVisible(child, visible);
        }
    }

    private void SetModelPartVisible(Node3D part, bool visible)
    {
        if (part is GeometryInstance3D visual)
            visual.Visible = visible;
    }

    private float _locomotionBlend = 0f;
    private float _locomotionPhase = 0f;
    private Vector3 _locomotionBaseModelPosition;
    private Vector3 _locomotionBaseModelRotation;
    private bool _locomotionBasePoseCaptured = false;

    private void UpdateFirstPersonArms(float delta)
    {
        if (_playerModelParts == null || !IsInstanceValid(_playerModelParts))
            return;

        if (!_locomotionBasePoseCaptured)
        {
            if (_playerModelRoot != null && IsInstanceValid(_playerModelRoot))
            {
                _locomotionBaseModelPosition = _playerModelRoot.Position;
                _locomotionBaseModelRotation = _playerModelRoot.Rotation;
            }
            if (_playerModelRoot != null && IsInstanceValid(_playerModelRoot))
                _locomotionBasePoseCaptured = true;
        }

        _sneakBlend = IsMounted
            ? 0f
            : Mathf.MoveToward(
                _sneakBlend, _isCrouching ? 1f : 0f, delta * 8f);

        if (_playerCamera != null)
        {
            if (_cameraMode == 0 && _playerHeadPivot != null &&
                _playerModelRoot != null)
            {
                // First-person camera follows the face pivot every frame.
                // Since the head belongs to the sneaking upper-body group,
                // this keeps the view anchored just in front of the face.
                Vector3 faceFront = _playerHeadPivot.GlobalPosition +
                    _playerModelRoot.GlobalBasis *
                    new Vector3(0f, 0f, -(_playerHeadHalfDepth + 0.12f));
                _playerCamera.Position = ToLocal(faceFront);
            }
            else
            {
                // Third-person camera lowers with the crouched character.
                Vector3 cameraTarget = _defaultPlayerCameraPosition +
                                       Vector3.Down * (0.5f * _sneakBlend);
                _playerCamera.Position = _playerCamera.Position.Lerp(
                    cameraTarget, Mathf.Clamp(delta * 10f, 0f, 1f));
            }
        }

        if (_playerUpperBodyPivot != null &&
            IsInstanceValid(_playerUpperBodyPivot))
        {
            _playerUpperBodyPivot.Position = _upperBodyBasePosition +
                                             Vector3.Down * (0.5f * _sneakBlend);
            // A modest forward lean gives the crouch the compact Skyrim-like
            // silhouette while the pelvis and thigh roots move as one unit.
            Vector3 upperBodyTargetRotation = _upperBodyBaseRotation +
                                              new Vector3(-0.816f * _sneakBlend, 0f, 0f);
            _playerUpperBodyPivot.Rotation = new Vector3(
                Mathf.LerpAngle(_playerUpperBodyPivot.Rotation.X, upperBodyTargetRotation.X,
                    Mathf.Clamp(delta * 10f, 0f, 1f)),
                Mathf.LerpAngle(_playerUpperBodyPivot.Rotation.Y, upperBodyTargetRotation.Y,
                    Mathf.Clamp(delta * 10f, 0f, 1f)),
                Mathf.LerpAngle(_playerUpperBodyPivot.Rotation.Z, upperBodyTargetRotation.Z,
                    Mathf.Clamp(delta * 10f, 0f, 1f)));
        }

        Vector3 worldVelocity = new Vector3(Velocity.X, 0f, Velocity.Z);
        float speed = worldVelocity.Length();
        bool grounded = IsOnFloor();

        // Riding uses a stable seated pose instead of the normal walking,
        // jumping or crouching gait.
        if (IsMounted)
        {
            _sneakBlend = 0f;
            ApplyLimbRotation(_leftLegParts, 1.28f, 0f, -0.22f);
            ApplyLimbRotation(_rightLegParts, 1.28f, 0f, 0.22f);
            ApplyLimbRotation(_leftLowerLegParts, -1.18f, 0f, -0.04f);
            ApplyLimbRotation(_rightLowerLegParts, -1.18f, 0f, 0.04f);
            ApplyLimbRotation(_leftArmParts, 0.08f, 0f, -0.025f);
            ApplyLimbRotation(_rightArmParts, 0.08f, 0f, 0.025f);
            ApplyLimbRotation(_leftForearmParts, 0.18f, 0f, 0f);
            ApplyLimbRotation(_rightForearmParts, 0.18f, 0f, 0f);
            UpdateLocomotionBodyPose(0f, 0f, 0f, delta);
            return;
        }

        // Climbing braces the legs against the wall and reaches overhead.
        if (_isClimbing)
        {
            ApplyLimbRotation(_leftLegParts, 0.35f, 0f, -0.10f);
            ApplyLimbRotation(_rightLegParts, 0.35f, 0f, 0.10f);
            ApplyLimbRotation(_leftLowerLegParts, -0.55f, 0f, 0f);
            ApplyLimbRotation(_rightLowerLegParts, -0.55f, 0f, 0f);
            ApplyLimbRotation(_leftArmParts, -1.25f, 0f, -0.10f);
            ApplyLimbRotation(_rightArmParts, -1.25f, 0f, 0.10f);
            ApplyLimbRotation(_leftForearmParts, -0.35f, 0f, 0f);
            ApplyLimbRotation(_rightForearmParts, -0.35f, 0f, 0f);
            UpdateLocomotionBodyPose(0f, 0.015f, 0f, delta);
            return;
        }

        // Wall-running is an active locomotion state, not an airborne pose.
        // Keep the same alternating leg/foot cycle running while attached.
        bool moving = speed > 0.12f && (grounded || _isWallRunning);

        // Roll the view toward the wall, matching Titanfall 2's readable
        // wall-run camera lean. The camera smooths the transition itself.
        if (_playerCamera != null)
        {
            float wallSide = 0f;
            if (_isWallRunning)
            {
                Vector3 playerRight = GlobalTransform.Basis.X;
                playerRight.Y = 0f;
                if (playerRight.LengthSquared() > 0.001f)
                    wallSide = Mathf.Sign(-_wallRunNormal.Dot(playerRight.Normalized()));
            }
            _playerCamera.SetWallRunTilt(_isWallRunning, wallSide);
        }

        // Work in the character's own space so forward, backward and strafing
        // each produce a slightly different gait rather than one fixed swing.
        Vector3 localDirection = Vector3.Zero;
        if (speed > 0.001f)
        {
            localDirection =
                _playerModelParts.GlobalTransform.Basis.Inverse() *
                (worldVelocity / speed);
            localDirection.Y = 0f;
            if (localDirection.LengthSquared() > 0.001f)
                localDirection = localDirection.Normalized();
        }

        float forwardAmount = -localDirection.Z;
        float sideAmount = localDirection.X;
        float sprintAmount = Mathf.Clamp(
            (speed - WalkSpeed) / Mathf.Max(0.1f, SprintSpeed - WalkSpeed),
            0f, 1f);

        float targetBlend = _isCrouching
            ? 1f
            : (moving ? 1f : (grounded ? 0.35f : 1f));
        _locomotionBlend = Mathf.MoveToward(
            _locomotionBlend, targetBlend, delta * (moving ? 5.5f : 4.0f));

        if (moving)
        {
            float cadence = Mathf.Lerp(6.4f, 9.0f, sprintAmount);
            _locomotionPhase = Mathf.PosMod(
                _locomotionPhase + delta * cadence, Mathf.Tau);
            _armAnimationTime = _locomotionPhase;
        }

        if (!grounded && !_isWallRunning)
        {
            // A small airborne pose gives jumps a little anticipation without
            // interfering with the player's movement or collision.
            // Lift the thighs slightly during a jump, then fold the knees
            // backward so the lower legs tuck beneath the body naturally.
            float jumpPoseMultiplier = _isCrouchJumping ? 2f : 1f;
            ApplyLimbRotation(_leftLegParts, 0.42f * jumpPoseMultiplier, 0.02f, -0.12f);
            ApplyLimbRotation(_rightLegParts, 0.42f * jumpPoseMultiplier, -0.02f, 0.12f);
            ApplyLimbRotation(_leftLowerLegParts, -0.58f * jumpPoseMultiplier, 0f, 0.12f);
            ApplyLimbRotation(_rightLowerLegParts, -0.58f * jumpPoseMultiplier, 0f, -0.12f);
            // A crouch jump doubles the outward arm spread as well as the leg tuck.
            float jumpArmSpread = _isCrouchJumping ? 0.16f : 0.08f;
            ApplyLimbRotation(_leftArmParts, 0.38f, 0f, -jumpArmSpread);
            ApplyLimbRotation(_rightArmParts, 0.38f, 0f, jumpArmSpread);
            ApplyLimbRotation(_leftForearmParts, 0.22f, 0f, 0f);
            ApplyLimbRotation(_rightForearmParts, 0.22f, 0f, 0f);
            UpdateLocomotionBodyPose(0.035f, -0.035f, 0f, delta);
            return;
        }

        if (moving)
        {
            float phaseLeft = _locomotionPhase;
            float phaseRight = _locomotionPhase + Mathf.Pi;
            float leftSwing = Mathf.Sin(phaseLeft);
            float rightSwing = Mathf.Sin(phaseRight);
            float leftLift = Mathf.Max(0f, Mathf.Cos(phaseLeft));
            float rightLift = Mathf.Max(0f, Mathf.Cos(phaseRight));

            float stride = Mathf.Lerp(0.34f, 0.52f, sprintAmount);
            float forwardSwingLeft = leftSwing * forwardAmount;
            float forwardSwingRight = rightSwing * forwardAmount;

            // A forward walk uses a broad hip swing. Strafing shifts the legs
            // laterally and adds a little hip yaw so the feet don't look locked
            // to a single front/back hinge.
            float sideStride = sideAmount * 0.20f;
            float legPitchLeft = forwardSwingLeft * stride;
            float legPitchRight = forwardSwingRight * stride;
            float legYaw = sideAmount * 0.14f;
            float legRollLeft = sideStride * leftSwing + sideAmount * 0.035f;
            float legRollRight = sideStride * rightSwing + sideAmount * 0.035f;

            ApplyLimbRotation(
                _leftLegParts,
                legPitchLeft,
                legYaw + sideAmount * leftLift * 0.035f,
                legRollLeft);
            ApplyLimbRotation(
                _rightLegParts,
                legPitchRight,
                legYaw - sideAmount * rightLift * 0.035f,
                legRollRight);

            // The knee folds most while the foot is travelling forward, then
            // opens as the leg reaches back to support the next step.
            float kneeBase = Mathf.Lerp(0.52f, 0.68f, sprintAmount);
            float leftKnee = kneeBase * leftLift +
                             Mathf.Max(0f, -forwardSwingLeft) * 0.18f;
            float rightKnee = kneeBase * rightLift +
                              Mathf.Max(0f, -forwardSwingRight) * 0.18f;
            // Lower legs hinge backward from the knee (negative local X).
            ApplyLimbRotation(_leftLowerLegParts, -leftKnee, 0f, -sideAmount * 0.035f);
            ApplyLimbRotation(_rightLowerLegParts, -rightKnee, 0f, sideAmount * 0.035f);

            // Arms move opposite the legs. Shoulder roll and yaw add a small
            // amount of 3D follow-through; elbows remain softly bent.
            float armAmount = Mathf.Lerp(0.62f, 0.78f, sprintAmount);
            ApplyLimbRotation(
                _leftArmParts,
                -forwardSwingRight * armAmount + sideAmount * 0.06f,
                -sideAmount * 0.10f,
                -legRollRight * 0.65f - 0.035f);
            ApplyLimbRotation(
                _rightArmParts,
                -forwardSwingLeft * armAmount - sideAmount * 0.06f,
                -sideAmount * 0.10f,
                -legRollLeft * 0.65f + 0.035f);

            float elbowLeft = 0.12f + Mathf.Max(0f, -forwardSwingRight) * 0.28f +
                              sprintAmount * 0.12f;
            float elbowRight = 0.12f + Mathf.Max(0f, -forwardSwingLeft) * 0.28f +
                               sprintAmount * 0.12f;
            ApplyLimbRotation(_leftForearmParts, elbowLeft, -sideAmount * 0.035f, 0f);
            ApplyLimbRotation(_rightForearmParts, elbowRight, sideAmount * 0.035f, 0f);

            float bob = (0.012f + sprintAmount * 0.012f) *
                       (0.5f + 0.5f * Mathf.Cos(_locomotionPhase * 2f));
            float forwardLean = 0.025f + sprintAmount * 0.055f;
            float sideLean = -sideAmount * 0.035f +
                             Mathf.Sin(_locomotionPhase) * sideAmount * 0.018f;
            UpdateLocomotionBodyPose(bob, forwardLean, sideLean, delta);
        }
        else
        {
            // Subtle idle breathing and relaxed, slightly asymmetrical posture.
            float breathe = Mathf.Sin((float)Time.GetTicksMsec() * 0.0012f);
            float idle = 1f - _locomotionBlend;
            ApplyLimbRotation(_leftLegParts, 0.015f * idle, 0f, 0.008f * idle);
            ApplyLimbRotation(_rightLegParts, -0.008f * idle, 0f, -0.006f * idle);
            ApplyLimbRotation(_leftLowerLegParts, 0f, 0f, 0f);
            ApplyLimbRotation(_rightLowerLegParts, 0f, 0f, 0f);
            ApplyLimbRotation(_leftArmParts, 0.025f * breathe, 0f, -0.025f);
            ApplyLimbRotation(_rightArmParts, 0.025f * breathe, 0f, 0.025f);
            ApplyLimbRotation(_leftForearmParts, 0.10f, 0f, 0f);
            ApplyLimbRotation(_rightForearmParts, 0.10f, 0f, 0f);
            UpdateLocomotionBodyPose(
                0.003f * breathe, 0f, 0f, delta);
        }
    }

    private void ApplyLimbRotation(
        List<(Node3D Node, Vector3 RestRotation)> parts,
        float pitch,
        float yaw,
        float roll)
    {
        // Skyrim-inspired sneak stance: knees spread, legs folded, and arms
        // drawn slightly inward. These are joint rotations, never mesh scaling.
        if (_sneakBlend > 0f)
        {
            if (ReferenceEquals(parts, _leftLegParts))
            {
                // Drive the thighs forward and spread the knees for a
                // deeper, balanced stealth squat.
                pitch += 1.9f * _sneakBlend;
                roll -= 0.48f * _sneakBlend;
            }
            else if (ReferenceEquals(parts, _rightLegParts))
            {
                pitch += 1.9f * _sneakBlend;
                roll += 0.48f * _sneakBlend;
            }
            else if (ReferenceEquals(parts, _leftLowerLegParts))
            {
                // Keep the shins folded while allowing the normal walk
                // cycle to continue adding its alternating knee motion.
                pitch -= 1.9f * _sneakBlend;
                roll -= 0.12f * _sneakBlend;
            }
            else if (ReferenceEquals(parts, _rightLowerLegParts))
            {
                pitch -= 1.9f * _sneakBlend;
                roll += 0.12f * _sneakBlend;
            }
            else if (ReferenceEquals(parts, _leftArmParts))
            {
                // Hold the upper arm farther out to create a wider
                // balancing posture while sneaking.
                roll -= 0.416f * _sneakBlend;
            }
            else if (ReferenceEquals(parts, _rightArmParts))
            {
                roll += 0.416f * _sneakBlend;
            }
            else if (ReferenceEquals(parts, _leftForearmParts) ||
                     ReferenceEquals(parts, _rightForearmParts))
            {
                // Bring the forearms forward, as if using them to
                // counterbalance the low crouched centre of gravity.
                pitch += 0.55f * _sneakBlend;
            }
        }

        foreach (var part in parts)
        {
            if (part.Node == null || !IsInstanceValid(part.Node))
                continue;

            Vector3 target = part.RestRotation +
                new Vector3(pitch, yaw, roll) * _locomotionBlend;
            Vector3 current = part.Node.Rotation;
            current.X = Mathf.LerpAngle(current.X, target.X, 1f);
            current.Y = Mathf.LerpAngle(current.Y, target.Y, 1f);
            current.Z = Mathf.LerpAngle(current.Z, target.Z, 1f);
            part.Node.Rotation = current;
        }
    }

    private void UpdateLocomotionBodyPose(
        float bob,
        float forwardLean,
        float sideLean,
        float delta)
    {
        if (_playerModelRoot == null || !IsInstanceValid(_playerModelRoot))
            return;

        Vector3 targetPosition = _locomotionBaseModelPosition +
                                 new Vector3(0f, bob, 0f);
        _playerModelRoot.Position = _playerModelRoot.Position.Lerp(
            targetPosition, Mathf.Clamp(delta * 10f, 0f, 1f));

        Vector3 targetRotation = _locomotionBaseModelRotation +
                                 new Vector3(forwardLean, 0f, sideLean);
        _playerModelRoot.Rotation = new Vector3(
            Mathf.LerpAngle(_playerModelRoot.Rotation.X, targetRotation.X, delta * 8f),
            Mathf.LerpAngle(_playerModelRoot.Rotation.Y, targetRotation.Y, delta * 8f),
            Mathf.LerpAngle(_playerModelRoot.Rotation.Z, targetRotation.Z, delta * 8f));
    }

    private void ApplyLimbSwing(
        List<(Node3D Node, Vector3 RestRotation)> parts,
        float swing)
    {
        foreach (var part in parts)
        {
            if (part.Node == null || !IsInstanceValid(part.Node))
                continue;

            part.Node.Rotation =
                part.RestRotation + new Vector3(swing, 0f, 0f);
        }
    }

    private void ResetLimbParts(
        List<(Node3D Node, Vector3 RestRotation)> parts,
        float delta)
    {
        foreach (var part in parts)
        {
            if (part.Node == null || !IsInstanceValid(part.Node))
                continue;

            Vector3 rotation = part.Node.Rotation;
            rotation.X = Mathf.LerpAngle(
                rotation.X,
                part.RestRotation.X,
                delta * 8f
            );
            rotation.Y = Mathf.LerpAngle(
                rotation.Y,
                part.RestRotation.Y,
                delta * 8f
            );
            rotation.Z = Mathf.LerpAngle(
                rotation.Z,
                part.RestRotation.Z,
                delta * 8f
            );
            part.Node.Rotation = rotation;
        }
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

        // Bamboo item art must take priority over generic ItemResource icons.
        // The three bamboo textures live in Assets/Textures/Items/stick/.
        string stickTexturePath =
            $"res://Assets/Textures/Items/stick/{itemId}.png";

        Texture2D tex = ResourceLoader.Exists(stickTexturePath)
            ? ResourceLoader.Load<Texture2D>(stickTexturePath)
            : null;

        var item = ItemRegistry.Instance?.GetItem(itemId);

        if (tex == null)
            tex = item?.Icon;

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

            // Seed item IDs are singular, while their icon files use
            // the plural "_seeds" naming convention.
            if (tex == null)
            {
                string seedTextureId = itemId switch
                {
                    "wheat_seed" => "wheat_seeds",
                    "tomato_seed" => "tomato_seeds",
                    "corn_seed" => "corn_seeds",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(seedTextureId))
                {
                    string seedTexturePath = FindItemTexturePath(
                        "res://Assets/Textures/Items",
                        seedTextureId
                    );

                    if (!string.IsNullOrEmpty(seedTexturePath) &&
                        ResourceLoader.Exists(seedTexturePath))
                    {
                        tex = ResourceLoader.Load<Texture2D>(seedTexturePath);
                    }
                }
            }
        }

        // Saplings use the Plants texture folder rather than Items.
        if (tex == null && itemId == "sapling")
        {
            const string plantPath = "res://Assets/Textures/Plants/sapling.png";
            if (ResourceLoader.Exists(plantPath))
                tex = ResourceLoader.Load<Texture2D>(plantPath);
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

    private List<(string Id, Vector3I Position, float Distance)> GetNearbyStations()
    {
        var stations = new List<(string Id, Vector3I Position, float Distance)>();
        var cm =
            GetTree().Root.FindChild(
                "ChunkManager",
                true,
                false
            ) as ChunkManager;

        if (cm == null)
            return stations;

        Vector3 pos = GlobalPosition;

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

            stations.Add((id, blockPosition, distance));

        }

        stations.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return stations;
    }

    private void UpdateCraftingProximity()
    {
        if (!_inventoryOpen ||
            RecipeManager.Instance == null)
            return;

        var nearby = GetNearbyStations();
        UpdateStationSelector(nearby);

        bool activeStationStillNearby = _activeStation == "default";
        if (!activeStationStillNearby)
        {
            foreach (var station in nearby)
            {
                if (station.Id == _activeStation &&
                    station.Position == _activeStationPosition)
                {
                    activeStationStillNearby = true;
                    break;
                }
            }
        }

        if (!activeStationStillNearby)
            SwitchStation("default");

        if (_activeStation == "crafter")
            _craftingPanel?.SetGridSize(3, true);
        else if (_activeStation == "default")
            _craftingPanel?.SetGridSize(2, false);

        if (_activeStation == "kiln" && _kilnPanel != null &&
            (!_kilnPanelHasPosition ||
             _kilnPanel.GetKilnPosition() != _activeStationPosition))
        {
            _kilnPanel.SetKiln(
                _activeStationPosition,
                KilnManager.Instance?.GetSlots(_activeStationPosition)
            );
            _kilnPanelHasPosition = true;
        }
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

        _activeStationPosition = new Vector3I(
            Mathf.FloorToInt(tPos.X),
            Mathf.FloorToInt(tPos.Y),
            Mathf.FloorToInt(tPos.Z)
        );

        if (b.BlockId == "kiln")
        {
            _kilnPanel?.SetKiln(
                _activeStationPosition,
                KilnManager.Instance?.GetSlots(_activeStationPosition)
            );
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
                var outputItem = ItemRegistry.Instance?.GetItem(peekId);
                bool outputIsStackable = outputItem == null || outputItem.IsStackable;

                if (_heldSlot.ItemId != peekId ||
                    !outputIsStackable ||
                    _heldSlot.Count >= _inventory.MaxStackSize)
                    return;
            }

            if (_craftingPanel.TryConsumeOneCraft(
                    out string resultId,
                    out int resultCount))
            {
                if (_heldSlot.IsEmpty)
                {
                    _heldSlot.ItemId = resultId;
                    _heldSlot.Count = resultCount;

                    // Crafted tools are non-stackable and must start with full durability.
                    var resultItem = ItemRegistry.Instance?.GetItem(resultId);
                    if (resultItem != null && !resultItem.IsStackable)
                        _heldSlot.CurrentDurability = resultItem.MaxDurability;
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
        var resultItem = ItemRegistry.Instance?.GetItem(resultId);
        bool stackable = resultItem == null || resultItem.IsStackable;
        int durability = resultItem?.MaxDurability ?? 0;

        if (!stackable)
        {
            // Tools need their own slot and a full durability value.
            if (_heldSlot.IsEmpty)
            {
                _heldSlot.ItemId = resultId;
                _heldSlot.Count = 1;
                _heldSlot.CurrentDurability = durability;
            }
            else
            {
                AddItemToInventory(resultId, resultCount, durability);
            }
        }
        else if (!_heldSlot.IsEmpty)
        {
            if (_heldSlot.ItemId != resultId ||
                _heldSlot.Count >= _inventory.MaxStackSize)
            {
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
            _heldSlot.Count = resultCount;
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
    UpdateFirstPersonArms((float)delta);
    UpdatePlayerHeadLook();

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
        // The floating item icon follows the cursor but must not
        // intercept the mouse when dropping items outside the UI.
        _cursorPanel.MouseFilter = Control.MouseFilterEnum.Ignore;

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

    private void UpdateCrouchCollision()
    {
        if (_playerCollision == null || _playerCapsule == null)
            return;

        if (_isCrouching)
        {
            _playerCapsule.Height = Mathf.Max(
                _standingCapsuleHeight - 0.5f,
                _playerCapsule.Radius * 2f);
            _playerCollision.Position = _standingCollisionPosition +
                                        new Vector3(0f, -0.25f, 0f);
        }
        else
        {
            _playerCapsule.Height = _standingCapsuleHeight;
            _playerCollision.Position = _standingCollisionPosition;
        }
    }

    // =========================================================================
    // PHYSICS
    // =========================================================================

    public override void _PhysicsProcess(double delta)
{
    float dt = (float)delta;

    // The RayCast3D is a child of the active camera, so keep its local
    // direction exactly on the camera's -Z axis (the screen-centre crosshair).
    // Limit its endpoint to the five-unit interaction sphere around the player
    // so third-person zoom does not increase the player's reach.
    if (_rayCast != null && _playerCamera?.GetCamera() is Camera3D interactionCamera)
    {
        Vector3 interactionOrigin = GlobalPosition + Vector3.Up * 0.6f;
        Vector3 cameraOffset = interactionCamera.GlobalPosition - interactionOrigin;
        Vector3 aimDirection = -interactionCamera.GlobalTransform.Basis.Z;

        float projection = cameraOffset.Dot(aimDirection);
        float discriminant = projection * projection + 25f - cameraOffset.LengthSquared();

        if (discriminant > 0f)
        {
            float rayLength = -projection + Mathf.Sqrt(discriminant);
            _rayCast.TargetPosition = new Vector3(0f, 0f, -rayLength);
        }
        else
        {
            // The camera's centre ray does not pass through the player's
            // interaction range, so it must not hit something out of reach.
            _rayCast.TargetPosition = Vector3.Zero;
        }

        _rayCast.ForceRaycastUpdate();
    }

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

    // Climbing is a standard player ability and is not tied to Pilot gear.
    // Press jump while facing a solid wall to grab it.
    if (_isClimbing)
    {
        HandleClimbing(dt);
        return;
    }

    if (Input.IsActionJustPressed("jump") && TryStartClimbing())
    {
        // Do not process the same jump press as a wall-jump release on the
        // frame the player first grabs the wall.
        Velocity = Vector3.Zero;
        MoveAndSlide();
        return;
    }

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
        _isCrouchJumping = false;
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
    UpdateCrouchCollision();

    // Holding crouch briefly on the ground winds up a stronger jump.
    if (_isCrouching && IsOnFloor())
        _crouchChargeTime = Mathf.Min(
            _crouchChargeTime + dt,
            CrouchJumpChargeDuration
        );
    else if (!_isCrouching)
        _crouchChargeTime = 0f;

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

    // Pilot slide starts when crouch is pressed during a sprint.
    // It keeps the sprint's momentum instead of immediately switching to
    // the normal slow crouch speed.
    if (CanWallClimb &&
        Input.IsActionJustPressed("crouch") &&
        Input.IsActionPressed("sprint") &&
        IsOnFloor())
    {
        TryStartPilotSlide(ref velocity, direction);
    }

    if (_isSliding)
    {
        _isCrouching = true;
        ApplyPilotSlide(ref velocity, direction, dt);
    }
    else if (direction != Vector3.Zero)
    {
        velocity.X = direction.X * moveSpeed;
        velocity.Z = direction.Z * moveSpeed;
    }
    else
    {
        velocity.X = Mathf.MoveToward(velocity.X, 0f, moveSpeed * 10f * dt);
        velocity.Z = Mathf.MoveToward(velocity.Z, 0f, moveSpeed * 10f * dt);
    }

    // ---------------------------------------------------------
    // PILOT WALL RUN
    // ---------------------------------------------------------

    ApplyPilotWallRun(ref velocity, direction, dt);

    // ---------------------------------------------------------
    // JUMP
    // ---------------------------------------------------------

    if (Input.IsActionJustPressed("jump") && !HandlePilotJump(ref velocity))
    {
        if (IsOnFloor())
        {
            bool useCrouchJump =
                _isCrouching &&
                _crouchChargeTime >= CrouchJumpChargeDuration;

            // 1.5x jump height requires sqrt(1.5) times the launch velocity.
            bool slideJump = _isSliding;
            velocity.Y = slideJump
                ? JumpVelocity * 1.22f
                : (useCrouchJump
                    ? JumpVelocity * Mathf.Sqrt(1.5f)
                    : JumpVelocity);

            if (slideJump)
            {
                // Keep the slide's horizontal momentum and add a small
                // forward carry into the Titanfall-style slide jump.
                Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
                if (horizontal.LengthSquared() > 0.01f)
                {
                    horizontal *= 1.10f;
                    velocity.X = horizontal.X;
                    velocity.Z = horizontal.Z;
                }
                _isSliding = false;
                _pilotSlideTime = 0f;
            }

            _isCrouchJumping = useCrouchJump;
            _crouchChargeTime = 0f;
            _hasDoubleJumped = false;
        }
        else if (CanDoubleJump &&
                 !_hasDoubleJumped)
        {
            // Pilot double jump: a stronger second impulse while
            // preserving the horizontal momentum from the first jump.
            velocity.Y = JumpVelocity * 1.18f;
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

    // Flowing water carries the player in its travel direction.
    ChunkManager liquidWorld = GetTree().Root.FindChild(
        "ChunkManager", true, false) as ChunkManager;
    if (liquidWorld != null)
    {
        Vector3I liquidPos = new Vector3I(
            Mathf.FloorToInt(GlobalPosition.X),
            Mathf.FloorToInt(GlobalPosition.Y + 0.5f),
            Mathf.FloorToInt(GlobalPosition.Z));
        Vector3 flow = liquidWorld.GetLiquidFlowVectorAtWorld(liquidPos);
        if (flow.LengthSquared() > 0.001f)
            velocity += flow * 1.25f;
    }

    // Minecraft-style sneak protection: while crouching on the ground,
    // shorten horizontal movement until the capsule still has ground below it.
    ApplySneakEdgeProtection(ref velocity, dt);

    if (AdvancePilotMantle(dt))
    {
        Velocity = Vector3.Zero;
        return;
    }

    Velocity = velocity;

    MoveAndSlide();
}

private void ApplySneakEdgeProtection(ref Vector3 velocity, float delta)
{
    if (!_isCrouching || _isSliding || !IsOnFloor() || delta <= 0f ||
        _playerCollision == null || _playerCapsule == null)
        return;

    Vector3 horizontalVelocity = new Vector3(velocity.X, 0f, velocity.Z);
    float requestedDistance = horizontalVelocity.Length() * delta;
    if (requestedDistance <= 0.0001f)
        return;

    Vector3 moveDirection = horizontalVelocity.Normalized();
    float safeDistance = requestedDistance;

    // Check the actual ground beneath the capsule footprint at the proposed
    // destination. TestMove only reports collisions along a movement sweep;
    // it is not a reliable "ground exists underneath" test for an edge.
    if (!HasSneakGroundSupport(GlobalTransform))
    {
        // If the support probes cannot confirm the current footing, fail
        // safely instead of allowing crouch movement toward an unseen edge.
        velocity.X = 0f;
        velocity.Z = 0f;
        return;
    }

    Transform3D candidateTransform = GlobalTransform;
    candidateTransform.Origin += moveDirection * requestedDistance;

    if (!HasSneakGroundSupport(candidateTransform))
    {
        float low = 0f;
        float high = requestedDistance;

        // Binary-search the furthest position that still has ground under
        // the whole sampled footprint, including diagonal block corners.
        for (int i = 0; i < 10; i++)
        {
            float middle = (low + high) * 0.5f;
            candidateTransform = GlobalTransform;
            candidateTransform.Origin += moveDirection * middle;

            if (HasSneakGroundSupport(candidateTransform))
                low = middle;
            else
                high = middle;
        }

        safeDistance = low;
    }

    // Leave a small safety margin so the capsule does not balance exactly
    // on the last supported sample.
    safeDistance = Mathf.Max(0f, safeDistance - 0.025f);
    float safeFraction = Mathf.Clamp(safeDistance / requestedDistance, 0f, 1f);
    velocity.X *= safeFraction;
    velocity.Z *= safeFraction;
}

private bool HasSneakGroundSupport(Transform3D playerTransform)
{
    if (_playerCollision == null || _playerCapsule == null)
        return false;

    PhysicsDirectSpaceState3D space = GetWorld3D()?.DirectSpaceState;
    if (space == null)
        return false;

    Vector3 capsuleCenter = playerTransform * _playerCollision.Position;
    float footY = capsuleCenter.Y - (_playerCapsule.Height * 0.5f);
    float sampleRadius = _playerCapsule.Radius * 0.78f;

    // Nine downward rays cover the centre and the capsule's outer footprint.
    // Starting slightly above the feet also accommodates small floor steps.
    Vector3[] sampleOffsets =
    {
        Vector3.Zero,
        new Vector3(sampleRadius, 0f, 0f),
        new Vector3(-sampleRadius, 0f, 0f),
        new Vector3(0f, 0f, sampleRadius),
        new Vector3(0f, 0f, -sampleRadius),
        new Vector3(sampleRadius * 0.7071f, 0f, sampleRadius * 0.7071f),
        new Vector3(-sampleRadius * 0.7071f, 0f, sampleRadius * 0.7071f),
        new Vector3(sampleRadius * 0.7071f, 0f, -sampleRadius * 0.7071f),
        new Vector3(-sampleRadius * 0.7071f, 0f, -sampleRadius * 0.7071f)
    };

    foreach (Vector3 offset in sampleOffsets)
    {
        Vector3 rayStart = new Vector3(
            capsuleCenter.X + offset.X,
            footY + 0.55f,
            capsuleCenter.Z + offset.Z);
        Vector3 rayEnd = new Vector3(
            rayStart.X,
            footY - 0.4f,
            rayStart.Z);

        var query = PhysicsRayQueryParameters3D.Create(
            rayStart,
            rayEnd,
            CollisionMask);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        if (space.IntersectRay(query).Count == 0)
            return false;
    }

    return true;
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

   
    private bool TryRightClickHarvestCrop()
    {
        if (_rayCast == null || !_rayCast.IsColliding())
            return false;

        var gm = GameModeManager.Instance;
        if (gm != null && gm.IsStory)
            return false;

        Node collider = _rayCast.GetCollider() as Node;
        if (collider == null || !collider.HasMeta("chunk"))
            return false;

        Chunk chunk = collider.GetMeta("chunk").AsGodotObject() as Chunk;
        if (chunk == null)
            return false;

        Vector3 localPosition =
            _rayCast.GetCollisionPoint() -
            _rayCast.GetCollisionNormal() * 0.5f -
            chunk.GlobalPosition;

        int bx = Mathf.FloorToInt(localPosition.X);
        int by = Mathf.FloorToInt(localPosition.Y);
        int bz = Mathf.FloorToInt(localPosition.Z);

        if (bx < 0 || bx >= Chunk.SIZE ||
            by < 0 || by >= Chunk.HEIGHT ||
            bz < 0 || bz >= Chunk.SIZE)
            return false;

        Vector3I worldPosition = new Vector3I(
            chunk.ChunkPosition.X * Chunk.SIZE + bx,
            chunk.ChunkPosition.Y * Chunk.HEIGHT + by,
            chunk.ChunkPosition.Z * Chunk.SIZE + bz
        );

        bool hasCornSeed = false;
        int cornSeedSlot = -1;

        for (int i = 0; i < _inventory.Slots.Length; i++)
        {
            InventorySlot slot = _inventory.Slots[i];
            if (slot != null && !slot.IsEmpty && slot.ItemId == "corn_seed")
            {
                hasCornSeed = true;
                cornSeedSlot = i;
                break;
            }
        }

        if (CropManager.Instance == null ||
            !CropManager.Instance.TryRightClickHarvest(
                worldPosition,
                hasCornSeed,
                out string harvestItem,
                out int harvestCount,
                out bool consumeCornSeed))
            return false;

        if (consumeCornSeed && cornSeedSlot >= 0)
        {
            InventorySlot seedSlot = _inventory.Slots[cornSeedSlot];
            seedSlot.Count--;
            if (seedSlot.Count <= 0)
                seedSlot.Clear();
            FireChanged();
        }

        Vector3 dropPosition = chunk.GlobalPosition +
            new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f);

        if (!string.IsNullOrEmpty(harvestItem) && harvestCount > 0)
            SpawnItemDrop(harvestItem, harvestCount, dropPosition);

        ResetBreak();
        return true;
    }

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
        // Hold Ctrl and use the wheel to zoom while in either third-person view.
        if (mb.Pressed &&
            (mb.ButtonIndex == MouseButton.WheelUp ||
             mb.ButtonIndex == MouseButton.WheelDown) &&
            _cameraMode != 0 &&
            Input.IsKeyPressed(Key.Ctrl) &&
            !_inventoryOpen &&
            !_chatOpen &&
            !_creativeMenuOpen)
        {
            _cameraZoomDistance +=
                mb.ButtonIndex == MouseButton.WheelUp
                    ? -CameraZoomStep
                    : CameraZoomStep;

            _cameraZoomDistance = Mathf.Clamp(
                _cameraZoomDistance,
                CameraZoomMin,
                CameraZoomMax
            );

            UpdateCameraModeTransform();
            return;
        }

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

            // Right mouse: harvest special mature crops, mount, interact, or place.
            if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                if (TryRightClickHarvestCrop())
                    return;

                // Use the selected hotbar slot for mob interactions.
                Mob targetMob = GetMobUnderCrosshair();

                if (targetMob != null)
                {
                    InventorySlot handSlot =
                        _inventory.Slots[MainInvSize + _selectedSlot];

                    // Feeding takes priority over mounting.
                    if (!handSlot.IsEmpty)
                    {
                        if (targetMob.TryFeed(handSlot.ItemId))
                        {
                            handSlot.Count--;

                            if (handSlot.Count <= 0)
                                handSlot.Clear();

                            FireChanged();
                            _isPlacing = false;
                            return;
                        }
                    }
                    // Mounting requires a genuinely empty selected hotbar slot.
                    else if (targetMob.StartRiding(this))
                    {
                        _mountedMob = targetMob;

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
        // PLAYER CAMERA MODE (F3)
        // =====================================================

        if (key.Keycode == Key.F3 &&
            !_inventoryOpen &&
            !_chatOpen &&
            !_creativeMenuOpen)
        {
            SetCameraMode((_cameraMode + 1) % 3);

            string cameraModeName = _cameraMode switch
            {
                0 => "First-person view",
                1 => "Third-person view",
                _ => "Front-facing view"
            };

            ShowFeedback(cameraModeName);
            return;
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

            _activeStation = "default";

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

    public bool AreChunkBordersVisible => _showChunkBorders;

    public void SetChunkBordersFromDebugMenu(bool visible)
    {
        _showChunkBorders = visible;
        ToggleChunkBorders();
    }

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
        // Save the player's inventory and position when leaving the world,
        // not only when the player manually presses F5.
        var chunkManager = GetTree().Root.FindChild(
            "ChunkManager",
            true,
            false
        ) as ChunkManager;

        if (chunkManager != null && _inventory != null)
        {
            chunkManager.SaveModifiedChunks();
            chunkManager.SaveInventory(_inventory);
            chunkManager.SavePlayerPosition(
                GlobalPosition,
                Rotation.Y,
                _playerCamera?.GetPitch() ?? 0f
            );
        }

        SaveManager.Instance?.SaveCurrentWorld();

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
            "Usage: /spawn <mob> [male/female] [shiny] [big]"
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

    string gender = "";
    bool? shinyOverride = null;
    bool? bigOverride = null;

    // Options can be supplied in any order:
    // /spawn pig male shiny big
    // /spawn pig big female
    for (int i = 2; i < parts.Length; i++)
    {
        string option = parts[i].Trim().ToLowerInvariant();

        if (option == "male" || option == "m" ||
            option == "female" || option == "f")
        {
            if (!string.IsNullOrEmpty(gender))
            {
                ShowFeedback("Only one gender option can be used.");
                return;
            }

            gender = option;
        }
        else if (option == "shiny")
        {
            shinyOverride = true;
        }
        else if (option == "notshiny")
        {
            shinyOverride = false;
        }
        else if (option == "big")
        {
            bigOverride = true;
        }
        else if (option == "notbig")
        {
            bigOverride = false;
        }
        else
        {
            ShowFeedback("Usage: /spawn <mob> [male/female] [shiny] [big]");
            return;
        }
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
            spawnPosition,
            shinyOverride,
            bigOverride
        );

    if (spawned)
    {
        var spawnDetails = new List<string>();

        spawnDetails.Add(
            string.IsNullOrEmpty(gender) ? "random gender" : gender
        );

        if (shinyOverride == true) spawnDetails.Add("shiny");
        else if (shinyOverride == false) spawnDetails.Add("not shiny");

        if (bigOverride == true) spawnDetails.Add("big");
        else if (bigOverride == false) spawnDetails.Add("not big");

        ShowFeedback(
            $"Spawned {mobId} ({string.Join(", ", spawnDetails)})."
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