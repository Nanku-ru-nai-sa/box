using Godot;
using System.Collections.Generic;

// Mob — base class for all mobs.
//
// Mob-specific information is loaded from JSON through MobDefinition.
// This keeps the C# Mob class generic so pigs, cows, chickens, etc.
// can eventually use the same Mob class with different definitions.
//
// Current systems:
// - Passive / Hostile behavior
// - VoxelPathfinder movement
// - Wandering
// - Hostile chase / attack
// - Passive flee when damaged
// - Knockback
// - Health
// - Health bar
// - GLTF model loading
// - Walking leg animation
// - Male / Female gender
// - Happiness based on full HP
// - Food / breeding data from JSON
//
// Planned systems:
// - Mob spawning
// - Feeding
// - Breeding
// - Baby mobs / growth
// - Ragdoll death
// - Grab / drag dead mobs

public enum MobBehaviorType
{
    Passive,
    Hostile
}

public partial class Mob : CharacterBody3D
{
    // =========================================================
    // MOB DEFINITION
    // =========================================================

    [ExportGroup("Mob Definition")]

    [Export]
    public string DefinitionPath { get; set; } =
        "res://Mobs/Definitions/Animals/pig.json";

    private MobDefinition _definition;


    // =========================================================
    // GLOBAL SETTINGS
    // =========================================================

    public static bool ShowHealthBars = false;


    // =========================================================
    // BASIC MOB SETTINGS
    // =========================================================

    [Export]
    public MobBehaviorType BehaviorType { get; set; } =
        MobBehaviorType.Passive;

    [Export]
    public float MaxHealth { get; set; } = 10f;

    [Export]
    public float MoveSpeed { get; set; } = 2.5f;

    [Export]
    public float TurnSpeed { get; set; } = 8f;

    [Export]
    public float WanderRadius { get; set; } = 6f;

    [Export]
    public float MinIdleTime { get; set; } = 2f;

    [Export]
    public float MaxIdleTime { get; set; } = 6f;

    [Export]
    public float DetectionRange { get; set; } = 8f;

    [Export]
    public float AttackRange { get; set; } = 1.3f;

    [Export]
    public float AttackDamage { get; set; } = 2f;

    [Export]
    public float AttackInterval { get; set; } = 1.2f;

    [Export]
    public float FleeDistance { get; set; } = 6f;

    [Export]
    public float FleeSpeedMultiplier { get; set; } = 1.8f;

    private bool _fleeEnabled = true;
    private bool _breedingReady = false;
    private float _breedCooldownTimer = 0f;


    // =========================================================
    // HIT FEEDBACK
    // =========================================================

    [ExportGroup("Hit Feedback")]

    [Export]
    public float KnockbackForce = 6f;

    [Export]
    public float KnockbackDuration = 0.25f;

    [Export]
    public float FlashDuration = 0.15f;


    // =========================================================
    // MOVEMENT / PATHING
    // =========================================================

    private const float Gravity = 20f;
    private const float RepathInterval = 0.6f;

    private const float StuckCheckInterval = 0.5f;
    private const float StuckDistanceThreshold = 0.15f;
    private const int StuckChecksBeforeGivingUp = 2;

    [ExportGroup("Pathing Safety")]

    [Export]
    public float MaxPathDuration = 4f;

    [Export]
    public float PathfindCooldown = 3f;


    // =========================================================
    // MOB STATE
    // =========================================================

    private float _health;

    private Vector3 _homePosition;

    private bool _hasLanded = false;

    private Node3D _player;
    private Node3D _threat;

    private readonly RandomNumberGenerator _rng =
        new RandomNumberGenerator();

    private enum State
    {
        Idle,
        Wander,
        Chase,
        Attack,
        Flee
    }

    private State _state = State.Idle;

    private float _idleTimer;
    private float _attackTimer;
    private float _repathTimer;

    private bool _fleeSpeedApplied = false;
    
private bool _isBreeding = false;
private const float BreedSearchRadius = 8f;


    // =========================================================
    // PATH
    // =========================================================

    private Queue<Vector3> _currentPath =
        new Queue<Vector3>();

    private Vector3 _currentTarget;

    private bool _hasTarget = false;


    // =========================================================
    // STUCK DETECTION
    // =========================================================

    private float _stuckCheckTimer;

    private Vector3 _stuckCheckLastPosition;

    private int _stuckCheckFailCount;

    private float _pathTimeoutTimer;

    private float _pathfindCooldownTimer;


    // =========================================================
    // HIT FEEDBACK STATE
    // =========================================================

    private Vector3 _knockbackVelocity;

