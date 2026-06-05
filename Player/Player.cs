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
    private bool _isSprinting = false;
    private bool _isCrouching = false;
    private bool _hasDoubleJumped = false;
    private bool _isGliding = false;

    public bool CanDoubleJump { get; set; } = false;
    public bool CanWallClimb { get; set; } = false;
    public bool CanGlide { get; set; } = false;
    public bool CanGrapple { get; set; } = false;

    public override void _Ready()
    {
        _stats = GetNodeOrNull<PlayerStats>("PlayerStats");
        _playerCamera = GetNodeOrNull<PlayerCamera>("PlayerCamera");

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
            if (_isGliding && CanGlide && velocity.Y < 0)
                velocity.Y -= (_gravity * 0.2f) * dt;
            else
                velocity.Y -= _gravity * dt;
        }
        else
        {
            _hasDoubleJumped = false;
            _isGliding = false;
        }

        if (Input.IsActionJustPressed("jump"))
        {
            if (IsOnFloor())
            {
                if (_stats.UseStamina(JumpStaminaCost))
                    velocity.Y = JumpVelocity;
            }
            else if (CanDoubleJump && !_hasDoubleJumped)
            {
                if (_stats.UseStamina(JumpStaminaCost))
                {
                    velocity.Y = JumpVelocity;
                    _hasDoubleJumped = true;
                }
            }
        }

        if (CanGlide && !IsOnFloor())
            _isGliding = Input.IsActionPressed("jump") && velocity.Y < 0;

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

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed
            && key.Keycode == Key.Escape)
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
                Input.MouseMode = Input.MouseModeEnum.Visible;
            else
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }
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