using Godot;
using System;
using System.Collections.Generic;

// KeybindsManager — autoload singleton (add to Project > Autoloads as "KeybindsManager").
// Owns every rebindable action's key, persists overrides to user://keybinds.json,
// and applies them to Godot's InputMap so the rest of the game just keeps calling
// Input.IsActionPressed("jump") etc. as normal — nothing else needs to know a key
// was ever remapped.
//
// Actions already defined in project.godot (move_forward/back/left/right, jump,
// sprint, crouch) are picked up automatically — this script does not duplicate
// their definitions, it just learns their current key so it can offer rebinding
// and a "reset to default" for them too. Actions that don't exist yet in
// project.godot are created here on first run.


public partial class KeybindsManager : Node
{
    public static KeybindsManager Instance { get; private set; }

    // (action name, display label) — order here is the order shown in the Keybinds menu.
    public static readonly (string Action, string Label)[] Rebindable =
    {
        ("move_forward",         "Move Forward"),
        ("move_back",            "Move Back"),
        ("move_left",            "Move Left"),
        ("move_right",           "Move Right"),
        ("jump",                 "Jump"),
        ("sprint",               "Sprint"),
        ("crouch",               "Crouch"),
        ("toggle_inventory",     "Inventory"),
        ("drop_item",            "Drop Item"),
        ("open_chat",            "Chat"),
        ("toggle_creative_menu", "Creative Menu"),
        ("cycle_gamemode",       "Cycle Gamemode"),
        ("toggle_hud",           "Toggle HUD"),
        ("take_screenshot",      "Screenshot"),
        ("toggle_chunk_borders", "Chunk Borders"),
        ("quick_save",           "Quick Save"),
    };

    // Fallback defaults for actions this script creates itself. For actions that
    // already exist in project.godot (movement/jump/sprint/crouch) these get
    // overwritten in _Ready with whatever key project.godot actually loaded,
    // before any user override is applied — so "Reset to Default" always means
    // "back to what the game shipped with," not just this hardcoded guess.
    private readonly Dictionary<string, Key> _defaults = new()
    {
        ["move_forward"]         = Key.W,
        ["move_back"]            = Key.S,
        ["move_left"]            = Key.A,
        ["move_right"]           = Key.D,
        ["jump"]                 = Key.Space,
        ["sprint"]               = Key.Z,
        ["crouch"]               = Key.Ctrl,
        ["toggle_inventory"]     = Key.Tab,
        ["drop_item"]            = Key.Q,
        ["open_chat"]            = Key.T,
        ["toggle_creative_menu"] = Key.V,
        ["cycle_gamemode"]       = Key.F4,
        ["toggle_hud"]           = Key.F1,
        ["take_screenshot"]      = Key.F2,
        ["toggle_chunk_borders"] = Key.F3,
        ["quick_save"]           = Key.F5,
    };

    // The key currently bound to each action — kept in sync with InputMap.
    private readonly Dictionary<string, Key> _current = new();

    private const string SavePath = "user://keybinds.json";

    // Fired whenever a binding changes, so any open UI can refresh its labels.
    public event Action OnKeybindsChanged;

    public override void _Ready()
    {
        Instance = this;

        // Make sure every rebindable action exists, and learn the real
        // default for ones project.godot already defines.
        foreach (var (action, _) in Rebindable)
        {
            if (InputMap.HasAction(action))
            {
                var events = InputMap.ActionGetEvents(action);
                if (events.Count > 0 && events[0] is InputEventKey existing)
                {
                    Key k = existing.PhysicalKeycode != Key.None ? existing.PhysicalKeycode : existing.Keycode;
                    if (k != Key.None) _defaults[action] = k;
                }
            }
            else
            {
                InputMap.AddAction(action);
            }
        }

        LoadAndApply();
    }

    // ── Public API (call these from the Keybinds menu) ─────────────────────────

    public Key GetKey(string action) => _current.TryGetValue(action, out var k) ? k : Key.None;

    public string GetKeyLabel(string action)
    {
        Key k = GetKey(action);
        return k == Key.None ? "—" : OS.GetKeycodeString(k);
    }

    public void Rebind(string action, Key key)
    {
        if (!_defaults.ContainsKey(action)) return;

        // Never let two rebindable actions silently share the same key —
        // if another one already used it, clear that one instead.
        foreach (var other in new List<string>(_current.Keys))
        {
            if (other != action && _current[other] == key)
                SetAction(other, Key.None);
        }

        SetAction(action, key);
        Save();
        OnKeybindsChanged?.Invoke();
    }

    public void ResetToDefault(string action)
    {
        if (!_defaults.TryGetValue(action, out var key)) return;
        SetAction(action, key);
        Save();
        OnKeybindsChanged?.Invoke();
    }

    public void ResetAllToDefault()
    {
        foreach (var (action, _) in Rebindable) SetAction(action, _defaults[action]);
        Save();
        OnKeybindsChanged?.Invoke();
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void SetAction(string action, Key key)
    {
        _current[action] = key;
        InputMap.ActionEraseEvents(action);
        if (key != Key.None)
        {
            var ev = new InputEventKey { Keycode = key, PhysicalKeycode = key };
            InputMap.ActionAddEvent(action, ev);
        }
    }

    private void Save()
    {
        var data = new Godot.Collections.Dictionary();
        foreach (var kvp in _current) data[kvp.Key] = (int)kvp.Value;

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null) { GD.PrintErr("KeybindsManager: could not write keybinds"); return; }
        file.StoreString(Json.Stringify(data));
        GD.Print("Keybinds saved.");
    }

    private void LoadAndApply()
    {
        Godot.Collections.Dictionary parsed = null;

        if (FileAccess.FileExists(SavePath))
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file != null) parsed = Json.ParseString(file.GetAsText()).AsGodotDictionary();
        }

        foreach (var (action, _) in Rebindable)
        {
            Key key = _defaults[action];
            if (parsed != null && parsed.ContainsKey(action))
                key = (Key)(int)parsed[action];
            SetAction(action, key);
        }

        GD.Print("Keybinds loaded.");
    }
    
}