    private float _knockbackTimer;

    private float _flashTimer;


    // =========================================================
    // HEALTH BAR
    // =========================================================

    private MeshInstance3D _healthBarFill;

    private MeshInstance3D _healthBarBackground;

    private const float HealthBarWidth = 0.8f;
    private const float HealthBarHeight = 0.1f;
    private const float HealthBarYOffset = 2.05f;


    // =========================================================
    // MODEL / ANIMATION
    // =========================================================

    private Node3D _mobModel;

    private Node3D _frontRightLeg;
    private Node3D _frontLeftLeg;
    private Node3D _backRightLeg;
    private Node3D _backLeftLeg;

    private float _walkAnimationTime = 0f;


    // =========================================================
    // GENDER
    // =========================================================

    private MobGender _gender;

    public MobGender Gender => _gender;


    // =========================================================
    // PUBLIC INFORMATION
    // =========================================================

    public MobDefinition Definition => _definition;

    public float Health => _health;

    /// <summary>
    /// For now HP = happiness.
    /// A mob at full HP is considered happy.
    /// </summary>
    public bool IsHappy =>
        _health >= MaxHealth;

    /// <summary>
    /// Currently breeding requires full HP.
    /// Breed food and partner checks will be handled
    /// by the future breeding system.
    /// </summary>
    public bool IsHappyEnoughToBreed =>
        IsHappy;

    public bool CanBreed
    {
        get
        {
            return _definition != null &&
                   _definition.breeding != null &&
                   _definition.breeding.enabled &&
                   IsHappy;
        }
    }


    // =========================================================
    // READY
    // =========================================================

    public override void _Ready()
    {
        AddToGroup("mobs");
        _rng.Randomize();

        LoadDefinition();

        BuildMobModel();

        BuildHealthBar();

        CreateCollision();

        AssignGender();

        _health = MaxHealth;

        _idleTimer = _rng.RandfRange(
            MinIdleTime,
            MaxIdleTime
        );
    }


    // =========================================================
    // COLLISION
    // =========================================================

    private void CreateCollision()
    {
        var shape = new CollisionShape3D();

        shape.Shape = new CapsuleShape3D
        {
            Radius = 0.4f,
            Height = 1.6f
        };

        shape.Position = new Vector3(
            0,
            0.8f,
            0
        );

        AddChild(shape);
    }


    // =========================================================
    // LOAD JSON DEFINITION
    // =========================================================

    private void LoadDefinition()
    {
        _definition =
            MobDefinitionLoader.Load(
                DefinitionPath
            );

        if (_definition == null)
        {
            GD.PrintErr(
                $"[Mob] Could not load definition: {DefinitionPath}"
            );

            return;
        }


        // -----------------------------------------------------
        // STATS
        // -----------------------------------------------------

        if (_definition.stats != null)
        {
            MaxHealth =
                _definition.stats.maxHealth;

            MoveSpeed =
                _definition.stats.moveSpeed;

            TurnSpeed =
                _definition.stats.turnSpeed;
        }


        // -----------------------------------------------------
        // BEHAVIOR
        // -----------------------------------------------------

        if (_definition.behavior != null)
        {
            string behavior =
                _definition.behavior.type ?? "passive";

            if (behavior.ToLowerInvariant() == "hostile")
            {
                BehaviorType =
                    MobBehaviorType.Hostile;
            }
            else
            {
                BehaviorType =
                    MobBehaviorType.Passive;
            }


            WanderRadius =
                _definition.behavior.wanderRadius;

            MinIdleTime =
                _definition.behavior.minIdleTime;

            MaxIdleTime =
                _definition.behavior.maxIdleTime;

            DetectionRange =
                _definition.behavior.detectionRange;

            AttackRange =
                _definition.behavior.attackRange;

            AttackDamage =
                _definition.behavior.attackDamage;

            AttackInterval =
                _definition.behavior.attackInterval;
        }


        // -----------------------------------------------------
        // FLEE
        // -----------------------------------------------------

        if (_definition.flee != null)
        {
            _fleeEnabled =
                _definition.flee.enabled;

            FleeDistance =
                _definition.flee.distance;

            FleeSpeedMultiplier =
                _definition.flee.speedMultiplier;
        }


        GD.Print(
            $"[Mob] Loaded definition: {_definition.displayName}"
        );
    }


    // =========================================================
    // GENDER
    // =========================================================

