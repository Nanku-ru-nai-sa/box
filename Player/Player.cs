using Godot;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
    [Export] public float WalkSpeed { get; set; } = 5f;
    [Export] public float SprintSpeed { get; set; } = 8f;
    [Export] public float CrouchSpeed { get; set; } = 2.5f;
    [Export] public float JumpVelocity { get; set; } = 6f;
    [Export] public float SprintStaminaCost { get; set; } = 10f;
    [Export] public float JumpStaminaCost { get; set; } = 10f;

    private float _gravity = 20f;
    private PlayerStats _stats;
    private PlayerCamera _playerCamera;
    private RayCast3D _rayCast;
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
    private const float BreakInterval = 0.15f;
    private string _selectedBlockId = "stone";
    private MeshInstance3D _blockOutline;
    private Panel[] _hotbarSlots = new Panel[5];
    private string[] _hotbarBlocks = { "dirt", "stone", "sand", "log", "leaves" };
    private int _selectedSlot = 1;
    private Inventory _inventory;
    private Label[] _hotbarLabels = new Label[5];
    private Control _inventoryScreen;
    private Label[] _inventorySlotLabels = new Label[20];
    private bool _inventoryOpen = false;
    private bool _showChunkBorders = false;
    private bool _hudVisible = true;
    private CanvasLayer _hotbarLayer;
    private CanvasLayer _crosshairLayer;
    private readonly List<MeshInstance3D> _chunkBorderMeshes = new();

    public bool CanDoubleJump { get; set; } = false;
    public bool CanWallClimb { get; set; } = false;
    public bool CanGlide { get; set; } = false;
    public bool CanGrapple { get; set; } = false;

    

    public override void _Ready()
    {
        _stats = GetNodeOrNull<PlayerStats>("PlayerStats");
        _playerCamera = GetNodeOrNull<PlayerCamera>("PlayerCamera");
        _rayCast = GetNode<RayCast3D>("PlayerCamera/Camera3D/RayCast3D");
        _rayCast.AddException(this);
        _inventory = new Inventory();
        AddChild(_inventory);
        
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
        var outlineMesh = new ArrayMesh();

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Lines);

        Vector3[] corners = new Vector3[]
        {
            new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1),
            new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(0,1,1)
        };

        int[][] edges = new int[][]
        {
            new int[]{0,1}, new int[]{1,2}, new int[]{2,3}, new int[]{3,0},
            new int[]{4,5}, new int[]{5,6}, new int[]{6,7}, new int[]{7,4},
            new int[]{0,4}, new int[]{1,5}, new int[]{2,6}, new int[]{3,7}
        };

        foreach (var edge in edges)
        {
            st.AddVertex(corners[edge[0]]);
            st.AddVertex(corners[edge[1]]);
        }

        outlineMesh = st.Commit();
        _blockOutline.Mesh = outlineMesh;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0, 0, 0);
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _blockOutline.MaterialOverride = mat;

        _blockOutline.Visible = false;
        GetTree().Root.CallDeferred("add_child", _blockOutline);

        _hotbarLayer = new CanvasLayer();
        GetTree().Root.CallDeferred("add_child", _hotbarLayer);
        

        var hotbarContainer = new HBoxContainer();
        hotbarContainer.AnchorLeft = 0.5f;
        hotbarContainer.AnchorRight = 0.5f;
        hotbarContainer.AnchorTop = 1.0f;
        hotbarContainer.AnchorBottom = 1.0f;
        hotbarContainer.OffsetLeft = -125;
        hotbarContainer.OffsetRight = 125;
        hotbarContainer.OffsetTop = -60;
        hotbarContainer.OffsetBottom = -10;
        hotbarContainer.AddThemeConstantOverride("separation", 4);

        for (int i = 0; i < 5; i++)
        {
            var slot = new Panel();
            slot.CustomMinimumSize = new Vector2(46, 46);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            style.BorderColor = i == _selectedSlot ? new Color(1, 1, 1) : new Color(0.3f, 0.3f, 0.3f);
            style.BorderWidthTop = 2;
            style.BorderWidthBottom = 2;
            style.BorderWidthLeft = 2;
            style.BorderWidthRight = 2;
            slot.AddThemeStyleboxOverride("panel", style);

            var label = new Label();
label.Text = _hotbarBlocks[i];
label.HorizontalAlignment = HorizontalAlignment.Center;
label.VerticalAlignment = VerticalAlignment.Center;
label.AnchorRight = 1.0f;
label.AnchorBottom = 1.0f;
label.AddThemeFontSizeOverride("font_size", 10);
slot.AddChild(label);
_hotbarLabels[i] = label;

            hotbarContainer.AddChild(slot);
            _hotbarSlots[i] = slot;
        }

        _hotbarLayer.CallDeferred("add_child", hotbarContainer);

        // Inventory Screen
