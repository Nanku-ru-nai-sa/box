using Godot;

[GlobalClass]
public partial class BlockResource : Resource
{
    // Basic Info
    [Export] public string BlockId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";

    // Textures (each face can be different)
    [Export] public Texture2D TextureTop { get; set; }
    [Export] public Texture2D TextureSide { get; set; }
    [Export] public Texture2D TextureBottom { get; set; }

    // Chisel System
    [Export] public bool CanChisel { get; set; } = true;

    // Block Properties
    [Export] public bool IsSolid { get; set; } = true;
    [Export] public bool IsTransparent { get; set; } = false;
    [Export] public bool CanRotate { get; set; } = false;

    [Export] public bool CanHaveFeatures { get; set; } = true;
    [Export] public bool GrassCanGrow { get; set; } = true;

    // Sound IDs (strings that reference AudioRegistry)
    [Export] public string SoundBreak { get; set; } = "";
    [Export] public string SoundPlace { get; set; } = "";
    [Export] public string SoundFootstep { get; set; } = "";

    // Drops (item IDs that drop when fully broken)
    [Export] public string[] Drops { get; set; } = new string[0];

    // Properties (slippery, flammable, light level etc)
    [Export] public float Hardness { get; set; } = 1.0f;
    [Export] public float Slipperiness { get; set; } = 0.0f;
    [Export] public int LightLevel { get; set; } = 0;
    [Export] public bool IsFlammable { get; set; } = false;
}