using Godot;

public partial class BlockResource : Resource
{
    [Export] public string BlockId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D TextureTop { get; set; }
    [Export] public Texture2D TextureSide { get; set; }
    [Export] public Texture2D TextureBottom { get; set; }
    [Export] public bool CanChisel { get; set; } = true;
    [Export] public bool IsSolid { get; set; } = true;
    [Export] public bool IsTransparent { get; set; } = false;
    [Export] public bool CanRotate { get; set; } = false;
    [Export] public bool CanHaveFeatures { get; set; } = true;
    [Export] public bool GrassCanGrow { get; set; } = true;
    [Export] public string SoundBreak { get; set; } = "";
    [Export] public string SoundPlace { get; set; } = "";
    [Export] public string SoundFootstep { get; set; } = "";
    [Export] public string[] Drops { get; set; } = new string[0];
    [Export] public float Hardness { get; set; } = 1.0f;
    [Export] public float Slipperiness { get; set; } = 0.0f;
    [Export] public int LightLevel { get; set; } = 0;
    [Export] public bool IsFlammable { get; set; } = false;
    [Export] public bool IsCross { get; set; } = false; // renders as X cross (flowers)
[Export] public bool IsFlatGround { get; set; } = false; // renders as flat 1px slab (clover)
}