    private void AssignGender()
    {
        if (_definition == null ||
            _definition.gender == null ||
            !_definition.gender.enabled)
        {
            _gender = MobGender.Male;
            return;
        }


        float maleChance =
            Mathf.Max(
                0f,
                _definition.gender.maleChance
            );

        float femaleChance =
            Mathf.Max(
                0f,
                _definition.gender.femaleChance
            );


        float total =
            maleChance +
            femaleChance;


        if (total <= 0f)
        {
            maleChance = 0.5f;
            femaleChance = 0.5f;

            total = 1f;
        }


        float roll =
            _rng.RandfRange(
                0f,
                total
            );


        if (roll < maleChance)
        {
            _gender =
                MobGender.Male;
        }
        else
        {
            _gender =
                MobGender.Female;
        }


        if (_definition != null)
        {
            GD.Print(
                $"[Mob] {_definition.displayName} spawned as {_gender}"
            );
        }
    }


    // =========================================================
    // MODEL
    // =========================================================

    private void BuildMobModel()
    {
        string modelPath =
            "res://Assets/Models/Mobs/pig.gltf";


        if (_definition != null &&
            !string.IsNullOrEmpty(
                _definition.model))
        {
            modelPath =
                _definition.model;
        }


        var scene =
            GD.Load<PackedScene>(
                modelPath
            );


        if (scene == null)
        {
            GD.PrintErr(
                $"[Mob] Could not load mob model: {modelPath}"
            );

            return;
        }


        _mobModel =
            scene.Instantiate<Node3D>();

        AddChild(_mobModel);


        _frontRightLeg =
            _mobModel.FindChild(
                "front_right",
                true,
                false
            ) as Node3D;


        _frontLeftLeg =
            _mobModel.FindChild(
                "front_left",
                true,
                false
            ) as Node3D;


        _backRightLeg =
            _mobModel.FindChild(
                "back_right",
                true,
                false
            ) as Node3D;


        _backLeftLeg =
            _mobModel.FindChild(
                "back_left",
                true,
                false
            ) as Node3D;


        if (_frontRightLeg == null ||
            _frontLeftLeg == null ||
            _backRightLeg == null ||
            _backLeftLeg == null)
        {
            GD.PrintErr(
                $"[Mob] Could not find all four leg nodes in {modelPath}"
            );
        }


        _mobModel.Scale =
            new Vector3(
                1f,
                1f,
                1f
            );
    }


    // =========================================================
    // WALK ANIMATION
    // =========================================================

    private void UpdateWalkAnimation(float dt)
    {
        if (_mobModel == null ||
            _frontRightLeg == null ||
            _frontLeftLeg == null ||
            _backRightLeg == null ||
            _backLeftLeg == null)
        {
            return;
        }


        bool walking =
            Mathf.Abs(Velocity.X) > 0.05f ||
            Mathf.Abs(Velocity.Z) > 0.05f;


        if (!walking)
        {
            _frontRightLeg.Rotation =
                new Vector3(
                    Mathf.LerpAngle(
                        _frontRightLeg.Rotation.X,
                        0f,
                        dt * 8f
                    ),
                    _frontRightLeg.Rotation.Y,
                    _frontRightLeg.Rotation.Z
                );


            _frontLeftLeg.Rotation =
                new Vector3(
                    Mathf.LerpAngle(
                        _frontLeftLeg.Rotation.X,
                        0f,
                        dt * 8f
                    ),
                    _frontLeftLeg.Rotation.Y,
                    _frontLeftLeg.Rotation.Z
                );


            _backRightLeg.Rotation =
                new Vector3(
                    Mathf.LerpAngle(
                        _backRightLeg.Rotation.X,
                        0f,
                        dt * 8f
                    ),
                    _backRightLeg.Rotation.Y,
                    _backRightLeg.Rotation.Z
                );


            _backLeftLeg.Rotation =
                new Vector3(
                    Mathf.LerpAngle(
                        _backLeftLeg.Rotation.X,
                        0f,
                        dt * 8f
                    ),
                    _backLeftLeg.Rotation.Y,
                    _backLeftLeg.Rotation.Z
                );


            return;
        }


        float speed =
            new Vector2(
                Velocity.X,
                Velocity.Z
            ).Length();


        float animationSpeed =
            Mathf.Clamp(
                speed / Mathf.Max(MoveSpeed, 0.01f),
                0.5f,
                2f
            );


        _walkAnimationTime +=
            dt *
            animationSpeed *
            7f;


        float swing =
            Mathf.Sin(
                _walkAnimationTime
            ) * 0.45f;


        _frontRightLeg.Rotation =
            new Vector3(
                swing,
                _frontRightLeg.Rotation.Y,
                _frontRightLeg.Rotation.Z
            );


        _backLeftLeg.Rotation =
            new Vector3(
                swing,
                _backLeftLeg.Rotation.Y,
                _backLeftLeg.Rotation.Z
            );


        _frontLeftLeg.Rotation =
            new Vector3(
                -swing,
                _frontLeftLeg.Rotation.Y,
                _frontLeftLeg.Rotation.Z
            );


        _backRightLeg.Rotation =
            new Vector3(
                -swing,
                _backRightLeg.Rotation.Y,
                _backRightLeg.Rotation.Z
            );
    }


