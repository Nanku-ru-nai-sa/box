using Godot;

// Metadata for one ore "overlay" - the transparent-PNG fleck texture drawn
// on top of whatever host block it's logged onto (stone, dirt, gravel,
// rock...), plus which item it drops when that host block is mined.
public partial class OreResource : Resource
{
    [Export] public string OreId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D OverlayTexture { get; set; }
    [Export] public string ItemId { get; set; } = ""; // item given in addition to the host block's own drop
}