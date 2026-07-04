using Godot;
using System;

// SettingsManager — autoload singleton (add to Project > Autoloads as "SettingsManager")
// Persists settings to user://settings.json and provides a central place to read/write them.
// All other code reads from SettingsManager.Instance rather than storing settings locally.

public partial class SettingsManager : Node
{
    public static SettingsManager Instance { get; private set; }

    // ── Settings values ──────────────────────────────────────────────────────
    // Stored as 0-100 slider value; converted to actual multiplier in PlayerCamera
    public float MouseSensitivity { get; private set; } = 50f;
    public float Fov              { get; private set; } = 75f;
    public float MasterVolume     { get; private set; } = 1.0f;  // 0.0 - 1.0
    public bool  Fullscreen       { get; private set; } = false;

    // Admin — unlocked by typing /admin <password> in chat
    public bool   IsAdmin        { get; private set; } = false;
    private const string AdminPassword = "boxadmin"; // change this to whatever you want

    private const string SavePath = "user://settings.json";

    // Fired whenever any setting changes so UI can update itself
    public event Action OnSettingsChanged;

    public override void _Ready()
    {
        Instance = this;
        Load();
        ApplyAll();
    }

    // ── Setters (call these from menus) ─────────────────────────────────────

    public void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, 0f, 100f);
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetFov(float value)
    {
        Fov = Mathf.Clamp(value, 50f, 120f);
        ApplyFov();
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp(value, 0f, 1f);
        ApplyVolume();
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetFullscreen(bool value)
    {
        Fullscreen = value;
        ApplyFullscreen();
        Save();
        OnSettingsChanged?.Invoke();
    }

    public bool TryUnlockAdmin(string password)
    {
        if (password == AdminPassword)
        {
            IsAdmin = true;
            GD.Print("Admin unlocked.");
            return true;
        }
        GD.Print("Wrong admin password.");
        return false;
    }

    public void RevokeAdmin() { IsAdmin = false; }

    // ── Apply to engine ──────────────────────────────────────────────────────

    private void ApplyAll()
    {
        ApplyFov();
        ApplyVolume();
        ApplyFullscreen();
    }

    private void ApplyFov()
    {
        // Find PlayerCamera and call RefreshFov so it updates immediately
        var playerCam = GetTree().Root.FindChild("PlayerCamera", true, false) as PlayerCamera;
        playerCam?.RefreshFov();
    }

    private void ApplyVolume()
    {
        // Convert linear 0-1 to dB for Godot's audio bus
        float db = MasterVolume > 0f
            ? 20f * Mathf.Log(MasterVolume) / Mathf.Log(10f)  // linear → dB
            : -80f;
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
    }

    private void ApplyFullscreen()
    {
        DisplayServer.WindowSetMode(
            Fullscreen
                ? DisplayServer.WindowMode.Fullscreen
                : DisplayServer.WindowMode.Windowed);
    }

    // ── Save / Load ──────────────────────────────────────────────────────────

    public void Save()
    {
        var data = new Godot.Collections.Dictionary
        {
            ["mouse_sensitivity"] = MouseSensitivity,
            ["fov"]               = Fov,
            ["master_volume"]     = MasterVolume,
            ["fullscreen"]        = Fullscreen
        };

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null) { GD.PrintErr("SettingsManager: could not write settings"); return; }
        file.StoreString(Json.Stringify(data));
        GD.Print("Settings saved.");
    }

    public void Load()
    {
        if (!FileAccess.FileExists(SavePath)) return;

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null) return;

        var parsed = Json.ParseString(file.GetAsText()).AsGodotDictionary();
        if (parsed == null) return;

        if (parsed.ContainsKey("mouse_sensitivity")) MouseSensitivity = (float)parsed["mouse_sensitivity"];
        if (parsed.ContainsKey("fov"))               Fov              = (float)parsed["fov"];
        if (parsed.ContainsKey("master_volume"))     MasterVolume     = (float)parsed["master_volume"];
        if (parsed.ContainsKey("fullscreen"))        Fullscreen       = (bool)parsed["fullscreen"];

        GD.Print("Settings loaded.");
    }
}