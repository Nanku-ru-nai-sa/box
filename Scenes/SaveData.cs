using System;
using System.Collections.Generic;

public class CharacterMeta
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastPlayed { get; set; }

    public string LockedGameMode { get; set; } = "Survival";
    public string LockedDifficulty { get; set; } = "Normal";
    public string LockedCheatCodes { get; set; } = "Off";
    public string LockedKeepInventory { get; set; } = "Off";

    public Dictionary<string, float[]> WorldPositions { get; set; } = new();
}

public class WorldMeta
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public long Seed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastPlayed { get; set; }

    public string LockedGameMode { get; set; } = "Survival";
    public string LockedDifficulty { get; set; } = "Normal";

    public string Theme { get; set; } = "Normal";
    public string StartBonus { get; set; } = "None";
    public string Type { get; set; } = "Normal";
    public string Season { get; set; } = "Spring";

public bool SeasonLocked { get; set; } = false;

// ============================================================
// DAY / NIGHT
// ============================================================

// 0.0   = Sunrise
// 0.25  = Noon
// 0.5   = Sunset
// 0.75  = Midnight
// 1.0   = Next Sunrise

public float TimeOfDay { get; set; } = 0.0f;
}