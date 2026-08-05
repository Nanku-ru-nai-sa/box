// NEW FILE
// Put this in Scripts/Tools/MaterialStatsDb.cs
//
// SETUP STEP (one time, in the Godot editor):
// 1. Project -> Project Settings -> Autoload tab
// 2. Add this script, name it "MaterialStatsDb"
// 3. Select the MaterialStatsDb node in the Autoload list, open Inspector
// 4. You'll see a "Materials" array - click it, add elements, each one
//    is a MaterialStatEntry you can fill in directly (MaterialId, DurabilityPerUnit, etc)
//
// That gives you a live, editable list of every material's stats without
// touching code - exactly what you asked for.

using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class MaterialStatsDb : Node
{
    public static MaterialStatsDb Instance { get; private set; }

    [Export] public MaterialStatEntry[] Materials { get; set; } = new MaterialStatEntry[0];

    private Dictionary<string, MaterialStatEntry> _lookup = new();

    public override void _Ready()
    {
        Instance = this;

        _lookup.Clear();
        foreach (var m in Materials)
        {
            if (m != null && !string.IsNullOrEmpty(m.MaterialId))
                _lookup[m.MaterialId] = m;
        }

        GD.Print($"MaterialStatsDb loaded {_lookup.Count} materials.");
    }

    public MaterialStatEntry Get(string materialId)
    {
        if (_lookup.TryGetValue(materialId, out var m))
            return m;
        GD.PrintErr($"MaterialStatsDb: Material not found: {materialId}");
        return null;
    }
}