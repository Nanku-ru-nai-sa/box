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

    public string Theme { get; set; } = "Plains";
    public string StartBonus { get; set; } = "None";
    public string Type { get; set; } = "Normal";
    public string Season { get; set; } = "Spring";
    public bool SeasonLocked { get; set; } = false; // true = stays on Season forever, false = cycles normally
}