    // =========================================================
    // HEALTH BAR
    // =========================================================

    private void BuildHealthBar()
    {
        var bg =
            new MeshInstance3D();


        bg.Mesh =
            new QuadMesh
            {
                Size =
                    new Vector2(
                        HealthBarWidth,
                        HealthBarHeight
                    )
            };


        bg.Position =
            new Vector3(
                0,
                HealthBarYOffset,
                0
            );


        var bgMat =
            new StandardMaterial3D
            {
                AlbedoColor =
                    new Color(
                        0.1f,
                        0.1f,
                        0.1f,
                        0.85f
                    ),

                ShadingMode =
                    BaseMaterial3D.ShadingModeEnum.Unshaded,

                BillboardMode =
                    BaseMaterial3D.BillboardModeEnum.Enabled,

                Transparency =
                    BaseMaterial3D.TransparencyEnum.Alpha
            };


        bg.MaterialOverride =
            bgMat;

        bg.Visible = false;

        AddChild(bg);

        _healthBarBackground =
            bg;


        _healthBarFill =
            new MeshInstance3D();


        _healthBarFill.Mesh =
            new QuadMesh
            {
                Size =
                    new Vector2(
                        HealthBarWidth,
                        HealthBarHeight
                    )
            };


        _healthBarFill.Position =
            new Vector3(
                0,
                HealthBarYOffset,
                0.01f
            );


        var fillMat =
            new StandardMaterial3D
            {
                AlbedoColor =
                    new Color(
                        0.2f,
                        0.85f,
                        0.2f
                    ),

                ShadingMode =
                    BaseMaterial3D.ShadingModeEnum.Unshaded,

                BillboardMode =
                    BaseMaterial3D.BillboardModeEnum.Enabled
            };


        _healthBarFill.MaterialOverride =
            fillMat;

        _healthBarFill.Visible = false;

        AddChild(_healthBarFill);
    }


    private void UpdateHealthBar()
    {
        if (_healthBarBackground == null ||
            _healthBarFill == null)
        {
            return;
        }


        bool shouldShow =
            ShowHealthBars &&
            _health < MaxHealth &&
            _health > 0f;


        _healthBarBackground.Visible =
            shouldShow;

        _healthBarFill.Visible =
            shouldShow;


        if (!shouldShow)
            return;


        float frac =
            Mathf.Clamp(
                _health / MaxHealth,
                0f,
                1f
            );


        var mesh =
            _healthBarFill.Mesh as QuadMesh;


        if (mesh == null)
            return;


        mesh.Size =
            new Vector2(
                HealthBarWidth * frac,
                HealthBarHeight
            );


        _healthBarFill.Position =
            new Vector3(
                -(HealthBarWidth *
                  (1f - frac)) / 2f,

                HealthBarYOffset,

                0.01f
            );
    }


    // =========================================================
    // PHYSICS PROCESS
    // =========================================================

