using Godot;

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
    private float _placeTimer = 0f;
    private const float PlaceInterval = 0.15f; // seconds between placements while held
    private bool _isBreaking = false;
private float _breakTimer = 0f;
private const float BreakInterval = 0.15f;

    public bool CanDoubleJump { get; set; } = false;
    public bool CanWallClimb { get; set; } = false;
    public bool CanGlide { get; set; } = false;
    public bool CanGrapple { get; set; } = false;

    public override void _Ready()
    {
        _stats = GetNodeOrNull<PlayerStats>("PlayerStats");
        _playerCamera = GetNodeOrNull<PlayerCamera>("PlayerCamera");
        _rayCast = GetNode<RayCast3D>("PlayerCamera/Camera3D/RayCast3D");_rayCast.AddException(this);

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
        GD.Print("Player ready.");
    }

    public override void _PhysicsProcess(double delta)
{
    if (_stats == null || _stats.IsDead) return;

    float dt = (float)delta;
    Vector3 velocity = Velocity;

    if (!IsOnFloor())
    {
        velocity.Y -= _gravity * dt;
    }
    else
    {
        _hasDoubleJumped = false;
        _isGliding = false;
    }

    if (Input.IsActionJustPressed("jump") && IsOnFloor())
    {
            velocity.Y = JumpVelocity;
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
}
    private void TryBreakBlock()
{
    if (!_rayCast.IsColliding()) return;

    var collider = _rayCast.GetCollider() as Node;
    if (collider == null || !collider.HasMeta("chunk")) return;

    Chunk chunk = (Chunk)collider.GetMeta("chunk").AsGodotObject();
    Vector3 hitPoint = _rayCast.GetCollisionPoint();
    Vector3 hitNormal = _rayCast.GetCollisionNormal();

    // Step slightly INTO the block we're looking at (not the surface)
    Vector3 targetPos = hitPoint - hitNormal * 0.5f;

    Vector3 localPos = targetPos - chunk.GlobalPosition;
    int bx = Mathf.FloorToInt(localPos.X);
    int by = Mathf.FloorToInt(localPos.Y);
    int bz = Mathf.FloorToInt(localPos.Z);

    GD.Print($"Breaking block at local ({bx},{by},{bz}) in chunk {chunk.ChunkPosition}");

    chunk.SetBlock(bx, by, bz, BlockState.Air);
}

private void TryPlaceBlock()
{
    if (!_rayCast.IsColliding()) return;

    var collider = _rayCast.GetCollider() as Node;
    if (collider == null || !collider.HasMeta("chunk")) return;

    Chunk hitChunk = (Chunk)collider.GetMeta("chunk").AsGodotObject();
    Vector3 hitPoint = _rayCast.GetCollisionPoint();
    Vector3 hitNormal = _rayCast.GetCollisionNormal();

    Vector3 worldTargetPos = hitPoint + hitNormal * 0.5f;

    // Prevent placing inside the player
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

    var newBlock = new BlockState { BlockId = "stone", BitMask = 0xFF };
    targetChunk.SetBlock(bx, by, bz, newBlock);
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