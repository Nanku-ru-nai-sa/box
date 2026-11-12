using Godot;
using System;
using System.Collections.Generic;

// =============================================================
// MOB
// =============================================================
//
// Generic mob framework.
//
// MOVEMENT MODEL
// -------------------------------------------------------------
// Mobs use simple X/Z steering rather than vertical voxel A*.
//
// - AI chooses an X/Z destination.
// - Mob walks toward that destination.
// - A one-block obstacle can be automatically stepped over.
// - Two-block+ obstacles are treated as walls.
// - Drop-offs are allowed.
// - Gravity handles falling naturally.
// - The automatically generated collision box is used as the
//   physical size of the mob.
//
// =============================================================

public enum MobBehaviorType
{
    Passive,
    Neutral,
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

    private Vector3 _mountedMoveDirection = Vector3.Zero;
    private bool _mountedSprinting = false;
    private bool _mountedJumpRequested = false;
    private float _mountedJumpVelocity = 6f;


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
    private bool _isShiny = false;
private bool _isBig = false;
private float _mobScaleMultiplier = 1f;
public bool? SavedShinyOverride { get; set; }
public bool? SavedBigOverride { get; set; }
public bool IsShiny =>
    _isShiny;

public bool IsBig =>
    _isBig;
    // =========================================================
    // FUR / SHEARING
    // =========================================================

    public bool HasFur { get; private set; }

    public bool CanBeSheared { get; private set; }

    public string FurItem { get; private set; } = "";

    public int FurShearAmount { get; private set; } = 1;

    public int FurDeathAmount { get; private set; } = 1;

    public bool AngerWhenShearedAwake { get; private set; }


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

    [Export]
    public float KnockbackVerticalMultiplier = 0.2f;


    // =========================================================
    // MOVEMENT
    // =========================================================

    private const float Gravity = 20f;

    private const float StuckCheckInterval = 0.75f;

    private const float StuckDistanceThreshold = 0.10f;

    private const int StuckChecksBeforeGivingUp = 3;

    [ExportGroup("Movement")]

    // Maximum automatic step height.
    // One normal voxel block.
    [Export]
    public float StepHeight = 1.05f;

    // How far ahead we test for an obstacle.
    [Export]
    public float StepCheckDistance = 0.45f;

    // Prevents repeated stepping every physics frame.
    [Export]
    public float StepCooldown = 0.12f;

    // How strongly a mob turns away when it encounters
    // a wall it cannot step over.
    [Export]
    public float WallAvoidanceStrength = 1.5f;

    private float _stepCooldownTimer = 0f;

    [Export]
public float StepDuration = 0.3f;

private float _stepTimer = 0f;
private float _stepStartY = 0f;
private float _stepTargetY = 0f;

    // =========================================================
    // MOB STATE
    // =========================================================

    private float _health;

    private Vector3 _homePosition;

    private bool _hasLanded = false;

    private Node3D _player;

    private Node3D _threat;

    private Node3D _hostileTarget;

    private readonly RandomNumberGenerator _rng =
        new RandomNumberGenerator();

    private enum State
    {
        Idle,
        Wander,
        Chase,
        Attack,
        Flee,
        Sleep
    }

    private State _state = State.Idle;

    private float _idleTimer;

    private float _attackTimer;

    private bool _fleeSpeedApplied = false;


    // =========================================================
    // SLEEP
    // =========================================================

    private bool _sleepEnabled = false;

    private float _sleepStartHour = 20f;

    private float _sleepEndHour = 6f;

    private bool _isSleeping = false;

    private DayNightCycle _dayNightCycle;


    // =========================================================
    // MOVEMENT TARGET
    // =========================================================

    // This is intentionally X/Z based.
    //
    // The Y value is ignored for AI movement.
    // Gravity is responsible for vertical movement.
    private Vector3 _movementTarget;

    private bool _hasMovementTarget = false;


    // =========================================================
    // STUCK DETECTION
    // =========================================================

    private float _stuckCheckTimer;

    private Vector3 _stuckCheckLastPosition;

    private int _stuckCheckFailCount;


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
    // MOUNT / RIDING
    // =========================================================

    public bool MountEnabled { get; private set; } = false;

    public bool SaddleAllowed { get; private set; } = false;

    public float MountSpeedMultiplier { get; private set; } = 1f;

    public float MountSprintMultiplier { get; private set; } = 1.5f;

    public float MountJumpMultiplier { get; private set; } = 1f;

    public bool MountCanSprint { get; private set; } = true;

    public bool HasSaddle { get; private set; } = false;

    private Player _mountedPlayer = null;

    public bool IsMounted =>
        _mountedPlayer != null &&
        IsInstanceValid(_mountedPlayer);

    public Player MountedPlayer =>
        _mountedPlayer;


    // =========================================================
    // MODEL / ANIMATION
    // =========================================================

    private Node3D _mobModel;

    private MobAnimation _animation;


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

