using Godot;

[GlobalClass]
public partial class SoundResource : Resource
{
    // Basic Info
    [Export] public string SoundId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";

    // The actual audio file
    [Export] public AudioStream AudioStream { get; set; }

    // Category for volume control in settings
    // "sfx", "ambient", "music", "ui"
    [Export] public string Category { get; set; } = "sfx";

    // Volume
    [Export] public float VolumeDb { get; set; } = 0f;

    // Pitch variation - keeps repeated sounds natural
    // e.g. Min 0.9 Max 1.1 gives slight random pitch each play
    [Export] public float PitchMin { get; set; } = 0.9f;
    [Export] public float PitchMax { get; set; } = 1.1f;

    // 3D positional audio
    // true = sound comes from a point in the world
    // false = flat stereo (UI sounds, music)
    [Export] public bool IsSpatial { get; set; } = true;

    // Max distance before sound is inaudible (spatial only)
    [Export] public float MaxDistance { get; set; } = 32f;

    // Can this sound overlap itself
    // e.g. footsteps yes, death sound no
    [Export] public bool CanOverlap { get; set; } = true;

    // Looping (ambient sounds, music handled separately)
    [Export] public bool Loop { get; set; } = false;
}