    public override void _PhysicsProcess(double delta)
    {
        float dt =
            (float)delta;

// ---------------------------------------------------------
// BREEDING
// ---------------------------------------------------------

if (_breedCooldownTimer > 0f)
{
    _breedCooldownTimer -= (float)delta;
}

if (_breedingReady &&
    !_isBreeding &&
    _breedCooldownTimer <= 0f)
{
    TryFindBreedingPartner();
}
        // -----------------------------------------------------
        // Pathfind cooldown
        // -----------------------------------------------------

        if (_pathfindCooldownTimer > 0f)
        {
            _pathfindCooldownTimer -= dt;
        }


        // -----------------------------------------------------
        // Find player
        // -----------------------------------------------------

        if (_player == null)
        {
            var found =
                GetTree()
                    .Root
                    .FindChild(
                        "player",
                        true,
                        false
                    );

            _player =
                found as Node3D;
        }


        // -----------------------------------------------------
        // Hit feedback
        // -----------------------------------------------------

        UpdateHitFeedback(dt);


        // -----------------------------------------------------
        // Gravity
        // -----------------------------------------------------

        Vector3 velocity =
            Velocity;


        if (!IsOnFloor())
        {
            velocity.Y -=
                Gravity * dt;
        }
        else
        {
            velocity.Y = 0f;
        }


        // -----------------------------------------------------
        // Initial landing
        // -----------------------------------------------------

        if (!_hasLanded)
        {
            if (IsOnFloor())
            {
                _hasLanded = true;

                _homePosition =
                    GlobalPosition;

                _stuckCheckLastPosition =
                    GlobalPosition;
            }


            Velocity =
                velocity;

            MoveAndSlide();

            return;
        }


        // -----------------------------------------------------
        // Knockback
        // -----------------------------------------------------

        if (_knockbackTimer > 0f)
        {
            velocity.X =
                _knockbackVelocity.X;

            velocity.Z =
                _knockbackVelocity.Z;


            Velocity =
                velocity;

            MoveAndSlide();

            return;
        }


        // -----------------------------------------------------
        // AI
        // -----------------------------------------------------

        UpdateState(dt);

        RunState(dt);

        ApplyPathMovement(
            dt,
            ref velocity
        );


        // -----------------------------------------------------
        // Visuals
        // -----------------------------------------------------

        UpdateWalkAnimation(dt);

        UpdateHealthBar();


        // -----------------------------------------------------
        // Movement
        // -----------------------------------------------------

        Velocity =
            velocity;

        MoveAndSlide();
    }


    // =========================================================
    // HIT FEEDBACK
    // =========================================================

    private void UpdateHitFeedback(float dt)
    {
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= dt;


            float t =
                Mathf.Clamp(
                    _knockbackTimer /
                    Mathf.Max(
                        KnockbackDuration,
                        0.001f
                    ),
                    0f,
                    1f
                );


            float originalSpeed =
                _knockbackVelocity.Length();


            if (originalSpeed > 0.001f)
            {
                _knockbackVelocity =
                    _knockbackVelocity.Normalized() *
                    (originalSpeed * t);
            }


            if (_knockbackTimer <= 0f)
            {
                _knockbackVelocity =
                    Vector3.Zero;

                _stuckCheckFailCount =
                    0;

                _stuckCheckLastPosition =
                    GlobalPosition;
            }
        }


        if (_flashTimer > 0f)
        {
            _flashTimer -= dt;
        }
    }


    // =========================================================
    // PATH MOVEMENT
    // =========================================================

    private void ApplyPathMovement(
        float dt,
        ref Vector3 velocity)
    {
        if (_hasTarget)
        {
            CheckIfStuck(dt);


            _pathTimeoutTimer += dt;


            if (_pathTimeoutTimer >
                MaxPathDuration)
            {
                AbandonPath();

                return;
            }


            Vector3 toTarget =
                _currentTarget -
                GlobalPosition;


            float verticalDiff =
                toTarget.Y;


            toTarget.Y = 0;


            if (toTarget.Length() < 0.3f &&
                Mathf.Abs(verticalDiff) < 0.6f)
            {
                AdvancePath();
            }
            else
            {
                Vector3 dir =
                    toTarget.Length() > 0.01f
                        ? toTarget.Normalized()
                        : Vector3.Zero;


                velocity.X =
                    dir.X *
                    MoveSpeed;


                velocity.Z =
                    dir.Z *
                    MoveSpeed;


                if (dir != Vector3.Zero)
                {
                    FaceDirection(
                        dir,
                        dt
                    );
                }


                if (verticalDiff > 0.5f &&
                    IsOnFloor())
                {
                    velocity.Y =
                        Mathf.Sqrt(
                            2f *
                            Gravity *
                            1.15f
                        );
                }
            }
        }
        else
        {
            velocity.X =
                Mathf.MoveToward(
                    velocity.X,
                    0,
                    MoveSpeed *
                    dt *
                    4f
                );


            velocity.Z =
                Mathf.MoveToward(
                    velocity.Z,
                    0,
                    MoveSpeed *
                    dt *
                    4f
                );
        }
    }

