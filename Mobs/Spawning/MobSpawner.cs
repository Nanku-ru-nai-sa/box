using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class MobSpawner : Node3D
{
    // ---------------------------------------------------------
    // SETTINGS
    // ---------------------------------------------------------

    [ExportGroup("Mob Scene")]

    [Export]
    public PackedScene MobScene { get; set; }


    [ExportGroup("Spawning")]

    [Export]
    public float SpawnInterval = 10f;

    [Export]
    public int MaxSpawnAttemptsPerTick = 5;

    [Export]
    public bool SpawnAutomatically = true;


    // ---------------------------------------------------------
    // INTERNAL STATE
    // ---------------------------------------------------------

    private readonly RandomNumberGenerator _rng =
        new RandomNumberGenerator();

    private float _spawnTimer;

    private string[] DefinitionPaths =
        Array.Empty<string>();


    // ---------------------------------------------------------
    // READY
    // ---------------------------------------------------------

    public override void _Ready()
    {
        AddToGroup("mob_spawner");

        _rng.Randomize();

        _spawnTimer =
            SpawnInterval;

        // -----------------------------------------------------
        // FIND ALL MOB JSON DEFINITIONS AUTOMATICALLY
        // -----------------------------------------------------

        DefinitionPaths =
            FindAllMobDefinitions();

        GD.Print(
            $"[MobSpawner] Found " +
            $"{DefinitionPaths.Length} mob definitions."
        );

        foreach (string definitionPath in DefinitionPaths)
        {
            GD.Print(
                $"[MobSpawner] Definition: " +
                $"{definitionPath}"
            );
        }

        if (MobScene == null)
        {
            GD.PrintErr(
                "[MobSpawner] MobScene has not been assigned."
            );
        }

        // -----------------------------------------------------
        // RESTORE SAVED MOBS ONLY AFTER WORLD IS READY
        // -----------------------------------------------------

        var chunkManager =
            GetTree().Root.FindChild(
                "ChunkManager",
                true,
                false
            ) as ChunkManager;

        if (chunkManager != null)
        {
            chunkManager.WorldReady +=
                LoadSavedMobs;

            // Handle the case where the world
            // was already ready.
            if (chunkManager.IsInitialLoadComplete)
            {
                CallDeferred(
                    nameof(LoadSavedMobs)
                );
            }
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

        _spawnTimer -=
            (float)delta;

        if (_spawnTimer > 0f)
            return;

        _spawnTimer =
            SpawnInterval;

        TrySpawnMobs();
    }


    // ---------------------------------------------------------
    // AUTOMATIC SPAWN
    // ---------------------------------------------------------

    private void TrySpawnMobs()
    {
        int attempts = 0;

        while (
            attempts <
            MaxSpawnAttemptsPerTick)
        {
            attempts++;

            string definitionPath =
                ChooseRandomDefinition();

            if (string.IsNullOrEmpty(
                definitionPath))
            {
                return;
            }

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

            if (!IsCurrentSeasonAllowed(
                definition.spawning))
            {
                continue;
            }

            // -------------------------------------------------
            // POPULATION LIMIT
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
            // GROUP SIZE
            // -------------------------------------------------

            int groupSize =
                _rng.RandiRange(
                    definition.spawning.minGroupSize,
                    definition.spawning.maxGroupSize
                );

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
            // SPAWN GROUP
            // -------------------------------------------------

            for (
                int i = 0;
                i < groupSize;
                i++)
            {
                Vector3 spawnPosition =
                    FindSpawnPosition(
                        definition
                    );

                if (spawnPosition ==
                    Vector3.Inf)
                {
                    GD.Print(
                        $"[MobSpawner] Could not find " +
                        $"spawn position for " +
                        $"{definition.displayName}"
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
    // COMMAND SPAWN
    // ---------------------------------------------------------

    /// <summary>
    /// Spawns a specific mob at a specific position.
    ///
    /// mobId:
    ///     pig
    ///     beefalo
    ///     zombie
    ///     etc.
    ///
    /// gender:
    ///     empty = use JSON gender chances
    ///     male / m
    ///     female / f
    /// </summary>

    public bool TrySpawnMob(
        string mobId,
        string gender,
        Vector3 spawnPosition)
    {
        if (string.IsNullOrWhiteSpace(
            mobId))
        {
            return false;
        }

        if (MobScene == null)
        {
            GD.PrintErr(
                "[MobSpawner] Cannot command-spawn " +
                "because MobScene is null."
            );

            return false;
        }

        string definitionPath =
            FindMobDefinition(
                mobId
            );

        if (string.IsNullOrEmpty(
            definitionPath))
        {
            GD.Print(
                $"[MobSpawner] Could not find mob definition: " +
                $"{mobId}"
            );

            return false;
        }

        MobGender? forcedGender = null;

        if (!string.IsNullOrWhiteSpace(
            gender))
        {
            if (
                gender.Equals(
                    "male",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                gender.Equals(
                    "m",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                forcedGender =
                    MobGender.Male;
            }
            else if (
                gender.Equals(
                    "female",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                gender.Equals(
                    "f",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                forcedGender =
                    MobGender.Female;
            }
            else
            {
                return false;
            }
        }

        Mob mob =
            MobScene.Instantiate<Mob>();

        if (mob == null)
        {
            GD.PrintErr(
                "[MobSpawner] MobScene did not contain a Mob."
            );

            return false;
        }

        // -----------------------------------------------------
        // SET THESE BEFORE ADDCHILD
        // -----------------------------------------------------
        //
        // AddChild triggers Mob._Ready().
        //

        mob.DefinitionPath =
            definitionPath;

        if (forcedGender.HasValue)
        {
            mob.ForcedGender =
                forcedGender.Value;
        }

        AddChild(mob);

        mob.GlobalPosition =
            spawnPosition;

        mob.Rotation =
            new Vector3(
                0f,
                _rng.RandfRange(
                    0f,
                    Mathf.Tau
                ),
                0f
            );

        GD.Print(
            $"[MobSpawner] Command spawned " +
            $"{mobId} at {spawnPosition}"
        );

        return true;
    }


    // ---------------------------------------------------------
    // FIND ALL MOB DEFINITIONS
    // ---------------------------------------------------------
    //
    // Automatically searches:
    //
    // res://Mobs/Definitions/
    //
    // and every folder underneath it.
    //
    // Any .json file found here becomes available
    // to the automatic spawning system.
    //
    // The JSON's spawning.enabled setting still determines
    // whether that mob can actually spawn.
    //
    // ---------------------------------------------------------

    private string[] FindAllMobDefinitions()
    {
        const string root =
            "res://Mobs/Definitions";

        var results =
            new List<string>();

        FindAllMobDefinitionsRecursive(
            root,
            results
        );

        return results.ToArray();
    }


    private void FindAllMobDefinitionsRecursive(
        string directoryPath,
        List<string> results)
    {
        DirAccess dir =
            DirAccess.Open(
                directoryPath
            );

        if (dir == null)
        {
            GD.PrintErr(
                $"[MobSpawner] Could not open mob definition folder: " +
                $"{directoryPath}"
            );

            return;
        }

        dir.ListDirBegin();

        while (true)
        {
            string entry =
                dir.GetNext();

            if (string.IsNullOrEmpty(
                entry))
            {
                break;
            }

            if (entry == "." ||
                entry == "..")
            {
                continue;
            }

            string fullPath =
                directoryPath +
                "/" +
                entry;

            if (dir.CurrentIsDir())
            {
                FindAllMobDefinitionsRecursive(
                    fullPath,
                    results
                );

                continue;
            }

            if (!entry.EndsWith(
                ".json",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(
                fullPath
            );
        }

        dir.ListDirEnd();
    }


    // ---------------------------------------------------------
    // FIND ONE MOB DEFINITION
    // ---------------------------------------------------------
    //
    // Used by /spawn.
    //
    // Searches every folder underneath:
    //
    // res://Mobs/Definitions/
    //
    // ---------------------------------------------------------

    private string FindMobDefinition(
        string mobId)
    {
        const string root =
            "res://Mobs/Definitions";

        return FindMobDefinitionRecursive(
            root,
            mobId
        );
    }


    private string FindMobDefinitionRecursive(
        string directoryPath,
        string mobId)
    {
        DirAccess dir =
            DirAccess.Open(
                directoryPath
            );

        if (dir == null)
            return null;

        dir.ListDirBegin();

        while (true)
        {
            string fileName =
                dir.GetNext();

            if (string.IsNullOrEmpty(
                fileName))
            {
                break;
            }

            if (fileName == "." ||
                fileName == "..")
            {
                continue;
            }

            string fullPath =
                directoryPath +
                "/" +
                fileName;

            if (dir.CurrentIsDir())
            {
                string found =
                    FindMobDefinitionRecursive(
                        fullPath,
                        mobId
                    );

                if (!string.IsNullOrEmpty(
                    found))
                {
                    dir.ListDirEnd();

                    return found;
                }
            }
            else if (
                fileName.EndsWith(
                    ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                string fileId =
                    Path.GetFileNameWithoutExtension(
                        fileName
                    );

                if (fileId.Equals(
                    mobId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    dir.ListDirEnd();

                    return fullPath;
                }
            }
        }

        dir.ListDirEnd();

        return null;
    }


    // ---------------------------------------------------------
    // NORMAL SPAWN MOB
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
            $"[MobSpawner] Spawned mob from " +
            $"{definitionPath} " +
            $"at {spawnPosition} " +
            $"facing Y={rotationY}"
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
        {
            chunkManager.WorldReady -=
                LoadSavedMobs;
        }

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
                "[MobSpawner] Cannot restore mobs " +
                "because MobScene is null."
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

        foreach (
            SavedMobData saved in savedMobs)
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
            $"[MobSpawner] Restored " +
            $"{savedMobs.Count} saved mobs."
        );
    }


    // ---------------------------------------------------------
    // COUNT MOBS
    // ---------------------------------------------------------

    private int CountMobsForDefinition(
        string definitionId)
    {
        if (string.IsNullOrEmpty(
            definitionId))
        {
            return 0;
        }

        int count = 0;

        foreach (
            Node child in
            GetTree().GetNodesInGroup("mobs"))
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
    // SEASON CHECK
    // ---------------------------------------------------------

    private bool IsCurrentSeasonAllowed(
        MobSpawnSettings spawning)
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

        foreach (
            string allowedSeason in
            spawning.seasons)
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

        float distance =
            _rng.RandfRange(
                minDistance,
                maxDistance
            );

        Vector3 position =
            GlobalPosition +
            direction * distance;

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
            spaceState.IntersectRay(
                query
            );

        if (result.Count == 0)
            return Vector3.Inf;

        if (!result.ContainsKey(
            "position"))
        {
            return Vector3.Inf;
        }

        Vector3 groundPosition =
            (Vector3)result["position"];

        groundPosition.Y +=
            0.15f;

        return groundPosition;
    }
}