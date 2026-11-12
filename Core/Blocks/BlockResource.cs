using Godot;

public partial class BlockResource : Resource
{
    [Export] public string BlockId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";

    [Export] public Texture2D TextureTop { get; set; }
    [Export] public Texture2D TextureSide { get; set; }
    [Export] public Texture2D TextureBottom { get; set; }
    // Optional directional front face. When set, block Rotation selects which horizontal face uses it.
    [Export] public Texture2D TextureFront { get; set; }

    [Export] public bool CanChisel { get; set; } = true;
    [Export] public bool IsSolid { get; set; } = true;
    [Export] public bool IsTransparent { get; set; } = false;

    // Shared liquid-system metadata. Empty LiquidId means this is not a liquid.
    [Export] public string LiquidId { get; set; } = "";
    [Export] public bool IsLiquidSource { get; set; } = false;
    public bool IsLiquid => !string.IsNullOrEmpty(LiquidId);
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

    [Export] public bool IsCross { get; set; } = false;
    [Export] public bool IsFlatGround { get; set; } = false;
    [Export] public bool IsThinItem { get; set; } = false;

    // ============================================================
    // CUSTOM SHAPE
    // ============================================================
    // Allows a solid block to occupy only part of its normal
    // 1x1x1 world block while still behaving as a real block.
    //
    // Size is measured in world-block units.
    //
    // Example:
    // 2x16x2 = new Vector3(2f / 16f, 1f, 2f / 16f)
    // 4x16x4 = new Vector3(4f / 16f, 1f, 4f / 16f)
    // 6x16x6 = new Vector3(6f / 16f, 1f, 6f / 16f)
    //
    // The shape is centered horizontally inside the block.
    // ============================================================

    [Export] public bool IsCustomShape { get; set; } = false;

    [Export] public Vector3 CustomShapeSize { get; set; } = Vector3.One;
}