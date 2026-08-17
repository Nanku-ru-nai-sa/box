using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; }

    public const string CharactersRoot = "user://saves/characters/";
    public const string WorldsRoot = "user://saves/worlds/";

    public string ActiveCharacterId { get; private set; } = "";
    public string ActiveWorldId { get; private set; } = "";

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true };

    public override void _Ready()
    {
        Instance = this;

        DirAccess.MakeDirRecursiveAbsolute(CharactersRoot);
        DirAccess.MakeDirRecursiveAbsolute(WorldsRoot);
    }

    // ---------- CHARACTERS ----------

    public List<CharacterMeta> ListCharacters()
    {
        var results = new List<CharacterMeta>();

        using var dir = DirAccess.Open(CharactersRoot);
        if (dir == null) return results;

        dir.ListDirBegin();

        string name = dir.GetNext();

        while (name != "")
        {
            if (dir.CurrentIsDir() && name != "." && name != "..")
            {
                var meta = LoadCharacterMeta(name);

                if (meta != null)
                    results.Add(meta);
            }

            name = dir.GetNext();
        }

        dir.ListDirEnd();

        return results
            .OrderByDescending(c => c.LastPlayed)
            .ToList();
    }

    public CharacterMeta LoadCharacterMeta(string characterId)
    {
        string path =
            $"{CharactersRoot}{characterId}/character.json";

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
                $"character.json parse failed ({characterId}): {e.Message}"
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

        var meta = new CharacterMeta
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

    public void SaveCharacterMeta(CharacterMeta meta)
    {
        DirAccess.MakeDirRecursiveAbsolute(
            $"{CharactersRoot}{meta.Id}/"
        );

        using var file =
            FileAccess.Open(
                $"{CharactersRoot}{meta.Id}/character.json",
                FileAccess.ModeFlags.Write
            );

        file.StoreString(
            JsonSerializer.Serialize(
                meta,
                JsonOpts
            )
        );
    }

    public void SetActiveCharacter(string characterId)
    {
        ActiveCharacterId = characterId;

        var meta = LoadCharacterMeta(characterId);

        if (meta != null)
        {
            meta.LastPlayed = DateTime.UtcNow;
            SaveCharacterMeta(meta);
        }
    }

    public void DeleteCharacter(string characterId)
    {
        DeleteDirRecursive(
            $"{CharactersRoot}{characterId}/"
        );

        if (ActiveCharacterId == characterId)
            ActiveCharacterId = "";
    }

    // ---------- WORLDS ----------

    public List<WorldMeta> ListWorlds()
    {
        var results = new List<WorldMeta>();

        using var dir = DirAccess.Open(WorldsRoot);
        if (dir == null) return results;

        dir.ListDirBegin();

        string name = dir.GetNext();

        while (name != "")
        {
            if (dir.CurrentIsDir() && name != "." && name != "..")
            {
                var meta = LoadWorldMeta(name);

                if (meta != null)
                    results.Add(meta);
            }

            name = dir.GetNext();
        }

        dir.ListDirEnd();

        return results
            .OrderByDescending(w => w.LastPlayed)
            .ToList();
    }

    public WorldMeta LoadWorldMeta(string worldId)
    {
        string path =
            $"{WorldsRoot}{worldId}/world_meta.json";

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

            // Keep TimeOfDay safely between 0 and 1.
            meta.TimeOfDay =
                Mathf.PosMod(
                    meta.TimeOfDay,
                    1.0f
                );

            return meta;
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"world_meta.json parse failed ({worldId}): {e.Message}"
            );

            return null;
        }
    }

    // ---------- CREATE WORLD ----------

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

        var meta = new WorldMeta
        {
            Id = id,
            DisplayName = displayName,
            Seed = seed,
            Theme = theme,
            StartBonus = startBonus,
            Type = type,
            Season = season,
            SeasonLocked = seasonLocked,

            // NEW WORLD ALWAYS STARTS AT SUNRISE
            TimeOfDay = 0.0f,

            CreatedAt = DateTime.UtcNow,
            LastPlayed = DateTime.UtcNow
        };

        SaveWorldMeta(meta);

        return meta;
    }

    // ---------- SAVE WORLD ----------

    public void SaveWorldMeta(WorldMeta meta)
    {
        if (meta == null)
            return;

        // Keep time safely between 0 and 1.
        meta.TimeOfDay =
            Mathf.PosMod(
                meta.TimeOfDay,
                1.0f
            );

        DirAccess.MakeDirRecursiveAbsolute(
            $"{WorldsRoot}{meta.Id}/"
        );

        using var file =
            FileAccess.Open(
                $"{WorldsRoot}{meta.Id}/world_meta.json",
                FileAccess.ModeFlags.Write
            );

        file.StoreString(
            JsonSerializer.Serialize(
                meta,
                JsonOpts
            )
        );
    }

    // ---------- ACTIVE WORLD ----------

    public void SetActiveWorld(string worldId)
    {
        ActiveWorldId = worldId;

        var meta = LoadWorldMeta(worldId);

        if (meta != null)
        {
            meta.LastPlayed = DateTime.UtcNow;

            SaveWorldMeta(meta);
        }
    }

    // ============================================================
    // DAY / NIGHT TIME
    // ============================================================

    public void SaveWorldTime(float timeOfDay)
    {
        if (string.IsNullOrEmpty(ActiveWorldId))
        {
            GD.PrintErr(
                "[SaveManager] Cannot save world time. " +
                "There is no active world."
            );

            return;
        }

        var meta =
            LoadWorldMeta(ActiveWorldId);

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
        if (string.IsNullOrEmpty(ActiveWorldId))
        {
            GD.Print(
                "[SaveManager] No active world. " +
                "Starting at sunrise."
            );

            return 0.0f;
        }

        var meta =
            LoadWorldMeta(ActiveWorldId);

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

    // ---------- DELETE WORLD ----------

    public void DeleteWorld(string worldId)
    {
        DeleteDirRecursive(
            $"{WorldsRoot}{worldId}/"
        );

        if (ActiveWorldId == worldId)
            ActiveWorldId = "";
    }

    // ---------- HELPERS ----------

    private string SanitizeId(string displayName)
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

    private void DeleteDirRecursive(string path)
    {
        using var dir = DirAccess.Open(path);

        if (dir == null)
            return;

        dir.ListDirBegin();

        string name = dir.GetNext();

        while (name != "")
        {
            if (name != "." && name != "..")
            {
                string full = path + name;

                if (dir.CurrentIsDir())
                {
                    DeleteDirRecursive(full + "/");

                    DirAccess.RemoveAbsolute(full);
                }
                else
                {
                    DirAccess.RemoveAbsolute(full);
                }
            }

            name = dir.GetNext();
        }

        dir.ListDirEnd();

        DirAccess.RemoveAbsolute(path);
    }
}