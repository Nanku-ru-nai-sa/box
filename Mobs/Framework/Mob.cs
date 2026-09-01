using Godot;
using System.Collections.Generic;

// =============================================================
// MOB
// =============================================================
//
// Generic mob framework.
//
// Supports:
// - Passive / Hostile AI
// - VoxelPathfinder movement
// - Wandering
// - Hostile chasing
// - Hostile attacking
// - Passive fleeing
// - Knockback
// - Health
// - Health bar
// - GLTF models
// - Walking animation
// - Male / Female gender
// - Male / Female models
// - Male / Female textures
// - Feeding
// - Breeding
// - Baby mobs
// - Custom baby models
// - Custom baby textures
// - Baby growth
// - JSON-configurable drops
// - Automatic model-based collision
//
// IMPORTANT:
// Baby mobs are NOT created by scaling the CharacterBody3D.
// If a custom baby model is provided, that model is used.
// Otherwise the adult model is scaled down.
// Collision is automatically rebuilt to match the active model.
// =============================================================

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


    // =========================================================
    // BREEDING
    // =========================================================

    private bool _breedingReady = false;
    private bool _isBreeding = false;
    private float _breedCooldownTimer = 0f;

    private const float BreedSearchRadius = 8f;


    // =========================================================
    // BABY
    // =========================================================

    private bool _isBaby = false;

    private float _babyGrowthTimer = 0f;

    private Vector3 _adultModelScale = Vector3.One;

    private bool _usingCustomBabyModel = false;


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
    // AUTOMATIC COLLISION
    // =========================================================

    private CollisionShape3D _modelCollision;

    private Vector3 _adultCollisionSize;

    private Vector3 _adultCollisionPosition;

    private bool _collisionBuilt = false;


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

    public bool IsHappy =>
        _health >= MaxHealth;

    public bool IsHappyEnoughToBreed =>
        IsHappy;

    public bool CanBreed
    {
        get
        {
            return _definition != null &&
                   _definition.breeding != null &&
                   _definition.breeding.enabled &&
                   IsHappy &&
                   !_isBaby;
        }
    }

    public bool IsBaby => _isBaby;


    // =========================================================
    // READY
    // =========================================================

    public override void _Ready()
    {
        AddToGroup("mobs");

        _rng.Randomize();

        LoadDefinition();

        // IMPORTANT:
        // Gender must be assigned BEFORE building the model.
        AssignGender();

        BuildMobModel();

        BuildHealthBar();

        CreateCollision();

        _health = MaxHealth;

        _state = State.Idle;

        _hasTarget = false;

        _currentPath.Clear();

        _idleTimer =
            _rng.RandfRange(
                MinIdleTime,
                MaxIdleTime
            );

        GD.Print(
            $"[Mob] {Name} initialized. " +
            $"Definition={DefinitionPath} " +
            $"Behavior={BehaviorType} " +
            $"Gender={_gender}"
        );
    }


    // =========================================================
    // COLLISION
    // =========================================================

    private void CreateCollision()
    {
        if (_mobModel == null)
        {
            GD.PrintErr(
                $"[Mob] {Name}: Cannot build collision because model is null."
            );

            return;
        }

        bool foundMesh = false;

        Aabb combinedBounds =
            new Aabb(
                Vector3.Zero,
                Vector3.Zero
            );

        foreach (Node node in
                 _mobModel.FindChildren(
                     "*",
                     "MeshInstance3D",
                     true,
                     false
                 ))
        {
            if (node is not MeshInstance3D meshInstance)
                continue;

            if (meshInstance.Mesh == null)
                continue;

            Aabb localAabb =
                meshInstance.Mesh.GetAabb();

            Transform3D meshToMob =
                meshInstance.GlobalTransform *
                GlobalTransform.AffineInverse();

            Vector3 min =
                localAabb.Position;

            Vector3 max =
                localAabb.Position +
                localAabb.Size;

            Vector3[] corners =
            {
                new Vector3(min.X, min.Y, min.Z),
                new Vector3(max.X, min.Y, min.Z),
                new Vector3(min.X, max.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z),

                new Vector3(min.X, min.Y, max.Z),
                new Vector3(max.X, min.Y, max.Z),
                new Vector3(min.X, max.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 point =
                    meshToMob * corner;

                if (!foundMesh)
                {
                    combinedBounds =
                        new Aabb(
                            point,
                            Vector3.Zero
                        );

                    foundMesh = true;
                }
                else
                {
                    combinedBounds =
                        combinedBounds.Expand(
                            point
                        );
                }
            }
        }

        if (!foundMesh)
        {
            GD.PrintErr(
                $"[Mob] {Name}: No mesh found for automatic collision."
            );

            return;
        }

        Vector3 size =
            combinedBounds.Size;

        size.X =
            Mathf.Max(
                size.X,
                0.1f
            );

        size.Y =
            Mathf.Max(
                size.Y,
                0.1f
            );

        size.Z =
            Mathf.Max(
                size.Z,
                0.1f
            );

        var box =
            new BoxShape3D();

        box.Size =
            size;

        _modelCollision =
            new CollisionShape3D();

        _modelCollision.Shape =
            box;

        _modelCollision.Position =
            combinedBounds.Position +
            combinedBounds.Size * 0.5f;

        AddChild(
            _modelCollision
        );

        _adultCollisionSize =
            box.Size;

        _adultCollisionPosition =
            _modelCollision.Position;

        _collisionBuilt = true;

        GD.Print(
            $"[Mob] {Name}: Automatic collision created. " +
            $"Size={_adultCollisionSize} " +
            $"Position={_adultCollisionPosition}"
        );
    }


    // =========================================================
    // REMOVE COLLISION
    // =========================================================

    private void RemoveModelCollision()
    {
        if (_modelCollision != null &&
            IsInstanceValid(_modelCollision))
        {
            _modelCollision.QueueFree();
        }

        _modelCollision = null;
        _collisionBuilt = false;
    }


    // =========================================================
    // UPDATE COLLISION SCALE
    // =========================================================

    private void UpdateCollisionScale(
        float scale)
    {
        if (!_collisionBuilt ||
            _modelCollision == null)
        {
            return;
        }

        if (_modelCollision.Shape
            is not BoxShape3D box)
        {
            return;
        }

        box.Size =
            _adultCollisionSize *
            scale;

        _modelCollision.Position =
            _adultCollisionPosition *
            scale;
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

        if (_definition.stats != null)
        {
            MaxHealth =
                _definition.stats.maxHealth;

            MoveSpeed =
                _definition.stats.moveSpeed;

            TurnSpeed =
                _definition.stats.turnSpeed;
        }

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
            _gender = MobGender.Male;
        }
        else
        {
            _gender = MobGender.Female;
        }

        GD.Print(
            $"[Mob] {_definition.displayName} spawned as {_gender}"
        );
    }


    // =========================================================
    // GET ADULT MODEL PATH
    // =========================================================

    private string GetAdultModelPath()
    {
        string defaultModel =
            "res://Assets/Models/Mobs/pig.gltf";

        if (_definition == null)
            return defaultModel;

        if (_definition.gender != null &&
            _definition.gender.enabled)
        {
            if (_gender == MobGender.Male &&
                !string.IsNullOrWhiteSpace(
                    _definition.gender.maleModel))
            {
                return _definition.gender.maleModel;
            }

            if (_gender == MobGender.Female &&
                !string.IsNullOrWhiteSpace(
                    _definition.gender.femaleModel))
            {
                return _definition.gender.femaleModel;
            }
        }

        if (!string.IsNullOrWhiteSpace(
            _definition.model))
        {
            return _definition.model;
        }

        return defaultModel;
    }


    // =========================================================
    // GET ADULT TEXTURE PATH
    // =========================================================

    private string GetAdultTexturePath()
    {
        if (_definition == null ||
            _definition.gender == null ||
            !_definition.gender.enabled)
        {
            return "";
        }

        if (_gender == MobGender.Male)
        {
            return _definition.gender.maleTexture ?? "";
        }

        return _definition.gender.femaleTexture ?? "";
    }


    // =========================================================
    // GET BABY MODEL PATH
    // =========================================================

    private string GetBabyModelPath()
    {
        if (_definition != null &&
            _definition.baby != null &&
            !string.IsNullOrWhiteSpace(
                _definition.baby.model))
        {
            return _definition.baby.model;
        }

        return GetAdultModelPath();
    }


    // =========================================================
    // GET BABY TEXTURE PATH
    // =========================================================

    private string GetBabyTexturePath()
    {
        if (_definition != null &&
            _definition.baby != null &&
            !string.IsNullOrWhiteSpace(
                _definition.baby.texture))
        {
            return _definition.baby.texture;
        }

        return GetAdultTexturePath();
    }


    // =========================================================
    // LOAD TEXTURE
    // =========================================================

    private Texture2D LoadMobTexture(
        string texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
            return null;

        Texture2D texture =
            GD.Load<Texture2D>(
                texturePath
            );

        if (texture == null)
        {
            GD.PrintErr(
                $"[Mob] {Name}: Could not load mob texture: {texturePath}"
            );
        }

        return texture;
    }


    // =========================================================
