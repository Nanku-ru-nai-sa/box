using Godot;
using System.Collections.Generic;

// Mob — base class for all mobs. BehaviorType controls whether it just
// wanders (Passive) or notices and attacks the player (Hostile). Movement
// goes through VoxelPathfinder (real A* over your block grid), so mobs
// route around obstacles, step up ledges, and drop down ledges instead of
// walking straight at a target and getting stuck.
//
// PLACEHOLDER MODEL: builds a simple capsule+sphere in code so you're not
// blocked on art. When you have a real model, drop a .glb into something
// like res://Assets/Models/Mobs/ and load it in _Ready() instead of calling
// BuildPlaceholderMesh() — glTF bundles mesh + skeleton + animations in one
// file and Godot imports it natively.
public enum MobBehaviorType
{
    Passive,
    Hostile,
}

public partial class Mob : CharacterBody3D
{
    // Global toggle for the above-head health bar. Off by default per your
    // request — wire a checkbox in your settings menu to flip this, e.g.
    // Mob.ShowHealthBars = true; Even when true, a given mob's bar only
    // actually shows once it's taken damage (see UpdateHealthBar below).
    public static bool ShowHealthBars = false;

    [Export] public MobBehaviorType BehaviorType { get; set; } = MobBehaviorType.Passive;

    [Export] public float MaxHealth      { get; set; } = 10f;
    [Export] public float MoveSpeed      { get; set; } = 2.5f;
    [Export] public float TurnSpeed      { get; set; } = 8f;
    [Export] public float WanderRadius   { get; set; } = 6f;   // how far from where it spawned it'll wander
    [Export] public float MinIdleTime    { get; set; } = 2f;
    [Export] public float MaxIdleTime    { get; set; } = 6f;
    [Export] public float DetectionRange { get; set; } = 8f;   // hostile mobs notice the player within this range
    [Export] public float AttackRange    { get; set; } = 1.3f;
    [Export] public float AttackDamage   { get; set; } = 2f;
    [Export] public float AttackInterval { get; set; } = 1.2f; // seconds between hits while attacking
    [Export] public float FleeDistance   { get; set; } = 6f;
    [Export] public float FleeSpeedMultiplier { get; set; } = 1.8f;

    [ExportGroup("Hit Feedback")]
    [Export] public float KnockbackForce = 6f;
    [Export] public float KnockbackDuration = 0.25f; // AI pauses for this long while knocked back
    [Export] public float FlashDuration = 0.15f;

    private const float Gravity = 20f;
    private const float RepathInterval = 0.6f; // how often Chase/Flee re-request a path while their target keeps moving

    private float _health;
    private Vector3 _homePosition;
    private bool _hasLanded = false; // don't lock in a wander-home position until we've actually settled on the ground
    private Node3D _player;
    private Node3D _threat;
    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

    private enum State { Idle, Wander, Chase, Attack, Flee }
    private State _state = State.Idle;

    private float _idleTimer;
    private float _attackTimer;
    private float _repathTimer;
    private bool _fleeSpeedApplied = false;

    private Queue<Vector3> _currentPath = new Queue<Vector3>();
    private Vector3 _currentTarget;
    private bool _hasTarget = false;

    // Stuck detection: if we're supposed to be moving but haven't actually
    // gotten anywhere in a while (new block placed in our way, missed a
    // step-up, etc), abandon the current path so the state machine tries
    // a fresh one against the current world state instead of pushing
    // uselessly against an obstacle forever.
    private const float StuckCheckInterval = 0.5f;
    private const float StuckDistanceThreshold = 0.15f;
    private const int StuckChecksBeforeGivingUp = 2; // ~1 second of no progress
    private float _stuckCheckTimer;
    private Vector3 _stuckCheckLastPosition;
    private int _stuckCheckFailCount;

    // Hard ceiling on how long a single path attempt is allowed to run,
    // regardless of whether the mob is technically moving. Catches the case
    // the distance-based stuck check above can miss: jittering or hopping
    // in place near an obstacle still counts as "movement" each check, so a
    // mob can loop on a bad spot indefinitely without ever tripping it.
    [ExportGroup("Pathing Safety")]
    [Export] public float MaxPathDuration = 4f;

    // NEW: after a path is abandoned (timeout or stuck), wait this long
    // before trying to pathfind again at all. Without this, Chase state in
    // particular would just request a fresh path on its very next repath
    // tick and immediately re-hit the same obstacle.
    [Export] public float PathfindCooldown = 3f;
    private float _pathTimeoutTimer;
    private float _pathfindCooldownTimer; // NEW

    // Hit feedback state
    private Vector3 _knockbackVelocity;
    private float _knockbackTimer;
    private float _flashTimer;
    private StandardMaterial3D _bodyMaterial;
    private StandardMaterial3D _headMaterial;
    private Color _normalColor;