var inventoryLayer = new CanvasLayer();
inventoryLayer.Layer = 10;
GetTree().Root.CallDeferred("add_child", inventoryLayer);

_inventoryScreen = new Panel();
_inventoryScreen.AnchorLeft = 0.5f;
_inventoryScreen.AnchorRight = 0.5f;
_inventoryScreen.AnchorTop = 0.5f;
_inventoryScreen.AnchorBottom = 0.5f;
_inventoryScreen.OffsetLeft = -200;
_inventoryScreen.OffsetRight = 200;
_inventoryScreen.OffsetTop = -250;
_inventoryScreen.OffsetBottom = 250;

var invStyle = new StyleBoxFlat();
invStyle.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
invStyle.BorderColor = new Color(0.5f, 0.5f, 0.5f);
invStyle.BorderWidthTop = 2;
invStyle.BorderWidthBottom = 2;
invStyle.BorderWidthLeft = 2;
invStyle.BorderWidthRight = 2;
_inventoryScreen.AddThemeStyleboxOverride("panel", invStyle);

var invTitle = new Label();
invTitle.Text = "Inventory";
invTitle.HorizontalAlignment = HorizontalAlignment.Center;
invTitle.AnchorRight = 1.0f;
invTitle.OffsetTop = 8;
invTitle.OffsetBottom = 30;
_inventoryScreen.AddChild(invTitle);

var grid = new GridContainer();
grid.Columns = 5;
grid.AnchorRight = 1.0f;
grid.AnchorBottom = 1.0f;
grid.OffsetTop = 40;
grid.OffsetLeft = 10;
grid.OffsetRight = -10;
grid.OffsetBottom = -10;
grid.AddThemeConstantOverride("h_separation", 6);
grid.AddThemeConstantOverride("v_separation", 6);

for (int i = 0; i < 20; i++)
{
    var slot = new Panel();
    slot.CustomMinimumSize = new Vector2(68, 68);

    var slotStyle = new StyleBoxFlat();
    slotStyle.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
    slotStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f);
    slotStyle.BorderWidthTop = 2;
    slotStyle.BorderWidthBottom = 2;
    slotStyle.BorderWidthLeft = 2;
    slotStyle.BorderWidthRight = 2;
    slot.AddThemeStyleboxOverride("panel", slotStyle);

    var slotLabel = new Label();
    slotLabel.HorizontalAlignment = HorizontalAlignment.Center;
    slotLabel.VerticalAlignment = VerticalAlignment.Center;
    slotLabel.AnchorRight = 1.0f;
    slotLabel.AnchorBottom = 1.0f;
    slotLabel.AddThemeFontSizeOverride("font_size", 10);
    slotLabel.AutowrapMode = TextServer.AutowrapMode.Word;
    slot.AddChild(slotLabel);

    _inventorySlotLabels[i] = slotLabel;
    grid.AddChild(slot);
}

_inventoryScreen.AddChild(grid);
_inventoryScreen.Visible = false;
inventoryLayer.CallDeferred("add_child", _inventoryScreen);

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

        Input.MouseMode = Input.MouseModeEnum.Captured;
        _inventory.OnInventoryChanged += UpdateHotbarLabels;
UpdateHotbarLabels();
        GD.Print("Player ready.");
    }


    public override void _PhysicsProcess(double delta)
    {
        UpdateBlockOutline();
        if (_stats == null || _stats.IsDead) return;

CheckWaterStatus();

        float dt = (float)delta;
        Vector3 velocity = Velocity;

        if (_isInWater)
{
    // Gentle buoyancy - much weaker gravity, slight upward drift
    velocity.Y -= (_gravity * 0.2f) * dt;
    velocity.Y = Mathf.Clamp(velocity.Y, -2f, 8f);
}
else if (!IsOnFloor())
{
    velocity.Y -= _gravity * dt;
}

        else
        {
            _hasDoubleJumped = false;
            _isGliding = false;
        }

      if (_isInWater)
{
    if (Input.IsActionPressed("jump"))
    {
        velocity.Y = Mathf.MoveToward(velocity.Y, 6f, 12f * dt);
    }
}
else if (Input.IsActionPressed("jump") && IsOnFloor())
{
    velocity.Y = JumpVelocity;
}

        if (Input.IsActionJustReleased("ui_cancel"))
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
                Input.MouseMode = Input.MouseModeEnum.Visible;
            else
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        _isCrouching = Input.IsActionPressed("crouch");

        bool wantsSprint = Input.IsActionPressed("sprint");
        if (wantsSprint && _stats.Stamina > 0 && !_isCrouching)
        {
            _isSprinting = true;
            _stats.UseStamina(SprintStaminaCost * dt);
        }
        else
        {
            _isSprinting = false;
        }

        Vector2 inputDir = Input.GetVector(
            "move_left", "move_right",
            "move_forward", "move_back");

        Vector3 direction = (
            Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)
        ).Normalized();

        if (_isBreaking)
        {
            _breakTimer += dt;
            if (_breakTimer >= BreakInterval)
            {
                TryBreakBlock();
                _breakTimer = 0f;
            }
        }

        if (_isPlacing)
        {
            _placeTimer += dt;
            if (_placeTimer >= PlaceInterval)
            {
                TryPlaceBlock();
                _placeTimer = 0f;
            }
        }

        float speed = _isCrouching ? CrouchSpeed
    : _isSprinting ? SprintSpeed
    : WalkSpeed;

