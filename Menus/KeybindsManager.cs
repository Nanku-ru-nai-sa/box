using Godot;
using System;
using System.Collections.Generic;

public partial class KeybindsManager : Node
{
    public static KeybindsManager Instance { get; private set; }

    // ============================================================
    // REBINDABLE ACTIONS
    // ============================================================

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
        ("toggle_calendar",      "Calendar"),

        ("take_screenshot",      "Screenshot"),
        ("toggle_chunk_borders", "Chunk Borders"),
        ("quick_save",           "Quick Save"),
    };

    // ============================================================
    // DEFAULT KEYS
    // ============================================================

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
        ["toggle_calendar"]      = Key.O,

        ["take_screenshot"]      = Key.F2,
        ["toggle_chunk_borders"] = Key.F3,
        ["quick_save"]           = Key.F5,

        // F6 is intentionally NOT managed here.
        // F6 remains your Debug Menu key.
    };

    // ============================================================
    // CURRENT KEYS
    // ============================================================

    private readonly Dictionary<string, Key> _current = new();

    private const string SavePath = "user://keybinds.json";

    public event Action OnKeybindsChanged;

    // ============================================================
    // READY
    // ============================================================

    public override void _Ready()
    {
        Instance = this;

        foreach (var (action, _) in Rebindable)
        {
            // Create action if it doesn't already exist.
            if (!InputMap.HasAction(action))
            {
                InputMap.AddAction(action);
            }
            else
            {
                // Learn the actual default from project.godot.
                var events = InputMap.ActionGetEvents(action);

                if (events.Count > 0 &&
                    events[0] is InputEventKey existing)
                {
                    Key key =
                        existing.PhysicalKeycode != Key.None
                            ? existing.PhysicalKeycode
                            : existing.Keycode;

                    if (key != Key.None)
                        _defaults[action] = key;
                }
            }
        }

        LoadAndApply();

        GD.Print("[KeybindsManager] Keybinds loaded.");
        GD.Print(
            $"[KeybindsManager] Calendar = " +
            $"{GetKeyLabel("toggle_calendar")}"
        );
    }

    // ============================================================
    // GET KEY
    // ============================================================

    public Key GetKey(string action)
    {
        if (_current.TryGetValue(action, out var key))
            return key;

        return Key.None;
    }

    public string GetKeyLabel(string action)
    {
        Key key = GetKey(action);

        if (key == Key.None)
            return "—";

        return OS.GetKeycodeString(key);
    }

    // ============================================================
    // REBIND
    // ============================================================

    public void Rebind(string action, Key key)
    {
        if (!_defaults.ContainsKey(action))
            return;

        // Prevent duplicate keys between rebindable actions.
        foreach (var other in new List<string>(_current.Keys))
        {
            if (other != action &&
                _current[other] == key)
            {
                SetAction(other, Key.None);
            }
        }

        SetAction(action, key);

        Save();

        OnKeybindsChanged?.Invoke();
    }

    // ============================================================
    // RESET ONE
    // ============================================================

    public void ResetToDefault(string action)
    {
        if (!_defaults.TryGetValue(action, out var key))
            return;

        SetAction(action, key);

        Save();

        OnKeybindsChanged?.Invoke();
    }

    // ============================================================
    // RESET ALL
    // ============================================================

    public void ResetAllToDefault()
    {
        foreach (var (action, _) in Rebindable)
        {
            if (_defaults.TryGetValue(action, out var key))
                SetAction(action, key);
        }

        Save();

        OnKeybindsChanged?.Invoke();
    }

    // ============================================================
    // APPLY ACTION
    // ============================================================

    private void SetAction(string action, Key key)
    {
        _current[action] = key;

        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        InputMap.ActionEraseEvents(action);

        if (key == Key.None)
            return;

        var inputEvent = new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key
        };

        InputMap.ActionAddEvent(
            action,
            inputEvent
        );
    }

    // ============================================================
    // SAVE
    // ============================================================

    private void Save()
    {
        var data =
            new Godot.Collections.Dictionary();

        foreach (var kvp in _current)
        {
            data[kvp.Key] =
                (int)kvp.Value;
        }

        using var file =
            FileAccess.Open(
                SavePath,
                FileAccess.ModeFlags.Write
            );

        if (file == null)
        {
            GD.PrintErr(
                "[KeybindsManager] Could not save keybinds."
            );

            return;
        }

        file.StoreString(
            Json.Stringify(data)
        );

        GD.Print(
            "[KeybindsManager] Keybinds saved."
        );
    }

    // ============================================================
    // LOAD
    // ============================================================

    private void LoadAndApply()
    {
        Godot.Collections.Dictionary parsed = null;

        if (FileAccess.FileExists(SavePath))
        {
            using var file =
                FileAccess.Open(
                    SavePath,
                    FileAccess.ModeFlags.Read
                );

            if (file != null)
            {
                parsed =
                    Json.ParseString(
                        file.GetAsText()
                    ).AsGodotDictionary();
            }
        }

        foreach (var (action, _) in Rebindable)
        {
            if (!_defaults.TryGetValue(
                action,
                out Key defaultKey))
            {
                continue;
            }

            Key key = defaultKey;

            if (parsed != null &&
                parsed.ContainsKey(action))
            {
                key =
                    (Key)(int)parsed[action];
            }

            SetAction(
                action,
                key
            );
        }
    }
}