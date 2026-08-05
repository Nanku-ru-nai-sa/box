// NEW FILE
// Put this in Scripts/Tools/MaterialStatEntry.cs
//
// This is ONE material's stats (e.g. "flint" or "stick"). You will create
// one of these per material as a .tres resource file in the Godot editor
// (right click in FileSystem dock -> New Resource -> MaterialStatEntry),
// or they can be edited directly in the array on MaterialStatsDb.
//
// This is the thing you asked for: "someplace that has all the durabilities
// that I can adjust them till they feel about right".

using Godot;

[GlobalClass]
public partial class MaterialStatEntry : Resource
{
    // Must match the material name used in your texture filenames,
    // e.g. "flint" for Assets/Textures/Items/tool/pickaxe/flint_head.png
    [Export] public string MaterialId { get; set; } = "";

    // Durability contributed PER UNIT of this material used in a recipe.
    // Example: flint = 32, stick = 0. A flint sword (2 flint + 1 stick)
    // = 2*32 + 1*0 = 64 total durability.
    [Export] public int DurabilityPerUnit { get; set; } = 0;

    // Multiplier on mining speed when this material is in a Head slot.
    [Export] public float MiningSpeedMod { get; set; } = 1.0f;

    // Multiplier on attack damage when this material is in a Head slot.
    [Export] public float AttackDamageMod { get; set; } = 1.0f;

    // Tier gate - used later to decide what ores a tool can mine.
    [Export] public int Tier { get; set; } = 0;

    // Placeholder for the Tinkers-style trait system - leave blank for now.
    [Export] public string TraitId { get; set; } = "";
    [Export] public float TraitMagnitude { get; set; } = 0f;
}