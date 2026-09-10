using Godot;
using System.Collections.Generic;

public partial class MobSpawnerManager : Node
{
    private bool _loadedSavedMobs = false;

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
}