    // Health bar (built from two billboarded quads - background + fill)
    private MeshInstance3D _healthBarFill;
    private const float HealthBarWidth = 0.8f;
    private const float HealthBarHeight = 0.1f;
    private const float HealthBarYOffset = 2.05f;

    public override void _Ready()
    {
        _health = MaxHealth;
        _rng.Randomize();
        _idleTimer = _rng.RandfRange(MinIdleTime, MaxIdleTime);

        BuildPlaceholderMesh();
        BuildHealthBar();

        var shape = new CollisionShape3D();
        shape.Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.6f };
        shape.Position = new Vector3(0, 0.8f, 0);
        AddChild(shape);
    }

    private void BuildPlaceholderMesh()
    {
        _normalColor = BehaviorType == MobBehaviorType.Hostile
            ? new Color(0.75f, 0.15f, 0.15f)
            : new Color(0.35f, 0.65f, 0.35f);

        var body = new MeshInstance3D();
        body.Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.2f };
        body.Position = new Vector3(0, 0.8f, 0);
        _bodyMaterial = new StandardMaterial3D { AlbedoColor = _normalColor };
        body.MaterialOverride = _bodyMaterial;

        var head = new MeshInstance3D();
        head.Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
        head.Position = new Vector3(0, 1.55f, 0.15f); // pushed slightly forward so you can tell facing direction
        _headMaterial = new StandardMaterial3D { AlbedoColor = _normalColor };
        head.MaterialOverride = _headMaterial;

        AddChild(body);
        AddChild(head);
    }

    private void BuildHealthBar()
    {
        var bg = new MeshInstance3D();
        bg.Mesh = new QuadMesh { Size = new Vector2(HealthBarWidth, HealthBarHeight) };
        bg.Position = new Vector3(0, HealthBarYOffset, 0);
        var bgMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.1f, 0.1f, 0.1f, 0.85f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        bg.MaterialOverride = bgMat;
        bg.Visible = false;
        AddChild(bg);

        _healthBarFill = new MeshInstance3D();
        _healthBarFill.Mesh = new QuadMesh { Size = new Vector2(HealthBarWidth, HealthBarHeight) };
        // Sits a hair in front of the background (local Z) so it doesn't z-fight
        _healthBarFill.Position = new Vector3(0, HealthBarYOffset, 0.01f);
        var fillMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.85f, 0.2f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        _healthBarFill.MaterialOverride = fillMat;
        _healthBarFill.Visible = false;
        AddChild(_healthBarFill);

        // Keep a reference to the background too so UpdateHealthBar can toggle it alongside the fill
        _healthBarBackground = bg;
    }

    private MeshInstance3D _healthBarBackground;

    private void UpdateHealthBar()
    {
        bool shouldShow = ShowHealthBars && _health < MaxHealth && _health > 0f;
        _healthBarBackground.Visible = shouldShow;
        _healthBarFill.Visible = shouldShow;
        if (!shouldShow) return;

        float frac = Mathf.Clamp(_health / MaxHealth, 0f, 1f);
        var mesh = (QuadMesh)_healthBarFill.Mesh;
        mesh.Size = new Vector2(HealthBarWidth * frac, HealthBarHeight);
        // Anchor the shrinking bar to the left edge instead of shrinking from the center
        _healthBarFill.Position = new Vector3(-(HealthBarWidth * (1f - frac)) / 2f, HealthBarYOffset, 0.01f);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // NEW: always tick the pathfind cooldown down, regardless of state,
        // so it counts real time even while idle/knocked back/etc.
        if (_pathfindCooldownTimer > 0f)
            _pathfindCooldownTimer -= dt;

        if (_player == null)
        {
            var found = GetTree().Root.FindChild("player", true, false);
            _player = found as Node3D;
        }

        UpdateHitFeedback(dt);

        Vector3 velocity = Velocity;
        if (!IsOnFloor())
            velocity.Y -= Gravity * dt;
        else
            velocity.Y = 0f;

        if (!_hasLanded)
        {
            // Falling from wherever it was placed in the editor - don't lock
            // in a wander-home position or run any AI until it's actually
            // settled on solid ground, otherwise home ends up wherever it
            // spawned (possibly mid-air) instead of where it lands.
            if (IsOnFloor())
            {
                _hasLanded = true;
                _homePosition = GlobalPosition;
            }
            Velocity = velocity;
            MoveAndSlide();
            return;
        }

        if (_knockbackTimer > 0f)
        {
            // Knocked back - AI is paused, just ride out the impulse (which
            // decays to zero over KnockbackDuration) plus gravity.
            velocity.X = _knockbackVelocity.X;
            velocity.Z = _knockbackVelocity.Z;
            Velocity = velocity;
            MoveAndSlide();
            return;
        }

        UpdateState(dt);
        RunState(dt);
        ApplyPathMovement(dt, ref velocity);
        UpdateHealthBar();

        Velocity = velocity;
        MoveAndSlide();
    }

    // ---- hit feedback: flash + knockback decay ----

    private void UpdateHitFeedback(float dt)
    {
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= dt;
            float t = Mathf.Clamp(_knockbackTimer / KnockbackDuration, 0f, 1f);
            _knockbackVelocity = _knockbackVelocity.Normalized() * (_knockbackVelocity.Length() * t + 0.001f);
            if (_knockbackTimer <= 0f)
            {
                _knockbackVelocity = Vector3.Zero;
                // Reset stuck-detection so getting shoved around doesn't
                // immediately trip it into abandoning a perfectly good path.
                _stuckCheckFailCount = 0;
                _stuckCheckLastPosition = GlobalPosition;
            }
        }

        if (_flashTimer > 0f)
        {
            _flashTimer -= dt;
            if (_flashTimer <= 0f)
            {
                _bodyMaterial.AlbedoColor = _normalColor;
                _headMaterial.AlbedoColor = _normalColor;
            }
        }
    }

    // ---- movement along whatever path we currently have ----

    private void ApplyPathMovement(float dt, ref Vector3 velocity)
    {
        if (_hasTarget)
        {
            CheckIfStuck(dt);

            _pathTimeoutTimer += dt;
            if (_pathTimeoutTimer > MaxPathDuration)
            {
                AbandonPath();
                return;
            }

            Vector3 toTarget = _currentTarget - GlobalPosition;
            float verticalDiff = toTarget.Y;
            toTarget.Y = 0;

            if (toTarget.Length() < 0.3f && Mathf.Abs(verticalDiff) < 0.6f)
            {
                AdvancePath();
            }
            else
            {
                Vector3 dir = toTarget.Length() > 0.01f ? toTarget.Normalized() : Vector3.Zero;
                velocity.X = dir.X * MoveSpeed;
                velocity.Z = dir.Z * MoveSpeed;
                if (dir != Vector3.Zero) FaceDirection(dir, dt);

                // Step up onto a higher block: the pathfinder already only
                // ever plans steps of +1 block, so a small hop is always
                // enough - this isn't a general jump, just enough impulse
                // to clear one block's height.
                if (verticalDiff > 0.5f && IsOnFloor())
                {
                    velocity.Y = Mathf.Sqrt(2f * Gravity * 1.15f);
                }
            }
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, MoveSpeed * dt * 4f);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0, MoveSpeed * dt * 4f);
        }
    }

    // ---- state machine: decides what state we SHOULD be in ----

    private void UpdateState(float dt)
    {
        if (_state == State.Flee)
        {
            bool threatGone = _threat == null || !IsInstanceValid(_threat);
            bool threatFar = !threatGone && GlobalPosition.DistanceTo(_threat.GlobalPosition) > FleeDistance * 1.5f;
            if (threatGone || threatFar)
            {
                if (_fleeSpeedApplied) { MoveSpeed /= FleeSpeedMultiplier; _fleeSpeedApplied = false; }
                EnterIdle();
            }
            return;
        }

        if (BehaviorType == MobBehaviorType.Hostile && _player != null)
        {
            float distToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);

            if (distToPlayer <= AttackRange)
            {
                _state = State.Attack;
                _hasTarget = false; // stand still while attacking
                return;
            }
            if (distToPlayer <= DetectionRange)
            {
                if (_state != State.Chase) { _state = State.Chase; _repathTimer = 0f; }
                return;
            }
            if (_state == State.Chase || _state == State.Attack)
                EnterIdle(); // lost the player - go back to wandering
        }

        if (_state == State.Idle)
        {
            _idleTimer -= dt;
            if (_idleTimer <= 0f) PickWanderTarget();
        }
        else if (_state == State.Wander && !_hasTarget)
        {
            EnterIdle();
        }
    }

    // ---- state machine: does the actual work for the current state ----

    private void RunState(float dt)
    {
        switch (_state)
        {
            case State.Chase:
                _repathTimer -= dt;
                if (_repathTimer <= 0f)
                {
                    _repathTimer = RepathInterval;
                    RequestPathTo(_player.GlobalPosition, 32);
                }
                break;

            case State.Attack:
                _attackTimer -= dt;
                if (_attackTimer <= 0f)
                {
                    _attackTimer = AttackInterval;
                    // TODO: hook this up to your actual player-damage method,
                    // e.g. _player.GetStats().TakeDamage(AttackDamage);
                    GD.Print($"{Name} hits the player for {AttackDamage}");
                }
                break;
        }
    }

    private void EnterIdle()
    {
        _state = State.Idle;
        _idleTimer = _rng.RandfRange(MinIdleTime, MaxIdleTime);
    }

    private void PickWanderTarget()
    {
        Vector2 offset = new Vector2(
            _rng.RandfRange(-WanderRadius, WanderRadius),
            _rng.RandfRange(-WanderRadius, WanderRadius));
        Vector3 target = _homePosition + new Vector3(offset.X, 0, offset.Y);

        if (RequestPathTo(target, 24))
            _state = State.Wander;
        else
            _idleTimer = _rng.RandfRange(MinIdleTime, MaxIdleTime); // couldn't find a route - wait and try again later
    }

    // ---- pathing helpers ----

    private bool RequestPathTo(Vector3 worldTarget, int maxRange)
    {
        // NEW: refuse to even attempt a new path while the cooldown from a
        // recently abandoned path is still active. Covers both Wander
        // (via PickWanderTarget) and Chase (via RunState's repath) with a
        // single guard.
        if (_pathfindCooldownTimer > 0f) return false;

        var path = VoxelPathfinder.FindPath(GlobalPosition, worldTarget, maxRange);
        if (path == null || path.Count == 0) return false;
        _currentPath = new Queue<Vector3>(path);
        _pathTimeoutTimer = 0f;
        AdvancePath();
        return true;
    }

    // If we've had a target for a while but haven't actually covered any
    // ground, something's physically blocking us (a placed block, a missed
    // step, etc) - give up on this path so the state machine requests a
    // fresh one against the current world state instead of pushing against
    // the obstacle forever.
    private void CheckIfStuck(float dt)
    {
        _stuckCheckTimer += dt;
        if (_stuckCheckTimer < StuckCheckInterval) return;
        _stuckCheckTimer = 0f;

        float moved = GlobalPosition.DistanceTo(_stuckCheckLastPosition);
        _stuckCheckLastPosition = GlobalPosition;

        if (moved < StuckDistanceThreshold)
        {
            _stuckCheckFailCount++;
            if (_stuckCheckFailCount >= StuckChecksBeforeGivingUp)
            {
                AbandonPath();
            }
        }
        else
        {
            _stuckCheckFailCount = 0;
        }
    }

    // Give up on whatever path we're currently following (timeout or stuck)
    // and start a cooldown before the next pathfind attempt is allowed, so
    // the mob visibly pauses instead of immediately requesting a new path
    // against the same obstacle.
    private void AbandonPath() // NEW
    {
        _currentPath.Clear();
        _hasTarget = false;
        _pathTimeoutTimer = 0f;
        _stuckCheckFailCount = 0;
        _pathfindCooldownTimer = PathfindCooldown;
    }

    private void AdvancePath()
    {
        if (_currentPath.Count > 0)
        {
            _currentTarget = _currentPath.Dequeue();
            _hasTarget = true;
        }
        else
        {
            _hasTarget = false;
        }
    }

    private void FaceDirection(Vector3 dir, float dt)
    {
        if (dir.LengthSquared() < 0.0001f) return;
        Transform3D targetXform = Transform3D.Identity.LookingAt(dir, Vector3.Up);
        Quaternion current = Transform.Basis.GetRotationQuaternion();
        Quaternion target = targetXform.Basis.GetRotationQuaternion();
        Quaternion smoothed = current.Slerp(target, Mathf.Clamp(TurnSpeed * dt, 0f, 1f));
        Transform3D t = Transform;
        t.Basis = new Basis(smoothed);
        Transform = t;
    }

    // ---- damage / death ----

    // Call this from the player's attack code when a raycast/hit detects a
    // mob. sourcePosition (usually the player's position) decides which way
    // the knockback pushes - if you don't have it handy, it falls back to
    // pushing away from wherever the player currently is.
    public void TakeDamage(float amount, Vector3? sourcePosition = null)
    {
        _health -= amount;

        Vector3 source = sourcePosition ?? (_player?.GlobalPosition ?? GlobalPosition - Vector3.Forward);
        Vector3 away = (GlobalPosition - source);
        away.Y = 0;
        away = away.Length() > 0.01f ? away.Normalized() : Vector3.Forward;

        _knockbackVelocity = away * KnockbackForce;
        _knockbackTimer = KnockbackDuration;

        _bodyMaterial.AlbedoColor = Colors.White;
        _headMaterial.AlbedoColor = Colors.White;
        _flashTimer = FlashDuration;

        if (BehaviorType == MobBehaviorType.Passive && _player != null && _state != State.Flee)
        {
            _threat = _player;
            _state = State.Flee;
            if (!_fleeSpeedApplied) { MoveSpeed *= FleeSpeedMultiplier; _fleeSpeedApplied = true; }
        }

        if (_health <= 0f)
            Die();
    }

    private void Die()
    {
        // TODO: drop an item here once crafting/items are further along -
        // e.g. spawn a pickup, or call into whatever your inventory pickup
        // system uses for dropped blocks/items.
        QueueFree();
    }
}