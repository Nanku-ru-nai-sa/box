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
        "res://Mobs/Definitions/Animals/pig.json"
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
        Vector3 spawnPosition)
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

        GD.Print(
            $"[MobSpawner] Spawned mob from {definitionPath} at {spawnPosition}"
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