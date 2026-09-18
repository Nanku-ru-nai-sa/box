using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Generic mob spawner.
///
/// Reads mob definitions from JSON and spawns the appropriate Mob scene.
/// The spawner itself does not care whether the mob is a pig, cow,
/// chicken, etc.
///
/// Future systems can add:
/// - biome restrictions
/// - season restrictions
/// - time-of-day restrictions
/// - chunk-based spawning
/// - mob despawning
/// - population balancing
/// </summary>
public partial class MobSpawner : Node3D
{
    // ---------------------------------------------------------
    // SETTINGS
    // ---------------------------------------------------------

    [ExportGroup("Mob Scene")]

    /// <summary>
    /// Generic Mob scene used for every spawned mob.
    ///
    /// The Mob itself reads the JSON definition to determine
    /// which model, stats, behavior, food, breeding settings, etc.
    /// it should use.
    /// </summary>
    [Export]
    public PackedScene MobScene { get; set; }


    [ExportGroup("Spawn Definitions")]

    /// <summary>
    /// JSON definitions to use for spawning.
    ///
    /// For now this can contain only pig.json.
    /// Later add cow.json, chicken.json, sheep.json, etc.
    /// </summary>
    [Export]
   public string[] DefinitionPaths =
{
    "res://Mobs/Definitions/Animals/pig.json",
    "res://Mobs/Definitions/Animals/beefalo.json"
};


    [ExportGroup("Spawning")]

    /// <summary>
    /// How often the spawner attempts to create mobs.
    /// </summary>
    [Export]
    public float SpawnInterval = 10f;


    /// <summary>
    /// Maximum number of spawn attempts during one update.
    /// Prevents a bad spawn area from causing a huge burst.
    /// </summary>
    [Export]
    public int MaxSpawnAttemptsPerTick = 5;


    /// <summary>
    /// Whether this spawner starts automatically.
    /// </summary>
    [Export]
    public bool SpawnAutomatically = true;


    // ---------------------------------------------------------
    // INTERNAL STATE
    // ---------------------------------------------------------

    private readonly RandomNumberGenerator _rng =
        new RandomNumberGenerator();

    private float _spawnTimer;


    // ---------------------------------------------------------
    // READY
    // ---------------------------------------------------------

    public override void _Ready()
{
    _rng.Randomize();

    _spawnTimer = SpawnInterval;

    if (MobScene == null)
    {
        GD.PrintErr(
            "[MobSpawner] MobScene has not been assigned."
        );
    }

    if (DefinitionPaths == null ||
        DefinitionPaths.Length == 0)
    {
        GD.PrintErr(
            "[MobSpawner] No mob definition paths assigned."
        );
    }

    // Restore mobs only after the world/chunks are ready.
var chunkManager =
    GetTree().Root.FindChild(
        "ChunkManager",
        true,
        false
    ) as ChunkManager;

if (chunkManager != null)
{
    chunkManager.WorldReady += LoadSavedMobs;

    // Handle the case where the world was already ready.
    if (chunkManager.IsInitialLoadComplete)
        CallDeferred(nameof(LoadSavedMobs));
}
else
{
    GD.PrintErr(
        "[MobSpawner] ChunkManager not found. " +
        "Saved mobs cannot be restored."
    );
}
}


    // ---------------------------------------------------------
    // PROCESS
    // ---------------------------------------------------------

    public override void _Process(double delta)
    {
        if (!SpawnAutomatically)
            return;

        if (MobScene == null)
            return;

        if (DefinitionPaths == null ||
            DefinitionPaths.Length == 0)
        {
            return;
        }

        _spawnTimer -= (float)delta;

        if (_spawnTimer > 0f)
            return;

        _spawnTimer = SpawnInterval;

        TrySpawnMobs();
    }


    // ---------------------------------------------------------
    // SPAWN
    // ---------------------------------------------------------

