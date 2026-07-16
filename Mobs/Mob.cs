using Godot;

// Mob — base class for all mobs. BehaviorType controls whether it just
// wanders (Passive) or notices and attacks the player (Hostile). Add more
// values to MobBehaviorType + a case in RunState()/UpdateState() below when
// you want new behaviors (e.g. Neutral = only retaliates if hit).
//
// PLACEHOLDER MODEL: this builds a simple capsule+sphere in code so you're
// not blocked on art. When you have a real model, drop a .glb into
// something like res://Assets/Models/Mobs/ and load it in _Ready() instead
// of calling BuildPlaceholderMesh() — glTF is the best format for mobs
// since it bundles mesh + skeleton + animations in one file and Godot
// imports it natively.
public enum MobBehaviorType
{
    Passive,
    Hostile,
}

public partial class Mob : CharacterBody3D
{
    [Export] public MobBehaviorType BehaviorType { get; set; } = MobBehaviorType.Passive;

    [Export] public float MaxHealth      { get; set; } = 10f;
    [Export] public float MoveSpeed      { get; set; } = 2.5f;
    [Export] public float WanderRadius   { get; set; } = 6f;   // how far from where it spawned it'll wander
    [Export] public float DetectionRange { get; set; } = 8f;   // hostile mobs notice the player within this range
    [Export] public float AttackRange    { get; set; } = 1.3f;
    [Export] public float AttackDamage   { get; set; } = 2f;
    [Export] public float AttackInterval { get; set; } = 1.2f; // seconds between hits while attacking

    private const float Gravity = 20f;

    private float    _health;
    private Vector3  _homePosition;
    private Vector3  _wanderTarget;
    private float    _wanderPauseTimer = 0f;
    private float    _attackTimer = 0f;
    private float    _fleeTimer = 0f;
    private Node3D   _player;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    private enum State { Wander, Chase, Attack, Flee }
    private State _state = State.Wander;

    public override void _Ready()
    {
        _health = MaxHealth;
        _homePosition = GlobalPosition;
        _rng.Randomize();
        PickNewWanderTarget();

        BuildPlaceholderMesh();

        var shape = new CollisionShape3D();
        shape.Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.6f };
        shape.Position = new Vector3(0, 0.8f, 0);
        AddChild(shape);
    }

    private void BuildPlaceholderMesh()
    {
        var body = new MeshInstance3D();
        body.Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.2f };
        body.Position = new Vector3(0, 0.8f, 0);

        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
        head.Position = new Vector3(0, 1.55f, 0.15f); // pushed slightly forward so you can tell facing direction

        // Green for passive, red for hostile - just so you can tell them
        // apart at a glance until real models are in. Swap this whole
        // method out once you have a .glb to load instead.
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = BehaviorType == MobBehaviorType.Hostile
            ? new Color(0.75f, 0.15f, 0.15f)
            : new Color(0.35f, 0.65f, 0.35f);
        body.MaterialOverride = mat;
        head.MaterialOverride = mat;

        AddChild(body);
        AddChild(head);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_player == null)
        {
            var found = GetTree().Root.FindChild("player", true, false);
            _player = found as Node3D;
        }

        Vector3 velocity = Velocity;
        if (!IsOnFloor())
            velocity.Y -= Gravity * dt;
        else
            velocity.Y = 0f;

        UpdateState(dt);
        Vector3 moveDir = RunState(dt);

        velocity.X = moveDir.X * MoveSpeed;
        velocity.Z = moveDir.Z * MoveSpeed;

        Velocity = velocity;
        MoveAndSlide();
    }

    // Decides which state we SHOULD be in based on distance to player etc.
    // Kept separate from RunState (which does the actual movement/action)
    // so adding new behaviors later is just "add a branch here + a case
    // below" instead of untangling movement code.
    private void UpdateState(float dt)
    {
        if (_state == State.Flee)
        {
            _fleeTimer -= dt;
            if (_fleeTimer <= 0f) _state = State.Wander;
            return;
        }

        if (BehaviorType == MobBehaviorType.Hostile && _player != null)
        {
            float distToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);

            if (distToPlayer <= AttackRange)
            {
                _state = State.Attack;
                return;
            }
            if (distToPlayer <= DetectionRange)
            {
                _state = State.Chase;
                return;
            }
        }

        if (_state == State.Chase || _state == State.Attack)
            _state = State.Wander; // lost the player - go back to wandering
    }

    // Returns the horizontal movement direction (not yet scaled by speed)
    // for whatever state we're currently in.
    private Vector3 RunState(float dt)
    {
        switch (_state)
        {
            case State.Chase:
            {
                Vector3 toPlayer = _player.GlobalPosition - GlobalPosition;
                toPlayer.Y = 0;
                return toPlayer.Length() > 0.1f ? toPlayer.Normalized() : Vector3.Zero;
            }

            case State.Attack:
            {
                _attackTimer -= dt;
                if (_attackTimer <= 0f)
                {
                    _attackTimer = AttackInterval;
                    // TODO: hook this up to your actual player-damage method,
                    // e.g. _player.GetStats().TakeDamage(AttackDamage);
                    // Left as a print for now since PlayerStats' damage API
                    // wasn't in front of me - just swap this one line.
                    GD.Print($"{Name} hits the player for {AttackDamage}");
                }
                return Vector3.Zero; // stand still while attacking
            }

            case State.Flee:
            {
                if (_player == null) return Vector3.Zero;
                Vector3 away = GlobalPosition - _player.GlobalPosition;
                away.Y = 0;
                return away.Length() > 0.1f ? away.Normalized() : Vector3.Zero;
            }

            case State.Wander:
            default:
                return RunWander(dt);
        }
    }

    private Vector3 RunWander(float dt)
    {
        Vector3 toTarget = _wanderTarget - GlobalPosition;
        toTarget.Y = 0;

        if (toTarget.Length() < 0.5f)
        {
            _wanderPauseTimer -= dt;
            if (_wanderPauseTimer <= 0f)
                PickNewWanderTarget();
            return Vector3.Zero;
        }

        return toTarget.Normalized();
    }

    private void PickNewWanderTarget()
    {
        float angle  = _rng.RandfRange(0f, Mathf.Tau);
        float radius = _rng.RandfRange(1f, WanderRadius);
        _wanderTarget = _homePosition + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        _wanderPauseTimer = _rng.RandfRange(1.5f, 4f);
    }

    // Call this from the player's attack code when a raycast hits a mob.
    public void TakeDamage(float amount)
    {
        _health -= amount;

        if (BehaviorType == MobBehaviorType.Passive)
        {
            _state = State.Flee;
            _fleeTimer = 2.5f;
        }

        if (_health <= 0f)
            Die();
    }

    private void Die()
    {
        // TODO: drop an item here once crafting/items are further along -
        // e.g. spawn a pickup, or call into the same AddItemToInventory-style
        // system your ChunkManager blocks use.
        QueueFree();
    }
}