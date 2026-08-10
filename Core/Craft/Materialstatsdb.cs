// UPDATED FILE - replaces MaterialStatsDb.cs
//
// Loads every *.json file inside res://Data/Materials/ - one file per
// material, e.g. Data/Materials/flint.json, Data/Materials/stick.json.
// Each file is just a single material's stats (no wrapper/array). Add a
// new material by dropping in a new .json file - nothing else to touch.

using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class MaterialStatsDb
{
    // Every *.json file in this folder gets loaded as one material.
    private const string MaterialsFolder = "res://Data/Materials/";

    private static Dictionary<string, MaterialStatEntryData> _lookup;

    private static void EnsureLoaded()
    {
        if (_lookup != null) return; // already loaded this session
        _lookup = new Dictionary<string, MaterialStatEntryData>();

        using var dir = DirAccess.Open(MaterialsFolder);
        if (dir == null)
        {
            GD.PrintErr($"MaterialStatsDb: folder not found: {MaterialsFolder}");
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
                LoadOneFile(MaterialsFolder + fileName, fileName);
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        GD.Print($"MaterialStatsDb loaded {_lookup.Count} materials from {MaterialsFolder}");
    }

    private static void LoadOneFile(string fullPath, string fileName)
    {
        using var file = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"MaterialStatsDb: couldn't open {fullPath}");
            return;
        }

        string text = file.GetAsText();

        try
        {
            var entry = JsonSerializer.Deserialize<MaterialStatEntryData>(text);
            if (entry == null) return;

            // Falls back to the filename (minus .json) if materialId was left blank,
            // so "flint.json" with no materialId set still works as "flint".
            if (string.IsNullOrEmpty(entry.MaterialId))
                entry.MaterialId = fileName.Substring(0, fileName.Length - ".json".Length);

            _lookup[entry.MaterialId] = entry;
        }
        catch (Exception e)
        {
            GD.PrintErr($"MaterialStatsDb: failed to parse {fullPath}: {e.Message}");
        }
    }

    public static MaterialStatEntryData Get(string materialId)
    {
        EnsureLoaded();
        if (_lookup.TryGetValue(materialId, out var m))
            return m;
        GD.PrintErr($"MaterialStatsDb: Material not found: {materialId}");
        return null;
    }

    // Call this (e.g. from a debug key) to re-scan the Materials folder
    // without restarting the game - handy while playtesting.
    public static void Reload() => _lookup = null;
}

// Plain data class (not a Godot Resource) - one JSON file deserializes
// straight into one of these.
public class MaterialStatEntryData
{
    [JsonPropertyName("materialId")]        public string MaterialId { get; set; } = "";
    [JsonPropertyName("durabilityPerUnit")] public int    DurabilityPerUnit { get; set; } = 0;
    [JsonPropertyName("miningSpeedMod")]    public float  MiningSpeedMod { get; set; } = 1.0f;
    [JsonPropertyName("attackDamageMod")]   public float  AttackDamageMod { get; set; } = 1.0f;
    [JsonPropertyName("tier")]              public int    Tier { get; set; } = 0;
    // NEW - tooltip stats. miningPower: "Pixels", 1-8. cooldownSeconds: swing/use time.
    [JsonPropertyName("miningPower")]       public int    MiningPower { get; set; } = 1;
    [JsonPropertyName("cooldownSeconds")]   public float  CooldownSeconds { get; set; } = 1.0f;
    [JsonPropertyName("traitId")]           public string TraitId { get; set; } = "";
    [JsonPropertyName("traitMagnitude")]    public float  TraitMagnitude { get; set; } = 0f;
}