    public MobGender? ForcedGender { get; set; }


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
                   !_isBaby &&
                   IsCurrentSeasonAllowedForBreeding();
        }
    }

    public bool IsBaby => _isBaby;


    public Vector3 GetMountPosition()
    {
        if (!_collisionBuilt)
            return GlobalPosition + new Vector3(0f, 1.5f, 0f);

        float topOfMob =
            _adultCollisionPosition.Y +
            (_adultCollisionSize.Y * 0.5f);

        const float riderCushion = 0.15f;

        return GlobalPosition +
               new Vector3(
                   0f,
                   topOfMob + riderCushion,
                   0f
               );
    }


    public bool IsSleeping =>
        _isSleeping;


    // =========================================================
    // READY
    // =========================================================

    public override void _Ready()
    {
        AddToGroup("mobs");

        _rng.Randomize();

        LoadDefinition();
        RollAppearance();

        _dayNightCycle =
            GetTree()
                .GetFirstNodeInGroup(
                    "day_night_cycle"
                ) as DayNightCycle;

        AssignGender();

        BuildMobModel();

        BuildHealthBar();

        CreateCollision();
        UpdateHealthBarPosition();

        _health = MaxHealth;

        _state = State.Idle;

        _hasMovementTarget = false;

        _hostileTarget = null;

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

private void RollAppearance()
{
    _isShiny = false;
    _isBig = false;
    _mobScaleMultiplier = 1f;

    // ---------------------------------------------------------
    // RESTORE SAVED SHINY STATE
    // ---------------------------------------------------------

    if (SavedShinyOverride.HasValue)
    {
        _isShiny =
            SavedShinyOverride.Value;
    }

    // ---------------------------------------------------------
    // RESTORE SAVED BIG STATE
    // ---------------------------------------------------------

    if (SavedBigOverride.HasValue)
    {
        _isBig =
            SavedBigOverride.Value;

        if (_isBig &&
            _definition?.appearance != null)
        {
            _mobScaleMultiplier =
                Mathf.Max(
                    1f,
                    _definition.appearance.bigScale
                );
        }
    }

    // ---------------------------------------------------------
    // IF BOTH STATES WERE PROVIDED, WE ARE DONE.
    // ---------------------------------------------------------

    if (SavedShinyOverride.HasValue &&
        SavedBigOverride.HasValue)
    {
        GD.Print(
            $"[Mob] {Name}: Restored Appearance " +
            $"Shiny={_isShiny} " +
            $"Big={_isBig} " +
            $"Scale={_mobScaleMultiplier}"
        );

        return;
    }

    // ---------------------------------------------------------
    // NORMAL RANDOM ROLL
    // ---------------------------------------------------------

    if (_definition?.appearance == null)
        return;

    if (!SavedShinyOverride.HasValue &&
        _definition.appearance.shinyEnabled &&
        _definition.appearance.shinyChance > 0f)
    {
        _isShiny =
            _rng.Randf() <
            _definition.appearance.shinyChance;
    }

    if (!SavedBigOverride.HasValue &&
        _definition.appearance.bigEnabled &&
        _definition.appearance.bigChance > 0f)
    {
        if (_rng.Randf() <
            _definition.appearance.bigChance)
        {
            _isBig = true;

            _mobScaleMultiplier =
                Mathf.Max(
                    1f,
                    _definition.appearance.bigScale
                );
        }
    }

    GD.Print(
        $"[Mob] {Name}: Appearance " +
        $"Shiny={_isShiny} " +
        $"Big={_isBig} " +
        $"Scale={_mobScaleMultiplier}"
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

            switch (behavior.ToLowerInvariant())
{
    case "hostile":
        BehaviorType =
            MobBehaviorType.Hostile;
        break;

    case "neutral":
        BehaviorType =
            MobBehaviorType.Neutral;
        break;

    default:
        BehaviorType =
            MobBehaviorType.Passive;
        break;
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

        // -----------------------------------------------------
        // FUR
        // -----------------------------------------------------

        if (_definition.fur != null)
        {
            HasFur =
                _definition.fur.enabled;

            FurItem =
                _definition.fur.item ?? "";

            FurShearAmount =
                Mathf.Max(
                    1,
                    _definition.fur.shearAmount
                );

            FurDeathAmount =
                Mathf.Max(
                    1,
                    _definition.fur.deathAmount
                );
        }

        if (_definition.shearing != null)
        {
            CanBeSheared =
                _definition.shearing.enabled;

            AngerWhenShearedAwake =
                _definition.shearing.angerWhenAwake;
        }
        else
        {
            CanBeSheared = false;
            AngerWhenShearedAwake = false;
        }

        // -----------------------------------------------------
        // MOUNT
        // -----------------------------------------------------

        if (_definition.mount != null)
        {
            MountEnabled =
                _definition.mount.enabled;

            SaddleAllowed =
                _definition.mount.saddleAllowed;

            MountSpeedMultiplier =
                Mathf.Max(
                    0f,
                    _definition.mount.speedMultiplier
                );

            MountSprintMultiplier =
                Mathf.Max(
                    1f,
                    _definition.mount.sprintMultiplier
                );

            MountJumpMultiplier =
                Mathf.Max(
                    0f,
                    _definition.mount.jumpMultiplier
                );

            MountCanSprint =
                _definition.mount.canSprint;
        }
        else
        {
            MountEnabled = false;
            SaddleAllowed = false;
            MountSpeedMultiplier = 1f;
            MountSprintMultiplier = 1.5f;
            MountJumpMultiplier = 1f;
            MountCanSprint = true;
        }

        // -----------------------------------------------------
        // SLEEP
        // -----------------------------------------------------

        if (_definition.sleep != null)
        {
            _sleepEnabled =
                _definition.sleep.enabled;

            _sleepStartHour =
                _definition.sleep.startHour;

            _sleepEndHour =
                _definition.sleep.endHour;
        }
        else
        {
            _sleepEnabled = false;
            _sleepStartHour = 20f;
            _sleepEndHour = 6f;
        }

        GD.Print(
            $"[Mob] Loaded definition: {_definition.displayName}"
        );
    }


    // =========================================================
    // SET SLEEPING
    // =========================================================

    public void SetSleeping(
        bool sleeping)
    {
        if (!_sleepEnabled)
            return;

        if (_health <= 0f)
            return;

        if (_isSleeping == sleeping)
            return;

        _isSleeping =
            sleeping;

        if (_isSleeping)
        {
            _state =
                State.Sleep;

            _hasMovementTarget =
                false;

            _hostileTarget = null;

            Velocity =
                new Vector3(
                    0f,
                    Velocity.Y,
                    0f
                );

            if (_animation != null)
            {
                _animation.SetSleeping(true);
            }

            GD.Print(
                $"[Mob] {Name} is going to sleep."
            );
        }
        else
        {
            _state =
                State.Idle;

            _hasMovementTarget =
                false;

            _idleTimer =
                _rng.RandfRange(
                    MinIdleTime,
                    MaxIdleTime
                );

            if (_animation != null)
            {
                _animation.SetSleeping(false);
            }

            GD.Print(
                $"[Mob] {Name} woke up."
            );
        }
    }


    // =========================================================
    // GENDER
    // =========================================================

    private void AssignGender()
    {
        if (ForcedGender.HasValue)
        {
            _gender = ForcedGender.Value;

            GD.Print(
                $"[Mob] {_definition?.displayName ?? Name} " +
                $"spawned as forced {_gender}"
            );

            return;
        }

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
            _gender = MobGender.Male;
        else
            _gender = MobGender.Female;

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
    // ---------------------------------------------------------
    // SHINY
    // ---------------------------------------------------------

    if (_isShiny)
    {
        string modelPath =
            GetAdultModelPath();

        string shinyPath =
            modelPath.GetBaseDir() +
            "/shiny.png";

        if (ResourceLoader.Exists(shinyPath))
        {
            return shinyPath;
        }

        GD.PrintErr(
            $"[Mob] {Name}: Shiny was rolled, " +
            $"but shiny.png was not found at {shinyPath}"
        );
    }

    // ---------------------------------------------------------
    // NORMAL GENDER TEXTURES
    // ---------------------------------------------------------

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

            material.AlbedoTexture =
                texture;

            material.TextureFilter =
                BaseMaterial3D.TextureFilterEnum.Nearest;

            if (texture.HasAlpha())
            {
                material.Transparency =
                    BaseMaterial3D.TransparencyEnum.Alpha;

                material.ShadingMode =
                    BaseMaterial3D.ShadingModeEnum.PerPixel;

                material.CullMode =
                    BaseMaterial3D.CullModeEnum.Disabled;
            }

            mesh.MaterialOverride =
                material;
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
    Vector3.One *
    _mobScaleMultiplier;

        _adultModelScale =
            _mobModel.Scale;

        _animation =
            new MobAnimation();

        _animation.Setup(
            _mobModel,
            MoveSpeed
        );

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

        RemoveModelCollision();

        if (_mobModel != null &&
            IsInstanceValid(_mobModel))
        {
            _mobModel.QueueFree();
        }

        _mobModel = null;

        _animation = null;

        _mobModel =
            scene.Instantiate<Node3D>();

        AddChild(
            _mobModel
        );

        _mobModel.Scale =
    Vector3.One *
    _mobScaleMultiplier;

        _usingCustomBabyModel =
            customBabyModel;

        _animation =
            new MobAnimation();

        _animation.Setup(
            _mobModel,
            MoveSpeed
        );

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

        CreateCollision();
        UpdateHealthBarPosition();

        GD.Print(
            $"[Mob] {Name}: Switched model to {modelPath}"
        );

        return true;
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


    private void UpdateHealthBarPosition()
    {
        float y = _collisionBuilt
            ? _adultCollisionPosition.Y +
              _adultCollisionSize.Y * 0.5f +
              0.35f
            : HealthBarYOffset;

        if (_healthBarBackground != null)
            _healthBarBackground.Position = new Vector3(0f, y, 0f);

        if (_healthBarFill != null)
            _healthBarFill.Position = new Vector3(0f, y, 0.01f);
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
            if (_stepTimer > 0f)
{
    _stepTimer -= dt;

    float progress =
        1f -
        Mathf.Clamp(
            _stepTimer / StepDuration,
            0f,
            1f
        );

    progress =
        progress * progress *
        (3f - 2f * progress);

    Vector3 pos =
        GlobalPosition;

    pos.Y =
        Mathf.Lerp(
            _stepStartY,
            _stepTargetY,
            progress
        );

    GlobalPosition =
        pos;
}

        if (_stepCooldownTimer > 0f)
        {
            _stepCooldownTimer -= dt;
        }

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

        if (_isBaby)
        {
            _babyGrowthTimer -= dt;

            if (_babyGrowthTimer <= 0f)
            {
                GrowUp();
            }
        }

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

        UpdateHitFeedback(dt);

        Vector3 velocity =
            Velocity;

        // -----------------------------------------------------
        // GRAVITY
        // -----------------------------------------------------

        if (!IsOnFloor())
        {
            velocity.Y -=
                Gravity * dt;
        }
        else
        {
            velocity.Y = 0f;

            if (_mountedJumpRequested)
            {
                velocity.Y =
                    _mountedJumpVelocity *
                    MountJumpMultiplier;

                _mountedJumpRequested = false;
            }
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

                _state =
                    State.Idle;

                _hasMovementTarget =
                    false;

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

            velocity.Y =
                _knockbackVelocity.Y;

            velocity.Z =
                _knockbackVelocity.Z;

            Velocity =
                velocity;

            MoveAndSlide();

            return;
        }

        // -----------------------------------------------------
        // SLEEPING
        // -----------------------------------------------------

        if (_isSleeping)
        {
            velocity.X = 0f;
            velocity.Z = 0f;

            if (_animation != null)
            {
                _animation.Update(
                    dt,
                    velocity
                );
            }

            UpdateHealthBar();

            Velocity =
                velocity;

            MoveAndSlide();

            return;
        }

        // -----------------------------------------------------
        // MOUNTED
        // -----------------------------------------------------

        if (IsMounted)
        {
            float mountSpeed =
                GetMountSpeed(
                    _mountedSprinting
                );

            if (_mountedMoveDirection.LengthSquared() >
                0.0001f)
            {
                Vector3 moveDirection =
                    _mountedMoveDirection.Normalized();

                velocity.X =
                    moveDirection.X *
                    mountSpeed;

                velocity.Z =
                    moveDirection.Z *
                    mountSpeed;

                FaceDirection(
                    moveDirection,
                    dt
                );
            }
            else
            {
                velocity.X =
                    Mathf.MoveToward(
                        velocity.X,
                        0f,
                        mountSpeed * 10f * dt
                    );

                velocity.Z =
                    Mathf.MoveToward(
                        velocity.Z,
                        0f,
                        mountSpeed * 10f * dt
                    );
            }
        }
        else
        {
            // -------------------------------------------------
            // AI
            // -------------------------------------------------

            UpdateState(dt);

            RunState(dt);

            ApplySimpleAIMovement(
                dt,
                ref velocity
            );
        }

        // -----------------------------------------------------
        // VISUALS
        // -----------------------------------------------------

        if (_animation != null)
        {
            _animation.Update(
                dt,
                velocity
            );
        }

        UpdateHealthBar();

        Velocity =
            velocity;

        MoveAndSlide();
    }


    // =========================================================
    // SIMPLE AI MOVEMENT
    // =========================================================
    //
    // This intentionally ignores Y.
    //
    // The AI only decides:
    //
    // "I want to move in this X/Z direction."
    //
    // Physics decides whether that movement is possible.
    //
    // A one-block obstacle is handled by TryStepUp().
    //
    // A taller obstacle is treated as a wall and the AI
    // chooses another direction.
    //
    // Falling is allowed.
    //
    // =========================================================

    private void ApplySimpleAIMovement(
        float dt,
        ref Vector3 velocity)
    {
        if (_isSleeping)
        {
            velocity.X = 0f;
            velocity.Z = 0f;
            return;
        }

        if (!_hasMovementTarget)
        {
            velocity.X =
                Mathf.MoveToward(
                    velocity.X,
                    0f,
                    MoveSpeed * 6f * dt
                );

            velocity.Z =
                Mathf.MoveToward(
                    velocity.Z,
                    0f,
                    MoveSpeed * 6f * dt
                );

            return;
        }

        Vector3 toTarget =
            _movementTarget -
            GlobalPosition;

        toTarget.Y = 0f;

        float distance =
            toTarget.Length();

        // Target reached.
        if (distance < 0.35f)
        {
            _hasMovementTarget = false;

            velocity.X = 0f;
            velocity.Z = 0f;

            if (_state == State.Wander ||
                _state == State.Flee)
            {
                EnterIdle();
            }

            return;
        }

        Vector3 direction =
            toTarget.Normalized();

        // -----------------------------------------------------
        // CHECK WHETHER WE ARE ABOUT TO HIT A WALL
        // -----------------------------------------------------

        if (IsMovementBlocked(direction))
        {
            // Try walking up one normal block.
            if (TryStepUp(direction))
            {
                // We stepped successfully.
                velocity.X =
                    direction.X * MoveSpeed;

                velocity.Z =
                    direction.Z * MoveSpeed;

                FaceDirection(
                    direction,
                    dt
                );

                return;
            }

            // -------------------------------------------------
            // REAL WALL
            // -------------------------------------------------
            //
            // Don't push directly into it.
            //
            // Instead choose a sideways direction.
            //

            Vector3 alternate =
                FindAlternateDirection(
                    direction
                );

            if (alternate != Vector3.Zero)
            {
                velocity.X =
                    alternate.X *
                    MoveSpeed *
                    0.8f;

                velocity.Z =
                    alternate.Z *
                    MoveSpeed *
                    0.8f;

                FaceDirection(
                    alternate,
                    dt
                );

                return;
            }

            // No valid direction.
            velocity.X =
                Mathf.MoveToward(
                    velocity.X,
                    0f,
                    MoveSpeed * 8f * dt
                );

            velocity.Z =
                Mathf.MoveToward(
                    velocity.Z,
                    0f,
                    MoveSpeed * 8f * dt
                );

            return;
        }

        // -----------------------------------------------------
        // NORMAL MOVEMENT
        // -----------------------------------------------------

        velocity.X =
            direction.X *
            MoveSpeed;

        velocity.Z =
            direction.Z *
            MoveSpeed;

        FaceDirection(
            direction,
            dt
        );

        CheckIfStuck(dt);
    }


    // =========================================================
    // MOVEMENT BLOCK CHECK
    // =========================================================
    //
    // Uses the actual CharacterBody collision.
    //
    // This means the same code automatically works for:
    //
    // Pig
    // Beefalo
    // Baby mobs
    // Future mobs of different sizes
    //
    // =========================================================

    private bool IsMovementBlocked(
        Vector3 direction)
    {
        if (direction.LengthSquared() <
            0.0001f)
        {
            return false;
        }

        direction.Y = 0f;
        direction = direction.Normalized();

        float checkDistance =
            Mathf.Max(
                StepCheckDistance,
                0.15f
            );

        Transform3D testTransform =
            GlobalTransform;

        Vector3 motion =
            direction *
            checkDistance;

        return TestMove(
            testTransform,
            motion
        );
    }


    // =========================================================
    // AUTOMATIC STEP UP
    // =========================================================
    //
    // We do NOT raycast for a floor and teleport onto it.
    //
    // Instead:
    //
    // 1. Test normal movement.
    // 2. Raise the whole collision box one block.
    // 3. Test moving forward while raised.
    // 4. If it fits, move the body onto the raised position.
    //
    // Because TestMove uses this CharacterBody's actual
    // collision shape, the mob's real size is respected.
    //
    // =========================================================

    private bool TryStepUp(
        Vector3 direction)
    {
        if (_stepCooldownTimer > 0f)
            return false;

        if (!IsOnFloor())
            return false;

        if (!_collisionBuilt ||
            _modelCollision == null)
        {
            return false;
        }

        if (direction.LengthSquared() <
            0.0001f)
        {
            return false;
        }

        direction.Y = 0f;
        direction = direction.Normalized();

        float step =
            Mathf.Max(
                0.1f,
                StepHeight
            );

        float forwardDistance =
            Mathf.Max(
                StepCheckDistance,
                0.25f
            );

        // -----------------------------------------------------
        // FIRST:
        // Make sure the mob actually has an obstacle ahead.
        // -----------------------------------------------------

        if (!TestMove(
                GlobalTransform,
                direction * forwardDistance))
        {
            return false;
        }

        // -----------------------------------------------------
        // SECOND:
        // Raise the body by one block and move forward.
        // -----------------------------------------------------

        Transform3D raisedTransform =
            GlobalTransform;

        raisedTransform.Origin +=
            Vector3.Up * step;

        Vector3 raisedMotion =
            direction *
            forwardDistance;

        KinematicCollision3D collision =
            new KinematicCollision3D();

        bool blockedAtRaisedPosition =
            TestMove(
                raisedTransform,
                raisedMotion,
                collision
            );

        if (blockedAtRaisedPosition)
        {
            // This is effectively a wall.
            return false;
        }

        // -----------------------------------------------------
        // THIRD:
        // Make sure there is actually something underneath
        // the raised position.
        //
        // We don't want a random vertical teleport.
        // -----------------------------------------------------

        Transform3D finalTransform =
            raisedTransform;

        finalTransform.Origin +=
            direction *
            forwardDistance;

        KinematicCollision3D downwardCollision =
            new KinematicCollision3D();

        bool wouldCollideDownward =
            TestMove(
                finalTransform,
                Vector3.Down * 0.25f,
                downwardCollision
            );

        // A downward collision here is good: it means there
        // is floor underneath the stepped position.
        //
        // If there isn't floor, allow normal movement/falling
        // instead of creating an artificial step.
        if (!wouldCollideDownward)
        {
            return false;
        }

        // -----------------------------------------------------
        // APPLY THE STEP
        // -----------------------------------------------------

        _stepTimer = StepDuration;

_stepStartY =
    GlobalPosition.Y;

_stepTargetY =
    finalTransform.Origin.Y;

GlobalPosition =
    new Vector3(
        finalTransform.Origin.X,
        GlobalPosition.Y,
        finalTransform.Origin.Z
    );

velocityAfterStep();

_stepCooldownTimer =
    StepCooldown;

_stuckCheckFailCount = 0;

_stuckCheckLastPosition =
    GlobalPosition;

return true;
    }


    // =========================================================
    // STEP VELOCITY RESET
    // =========================================================

    private void velocityAfterStep()
    {
        Velocity =
            new Vector3(
                Velocity.X,
                0f,
                Velocity.Z
            );
    }


    // =========================================================
    // ALTERNATE WALL DIRECTION
    // =========================================================
    //
    // If the direct route is blocked by a 2+ block wall,
    // try left and right.
    //
    // This is deliberately simple.
    //
    // It is not a heavy pathfinding system.
    // =========================================================

    private Vector3 FindAlternateDirection(
        Vector3 desired)
    {
        desired.Y = 0f;

        if (desired.LengthSquared() <
            0.0001f)
        {
            return Vector3.Zero;
        }

        desired =
            desired.Normalized();

        Vector3 left =
            new Vector3(
                -desired.Z,
                0f,
                desired.X
            ).Normalized();

        Vector3 right =
            -left;

        // Try the side that gets us closer to the target.
        float leftScore =
            DirectionTargetScore(left);

        float rightScore =
            DirectionTargetScore(right);

        if (leftScore >= rightScore)
        {
            if (IsDirectionUsable(left))
                return left;

            if (IsDirectionUsable(right))
                return right;
        }
        else
        {
            if (IsDirectionUsable(right))
                return right;

            if (IsDirectionUsable(left))
                return left;
        }

        return Vector3.Zero;
    }


    // =========================================================
    // DIRECTION SCORE
    // =========================================================

    private float DirectionTargetScore(
        Vector3 direction)
    {
        Vector3 toTarget =
            _movementTarget -
            GlobalPosition;

        toTarget.Y = 0f;

        if (toTarget.LengthSquared() <
            0.0001f)
        {
            return 0f;
        }

        toTarget =
            toTarget.Normalized();

        return direction.Dot(
            toTarget
        );
    }


    // =========================================================
    // DIRECTION USABLE
    // =========================================================

    private bool IsDirectionUsable(
        Vector3 direction)
    {
        if (direction.LengthSquared() <
            0.0001f)
        {
            return false;
        }

        direction.Y = 0f;
        direction = direction.Normalized();

        if (!IsMovementBlocked(direction))
            return true;

        return TryStepTestOnly(direction);
    }


    // =========================================================
    // STEP TEST WITHOUT APPLYING
    // =========================================================

    private bool TryStepTestOnly(
        Vector3 direction)
    {
        if (!_collisionBuilt ||
            !IsOnFloor())
        {
            return false;
        }

        float step =
            Mathf.Max(
                0.1f,
                StepHeight
            );

        float distance =
            Mathf.Max(
                StepCheckDistance,
                0.25f
            );

        Transform3D raised =
            GlobalTransform;

        raised.Origin +=
            Vector3.Up * step;

        KinematicCollision3D collision =
            new KinematicCollision3D();

        if (TestMove(
                raised,
                direction * distance,
                collision))
        {
            return false;
        }

        Transform3D finalTransform =
            raised;

        finalTransform.Origin +=
            direction *
            distance;

        KinematicCollision3D floorCollision =
            new KinematicCollision3D();

        return TestMove(
            finalTransform,
            Vector3.Down * 0.25f,
            floorCollision
        );
    }


    // =========================================================
    // HIT FEEDBACK
    // =========================================================

    private void UpdateHitFeedback(
        float dt)
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
    // APPLY KNOCKBACK
    // =========================================================

    public void ApplyKnockback(
        Vector3 direction,
        float force)
    {
        if (_health <= 0f)
            return;

        if (force <= 0f)
            return;

        direction.Y = 0f;

        if (direction.LengthSquared() <
            0.0001f)
        {
            return;
        }

        direction =
            direction.Normalized();

        _knockbackVelocity =
            direction * force;

        _knockbackVelocity.Y =
            force *
            KnockbackVerticalMultiplier;

        _knockbackTimer =
            KnockbackDuration;

        _stuckCheckFailCount =
            0;

        _stuckCheckLastPosition =
            GlobalPosition;
    }


    // =========================================================
    // FUR / SHEARING
    // =========================================================

    public bool TryShear(
        bool isSleeping)
    {
        if (!HasFur)
            return false;

        if (!CanBeSheared)
            return false;

        HasFur = false;

        if (!string.IsNullOrWhiteSpace(FurItem))
        {
            var pickup =
                new ItemPickup();

            pickup.ItemId =
                FurItem;

            pickup.Count =
                FurShearAmount;

            pickup.TossVelocity =
                new Vector3(
                    _rng.RandfRange(
                        -0.8f,
                        0.8f
                    ),

                    _rng.RandfRange(
                        1.5f,
                        2.2f
                    ),

                    _rng.RandfRange(
                        -0.8f,
                        0.8f
                    )
                );

            pickup.GlobalPosition =
                GlobalPosition +
                Vector3.Up * 0.5f;

            GetParent().AddChild(
                pickup
            );
        }

        if (!isSleeping &&
            AngerWhenShearedAwake)
        {
            BecomeHostile();
        }

        GD.Print(
            $"[Mob] {Name} was sheared. " +
            $"Fur={FurItem} Amount={FurShearAmount}"
        );

        return true;
    }


    // =========================================================
    // BECOME HOSTILE
    // =========================================================

    private void BecomeHostile(
        bool alertNearby = true)
    {
        if (_health <= 0f)
            return;

        bool wasAlreadyHostile =
            BehaviorType ==
            MobBehaviorType.Hostile;

        BehaviorType =
            MobBehaviorType.Hostile;

        if (_isSleeping)
        {
            SetSleeping(false);
        }

        _threat = null;

        _hostileTarget = null;

        _hasMovementTarget = false;

        _attackTimer = 0f;

        if (alertNearby &&
            IsBeefalo())
        {
            AlertNearbyBeefalo();
        }

        Node3D closestTarget =
            FindClosestValidTarget();

        if (closestTarget != null)
        {
            _hostileTarget =
                closestTarget;

            float distance =
                GlobalPosition.DistanceTo(
                    closestTarget.GlobalPosition
                );

            if (distance <= AttackRange)
            {
                _state =
                    State.Attack;
            }
            else
            {
                _state =
                    State.Chase;
            }
        }
        else
        {
            _state =
                State.Idle;
        }

        if (!wasAlreadyHostile)
        {
            GD.Print(
                $"[Mob] {Name} became HOSTILE."
            );
        }
    }


    // =========================================================
    // CHECK BEEFALO
    // =========================================================

    private bool IsBeefalo()
    {
        if (_definition == null)
            return false;

        return string.Equals(
            _definition.id,
            "beefalo",
            StringComparison.OrdinalIgnoreCase
        );
    }


    // =========================================================
    // ALERT BEEFALO
    // =========================================================

    private void AlertNearbyBeefalo()
    {
        float alertRange =
            Mathf.Max(
                DetectionRange,
                1f
            );

        float alertRangeSquared =
            alertRange *
            alertRange;

        foreach (Node node in
                 GetTree().GetNodesInGroup("mobs"))
        {
            if (node == this)
                continue;

            if (node is not Mob other)
                continue;

            if (!IsInstanceValid(other))
                continue;

            if (other.Health <= 0f)
                continue;

            if (!other.IsBeefalo())
                continue;

            Vector3 difference =
                other.GlobalPosition -
                GlobalPosition;

            if (difference.LengthSquared() >
                alertRangeSquared)
            {
                continue;
            }

            other.BecomeHostile(false);
        }
    }


    // =========================================================
    // FIND CLOSEST VALID TARGET
    // =========================================================

    private Node3D FindClosestValidTarget()
    {
        Node3D closest = null;

        float closestDistanceSquared =
            float.MaxValue;

        foreach (Node node in
                 GetTree().GetNodesInGroup("player"))
        {
            if (node is not Node3D candidate)
                continue;

            if (!IsInstanceValid(candidate))
                continue;

            float distanceSquared =
                GlobalPosition.DistanceSquaredTo(
                    candidate.GlobalPosition
                );

            if (distanceSquared <
                closestDistanceSquared)
            {
                closest = candidate;
                closestDistanceSquared =
                    distanceSquared;
            }
        }

        if (IsBeefalo())
        {
            foreach (Node node in
                     GetTree().GetNodesInGroup("mobs"))
            {
                if (node == this)
                    continue;

                if (node is not Mob otherMob)
                    continue;

                if (!IsInstanceValid(otherMob))
                    continue;

                if (otherMob.Health <= 0f)
                    continue;

                if (otherMob.IsBeefalo())
                    continue;

                float distanceSquared =
                    GlobalPosition.DistanceSquaredTo(
                        otherMob.GlobalPosition
                    );

                if (distanceSquared <
                    closestDistanceSquared)
                {
                    closest = otherMob;
                    closestDistanceSquared =
                        distanceSquared;
                }
            }
        }

        if (closest == null &&
            _player != null &&
            IsInstanceValid(_player))
        {
            closest =
                _player;
        }

        return closest;
    }


    // =========================================================
    // BREEDING
    // =========================================================

    private void TryFindBreedingPartner()
    {
        if (!CanBreed ||
            !_breedingReady)
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
                other._definition.id !=
                _definition.id)
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
               !_isBaby &&
               IsCurrentSeasonAllowedForBreeding();
    }


    private bool IsCurrentSeasonAllowedForBreeding()
    {
        if (_definition == null ||
            _definition.breeding == null)
        {
            return false;
        }

        string[] allowedSeasons =
            _definition.breeding.seasons;

        if (allowedSeasons == null ||
            allowedSeasons.Length == 0)
        {
            return true;
        }

        SeasonManager seasonManager =
            GetTree().Root.FindChild(
                "SeasonManager",
                true,
                false
            ) as SeasonManager;

        if (seasonManager == null)
        {
            GD.PrintErr(
                $"[Mob] {Name}: SeasonManager not found. " +
                "Seasonal breeding is blocked."
            );

            return false;
        }

        string currentSeason =
            seasonManager.GetSeasonName();

        foreach (string allowedSeason in
                 allowedSeasons)
        {
            if (string.IsNullOrWhiteSpace(
                allowedSeason))
            {
                continue;
            }

            if (string.Equals(
                allowedSeason.Trim(),
                currentSeason,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private void BreedWith(
        Mob partner)
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

    private void SpawnBabies(
        Mob partner)
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

        for (int i = 0;
             i < litterSize;
             i++)
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

            GetParent().AddChild(
                baby
            );

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

        float growthMin = 300f;
        float growthMax = 900f;

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

        _state =
            State.Idle;

        _hasMovementTarget =
            false;

        _hostileTarget = null;

        _idleTimer =
            _rng.RandfRange(
                MinIdleTime,
                MaxIdleTime
            );

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
            if (_mobModel != null)
            {
                _mobModel.Scale =
                    _adultModelScale;
            }

            UpdateCollisionScale(
                1f
            );
        }

        _state =
            State.Idle;

        _hasMovementTarget =
            false;

        _hostileTarget = null;

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

    private void UpdateState(
        float dt)
    {
        if (_isSleeping)
            return;

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
        // HOSTILE
        // -----------------------------------------------------

        if (BehaviorType ==
            MobBehaviorType.Hostile)
        {
            Node3D closestTarget =
                FindClosestValidTarget();

            if (closestTarget != null)
            {
                _hostileTarget =
                    closestTarget;

                float distance =
                    GlobalPosition.DistanceTo(
                        closestTarget.GlobalPosition
                    );

                if (distance <= AttackRange)
                {
                    _state =
                        State.Attack;

                    _hasMovementTarget =
                        false;

                    return;
                }

                if (distance <=
                    DetectionRange)
                {
                    if (_state != State.Chase)
                    {
                        _state =
                            State.Chase;
                    }

                    return;
                }
            }

            if (_state == State.Chase ||
                _state == State.Attack)
            {
                _hostileTarget = null;
                EnterIdle();
            }
        }

        // -----------------------------------------------------
        // PASSIVE
        // -----------------------------------------------------

        if (_state == State.Idle)
        {
            _idleTimer -= dt;

            if (_idleTimer <= 0f)
            {
                PickWanderTarget();
            }
        }
        else if (_state == State.Wander &&
                 !_hasMovementTarget)
        {
            EnterIdle();
        }
    }


    // =========================================================
    // STATE ACTIONS
    // =========================================================

    private void RunState(
        float dt)
    {
        if (_isSleeping)
            return;

        switch (_state)
        {
            case State.Chase:

                if (_hostileTarget != null &&
                    IsInstanceValid(_hostileTarget))
                {
                    float distance =
                        GlobalPosition.DistanceTo(
                            _hostileTarget.GlobalPosition
                        );

                    if (distance <= AttackRange)
                    {
                        _state =
                            State.Attack;

                        _hasMovementTarget =
                            false;

                        return;
                    }

                    if (distance <=
                        DetectionRange)
                    {
                        SetMovementTarget(
                            _hostileTarget.GlobalPosition
                        );
                    }
                    else
                    {
                        EnterIdle();
                    }
                }
                else
                {
                    EnterIdle();
                }

                break;


            case State.Flee:

                if (_threat != null &&
                    IsInstanceValid(_threat))
                {
                    Vector3 away =
                        GlobalPosition -
                        _threat.GlobalPosition;

                    away.Y = 0f;

                    if (away.LengthSquared() <
                        0.0001f)
                    {
                        away =
                            Vector3.Forward;
                    }
                    else
                    {
                        away =
                            away.Normalized();
                    }

                    SetMovementTarget(
                        GlobalPosition +
                        away * FleeDistance
                    );
                }

                break;


            case State.Attack:

                _attackTimer -= dt;

                if (_attackTimer <= 0f)
                {
                    _attackTimer =
                        AttackInterval;

                    Node3D attackTarget =
                        FindClosestValidTarget();

                    if (attackTarget != null &&
                        IsInstanceValid(attackTarget))
                    {
                        _hostileTarget =
                            attackTarget;

                        float distance =
                            GlobalPosition.DistanceTo(
                                attackTarget.GlobalPosition
                            );

                        if (distance >
                            AttackRange)
                        {
                            _state =
                                State.Chase;

                            return;
                        }

                        DealAttackDamage(
                            attackTarget
                        );
                    }
                    else
                    {
                        _hostileTarget = null;
                        EnterIdle();
                    }
                }

                break;
        }
    }


    // =========================================================
    // SET MOVEMENT TARGET
    // =========================================================

    private void SetMovementTarget(
        Vector3 target)
    {
        _movementTarget =
            new Vector3(
                target.X,
                GlobalPosition.Y,
                target.Z
            );

        _hasMovementTarget =
            true;
    }


    // =========================================================
    // DEAL ATTACK DAMAGE
    // =========================================================

    private void DealAttackDamage(
        Node3D target)
    {
        if (target == null ||
            !IsInstanceValid(target))
        {
            return;
        }

        if (target is Mob targetMob)
        {
            if (targetMob == this ||
                targetMob.Health <= 0f)
            {
                return;
            }

            if (IsBeefalo() &&
                targetMob.IsBeefalo())
            {
                return;
            }

            targetMob.TakeDamage(
                AttackDamage,
                GlobalPosition,
                this
            );

            GD.Print(
                $"[Mob] {Name} hits {targetMob.Name} " +
                $"for {AttackDamage} damage."
            );

            return;
        }

        if (target is Player player)
        {
            player.TakeDamage(
                AttackDamage
            );

            GD.Print(
                $"[Mob] {Name} attacks the player " +
                $"for {AttackDamage} damage."
            );

            return;
        }

        if (IsPlayerNode(target))
        {
            GD.Print(
                $"[Mob] {Name} found a player target, " +
                $"but the target is not a Player instance."
            );
        }
    }


    // =========================================================
    // CHECK PLAYER NODE
    // =========================================================

    private bool IsPlayerNode(
        Node3D node)
    {
        if (node == null ||
            !IsInstanceValid(node))
        {
            return false;
        }

        if (node.IsInGroup("player"))
            return true;

        return node.Name.ToString()
            .Equals(
                "player",
                StringComparison.OrdinalIgnoreCase
            );
    }


    // =========================================================
    // IDLE
    // =========================================================

    private void EnterIdle()
    {
        _state =
            State.Idle;

        _hasMovementTarget =
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
                0f,
                offset.Y
            );

        SetMovementTarget(
            target
        );

        _state =
            State.Wander;
    }


    // =========================================================
    // STUCK CHECK
    // =========================================================

    private void CheckIfStuck(
        float dt)
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
                _hasMovementTarget =
                    false;

                _stuckCheckFailCount = 0;

                _idleTimer =
                    _rng.RandfRange(
                        MinIdleTime,
                        MaxIdleTime
                    );
            }
        }
        else
        {
            _stuckCheckFailCount =
                0;
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
    // MOUNT / RIDING
    // =========================================================

    public bool CanRide()
    {
        if (!MountEnabled)
            return false;

        if (IsMounted)
            return false;

        if (_health <= 0f)
            return false;

        if (HasSaddle)
            return true;

        return _health >= MaxHealth;
    }


    public bool EquipSaddle()
    {
        if (!MountEnabled)
            return false;

        if (!SaddleAllowed)
            return false;

        if (HasSaddle)
            return false;

        if (_health <= 0f)
            return false;

        HasSaddle = true;

        GD.Print(
            $"[Mob] {Name} was saddled."
        );

        return true;
    }


    public bool RemoveSaddle()
    {
        if (!HasSaddle)
            return false;

        if (IsMounted)
            return false;

        HasSaddle = false;

        GD.Print(
            $"[Mob] {Name} saddle removed."
        );

        return true;
    }


    public bool StartRiding(
        Player player)
    {
        if (player == null ||
            !IsInstanceValid(player))
        {
            return false;
        }

        if (!CanRide())
            return false;

        _mountedPlayer =
            player;

        _mountedMoveDirection =
            Vector3.Zero;

        _mountedSprinting = false;

        _mountedJumpRequested = false;

        _state =
            State.Idle;

        _hasMovementTarget =
            false;

        _hostileTarget = null;

        _threat = null;

        Velocity =
            Vector3.Zero;

        GD.Print(
            $"[Mob] {Name} is now being ridden by {player.Name}."
        );

        return true;
    }


    public void StopRiding()
    {
        if (!IsMounted)
        {
            _mountedPlayer = null;
            return;
        }

        Player rider =
            _mountedPlayer;

        _mountedMoveDirection =
            Vector3.Zero;

        _mountedSprinting = false;

        _mountedJumpRequested = false;

        _mountedPlayer = null;

        Velocity =
            Vector3.Zero;

        _state =
            State.Idle;

        _hasMovementTarget =
            false;

        _hostileTarget = null;

        _idleTimer =
            _rng.RandfRange(
                MinIdleTime,
                MaxIdleTime
            );

        GD.Print(
            $"[Mob] {Name} was dismounted by {rider.Name}."
        );
    }


    public float GetMountSpeed(
        bool sprinting)
    {
        float speed =
            MoveSpeed *
            MountSpeedMultiplier;

        if (sprinting &&
            MountCanSprint)
        {
            speed *=
                MountSprintMultiplier;
        }

        return speed;
    }


    public void JumpFromPlayer(
        float playerJumpVelocity)
    {
        if (!IsMounted)
            return;

        if (!IsOnFloor())
            return;

        _mountedJumpVelocity =
            playerJumpVelocity;

        _mountedJumpRequested =
            true;
    }


    public float GetMountJumpVelocity(
        float playerJumpVelocity)
    {
        return playerJumpVelocity *
               MountJumpMultiplier;
    }


    public void DriveFromPlayer(
        Vector3 direction,
        bool sprinting)
    {
        if (!IsMounted)
            return;

        direction.Y = 0f;

        if (direction.LengthSquared() >
            0.0001f)
        {
            direction =
                direction.Normalized();
        }
        else
        {
            direction =
                Vector3.Zero;
        }

        _mountedMoveDirection =
            direction;

        _mountedSprinting =
            sprinting;
    }


    // =========================================================
    // FOOD / FEEDING
    // =========================================================

    private bool IsBreedingFood(
        string itemId)
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
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    public bool CanEat(
        string itemId)
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

        foreach (string foodItem in
                 _definition.food.items)
        {
            if (string.Equals(
                foodItem,
                itemId,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    public bool IsBreedFood(
        string itemId)
    {
        return IsBreedingFood(itemId);
    }


    public bool TryFeed(
        string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;

        if (!CanEat(itemId))
            return false;

        return Feed(itemId);
    }


    public bool Feed(
        string itemId)
    {
        if (!CanEat(itemId))
            return false;

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
        Vector3? sourcePosition = null,
        Node3D sourceEntity = null)
    {
        if (amount <= 0f ||
            _health <= 0f)
        {
            return;
        }

        if (_isSleeping)
        {
            SetSleeping(false);
        }

        Node3D sourceTarget =
            sourceEntity;

        if (sourceTarget == null &&
            sourcePosition.HasValue)
        {
            sourceTarget =
                FindClosestPlayerToPosition(
                    sourcePosition.Value
                );
        }

        if (sourceTarget == null &&
            _player != null &&
            IsInstanceValid(_player))
        {
            sourceTarget =
                _player;
        }

        _health -= amount;

        _health =
            Mathf.Max(
                _health,
                0f
            );

        _flashTimer =
            FlashDuration;

        if (IsBeefalo())
        {
            BecomeHostile();
        }

        if (_health > 0f &&
            _fleeEnabled &&
            BehaviorType ==
                MobBehaviorType.Passive &&
            sourceTarget != null &&
            IsInstanceValid(sourceTarget) &&
            _state != State.Flee)
        {
            _threat =
                sourceTarget;

            _state =
                State.Flee;

            if (!_fleeSpeedApplied &&
                FleeSpeedMultiplier > 0f)
            {
                MoveSpeed *=
                    FleeSpeedMultiplier;

                _fleeSpeedApplied = true;
            }
        }

        if (_health <= 0f)
        {
            Die();
        }
    }


    // =========================================================
    // FIND CLOSEST PLAYER
    // =========================================================

    private Node3D FindClosestPlayerToPosition(
        Vector3 position)
    {
        Node3D closest = null;

        float closestDistanceSquared =
            float.MaxValue;

        foreach (Node node in
                 GetTree().GetNodesInGroup("player"))
        {
            if (node is not Node3D candidate)
                continue;

            if (!IsInstanceValid(candidate))
                continue;

            float distanceSquared =
                position.DistanceSquaredTo(
                    candidate.GlobalPosition
                );

            if (distanceSquared <
                closestDistanceSquared)
            {
                closest =
                    candidate;

                closestDistanceSquared =
                    distanceSquared;
            }
        }

        if (closest == null &&
            _player != null &&
            IsInstanceValid(_player))
        {
            closest =
                _player;
        }

        return closest;
    }


    // =========================================================
    // RESTORE NORMAL SPEED
    // =========================================================

    private void RestoreNormalSpeed()
    {
        if (!_fleeSpeedApplied)
            return;

        if (FleeSpeedMultiplier > 0f)
        {
            MoveSpeed /=
                FleeSpeedMultiplier;
        }

        _fleeSpeedApplied = false;
    }


    // =========================================================
    // DROP ITEMS
    // =========================================================

    private void SpawnDrops()
    {
        if (_definition == null)
            return;

        Node parent =
            GetParent();

        if (parent == null)
        {
            GD.PrintErr(
                $"[Mob] {Name}: Cannot spawn drops because parent is null."
            );

            return;
        }

        if (_definition.drops != null &&
            _definition.drops.enabled &&
            _definition.drops.items != null &&
            _definition.drops.items.Length > 0)
        {
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
                    continue;

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

                SpawnItemDrop(
                    parent,
                    drop.item,
                    count
                );
            }
        }

        if (HasFur &&
            !string.IsNullOrWhiteSpace(
                FurItem))
        {
            SpawnItemDrop(
                parent,
                FurItem,
                FurDeathAmount
            );

            GD.Print(
                $"[Mob] {Name} dropped fur: " +
                $"{FurItem} x{FurDeathAmount}"
            );
        }
    }


    // =========================================================
    // CREATE ITEM DROP
    // =========================================================

    private void SpawnItemDrop(
        Node parent,
        string itemId,
        int count)
    {
        if (parent == null ||
            string.IsNullOrWhiteSpace(itemId) ||
            count <= 0)
        {
            return;
        }

        ItemPickup pickup =
            new ItemPickup();

        pickup.ItemId =
            itemId;

        pickup.Count =
            count;

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
            $"{count}x {itemId}"
        );
    }


    // =========================================================
    // DEATH
    // =========================================================

    private void Die()
    {
        RestoreNormalSpeed();

        _hasMovementTarget =
            false;

        _hostileTarget = null;

        SpawnDrops();

        SetPhysicsProcess(false);

        QueueFree();
    }
}