using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class AudioManager : Node
{
    // Singleton
    public static AudioManager Instance { get; private set; }

    // Volume levels per category (0.0 to 1.0)
    private Dictionary<string, float> _volumes = new()
    {
        { "sfx",     1.0f },
        { "ambient", 1.0f },
        { "music",   1.0f },
        { "ui",      1.0f }
    };

    // Pool of audio players for SFX
    private List<AudioStreamPlayer> _sfxPool = new();
    private List<AudioStreamPlayer3D> _spatialPool = new();
    private int _poolSize = 16;

    // Current music player
    private AudioStreamPlayer _musicPlayer;
    private AudioStreamPlayer _ambientPlayer;

    // Sound registry
    private Dictionary<string, SoundResource> _sounds = new();

    public override void _Ready()
    {
        Instance = this;
        BuildAudioPools();
        LoadAllSounds();
        GD.Print($"AudioManager loaded {_sounds.Count} sounds.");
    }

    // Build reusable audio player pools
    // Pooling avoids creating/destroying nodes constantly
    private void BuildAudioPools()
    {
        // Flat stereo pool (UI, non-spatial SFX)
        for (int i = 0; i < _poolSize; i++)
        {
            var player = new AudioStreamPlayer();
            AddChild(player);
            _sfxPool.Add(player);
        }

        // 3D spatial pool (world sounds)
        for (int i = 0; i < _poolSize; i++)
        {
            var player = new AudioStreamPlayer3D();
            AddChild(player);
            _spatialPool.Add(player);
        }

        // Dedicated music player
        _musicPlayer = new AudioStreamPlayer();
        _musicPlayer.VolumeDb = 0f;
        AddChild(_musicPlayer);

        // Dedicated ambient player
        _ambientPlayer = new AudioStreamPlayer();
        _ambientPlayer.VolumeDb = 0f;
        AddChild(_ambientPlayer);
    }

    // Load all sounds from data folder
    private void LoadAllSounds()
{
    string path = "res://Assets/Data/Sounds/";

    using var dir = DirAccess.Open(path);
    if (dir == null)
    {
        GD.Print("AudioManager: No sounds loaded yet.");
        return;
    }

    dir.ListDirBegin();
    string fileName = dir.GetNext();

    while (fileName != "")
    {
        if (fileName.EndsWith(".tres"))
        {
            string fullPath = path + fileName;
            var sound = GD.Load<SoundResource>(fullPath);

            if (sound != null && sound.SoundId != "")
            {
                _sounds[sound.SoundId] = sound;
            }
        }
        fileName = dir.GetNext();
    }
}

    // Play a flat stereo sound (UI, non-spatial)
    public void PlaySound(string soundId)
    {
        if (!_sounds.TryGetValue(soundId, out SoundResource sound))
        {
            GD.PrintErr($"AudioManager: Sound not found: {soundId}");
            return;
        }

        // Find available player from pool
        AudioStreamPlayer player = null;
        foreach (var p in _sfxPool)
        {
            if (!p.Playing || sound.CanOverlap)
            {
                player = p;
                break;
            }
        }

        if (player == null) return;

        player.Stream = sound.AudioStream;
        player.VolumeDb = sound.VolumeDb + VolumeToDb(_volumes[sound.Category]);
        player.PitchScale = (float)GD.RandRange(sound.PitchMin, sound.PitchMax);
        player.Play();
    }

    // Play a 3D spatial sound at a world position
    public void PlaySoundAt(string soundId, Vector3 position)
    {
        if (!_sounds.TryGetValue(soundId, out SoundResource sound))
        {
            GD.PrintErr($"AudioManager: Sound not found: {soundId}");
            return;
        }

        AudioStreamPlayer3D player = null;
        foreach (var p in _spatialPool)
        {
            if (!p.Playing || sound.CanOverlap)
            {
                player = p;
                break;
            }
        }

        if (player == null) return;

        player.Stream = sound.AudioStream;
        player.VolumeDb = sound.VolumeDb + VolumeToDb(_volumes[sound.Category]);
        player.PitchScale = (float)GD.RandRange(sound.PitchMin, sound.PitchMax);
        player.MaxDistance = sound.MaxDistance;
        player.GlobalPosition = position;
        player.Play();
    }

    // Play music track with optional fade
    public void PlayMusic(string soundId, float fadeTime = 1.0f)
    {
        if (!_sounds.TryGetValue(soundId, out SoundResource sound)) return;

        // Simple crossfade using a tween
        var tween = CreateTween();
        tween.TweenProperty(_musicPlayer, "volume_db", -40f, fadeTime);
        tween.TweenCallback(Callable.From(() =>
        {
            _musicPlayer.Stream = sound.AudioStream;
            _musicPlayer.Play();
        }));
        tween.TweenProperty(_musicPlayer, "volume_db",
            sound.VolumeDb + VolumeToDb(_volumes["music"]), fadeTime);
    }

    // Play ambient loop (biome sounds, cave drips etc)
    public void PlayAmbient(string soundId, float fadeTime = 2.0f)
    {
        if (!_sounds.TryGetValue(soundId, out SoundResource sound)) return;

        var tween = CreateTween();
        tween.TweenProperty(_ambientPlayer, "volume_db", -40f, fadeTime);
        tween.TweenCallback(Callable.From(() =>
        {
            _ambientPlayer.Stream = sound.AudioStream;
            _ambientPlayer.Play();
        }));
        tween.TweenProperty(_ambientPlayer, "volume_db",
            sound.VolumeDb + VolumeToDb(_volumes["ambient"]), fadeTime);
    }

    // Set volume for a category (called from settings menu)
    public void SetVolume(string category, float volume)
    {
        if (!_volumes.ContainsKey(category)) return;
        _volumes[category] = Mathf.Clamp(volume, 0f, 1f);
    }

    public float GetVolume(string category)
    {
        return _volumes.TryGetValue(category, out float vol) ? vol : 1f;
    }

    // Convert 0-1 linear volume to decibels
    private float VolumeToDb(float linear)
    {
        return linear <= 0f ? -80f : 20f * Mathf.Log(linear) / Mathf.Log(10f);
    }
}