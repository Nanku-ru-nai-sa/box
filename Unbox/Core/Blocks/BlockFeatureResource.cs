using Godot;

[GlobalClass]
public partial class BlockFeatureResource : Resource
{
    // Basic Info
    [Export] public string FeatureId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";

    // Textures
    // Top overlay texture (sits on top face of block)
    [Export] public Texture2D TextureTop { get; set; }
    // Side overlay texture (few pixels on side faces)
    [Export] public Texture2D TextureSide { get; set; }

    // Rules
    // Can this feature exist if a block is on top
    [Export] public bool DiesWhenCovered { get; set; } = true;
    // Can this feature spread to adjacent blocks
    [Export] public bool CanSpread { get; set; } = false;
    // How many blocks away it can spread
    [Export] public int SpreadRange { get; set; } = 1;
    // Chance to spread per game tick (0.0 to 1.0)
    [Export] public float SpreadChance { get; set; } = 0.02f;
    // Which blocks this feature can grow on
    // Empty = any block with GrassCanGrow = true
    [Export] public string[] CanGrowOn { get; set; } = new string[0];

    // Sounds
    [Export] public string SoundStep { get; set; } = "";
    [Export] public string SoundBreak { get; set; } = "";

    // Visual
    // How many pixels from top does side overlay sit
    [Export] public int SideOverlayHeightPixels { get; set; } = 4;
}