private void TryFindBreedingPartner()
{
    if (_definition == null ||
        _definition.breeding == null ||
        !_definition.breeding.enabled ||
        !_breedingReady)
    {
        return;
    }

    foreach (Node node in GetTree().GetNodesInGroup("mobs"))
    {
        if (node == this)
            continue;

        if (node is not Mob other)
            continue;

        if (!other.IsBreedingReady())
            continue;

        if (other._definition == null ||
            other._definition.id != _definition.id)
        {
            continue;
        }

        if (GlobalPosition.DistanceTo(other.GlobalPosition) > BreedSearchRadius)
            continue;

        BreedWith(other);
        return;
    }
}
public bool IsBreedingReady()
{
    return _breedingReady &&
           _breedCooldownTimer <= 0f &&
           !_isBreeding;
}
private void BreedWith(Mob partner)
{
    if (partner == null ||
        partner == this ||
        _isBreeding ||
        partner._isBreeding)
    {
        return;
    }

    _isBreeding = true;
    partner._isBreeding = true;

    _breedingReady = false;
    partner._breedingReady = false;

    float cooldown = _definition.breeding.breedCooldown;

    _breedCooldownTimer = cooldown;
    partner._breedCooldownTimer = cooldown;

    GD.Print(
        $"[Mob] {Name} and {partner.Name} are breeding!"
    );
}
    // =========================================================
    // STATE MACHINE
    // =========================================================

    private void UpdateState(float dt)
    {
        // -----------------------------------------------------
        // FLEE
        // -----------------------------------------------------

        if (_state == State.Flee)
        {
            bool threatGone =
                _threat == null ||
                !IsInstanceValid(_threat);


            bool threatFar =
                !threatGone &&
                GlobalPosition.DistanceTo(
                    _threat.GlobalPosition
                ) >
                FleeDistance * 1.5f;


            if (threatGone ||
                threatFar)
            {
                RestoreNormalSpeed();

                EnterIdle();
            }


            return;
        }


        // -----------------------------------------------------
        // HOSTILE AI
        // -----------------------------------------------------

        if (BehaviorType ==
                MobBehaviorType.Hostile &&
            _player != null)
        {
            float distToPlayer =
                GlobalPosition.DistanceTo(
                    _player.GlobalPosition
                );


            if (distToPlayer <=
                AttackRange)
            {
                _state =
                    State.Attack;

                _hasTarget =
                    false;

                return;
            }


            if (distToPlayer <=
                DetectionRange)
            {
                if (_state !=
                    State.Chase)
                {
                    _state =
                        State.Chase;

                    _repathTimer =
                        0f;
                }


                return;
            }


            if (_state ==
                    State.Chase ||
                _state ==
                    State.Attack)
            {
                EnterIdle();
            }
        }


        // -----------------------------------------------------
        // IDLE
        // -----------------------------------------------------

        if (_state ==
            State.Idle)
        {
            _idleTimer -= dt;


            if (_idleTimer <= 0f)
            {
                PickWanderTarget();
            }
        }


        // -----------------------------------------------------
        // WANDER
        // -----------------------------------------------------

        else if (_state ==
                 State.Wander &&
                 !_hasTarget)
        {
            EnterIdle();
        }
    }


    // =========================================================
    // STATE ACTIONS
    // =========================================================

    private void RunState(float dt)
    {
        switch (_state)
        {
            case State.Chase:

                _repathTimer -= dt;


                if (_repathTimer <= 0f)
                {
                    _repathTimer =
                        RepathInterval;


                    if (_player != null)
                    {
                        RequestPathTo(
                            _player.GlobalPosition,
                            32
                        );
                    }
                }

                break;


            case State.Flee:

                _repathTimer -= dt;


                if (_repathTimer <= 0f)
                {
                    _repathTimer =
                        RepathInterval;

                    RequestFleePath();
                }

                break;


            case State.Attack:

                _attackTimer -= dt;


                if (_attackTimer <= 0f)
                {
                    _attackTimer =
                        AttackInterval;


                    GD.Print(
                        $"{Name} hits the player for {AttackDamage}"
                    );
                }

                break;
        }
    }


    // =========================================================
    // IDLE
    // =========================================================

    private void EnterIdle()
    {
        _state =
            State.Idle;


        _hasTarget =
            false;


        _idleTimer =
            _rng.RandfRange(
                MinIdleTime,
                MaxIdleTime
            );
    }


    // =========================================================
    // WANDER
    // =========================================================

    private void PickWanderTarget()
    {
        Vector2 offset =
            new Vector2(
                _rng.RandfRange(
                    -WanderRadius,
                    WanderRadius
                ),

                _rng.RandfRange(
                    -WanderRadius,
                    WanderRadius
                )
            );


        Vector3 target =
            _homePosition +
            new Vector3(
                offset.X,
                0,
                offset.Y
            );


        if (RequestPathTo(
            target,
            24))
        {
            _state =
                State.Wander;
        }
        else
        {
            _idleTimer =
                _rng.RandfRange(
                    MinIdleTime,
                    MaxIdleTime
                );
        }
    }


    // =========================================================
    // REQUEST PATH
    // =========================================================

    private bool RequestPathTo(
        Vector3 worldTarget,
        int maxRange)
    {
        if (_pathfindCooldownTimer > 0f)
        {
            return false;
        }


        var path =
            VoxelPathfinder.FindPath(
                GlobalPosition,
                worldTarget,
                maxRange
            );


        if (path == null ||
            path.Count == 0)
        {
            return false;
        }


        _currentPath =
            new Queue<Vector3>(
                path
            );


        _pathTimeoutTimer =
            0f;

        _stuckCheckTimer =
            0f;

        _stuckCheckFailCount =
            0;

        _stuckCheckLastPosition =
            GlobalPosition;


        AdvancePath();


        return true;
    }


    // =========================================================
    // FLEE PATH
    // =========================================================

    private void RequestFleePath()
    {
        if (!_fleeEnabled)
        {
            return;
        }


        if (_threat == null ||
            !IsInstanceValid(_threat))
        {
            return;
        }


        Vector3 away =
            GlobalPosition -
            _threat.GlobalPosition;


        away.Y = 0;


        if (away.Length() < 0.01f)
        {
            away =
                Vector3.Forward;
        }
        else
        {
            away =
                away.Normalized();
        }


        Vector3 fleeTarget =
            GlobalPosition +
            away *
            FleeDistance;


        RequestPathTo(
            fleeTarget,
            24
        );
    }


    // =========================================================
    // STUCK CHECK
    // =========================================================

    private void CheckIfStuck(float dt)
    {
        _stuckCheckTimer += dt;


        if (_stuckCheckTimer <
            StuckCheckInterval)
        {
            return;
        }


        _stuckCheckTimer =
            0f;


        float moved =
            GlobalPosition.DistanceTo(
                _stuckCheckLastPosition
            );


        _stuckCheckLastPosition =
            GlobalPosition;


        if (moved <
            StuckDistanceThreshold)
        {
            _stuckCheckFailCount++;


            if (_stuckCheckFailCount >=
                StuckChecksBeforeGivingUp)
            {
                AbandonPath();
            }
        }
        else
        {
            _stuckCheckFailCount =
                0;
        }
    }


    // =========================================================
    // ABANDON PATH
    // =========================================================

    private void AbandonPath()
    {
        _currentPath.Clear();

        _hasTarget =
            false;

        _pathTimeoutTimer =
            0f;

        _stuckCheckFailCount =
            0;

        _pathfindCooldownTimer =
            PathfindCooldown;
    }


    // =========================================================
    // ADVANCE PATH
    // =========================================================

    private void AdvancePath()
    {
        if (_currentPath.Count > 0)
        {
            _currentTarget =
                _currentPath.Dequeue();

            _hasTarget =
                true;
        }
        else
        {
            _hasTarget =
                false;
        }
    }


    // =========================================================
    // FACE DIRECTION
    // =========================================================

    private void FaceDirection(
        Vector3 dir,
        float dt)
    {
        if (dir.LengthSquared() <
            0.0001f)
        {
            return;
        }


        Transform3D targetXform =
            Transform3D.Identity
                .LookingAt(
                    dir,
                    Vector3.Up
                );


        Quaternion current =
            Transform.Basis
                .GetRotationQuaternion();


        Quaternion target =
            targetXform.Basis
                .GetRotationQuaternion();


        Quaternion smoothed =
            current.Slerp(
                target,
                Mathf.Clamp(
                    TurnSpeed * dt,
                    0f,
                    1f
                )
            );


        Transform3D t =
            Transform;


        t.Basis =
            new Basis(smoothed);


        Transform =
            t;
    }

