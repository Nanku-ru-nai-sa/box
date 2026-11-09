using Godot;

public partial class OverlayResource : Resource
{
    [Export] public string OverlayId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D OverlayTexture { get; set; }
    [Export] public string ItemId { get; set; } = "";
}