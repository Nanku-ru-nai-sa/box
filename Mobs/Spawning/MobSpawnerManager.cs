using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class MobSpawnerManager : Node
{
    private bool _loadedSavedMobs = false;


    // =========================================================
    // READY
    // =========================================================

    public override void _Ready()
    {
        CallDeferred(nameof(LoadSavedMobs));
    }


    // =========================================================
    // LOAD SAVED MOBS
    // =========================================================

    private void LoadSavedMobs()
    {
        if (_loadedSavedMobs)
            return;

        _loadedSavedMobs = true;

        var saveManager = SaveManager.Instance;

        if (saveManager == null)
        {
            GD.PrintErr(
                "[MobSpawnerManager] SaveManager is not available."
            );

            return;
        }

        List<SavedMobData> savedMobs =
            saveManager.LoadWorldMobs();

        if (savedMobs == null ||
            savedMobs.Count == 0)
        {
            GD.Print(
                "[MobSpawnerManager] No saved mobs to restore."
            );

            return;
        }

        Node parent = GetParent();

        if (parent == null)
        {
            GD.PrintErr(
                "[MobSpawnerManager] No parent world node."
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

            PackedScene mobScene =
                ResourceLoader.Load<PackedScene>(
                    "res://Mobs/Framework/mob.tscn"
                );

            if (mobScene == null)
            {
                GD.PrintErr(
                    "[MobSpawnerManager] Could not load mob.tscn."
                );

                return;
            }

            Mob mob =
                mobScene.Instantiate<Mob>();

            if (mob == null)
            {
                GD.PrintErr(
                    "[MobSpawnerManager] mob.tscn did not contain a Mob."
                );

                continue;
            }

            mob.DefinitionPath =
                saved.DefinitionPath;

            parent.AddChild(mob);

            mob.GlobalPosition =
                new Vector3(
                    saved.Position[0],
                    saved.Position[1],
                    saved.Position[2]
                );

            mob.Rotation = new Vector3(
                0f,
                saved.RotationY,
                0f
            );

            GD.Print(
                $"[MobSpawnerManager] Restored mob: " +
                $"{saved.DefinitionPath} at " +
                $"{mob.GlobalPosition} " +
                $"facing Y={saved.RotationY}"
            );
        }

        GD.Print(
            $"[MobSpawnerManager] Restored " +
            $"{savedMobs.Count} saved mobs."
        );
    }


    // =========================================================
    // SPAWN MOB
    // =========================================================
    //
    // Generic command spawning.
    //
    // mobId:
    //     beefalo
    //     pig
    //     zombie
    //     etc.
    //
    // gender:
    //     ""       = normal JSON/random gender
    //     "male"   = male
    //     "m"      = male
    //     "female" = female
    //     "f"      = female
    //
    // position:
    //     Position supplied by the player's raycast.
    //
    // =========================================================

    public bool TrySpawnMob(
        string mobId,
        string gender,
        Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(mobId))
        {
            GD.PrintErr(
                "[MobSpawnerManager] Mob ID is empty."
            );

            return false;
        }

        mobId =
            mobId.Trim();

        gender =
            gender?.Trim() ?? "";


        // -----------------------------------------------------
        // FIND MOB DEFINITION
        // -----------------------------------------------------

        string definitionPath =
            FindMobDefinition(
                mobId
            );

        if (string.IsNullOrEmpty(
            definitionPath))
        {
            GD.PrintErr(
                $"[MobSpawnerManager] Could not find mob definition: {mobId}"
            );

            return false;
        }


        // -----------------------------------------------------
        // LOAD MOB SCENE
        // -----------------------------------------------------

        PackedScene mobScene =
            ResourceLoader.Load<PackedScene>(
                "res://Mobs/Framework/mob.tscn"
            );

        if (mobScene == null)
        {
            GD.PrintErr(
                "[MobSpawnerManager] Could not load mob.tscn."
            );

            return false;
        }


        // -----------------------------------------------------
        // CREATE MOB
        // -----------------------------------------------------

        Mob mob =
            mobScene.Instantiate<Mob>();

        if (mob == null)
        {
            GD.PrintErr(
                "[MobSpawnerManager] mob.tscn did not contain a Mob."
            );

            return false;
        }


        // -----------------------------------------------------
        // SET DEFINITION BEFORE READY
        // -----------------------------------------------------

        mob.DefinitionPath =
            definitionPath;


        // -----------------------------------------------------
        // SET OPTIONAL FORCED GENDER
        // -----------------------------------------------------

        if (!string.IsNullOrEmpty(gender))
        {
            if (gender.Equals(
                    "male",
                    StringComparison.OrdinalIgnoreCase) ||
                gender.Equals(
                    "m",
                    StringComparison.OrdinalIgnoreCase))
            {
                mob.ForcedGender =
                    MobGender.Male;
            }
            else if (
                gender.Equals(
                    "female",
                    StringComparison.OrdinalIgnoreCase) ||
                gender.Equals(
                    "f",
                    StringComparison.OrdinalIgnoreCase))
            {
                mob.ForcedGender =
                    MobGender.Female;
            }
            else
            {
                mob.QueueFree();

                GD.PrintErr(
                    $"[MobSpawnerManager] Unknown gender: {gender}"
                );

                return false;
            }
        }


        // -----------------------------------------------------
        // ADD TO WORLD
        // -----------------------------------------------------

        Node parent =
            GetParent();

        if (parent == null)
        {
            mob.QueueFree();

            GD.PrintErr(
                "[MobSpawnerManager] No parent world node."
            );

            return false;
        }

        parent.AddChild(mob);


        // -----------------------------------------------------
        // POSITION
        // -----------------------------------------------------

        mob.GlobalPosition =
            position;


        // -----------------------------------------------------
        // RANDOM FACING
        // -----------------------------------------------------

        mob.Rotation = new Vector3(
            0f,
            (float)GD.RandRange(
                0.0,
                Mathf.Tau
            ),
            0f
        );


        // -----------------------------------------------------
        // LOG
        // -----------------------------------------------------

        string genderText =
            mob.ForcedGender.HasValue
                ? mob.ForcedGender.Value.ToString()
                : "random";

        GD.Print(
            $"[MobSpawnerManager] Spawned " +
            $"{mobId} at {position} " +
            $"Gender={genderText}"
        );

        return true;
    }


    // =========================================================
    // FIND MOB DEFINITION
    // =========================================================
    //
    // Searches all folders underneath:
    //
    // res://Mobs/Definitions/
    //
    // This means /spawn is not limited to Animals.
    //
    // =========================================================

    private string FindMobDefinition(
        string mobId)
    {
        string root =
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
            return "";


        // -----------------------------------------------------
        // FILES
        // -----------------------------------------------------

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

            if (dir.CurrentIsDir())
                continue;

            if (!fileName.EndsWith(
                    ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string filePath =
                directoryPath +
                "/" +
                fileName;

            string fileId =
                Path.GetFileNameWithoutExtension(
                    fileName
                );

            if (string.Equals(
                    fileId,
                    mobId,
                    StringComparison.OrdinalIgnoreCase))
            {
                dir.ListDirEnd();

                return filePath;
            }
        }

        dir.ListDirEnd();


        // -----------------------------------------------------
        // SUBDIRECTORIES
        // -----------------------------------------------------

        dir =
            DirAccess.Open(
                directoryPath
            );

        if (dir == null)
            return "";

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

            if (!dir.CurrentIsDir())
                continue;

            if (entry == "." ||
                entry == "..")
            {
                continue;
            }

            string subdirectory =
                directoryPath +
                "/" +
                entry;

            string result =
                FindMobDefinitionRecursive(
                    subdirectory,
                    mobId
                );

            if (!string.IsNullOrEmpty(
                result))
            {
                dir.ListDirEnd();

                return result;
            }
        }

        dir.ListDirEnd();

        return "";
    }
}