if (_isInWater)
    speed *= 0.5f;

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, speed * dt * 10f);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, speed * dt * 10f);
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    private bool _isNearWaterSurface = false;

private void UpdateHotbarLabels()
{
    for (int i = 0; i < _hotbarBlocks.Length; i++)
    {
        int count = _inventory.GetItemCount(_hotbarBlocks[i]);
        _hotbarLabels[i].Text = $"{_hotbarBlocks[i]}\n{count}";
    }

    UpdateInventoryScreen();
}


private void CheckWaterStatus()
{
    var chunkManager = GetTree().Root.GetNodeOrNull<ChunkManager>("TestWorld/ChunkManager");
    if (chunkManager == null)
    {
        _isInWater = false;
        return;
    }

    // Check at feet position - if feet are clear, player is "out"
    Vector3 feetPos = GlobalPosition + new Vector3(0, -0.9f, 0);
    _isInWater = IsBlockWaterAt(chunkManager, feetPos);
}

private bool IsBlockWaterAt(ChunkManager chunkManager, Vector3 worldPos)
{
    Vector3I chunkPos = chunkManager.WorldToChunk(worldPos);
    Chunk chunk = chunkManager.GetChunk(chunkPos);
    if (chunk == null) return false;

    Vector3 localPos = worldPos - chunk.GlobalPosition;
    int bx = Mathf.FloorToInt(localPos.X);
    int by = Mathf.FloorToInt(localPos.Y);
    int bz = Mathf.FloorToInt(localPos.Z);

    BlockState block = chunk.GetBlock(bx, by, bz);
    return block.BlockId == "water";
}

    private void UpdateBlockOutline()
    {
        if (!_rayCast.IsColliding())
        {
            _blockOutline.Visible = false;
            return;
        }

        var collider = _rayCast.GetCollider() as Node;
        if (collider == null || !collider.HasMeta("chunk"))
        {
            _blockOutline.Visible = false;
            return;
        }

        Vector3 hitPoint = _rayCast.GetCollisionPoint();
        Vector3 hitNormal = _rayCast.GetCollisionNormal();
        Vector3 insidePos = hitPoint - hitNormal * 0.5f;

        Vector3 blockOrigin = new Vector3(
            Mathf.Floor(insidePos.X),
            Mathf.Floor(insidePos.Y),
            Mathf.Floor(insidePos.Z)
        );

        _blockOutline.GlobalPosition = blockOrigin;
        _blockOutline.Visible = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _isBreaking = mb.Pressed;
                if (mb.Pressed)
                {
                    TryBreakBlock();
                    _breakTimer = 0f;
                }
            }

            if (mb.ButtonIndex == MouseButton.Right)
            {
                _isPlacing = mb.Pressed;
                if (mb.Pressed)
                {
                    TryPlaceBlock();
                    _placeTimer = 0f;
                }
            }
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key5)
            {
                int slot = (int)keyEvent.Keycode - (int)Key.Key1;
                SelectHotbarSlot(slot);
            }

            if (keyEvent.Keycode == Key.Tab)
            {
                ToggleInventory();
            }
            if (keyEvent.Keycode == Key.F1)
            {
                _hudVisible = !_hudVisible;
                if (_hotbarLayer != null) _hotbarLayer.Visible = _hudVisible;
                if (_crosshairLayer != null) _crosshairLayer.Visible = _hudVisible;
            }
            if (keyEvent.Keycode == Key.F2)
            {
                var image = GetViewport().GetTexture().GetImage();
                string path = $"user://screenshot_{Time.GetDatetimeStringFromSystem().Replace(":", "-")}.png";
                image.SavePng(path);
                GD.Print($"Screenshot saved: {path}");
            }
            if (keyEvent.Keycode == Key.F3)
            {
                _showChunkBorders = !_showChunkBorders;
                ToggleChunkBorders();
            }

            if (keyEvent.Keycode == Key.F5)
            {
                var chunkManager = GetTree().Root.GetNode<ChunkManager>("TestWorld/ChunkManager");
                chunkManager.Call("SaveModifiedChunks");
                GD.Print("World saved!");
            }
        }
    }

