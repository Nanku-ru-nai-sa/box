// UPDATED FILE - replaces MaterialStatsDb.cs
//
// No longer an Autoload/Node - just a plain static class that loads
// res://Data/materials.json the first time anything asks for a material.
// Edit that JSON file directly to tune numbers, same idea as your JSON
// crafting recipes. No Project Settings setup needed at all.
//
// IMPORTANT: delete the OLD MaterialStatEntry.cs (the [Export]-based
// Resource version) - it's replaced by the plain MaterialStatEntryData
// class defined at the bottom of this file, and isn't needed anymore.
// Also remove "MaterialStatsDb" from Project Settings -> Autoload if you
// added it there before - it's not an Autoload anymore.

using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class MaterialStatsDb
{
    // Put your materials.json here: res://Data/materials.json
    private const string JsonPath = "res://Data/materials.json";

    private static Dictionary<string, MaterialStatEntryData> _lookup;

    private static void EnsureLoaded()
    {
        if (_lookup != null) return; // already loaded this session
        _lookup = new Dictionary<string, MaterialStatEntryData>();

        if (!FileAccess.FileExists(JsonPath))
        {
            GD.PrintErr($"MaterialStatsDb: {JsonPath} not found. Create it - see the example in the project notes.");
            return;
        }

        using var file = FileAccess.Open(JsonPath, FileAccess.ModeFlags.Read);
        string text = file.GetAsText();

        try
        {
            var wrapper = JsonSerializer.Deserialize<MaterialsWrapper>(text);
            foreach (var m in wrapper.Materials)
            {
                if (m != null && !string.IsNullOrEmpty(m.MaterialId))
                    _lookup[m.MaterialId] = m;
            }
            GD.Print($"MaterialStatsDb loaded {_lookup.Count} materials from JSON.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"MaterialStatsDb: failed to parse {JsonPath}: {e.Message}");
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

    // Call this (e.g. from a debug key) to re-read materials.json without
    // restarting the game - handy for tuning numbers while playtesting.
    public static void Reload() => _lookup = null;

    private class MaterialsWrapper
    {
        [JsonPropertyName("materials")] public List<MaterialStatEntryData> Materials { get; set; } = new();
    }
}

// Plain data class (not a Godot Resource) - one entry per material.
// Field names/behavior match the old MaterialStatEntry exactly.
public class MaterialStatEntryData
{
    [JsonPropertyName("materialId")]        public string MaterialId { get; set; } = "";
    [JsonPropertyName("durabilityPerUnit")] public int    DurabilityPerUnit { get; set; } = 0;
    [JsonPropertyName("miningSpeedMod")]    public float  MiningSpeedMod { get; set; } = 1.0f;
    [JsonPropertyName("attackDamageMod")]   public float  AttackDamageMod { get; set; } = 1.0f;
    [JsonPropertyName("tier")]              public int    Tier { get; set; } = 0;
    [JsonPropertyName("traitId")]           public string TraitId { get; set; } = "";
    [JsonPropertyName("traitMagnitude")]    public float  TraitMagnitude { get; set; } = 0f;
}