// APPLY TEXTURE
// =========================================================

private void ApplyMobTexture(
    Texture2D texture)
{
    if (_mobModel == null ||
        texture == null)
    {
        return;
    }

    foreach (Node node in
             _mobModel.FindChildren(
                 "*",
                 "MeshInstance3D",
                 true,
                 false
             ))
    {
        if (node is not MeshInstance3D mesh)
            continue;

        // -------------------------------------------------
        // Get the existing material from the GLTF.
        // -------------------------------------------------

        Material existingMaterial =
            mesh.GetActiveMaterial(0);

        StandardMaterial3D material;

        if (existingMaterial is StandardMaterial3D existingStandard)
        {
            material =
                existingStandard.Duplicate()
                    as StandardMaterial3D;
        }
        else
        {
            material =
                new StandardMaterial3D();
        }

        if (material == null)
            continue;

        // -------------------------------------------------
        // Replace ONLY the texture.
        // Keep the GLTF material's other settings.
        // -------------------------------------------------

        material.AlbedoTexture =
            texture;

        material.TextureFilter =
            BaseMaterial3D.TextureFilterEnum.Nearest;

        // -------------------------------------------------
        // Preserve transparency.
        // -------------------------------------------------

        if (texture.HasAlpha())
        {
            material.Transparency =
                BaseMaterial3D.TransparencyEnum.Alpha;

            material.ShadingMode =
                BaseMaterial3D.ShadingModeEnum.PerPixel;

            material.CullMode =
                BaseMaterial3D.CullModeEnum.Disabled;
        }

        // -------------------------------------------------
        // Apply the duplicated material.
        // -------------------------------------------------

        mesh.MaterialOverride =
            material;
    }
}


    // =========================================================
    // FIND LEG NODES
    // =========================================================

    private void FindLegNodes()
    {
        _frontRightLeg =
            _mobModel?.FindChild(
                "front_right",
                true,
                false
            ) as Node3D;

        _frontLeftLeg =
            _mobModel?.FindChild(
                "front_left",
                true,
                false
            ) as Node3D;

        _backRightLeg =
            _mobModel?.FindChild(
                "back_right",
                true,
                false
            ) as Node3D;

        _backLeftLeg =
            _mobModel?.FindChild(
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
                $"[Mob] {Name}: Could not find all four leg nodes in active mob model."
            );
        }
    }


    // =========================================================
    // BUILD MOB MODEL
    // =========================================================

    private void BuildMobModel()
    {
        string modelPath =
            GetAdultModelPath();

        string texturePath =
            GetAdultTexturePath();

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

        AddChild(
            _mobModel
        );

        _mobModel.Scale =
            Vector3.One;

        _adultModelScale =
            _mobModel.Scale;

        FindLegNodes();

        if (!string.IsNullOrWhiteSpace(
            texturePath))
        {
            Texture2D texture =
                LoadMobTexture(
                    texturePath
                );

            ApplyMobTexture(
                texture
            );
        }

        GD.Print(
            $"[Mob] {Name}: Loaded adult model {modelPath}"
        );

        if (!string.IsNullOrWhiteSpace(
            texturePath))
        {
            GD.Print(
                $"[Mob] {Name}: Applied texture {texturePath}"
            );
        }
    }


    // =========================================================
    // REPLACE MOB MODEL
    // =========================================================

    private bool ReplaceMobModel(
        string modelPath,
        string texturePath,
        bool customBabyModel)
    {
        if (string.IsNullOrWhiteSpace(
            modelPath))
        {
            GD.PrintErr(
                $"[Mob] {Name}: Model path is empty."
            );

            return false;
        }

        var scene =
            GD.Load<PackedScene>(
                modelPath
            );

        if (scene == null)
        {
            GD.PrintErr(
                $"[Mob] {Name}: Could not load model: {modelPath}"
            );

            return false;
        }

        // Remove old collision first.
        RemoveModelCollision();

        // Remove old model.
        if (_mobModel != null &&
            IsInstanceValid(_mobModel))
        {
            _mobModel.QueueFree();
        }

        _mobModel = null;

        // Clear animation references.
        _frontRightLeg = null;
        _frontLeftLeg = null;
        _backRightLeg = null;
        _backLeftLeg = null;

        // Build new model.
        _mobModel =
            scene.Instantiate<Node3D>();

        AddChild(
            _mobModel
        );

        _mobModel.Scale =
            Vector3.One;

        _usingCustomBabyModel =
            customBabyModel;

        FindLegNodes();

        if (!string.IsNullOrWhiteSpace(
            texturePath))
        {
            Texture2D texture =
                LoadMobTexture(
                    texturePath
                );

            ApplyMobTexture(
                texture
            );
        }

        // Rebuild collision against the new model.
        CreateCollision();

        GD.Print(
            $"[Mob] {Name}: Switched model to {modelPath}"
        );

        return true;
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
                speed /
                Mathf.Max(
                    MoveSpeed,
                    0.01f
                ),
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


        // -----------------------------------------------------
        // BREEDING
        // -----------------------------------------------------

        if (_breedCooldownTimer > 0f)
        {
            _breedCooldownTimer -= dt;
        }

        if (_breedingReady &&
            !_isBreeding &&
            !_isBaby &&
            _breedCooldownTimer <= 0f)
        {
            TryFindBreedingPartner();
        }


        // -----------------------------------------------------
        // BABY GROWTH
        // -----------------------------------------------------

        if (_isBaby)
        {
            _babyGrowthTimer -= dt;

            if (_babyGrowthTimer <= 0f)
            {
                GrowUp();
            }
        }


        // -----------------------------------------------------
        // PATHFIND COOLDOWN
        // -----------------------------------------------------

        if (_pathfindCooldownTimer > 0f)
        {
            _pathfindCooldownTimer -= dt;
        }


        // -----------------------------------------------------
        // FIND PLAYER
        // -----------------------------------------------------

        if (_player == null ||
            !IsInstanceValid(_player))
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
        // HIT FEEDBACK
        // -----------------------------------------------------

        UpdateHitFeedback(dt);


        // -----------------------------------------------------
        // GRAVITY
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
        // INITIAL LANDING
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

                _state = State.Idle;

                _hasTarget = false;

                _currentPath.Clear();

                _idleTimer =
                    _rng.RandfRange(
                        MinIdleTime,
                        MaxIdleTime
                    );
            }

            Velocity =
                velocity;

            MoveAndSlide();

            return;
        }


        // -----------------------------------------------------
        // KNOCKBACK
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
        // VISUALS
        // -----------------------------------------------------

        UpdateWalkAnimation(dt);

        UpdateHealthBar();


        // -----------------------------------------------------
        // MOVEMENT
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


    // =========================================================
    // BREEDING
    // =========================================================

    private void TryFindBreedingPartner()
    {
        if (_definition == null ||
            _definition.breeding == null ||
            !_definition.breeding.enabled ||
            !_breedingReady ||
            _isBaby)
        {
            return;
        }

        foreach (Node node in
                 GetTree().GetNodesInGroup("mobs"))
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

            if (other.Gender == Gender)
                continue;

            if (GlobalPosition.DistanceTo(
                    other.GlobalPosition) >
                BreedSearchRadius)
            {
                continue;
            }

            BreedWith(other);

            return;
        }
    }


    public bool IsBreedingReady()
    {
        return _breedingReady &&
               _breedCooldownTimer <= 0f &&
               !_isBreeding &&
               !_isBaby;
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

        if (_definition == null ||
            _definition.breeding == null ||
            !_definition.breeding.enabled)
        {
            return;
        }

        if (partner._definition == null ||
            partner._definition.breeding == null ||
            !partner._definition.breeding.enabled)
        {
            return;
        }

        _isBreeding = true;
        partner._isBreeding = true;

        _breedingReady = false;
        partner._breedingReady = false;

        float cooldown =
            _definition.breeding.breedCooldown;

        _breedCooldownTimer =
            cooldown;

        partner._breedCooldownTimer =
            cooldown;

        GD.Print(
            $"[Mob] {Name} and {partner.Name} are breeding!"
        );

        SpawnBabies(partner);

        _isBreeding = false;
        partner._isBreeding = false;
    }


    // =========================================================
    // SPAWN BABIES
    // =========================================================

    private void SpawnBabies(Mob partner)
    {
        if (partner == null ||
            _definition == null ||
            _definition.breeding == null)
        {
            return;
        }

        int litterMin =
            Mathf.Max(
                1,
                _definition.breeding.litterMin
            );

        int litterMax =
            Mathf.Max(
                litterMin,
                _definition.breeding.litterMax
            );

        int litterSize =
            GD.RandRange(
                litterMin,
                litterMax
            );

        string scenePath =
            SceneFilePath;

        if (string.IsNullOrEmpty(scenePath))
        {
            GD.PrintErr(
                $"[Mob] {Name}: SceneFilePath is empty."
            );

            return;
        }

        PackedScene mobScene =
            ResourceLoader.Load<PackedScene>(
                scenePath
            );

        if (mobScene == null)
        {
            GD.PrintErr(
                $"[Mob] {Name}: Could not load mob scene: {scenePath}"
            );

            return;
        }

        Vector3 center =
            (
                GlobalPosition +
                partner.GlobalPosition
            ) * 0.5f;

        for (int i = 0; i < litterSize; i++)
        {
            Mob baby =
                mobScene.Instantiate<Mob>();

            if (baby == null)
            {
                GD.PrintErr(
                    "[Mob] Failed to create baby mob."
                );

                continue;
            }

            baby.DefinitionPath =
                DefinitionPath;

            GetParent().AddChild(baby);

            Vector3 offset =
                new Vector3(
                    (float)GD.RandRange(
                        -1.0,
                        1.0
                    ),

                    0f,

                    (float)GD.RandRange(
                        -1.0,
                        1.0
                    )
                );

            Vector3 spawnPosition =
                center +
                offset;

            spawnPosition.Y += 2.5f;

            baby.GlobalPosition =
                spawnPosition;

            baby.MakeBaby();

            GD.Print(
                $"[Mob] Baby {baby.Name} was born. " +
                $"Definition={baby.DefinitionPath} " +
                $"Growth={baby._babyGrowthTimer:F1}s"
            );
        }
    }


    // =========================================================
    // MAKE BABY
    // =========================================================

    private void MakeBaby()
    {
        _isBaby = true;

        float growthMin =
            300f;

        float growthMax =
            900f;

        if (_definition != null &&
            _definition.breeding != null)
        {
            growthMin =
                Mathf.Max(
                    1f,
                    _definition.breeding.babyGrowthMin
                );

            growthMax =
                Mathf.Max(
                    growthMin,
                    _definition.breeding.babyGrowthMax
                );
        }

        _babyGrowthTimer =
            _rng.RandfRange(
                growthMin,
                growthMax
            );

        _breedingReady = false;

        _state = State.Idle;

        _hasTarget = false;

        _currentPath.Clear();

        _repathTimer = 0f;

        _pathTimeoutTimer = 0f;

        _pathfindCooldownTimer = 0f;

        _stuckCheckTimer = 0f;

        _stuckCheckFailCount = 0;

        _idleTimer =
            _rng.RandfRange(
                MinIdleTime,
                MaxIdleTime
            );

        // -----------------------------------------------------
        // CUSTOM BABY MODEL
        // -----------------------------------------------------

        bool hasCustomBabyModel =
            _definition != null &&
            _definition.baby != null &&
            !string.IsNullOrWhiteSpace(
                _definition.baby.model
            );

        if (hasCustomBabyModel)
        {
            string babyModel =
                GetBabyModelPath();

            string babyTexture =
                GetBabyTexturePath();

            if (ReplaceMobModel(
                    babyModel,
                    babyTexture,
                    true))
            {
                _usingCustomBabyModel = true;

                GD.Print(
                    $"[Mob] {Name}: Using custom baby model."
                );
            }
            else
            {
                _usingCustomBabyModel = false;

                if (_mobModel != null)
                {
                    _adultModelScale =
                        _mobModel.Scale;

                    _mobModel.Scale =
                        _adultModelScale * 0.55f;

                    UpdateCollisionScale(
                        0.55f
                    );
                }
            }
        }
        else
        {
            // -------------------------------------------------
            // FALLBACK:
            // Use adult model scaled to 55%.
            // -------------------------------------------------

            _usingCustomBabyModel = false;

            _adultModelScale =
                _mobModel != null
                    ? _mobModel.Scale
                    : Vector3.One;

            if (_mobModel != null)
            {
                _mobModel.Scale =
                    _adultModelScale * 0.55f;
            }

            UpdateCollisionScale(
                0.55f
            );
        }

        GD.Print(
            $"[Mob] {Name} is a baby. " +
            $"Growth time: {_babyGrowthTimer:F1} seconds."
        );
    }


    // =========================================================
    // GROW UP
    // =========================================================

    private void GrowUp()
    {
        if (!_isBaby)
            return;

        _isBaby = false;

        // -----------------------------------------------------
        // CUSTOM BABY MODEL
        // -----------------------------------------------------

        if (_usingCustomBabyModel)
        {
            string adultModel =
                GetAdultModelPath();

            string adultTexture =
                GetAdultTexturePath();

            ReplaceMobModel(
                adultModel,
                adultTexture,
                false
            );

            _usingCustomBabyModel = false;
        }
        else
        {
            // -------------------------------------------------
            // SCALED ADULT MODEL
            // -------------------------------------------------

            if (_mobModel != null)
            {
                _mobModel.Scale =
                    _adultModelScale;
            }

            UpdateCollisionScale(
                1f
            );
        }

        _state = State.Idle;

        _hasTarget = false;

        _currentPath.Clear();

        _pathTimeoutTimer = 0f;

        _stuckCheckTimer = 0f;

        _stuckCheckFailCount = 0;

        _idleTimer =
            _rng.RandfRange(
                MinIdleTime,
                MaxIdleTime
            );

        GD.Print(
            $"[Mob] {Name} has grown into an adult."
        );
    }


    // =========================================================
    // STATE MACHINE
    // =========================================================

    private void UpdateState(float dt)
    {
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

        if (BehaviorType ==
                MobBehaviorType.Hostile &&
            _player != null &&
            IsInstanceValid(_player))
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

        if (_state ==
            State.Idle)
        {
            _idleTimer -= dt;

            if (_idleTimer <= 0f)
            {
                PickWanderTarget();
            }
        }

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

                    if (_player != null &&
                        IsInstanceValid(_player))
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

        _currentPath.Clear();

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


    // =========================================================
    // FOOD / FEEDING
    // =========================================================

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

        foreach (string foodItem in
                 _definition.breeding.foodItems)
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
        {
            return false;
        }

        foreach (string foodItem in
                 _definition.food.items)
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


    public bool IsBreedFood(string itemId)
    {
        return IsBreedingFood(itemId);
    }


    public bool TryFeed(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        if (!CanEat(itemId))
        {
            return false;
        }

        return Feed(itemId);
    }


    public bool Feed(string itemId)
    {
        if (!CanEat(itemId))
        {
            return false;
        }

        bool breedingFood =
            IsBreedFood(itemId);

        if (_health >= MaxHealth &&
            !breedingFood)
        {
            return false;
        }

        if (_definition.food != null &&
            _health < MaxHealth)
        {
            _health +=
                _definition.food.healAmount;

            _health =
                Mathf.Clamp(
                    _health,
                    0f,
                    MaxHealth
                );
        }

        if (breedingFood &&
            !_isBaby &&
            _breedCooldownTimer <= 0f)
        {
            _breedingReady = true;

            GD.Print(
                $"[Mob] {Name} is ready to breed."
            );
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

        Vector3 source;

        if (sourcePosition.HasValue)
        {
            source =
                sourcePosition.Value;
        }
        else if (_player != null &&
                 IsInstanceValid(_player))
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
        // PASSIVE FLEE
        // -----------------------------------------------------

        if (_fleeEnabled &&
            BehaviorType ==
                MobBehaviorType.Passive &&
            _player != null &&
            IsInstanceValid(_player) &&
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
        // DEATH
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
    // DROP ITEMS
    // =========================================================

    private void SpawnDrops()
    {
        if (_definition == null ||
            _definition.drops == null ||
            !_definition.drops.enabled ||
            _definition.drops.items == null ||
            _definition.drops.items.Length == 0)
        {
            return;
        }

        Node parent =
            GetParent();

        if (parent == null)
        {
            GD.PrintErr(
                $"[Mob] {Name}: Cannot spawn drops because parent is null."
            );

            return;
        }

        foreach (MobDrop drop in
                 _definition.drops.items)
        {
            if (drop == null)
                continue;

            if (string.IsNullOrWhiteSpace(
                drop.item))
            {
                continue;
            }

            float chance =
                Mathf.Clamp(
                    drop.chance,
                    0f,
                    1f
                );

            float roll =
                _rng.Randf();

            if (roll > chance)
            {
                continue;
            }

            int min =
                Mathf.Max(
                    1,
                    drop.min
                );

            int max =
                Mathf.Max(
                    min,
                    drop.max
                );

            int count =
                _rng.RandiRange(
                    min,
                    max
                );

            if (count <= 0)
                continue;


            // -------------------------------------------------
            // CREATE ITEM PICKUP
            // -------------------------------------------------

            ItemPickup pickup =
                new ItemPickup();

            pickup.ItemId =
                drop.item;

            pickup.Count =
                count;


            // -------------------------------------------------
            // GIVE THE DROP A LITTLE POP
            // -------------------------------------------------

            float angle =
                _rng.RandfRange(
                    0f,
                    Mathf.Tau
                );

            float horizontalSpeed =
                _rng.RandfRange(
                    0.5f,
                    1.2f
                );

            pickup.TossVelocity =
                new Vector3(
                    Mathf.Cos(angle) *
                    horizontalSpeed,

                    _rng.RandfRange(
                        1.8f,
                        2.6f
                    ),

                    Mathf.Sin(angle) *
                    horizontalSpeed
                );


            // -------------------------------------------------
            // ADD TO WORLD
            // -------------------------------------------------

            parent.AddChild(
                pickup
            );

            pickup.GlobalPosition =
                GlobalPosition +
                new Vector3(
                    0f,
                    0.5f,
                    0f
                );


            GD.Print(
                $"[Mob] {Name} dropped " +
                $"{count}x {drop.item}"
            );
        }
    }


    // =========================================================
    // DEATH
    // =========================================================

    private void Die()
    {
        RestoreNormalSpeed();

        _hasTarget =
            false;

        _currentPath.Clear();


        // -----------------------------------------------------
        // DROP LOOT BEFORE REMOVING MOB
        // -----------------------------------------------------

        SpawnDrops();


        // -----------------------------------------------------
        // REMOVE MOB
        // -----------------------------------------------------

        SetPhysicsProcess(false);

        QueueFree();
    }
}