    private void TrySpawnMobs()
    {
        int attempts = 0;

        while (attempts < MaxSpawnAttemptsPerTick)
        {
            attempts++;

            string definitionPath =
                ChooseRandomDefinition();

            if (string.IsNullOrEmpty(definitionPath))
                return;

            MobDefinition definition =
                MobDefinitionLoader.Load(
                    definitionPath
                );

            if (definition == null)
                continue;

            if (definition.spawning == null ||
    !definition.spawning.enabled)
{
    continue;
}

if (!IsCurrentSeasonAllowed(definition.spawning))
{
    continue;
}

            // -------------------------------------------------
            // Population limit
            // -------------------------------------------------

            int currentCount =
                CountMobsForDefinition(
                    definition.id
                );

            if (currentCount >=
                definition.spawning.maxWorldCount)
            {
                continue;
            }


            // -------------------------------------------------
            // Group size
            // -------------------------------------------------

            int groupSize =
                _rng.RandiRange(
                    definition.spawning.minGroupSize,
                    definition.spawning.maxGroupSize
                );


            // Don't exceed the population limit.
            int availableSlots =
                definition.spawning.maxWorldCount -
                currentCount;

            groupSize =
                Mathf.Min(
                    groupSize,
                    availableSlots
                );


            if (groupSize <= 0)
                continue;


            // -------------------------------------------------
            // Spawn group
            // -------------------------------------------------

            for (int i = 0; i < groupSize; i++)
            {
                Vector3 spawnPosition =
                    FindSpawnPosition(
                        definition
                    );

                if (spawnPosition == Vector3.Inf)
                {
                    GD.Print(
                        $"[MobSpawner] Could not find spawn position for {definition.displayName}"
                    );

                    break;
                }

                SpawnMob(
                    definitionPath,
                    spawnPosition
                );
            }

            // Spawn one group per timer tick for now.
            return;
        }
    }


    // ---------------------------------------------------------
    // CHOOSE DEFINITION
    // ---------------------------------------------------------

    private string ChooseRandomDefinition()
    {
        if (DefinitionPaths == null ||
            DefinitionPaths.Length == 0)
        {
            return null;
        }

        int index =
            _rng.RandiRange(
                0,
                DefinitionPaths.Length - 1
            );

        return DefinitionPaths[index];
    }


    // ---------------------------------------------------------
    // SPAWN MOB
    // ---------------------------------------------------------

    private void SpawnMob(
    string definitionPath,
    Vector3 spawnPosition,
    float rotationY = 0f)
{
    if (MobScene == null)
        return;

    Mob mob =
        MobScene.Instantiate<Mob>();

    if (mob == null)
    {
        GD.PrintErr(
            "[MobSpawner] MobScene did not contain a Mob."
        );

        return;
    }

    // The Mob needs to know which JSON definition to use.
    mob.DefinitionPath =
        definitionPath;

    AddChild(mob);

    mob.GlobalPosition =
        spawnPosition;

    mob.Rotation =
        new Vector3(
            0f,
            rotationY,
            0f
        );

    GD.Print(
        $"[MobSpawner] Spawned mob from {definitionPath} " +
        $"at {spawnPosition} facing Y={rotationY}"
    );
}
// ---------------------------------------------------------
// LOAD SAVED MOBS
// ---------------------------------------------------------

private void LoadSavedMobs()
{
    var chunkManager =
        GetTree().Root.FindChild(
            "ChunkManager",
            true,
            false
        ) as ChunkManager;

    if (chunkManager != null)
        chunkManager.WorldReady -= LoadSavedMobs;
        
    if (SaveManager.Instance == null)
    {
        GD.PrintErr(
            "[MobSpawner] SaveManager is not available."
        );

        return;
    }

    if (MobScene == null)
    {
        GD.PrintErr(
            "[MobSpawner] Cannot restore mobs because MobScene is null."
        );

        return;
    }

    List<SavedMobData> savedMobs =
        SaveManager.Instance.LoadWorldMobs();

    if (savedMobs == null ||
        savedMobs.Count == 0)
    {
        GD.Print(
            "[MobSpawner] No saved mobs to restore."
        );

        return;
    }

    foreach (SavedMobData saved in savedMobs)
    {
        if (saved == null)
            continue;

        if (string.IsNullOrEmpty(
            saved.DefinitionPath))
        {
            continue;
        }

        if (saved.Position == null ||
            saved.Position.Length != 3)
        {
            continue;
        }

        Vector3 position =
            new Vector3(
                saved.Position[0],
                saved.Position[1],
                saved.Position[2]
            );

        SpawnMob(
    saved.DefinitionPath,
    position,
    saved.RotationY
);
    }

    GD.Print(
        $"[MobSpawner] Restored {savedMobs.Count} saved mobs."
    );
}