private void ToggleChunkBorders()
{
    foreach (var mesh in _chunkBorderMeshes)
        mesh.QueueFree();
    _chunkBorderMeshes.Clear();

    if (!_showChunkBorders) return;

    var chunkManager = GetTree().Root.GetNodeOrNull<ChunkManager>("TestWorld/ChunkManager");
    if (chunkManager == null) return;

    var mat = new StandardMaterial3D();
    mat.AlbedoColor = new Color(1, 1, 0);
    mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

    foreach (var chunkPos in chunkManager.GetLoadedChunkPositions())
    {
        float x0 = chunkPos.X * Chunk.SIZE;
        float x1 = x0 + Chunk.SIZE;
        float y0 = chunkPos.Y * Chunk.HEIGHT;
        float y1 = y0 + Chunk.HEIGHT;
        float z0 = chunkPos.Z * Chunk.SIZE;
        float z1 = z0 + Chunk.SIZE;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Lines);

        // Bottom face
        st.AddVertex(new Vector3(x0, y0, z0)); st.AddVertex(new Vector3(x1, y0, z0));
        st.AddVertex(new Vector3(x1, y0, z0)); st.AddVertex(new Vector3(x1, y0, z1));
        st.AddVertex(new Vector3(x1, y0, z1)); st.AddVertex(new Vector3(x0, y0, z1));
        st.AddVertex(new Vector3(x0, y0, z1)); st.AddVertex(new Vector3(x0, y0, z0));

        // Top face
        st.AddVertex(new Vector3(x0, y1, z0)); st.AddVertex(new Vector3(x1, y1, z0));
        st.AddVertex(new Vector3(x1, y1, z0)); st.AddVertex(new Vector3(x1, y1, z1));
        st.AddVertex(new Vector3(x1, y1, z1)); st.AddVertex(new Vector3(x0, y1, z1));
        st.AddVertex(new Vector3(x0, y1, z1)); st.AddVertex(new Vector3(x0, y1, z0));

        // Vertical edges
        st.AddVertex(new Vector3(x0, y0, z0)); st.AddVertex(new Vector3(x0, y1, z0));
        st.AddVertex(new Vector3(x1, y0, z0)); st.AddVertex(new Vector3(x1, y1, z0));
        st.AddVertex(new Vector3(x1, y0, z1)); st.AddVertex(new Vector3(x1, y1, z1));
        st.AddVertex(new Vector3(x0, y0, z1)); st.AddVertex(new Vector3(x0, y1, z1));

        var arrayMesh = st.Commit();

        var border = new MeshInstance3D();
        border.Mesh = arrayMesh;
        border.MaterialOverride = mat;

        GetTree().Root.AddChild(border);
        _chunkBorderMeshes.Add(border);
    }
}
    private void ToggleInventory()
{
    _inventoryOpen = !_inventoryOpen;
    _inventoryScreen.Visible = _inventoryOpen;

    if (_inventoryOpen)
    {
        UpdateInventoryScreen();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }
    else
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}
private void UpdateInventoryScreen()
{
    if (_inventorySlotLabels == null) return;

    for (int i = 0; i < _inventory.Slots.Length; i++)
    {
        var slot = _inventory.Slots[i];
        if (slot.IsEmpty)
            _inventorySlotLabels[i].Text = "";
        else
            _inventorySlotLabels[i].Text = $"{slot.ItemId}\n{slot.Count}";
    }
}



    private void SelectHotbarSlot(int slot)
    {
        _selectedSlot = slot;
        _selectedBlockId = _hotbarBlocks[slot];

        for (int i = 0; i < _hotbarSlots.Length; i++)
        {
            var style = (StyleBoxFlat)_hotbarSlots[i].GetThemeStylebox("panel");
            style.BorderColor = i == slot ? new Color(1, 1, 1) : new Color(0.3f, 0.3f, 0.3f);
        }
    }

   private void TryBreakBlock()
{
    if (!_rayCast.IsColliding()) return;

    var collider = _rayCast.GetCollider() as Node;
    // Check if we hit a melon
    if (collider is Melon melon)
    {
        melon.Break(_inventory);
        return;
    }
    if (collider == null || !collider.HasMeta("chunk")) return;

    Chunk chunk = (Chunk)collider.GetMeta("chunk").AsGodotObject();
    Vector3 hitPoint = _rayCast.GetCollisionPoint();
    Vector3 hitNormal = _rayCast.GetCollisionNormal();
    Vector3 targetPos = hitPoint - hitNormal * 0.5f;

    Vector3 localPos = targetPos - chunk.GlobalPosition;
    int bx = Mathf.FloorToInt(localPos.X);
    int by = Mathf.FloorToInt(localPos.Y);
    int bz = Mathf.FloorToInt(localPos.Z);

// Check one block above first - might be a non-solid decoration
BlockState blockAbove = chunk.GetBlock(bx, by + 1, bz);
if (blockAbove.BlockId == "rose" || blockAbove.BlockId == "clover")
{
    if (blockAbove.BlockId == "rose")
        _inventory.AddItem("rose", 1);
    chunk.SetBlock(bx, by + 1, bz, BlockState.Air);
    return;
}

    BlockState brokenBlock = chunk.GetBlock(bx, by, bz);
    if (!brokenBlock.IsAir())
    {
           // Rose drops item, clover just breaks
        if (brokenBlock.BlockId != "air" && brokenBlock.BlockId != "clover")
        _inventory.AddItem(brokenBlock.BlockId, 1);
        GD.Print($"Picked up: {brokenBlock.BlockId}, total now: {_inventory.GetItemCount(brokenBlock.BlockId)}");
    }

    chunk.SetBlock(bx, by, bz, BlockState.Air);
}

   private void TryPlaceBlock()
{
    if (!_rayCast.IsColliding()) return;

    if (!_inventory.HasItem(_selectedBlockId, 1))
    {
        GD.Print($"No {_selectedBlockId} in inventory to place");
        return;
    }

    var collider = _rayCast.GetCollider() as Node;
    if (collider == null || !collider.HasMeta("chunk")) return;

    Chunk hitChunk = (Chunk)collider.GetMeta("chunk").AsGodotObject();
    Vector3 hitPoint = _rayCast.GetCollisionPoint();
    Vector3 hitNormal = _rayCast.GetCollisionNormal();

    Vector3 worldTargetPos = hitPoint + hitNormal * 0.5f;

    Vector3 blockCenter = new Vector3(
        Mathf.Floor(worldTargetPos.X) + 0.5f,
        Mathf.Floor(worldTargetPos.Y) + 0.5f,
        Mathf.Floor(worldTargetPos.Z) + 0.5f
    );

    float playerDistance = blockCenter.DistanceTo(GlobalPosition);
    if (playerDistance < 0.9f)
    {
        GD.Print("Cannot place block - too close to player");
        return;
    }

    var chunkManager = hitChunk.GetParent() as ChunkManager;
    if (chunkManager == null) return;

    Vector3I chunkPos = chunkManager.WorldToChunk(worldTargetPos);
    Chunk targetChunk = chunkManager.GetChunk(chunkPos);
    if (targetChunk == null) return;

    Vector3 localPos = worldTargetPos - targetChunk.GlobalPosition;
    int bx = Mathf.FloorToInt(localPos.X);
    int by = Mathf.FloorToInt(localPos.Y);
    int bz = Mathf.FloorToInt(localPos.Z);

    var newBlock = new BlockState { BlockId = _selectedBlockId, BitMask = 0xFF };
    targetChunk.SetBlock(bx, by, bz, newBlock);

    _inventory.RemoveItem(_selectedBlockId, 1);
}

    public PlayerStats GetStats() => _stats;
    public PlayerCamera GetPlayerCamera() => _playerCamera;

    public void ApplyGearMovement(ItemResource gear)
    {
        if (gear.GrantsDoubleJump) CanDoubleJump = true;
        if (gear.GrantsWallClimb) CanWallClimb = true;
        if (gear.GrantsGliding) CanGlide = true;
        if (gear.GrantsGrapple) CanGrapple = true;
        WalkSpeed += gear.BonusMovementSpeed;
    }

    public void RemoveGearMovement(ItemResource gear)
    {
        if (gear.GrantsDoubleJump) CanDoubleJump = false;
        if (gear.GrantsWallClimb) CanWallClimb = false;
        if (gear.GrantsGliding) CanGlide = false;
        if (gear.GrantsGrapple) CanGrapple = false;
        WalkSpeed -= gear.BonusMovementSpeed;
    }
}