// ---------------------------------------------------------
// FOOD / FEEDING
// ---------------------------------------------------------

/// <summary>
/// Returns true if this mob can eat the specified item.
/// The item ID must exist in the mob's JSON food.items list.
private bool IsBreedingFood(string itemId)
{
    if (_definition == null ||
        _definition.breeding == null ||
        !_definition.breeding.enabled ||
        _definition.breeding.foodItems == null ||
        string.IsNullOrEmpty(itemId))
    {
        return false;
    }

    foreach (string foodItem in _definition.breeding.foodItems)
    {
        if (string.Equals(
            foodItem,
            itemId,
            System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}
public bool CanEat(string itemId)
{
    if (_definition == null ||
        _definition.food == null ||
        !_definition.food.enabled ||
        string.IsNullOrEmpty(itemId))
    {
        return false;
    }

    if (_definition.food.items == null)
        return false;

    foreach (string foodItem in _definition.food.items)
    {
        if (string.Equals(
            foodItem,
            itemId,
            System.StringComparison.OrdinalIgnoreCase
        ))
        {
            return true;
        }
    }

    return false;
}



/// Returns true if the specified item is one of this mob's
/// special breeding foods.
public bool IsBreedFood(string itemId)
{
    if (_definition == null ||
        _definition.breeding == null ||
        !_definition.breeding.enabled ||
        string.IsNullOrEmpty(itemId))
    {
        return false;
    }

    if (_definition.breeding.foodItems == null)
        return false;

    foreach (string foodItem in _definition.breeding.foodItems)
    {
        if (string.Equals(
            foodItem,
            itemId,
            System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

// =========================================================
// FEEDING INTERACTION
// =========================================================

/// Attempts to feed this mob using the specified item.
/// Returns true if the item was accepted.
public bool TryFeed(string itemId)
{
    if (string.IsNullOrEmpty(itemId))
        return false;

    if (!CanEat(itemId))
        return false;

    return Feed(itemId);
}

/// <summary>
/// Feeds this mob.
/// Returns true if the mob accepted the food.
/// </summary>
public bool Feed(string itemId)
{
    if (!CanEat(itemId))
        return false;

    bool breedingFood = IsBreedFood(itemId);

    // Normal food cannot be eaten when already at full health.
    // Breeding food (such as carrot) is still accepted.
    if (_health >= MaxHealth && !breedingFood)
        return false;

    // Heal if the mob isn't already at full health.
    if (_definition.food != null && _health < MaxHealth)
    {
        _health += _definition.food.healAmount;

        _health = Mathf.Clamp(
            _health,
            0f,
            MaxHealth
        );
    }

    // Remember that this mob has been given breeding food.
    if (breedingFood && _breedCooldownTimer <= 0f)
    {
        _breedingReady = true;
    }

    return true;
}

    // =========================================================
    // DAMAGE
    // =========================================================

    public void TakeDamage(
        float amount,
        Vector3? sourcePosition = null)
    {
        if (amount <= 0f ||
            _health <= 0f)
        {
            return;
        }


        _health -= amount;


        _health =
            Mathf.Max(
                _health,
                0f
            );


        // -----------------------------------------------------
        // Determine attack source
        // -----------------------------------------------------

        Vector3 source;


        if (sourcePosition.HasValue)
        {
            source =
                sourcePosition.Value;
        }
        else if (_player != null)
        {
            source =
                _player.GlobalPosition;
        }
        else
        {
            source =
                GlobalPosition -
                Vector3.Forward;
        }


        // -----------------------------------------------------
        // Knockback direction
        // -----------------------------------------------------

        Vector3 away =
            GlobalPosition -
            source;


        away.Y = 0;


        if (away.Length() >
            0.01f)
        {
            away =
                away.Normalized();
        }
        else
        {
            away =
                Vector3.Forward;
        }


        _knockbackVelocity =
            away *
            KnockbackForce;


        _knockbackTimer =
            KnockbackDuration;


        _flashTimer =
            FlashDuration;


        // -----------------------------------------------------
        // Passive flee
        // -----------------------------------------------------

        if (_fleeEnabled &&
            BehaviorType ==
                MobBehaviorType.Passive &&
            _player != null &&
            _state != State.Flee)
        {
            _threat =
                _player;


            _state =
                State.Flee;


            if (!_fleeSpeedApplied &&
                FleeSpeedMultiplier > 0f)
            {
                MoveSpeed *=
                    FleeSpeedMultiplier;

                _fleeSpeedApplied =
                    true;
            }


            _repathTimer =
                0f;


            RequestFleePath();
        }


        // -----------------------------------------------------
        // Death
        // -----------------------------------------------------

        if (_health <= 0f)
        {
            Die();
        }
    }


    // =========================================================
    // RESTORE NORMAL SPEED
    // =========================================================

    private void RestoreNormalSpeed()
    {
        if (!_fleeSpeedApplied)
        {
            return;
        }


        if (FleeSpeedMultiplier > 0f)
        {
            MoveSpeed /=
                FleeSpeedMultiplier;
        }


        _fleeSpeedApplied =
            false;
    }


    // =========================================================
    // DEATH
    // =========================================================

    private void Die()
    {
        // -----------------------------------------------------
        // Future death system
        // -----------------------------------------------------
        //
        // Mob
        //   ↓
        // Disable AI
        //   ↓
        // Create ragdoll
        //   ↓
        // Enable physical bones
        //   ↓
        // Corpse
        //   ↓
        // Can be grabbed / dragged
        //   ↓
        // Eventually despawn
        //
        // -----------------------------------------------------

        RestoreNormalSpeed();

        _hasTarget =
            false;

        _currentPath.Clear();

        SetPhysicsProcess(false);

        QueueFree();
    }
}