    // ---------------------------------------------------------
    // COUNT MOBS
    // ---------------------------------------------------------

    private int CountMobsForDefinition(
        string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
            return 0;

        int count = 0;

        foreach (Node child in GetTree().GetNodesInGroup("mobs"))
        {
            if (child is not Mob mob)
                continue;

            if (mob.Definition == null)
                continue;

            if (mob.Definition.id ==
                definitionId)
            {
                count++;
            }
        }

        return count;
    }


    // ---------------------------------------------------------
    // FIND SPAWN POSITION
    // ---------------------------------------------------------
private bool IsCurrentSeasonAllowed(MobSpawnSettings spawning)
{
    if (spawning == null)
        return false;

    // No seasons listed = allowed all year.
    if (spawning.seasons == null ||
        spawning.seasons.Length == 0)
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
            "[MobSpawner] SeasonManager not found. " +
            "Seasonal spawning is blocked."
        );

        return false;
    }

    string currentSeason =
        seasonManager.GetSeasonName();

    foreach (string allowedSeason in spawning.seasons)
    {
        if (string.IsNullOrWhiteSpace(allowedSeason))
            continue;

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
    private Vector3 FindSpawnPosition(
        MobDefinition definition)
    {
        if (definition == null ||
            definition.spawning == null)
        {
            return Vector3.Inf;
        }


        float minDistance =
            Mathf.Max(
                1f,
                definition.spawning.minSpawnDistance
            );


        float maxDistance =
            Mathf.Max(
                minDistance,
                definition.spawning.maxSpawnDistance
            );


        // -----------------------------------------------------
        // Pick a random direction.
        // -----------------------------------------------------

        float angle =
            _rng.RandfRange(
                0f,
                Mathf.Tau
            );


        Vector3 direction =
            new Vector3(
                Mathf.Cos(angle),
                0f,
                Mathf.Sin(angle)
            );


        // -----------------------------------------------------
        // Pick a random distance.
        // -----------------------------------------------------

        float distance =
            _rng.RandfRange(
                minDistance,
                maxDistance
            );


        Vector3 position =
            GlobalPosition +
            direction * distance;


        // -----------------------------------------------------
        // Find ground.
        //
        // For now we use a physics ray.
        // Later this can be replaced with a dedicated
        // voxel spawn-position system.
        // -----------------------------------------------------

        Vector3 rayStart =
            position +
            Vector3.Up * 32f;


        Vector3 rayEnd =
            position -
            Vector3.Up * 32f;


        var spaceState =
            GetWorld3D().DirectSpaceState;


        var query =
            PhysicsRayQueryParameters3D.Create(
                rayStart,
                rayEnd
            );


        query.CollideWithAreas = false;
        query.CollideWithBodies = true;


        var result =
            spaceState.IntersectRay(query);


        if (result.Count == 0)
            return Vector3.Inf;


        if (!result.ContainsKey("position"))
            return Vector3.Inf;


        Vector3 groundPosition =
            (Vector3)result["position"];


        // Put the mob slightly above the ground so
        // its CharacterBody3D can settle naturally.
        groundPosition.Y += 0.15f;


        return groundPosition;
    }
}