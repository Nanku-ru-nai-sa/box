using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

// ============================================================
// SAVED MOB DATA
// ============================================================

[Serializable]
public class SavedMobData
{
    public string DefinitionPath { get; set; } = "";

    public float[] Position { get; set; } =
        new float[3];

    // Mob's horizontal facing direction.
    public float RotationY { get; set; } = 0f;
}


// ============================================================
// SAVE MANAGER
// ============================================================

public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; }

    public const string CharactersRoot =
        "user://saves/characters/";

    public const string WorldsRoot =
        "user://saves/worlds/";

    public string ActiveCharacterId { get; private set; } = "";
    public string ActiveWorldId { get; private set; } = "";

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true };


    // ============================================================
    // READY
    // ============================================================

    public override void _Ready()
    {
        Instance = this;

        DirAccess.MakeDirRecursiveAbsolute(
            CharactersRoot
        );

        DirAccess.MakeDirRecursiveAbsolute(
            WorldsRoot
        );
    }


    // ============================================================
    // CHARACTERS
    // ============================================================

    public List<CharacterMeta> ListCharacters()
    {
        var results =
            new List<CharacterMeta>();

        using var dir =
            DirAccess.Open(CharactersRoot);

        if (dir == null)
            return results;

        dir.ListDirBegin();

        string name =
            dir.GetNext();

        while (name != "")
        {
            if (
                dir.CurrentIsDir() &&
                name != "." &&
                name != ".."
            )
            {
                var meta =
                    LoadCharacterMeta(name);

                if (meta != null)
                    results.Add(meta);
            }

            name = dir.GetNext();
        }

        dir.ListDirEnd();

        return results
            .OrderByDescending(
                c => c.LastPlayed
            )
            .ToList();
    }


    public CharacterMeta LoadCharacterMeta(
        string characterId)
    {
        string path =
            $"{CharactersRoot}" +
            $"{characterId}/character.json";

        if (!FileAccess.FileExists(path))
            return null;

        using var file =
            FileAccess.Open(
                path,
                FileAccess.ModeFlags.Read
            );

        try
        {
            return JsonSerializer.Deserialize<CharacterMeta>(
                file.GetAsText()
            );
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"character.json parse failed " +
                $"({characterId}): {e.Message}"
            );

            return null;
        }
    }


    public CharacterMeta CreateCharacter(
        string displayName,
        string cheatCodes,
        string keepInventory,
        string gameMode,
        string difficulty)
    {
        string id =
            SanitizeId(displayName) +
            "_" +
            DateTime.UtcNow.Ticks;

        var meta =
            new CharacterMeta
            {
                Id = id,
                DisplayName = displayName,
                CreatedAt = DateTime.UtcNow,
                LastPlayed = DateTime.UtcNow,
                LockedCheatCodes = cheatCodes,
                LockedKeepInventory = keepInventory,
                LockedGameMode = gameMode,
                LockedDifficulty = difficulty
            };

        SaveCharacterMeta(meta);

        return meta;
    }


    public void SaveCharacterMeta(
        CharacterMeta meta)
    {
        DirAccess.MakeDirRecursiveAbsolute(
            $"{CharactersRoot}{meta.Id}/"
        );

        using var file =
            FileAccess.Open(
                $"{CharactersRoot}" +
                $"{meta.Id}/character.json",
                FileAccess.ModeFlags.Write
            );

        file.StoreString(
            JsonSerializer.Serialize(
                meta,
                JsonOpts
            )
        );
    }


    public void SetActiveCharacter(
        string characterId)
    {
        ActiveCharacterId =
            characterId;

        var meta =
            LoadCharacterMeta(
                characterId
            );

        if (meta != null)
        {
            meta.LastPlayed =
                DateTime.UtcNow;

            SaveCharacterMeta(meta);
        }
    }


    public void DeleteCharacter(
        string characterId)
    {
        DeleteDirRecursive(
            $"{CharactersRoot}{characterId}/"
        );

        if (ActiveCharacterId ==
            characterId)
        {
            ActiveCharacterId = "";
        }
    }


    // ============================================================
    // WORLDS
    // ============================================================

    public List<WorldMeta> ListWorlds()
    {
        var results =
            new List<WorldMeta>();

        using var dir =
            DirAccess.Open(WorldsRoot);

        if (dir == null)
            return results;

        dir.ListDirBegin();

        string name =
            dir.GetNext();

        while (name != "")
        {
            if (
                dir.CurrentIsDir() &&
                name != "." &&
                name != ".."
            )
            {
                var meta =
                    LoadWorldMeta(name);

                if (meta != null)
                    results.Add(meta);
            }

            name = dir.GetNext();
        }

        dir.ListDirEnd();

        return results
            .OrderByDescending(
                w => w.LastPlayed
            )
            .ToList();
    }


    public WorldMeta LoadWorldMeta(
        string worldId)
    {
        string path =
            $"{WorldsRoot}" +
            $"{worldId}/world_meta.json";

        if (!FileAccess.FileExists(path))
            return null;

        using var file =
            FileAccess.Open(
                path,
                FileAccess.ModeFlags.Read
            );

        try
        {
            var meta =
                JsonSerializer.Deserialize<WorldMeta>(
                    file.GetAsText()
                );

            if (meta == null)
                return null;

            meta.TimeOfDay =
                Mathf.PosMod(
                    meta.TimeOfDay,
                    1.0f
                );

            meta.SeasonDay =
                Mathf.Max(
                    1,
                    meta.SeasonDay
                );

            meta.Year =
                Mathf.Max(
                    1,
                    meta.Year
                );

            return meta;
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"world_meta.json parse failed " +
                $"({worldId}): {e.Message}"
            );

            return null;
        }
    }


    // ============================================================
    // CREATE WORLD
    // ============================================================

    public WorldMeta CreateWorld(
        string displayName,
        long seed,
        string theme,
        string startBonus,
        string type,
        string season,
        bool seasonLocked)
    {
        string id =
            SanitizeId(displayName) +
            "_" +
            DateTime.UtcNow.Ticks;

        var meta =
            new WorldMeta
            {
                Id = id,
                DisplayName = displayName,
                Seed = seed,
                Theme = theme,
                StartBonus = startBonus,
                Type = type,

                Season =
                    string.IsNullOrEmpty(season)
                        ? "Spring"
                        : season,

                SeasonDay = 1,
                Year = 1,

                SeasonLocked =
                    seasonLocked,

                SpringEnabled = true,
                SummerEnabled = true,
                AutumnEnabled = true,
                WinterEnabled = true,

                // NEW WORLD STARTS AT SUNRISE
                TimeOfDay = 0.0f,

                CreatedAt =
                    DateTime.UtcNow,

                LastPlayed =
                    DateTime.UtcNow
            };

        SaveWorldMeta(meta);

        return meta;
    }


    // ============================================================
    // SAVE WORLD
    // ============================================================

    public void SaveWorldMeta(
        WorldMeta meta)
    {
        if (meta == null)
            return;

        meta.TimeOfDay =
            Mathf.PosMod(
                meta.TimeOfDay,
                1.0f
            );

        meta.SeasonDay =
            Mathf.Max(
                1,
                meta.SeasonDay
            );

        meta.Year =
            Mathf.Max(
                1,
                meta.Year
            );

        DirAccess.MakeDirRecursiveAbsolute(
            $"{WorldsRoot}{meta.Id}/"
        );

        using var file =
            FileAccess.Open(
                $"{WorldsRoot}" +
                $"{meta.Id}/world_meta.json",
                FileAccess.ModeFlags.Write
            );

        file.StoreString(
            JsonSerializer.Serialize(
                meta,
                JsonOpts
            )
        );
    }


    // ============================================================
    // ACTIVE WORLD
    // ============================================================

    public void SetActiveWorld(
        string worldId)
    {
        ActiveWorldId =
            worldId;

        var meta =
            LoadWorldMeta(worldId);

        if (meta != null)
        {
            meta.LastPlayed =
                DateTime.UtcNow;

            SaveWorldMeta(meta);
        }
    }


    // ============================================================
    // CENTRAL WORLD SAVE ENTRY POINT
    // ============================================================

    public void SaveCurrentWorld()
{
    if (string.IsNullOrEmpty(
        ActiveWorldId))
    {
        GD.PrintErr(
            "[SaveManager] Cannot save world. " +
            "There is no active world."
        );

        return;
    }

    // Save current GameMode
    if (GameModeManager.Instance != null)
    {
        GameModeManager.Instance.SaveToWorld();
    }

    // Save current mobs
    SaveWorldMobs();

    GD.Print(
        "[SaveManager] Current world save completed."
    );
}


    // ============================================================
    // DAY / NIGHT TIME
    // ============================================================

    public void SaveWorldTime(
        float timeOfDay)
    {
        if (string.IsNullOrEmpty(
            ActiveWorldId))
        {
            GD.PrintErr(
                "[SaveManager] Cannot save world time. " +
                "There is no active world."
            );

            return;
        }

        var meta =
            LoadWorldMeta(
                ActiveWorldId
            );

        if (meta == null)
        {
            GD.PrintErr(
                "[SaveManager] Cannot save world time. " +
                "World metadata could not be loaded."
            );

            return;
        }

        meta.TimeOfDay =
            Mathf.PosMod(
                timeOfDay,
                1.0f
            );

        meta.LastPlayed =
            DateTime.UtcNow;

        SaveWorldMeta(meta);

        GD.Print(
            $"[SaveManager] Saved world time: " +
            $"{meta.TimeOfDay:0.000}"
        );
    }


    public float LoadWorldTime()
    {
        if (string.IsNullOrEmpty(
            ActiveWorldId))
        {
            GD.Print(
                "[SaveManager] No active world. " +
                "Starting at sunrise."
            );

            return 0.0f;
        }

        var meta =
            LoadWorldMeta(
                ActiveWorldId
            );

        if (meta == null)
        {
            GD.Print(
                "[SaveManager] World metadata unavailable. " +
                "Starting at sunrise."
            );

            return 0.0f;
        }

        float time =
            Mathf.PosMod(
                meta.TimeOfDay,
                1.0f
            );

        GD.Print(
            $"[SaveManager] Loaded world time: " +
            $"{time:0.000}"
        );

        return time;
    }


    // ============================================================
    // SEASON STATE
    // ============================================================

    public void SaveWorldSeason(
        SeasonManager seasonManager)
    {
        if (seasonManager == null)
            return;

        if (string.IsNullOrEmpty(
            ActiveWorldId))
        {
            GD.PrintErr(
                "[SaveManager] Cannot save season. " +
                "There is no active world."
            );

            return;
        }

        var meta =
            LoadWorldMeta(
                ActiveWorldId
            );

        if (meta == null)
        {
            GD.PrintErr(
                "[SaveManager] Cannot save season. " +
                "World metadata could not be loaded."
            );

            return;
        }

        meta.Year =
            seasonManager.CurrentYear;

        meta.Season =
            seasonManager.GetSeasonName();

        meta.SeasonDay =
            seasonManager.CurrentDay;

        meta.SeasonLocked =
            seasonManager.SeasonLocked;

        meta.SpringEnabled =
            seasonManager.SpringEnabled;

        meta.SummerEnabled =
            seasonManager.SummerEnabled;

        meta.AutumnEnabled =
            seasonManager.AutumnEnabled;

        meta.WinterEnabled =
            seasonManager.WinterEnabled;

        meta.LastPlayed =
            DateTime.UtcNow;

        SaveWorldMeta(meta);

        GD.Print(
            $"[SaveManager] Saved calendar: " +
            $"Year {meta.Year}, " +
            $"{meta.Season}, " +
            $"Day {meta.SeasonDay}"
        );
    }


    public bool LoadWorldSeason(
        SeasonManager seasonManager)
    {
        if (seasonManager == null)
            return false;

        if (string.IsNullOrEmpty(
            ActiveWorldId))
        {
            return false;
        }

        var meta =
            LoadWorldMeta(
                ActiveWorldId
            );

        if (meta == null)
            return false;

        seasonManager.LoadCalendarState(
            meta.Year,
            meta.Season,
            meta.SeasonDay,
            meta.SeasonLocked,
            meta.SpringEnabled,
            meta.SummerEnabled,
            meta.AutumnEnabled,
            meta.WinterEnabled
        );

        GD.Print(
            $"[SaveManager] Loaded calendar: " +
            $"Year {meta.Year}, " +
            $"{meta.Season}, " +
            $"Day {meta.SeasonDay}"
        );

        return true;
    }


    // ============================================================
    // MOB STATE
    // ============================================================

    private string GetMobSavePath()
    {
        if (string.IsNullOrEmpty(
            ActiveWorldId))
        {
            return "";
        }

        return
            $"{WorldsRoot}" +
            $"{ActiveWorldId}/mobs.json";
    }


    public void SaveWorldMobs()
    {
        if (string.IsNullOrEmpty(
            ActiveWorldId))
        {
            GD.PrintErr(
                "[SaveManager] Cannot save mobs. " +
                "There is no active world."
            );

            return;
        }

        string path =
            GetMobSavePath();

        var savedMobs =
            new List<SavedMobData>();

        SceneTree tree =
            GetTree();

        if (tree != null)
        {
            foreach (Node node in
                tree.GetNodesInGroup("mobs"))
            {
                if (node is not Mob mob)
                    continue;

                Vector3 position =
                    mob.GlobalPosition;
savedMobs.Add(
    new SavedMobData
    {
        DefinitionPath =
            mob.DefinitionPath,

        Position =
            new float[]
            {
                position.X,
                position.Y,
                position.Z
            },

        RotationY =
            mob.Rotation.Y
    }
);
            }
        }

        // Do not open the file when there are zero mobs.
        // Opening with Write would truncate the existing mobs.json.
        if (savedMobs.Count == 0)
        {
            GD.Print(
                "[SaveManager] Found 0 mobs. " +
                "Existing mob save was left untouched."
            );

            return;
        }

        DirAccess.MakeDirRecursiveAbsolute(
            $"{WorldsRoot}{ActiveWorldId}/"
        );

        using var file =
            FileAccess.Open(
                path,
                FileAccess.ModeFlags.Write
            );

        if (file == null)
        {
            GD.PrintErr(
                $"[SaveManager] Could not open mob save file: {path}"
            );

            return;
        }

        file.StoreString(
            JsonSerializer.Serialize(
                savedMobs,
                JsonOpts
            )
        );

        GD.Print(
            $"[SaveManager] Saved {savedMobs.Count} mobs."
        );
    }


    public List<SavedMobData> LoadWorldMobs()
    {
        var results =
            new List<SavedMobData>();

        if (string.IsNullOrEmpty(
            ActiveWorldId))
        {
            return results;
        }

        string path =
            GetMobSavePath();

        if (!FileAccess.FileExists(path))
        {
            GD.Print(
                "[SaveManager] No saved mob data found."
            );

            return results;
        }

        using var file =
            FileAccess.Open(
                path,
                FileAccess.ModeFlags.Read
            );

        if (file == null)
            return results;

        try
        {
            var loaded =
                JsonSerializer.Deserialize<
                    List<SavedMobData>
                >(
                    file.GetAsText()
                );

            if (loaded == null)
                return results;

            foreach (var mob in loaded)
            {
                if (mob == null)
                    continue;

                if (string.IsNullOrEmpty(
                    mob.DefinitionPath))
                    continue;

                if (mob.Position == null ||
                    mob.Position.Length != 3)
                    continue;

                results.Add(mob);
            }

            GD.Print(
                $"[SaveManager] Loaded {results.Count} saved mobs."
            );
        }
        catch (Exception e)
        {
            GD.PrintErr(
                "[SaveManager] mobs.json parse failed: " +
                e.Message
            );
        }

        return results;
    }


    // ============================================================
    // DELETE WORLD
    // ============================================================

    public void DeleteWorld(
        string worldId)
    {
        DeleteDirRecursive(
            $"{WorldsRoot}{worldId}/"
        );

        if (ActiveWorldId ==
            worldId)
        {
            ActiveWorldId = "";
        }
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private string SanitizeId(
        string displayName)
    {
        var clean =
            new string(
                displayName
                    .Where(
                        c =>
                            char.IsLetterOrDigit(c) ||
                            c == '_' ||
                            c == '-'
                    )
                    .ToArray()
            );

        return string.IsNullOrEmpty(clean)
            ? "save"
            : clean.ToLower();
    }


    private void DeleteDirRecursive(
        string path)
    {
        using var dir =
            DirAccess.Open(path);

        if (dir == null)
            return;

        dir.ListDirBegin();

        string name =
            dir.GetNext();

        while (name != "")
        {
            if (
                name != "." &&
                name != ".."
            )
            {
                string full =
                    path + name;

                if (dir.CurrentIsDir())
                {
                    DeleteDirRecursive(
                        full + "/"
                    );

                    DirAccess.RemoveAbsolute(
                        full
                    );
                }
                else
                {
                    DirAccess.RemoveAbsolute(
                        full
                    );
                }
            }

            name = dir.GetNext();
        }

        dir.ListDirEnd();

        DirAccess.RemoveAbsolute(path);
    }
}