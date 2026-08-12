using Godot;
using System;
using System.Collections.Generic;

// SettingsPanel — the ONE place that builds the Settings + Keybinds UI.
// Both MainMenu and PauseMenu instantiate this instead of building their own
// copy, so there is only one place to fix if a slider range, a label, or a
// wiring bug ever needs fixing again.
//
// Usage:
//   var panel = new SettingsPanel(() => { panel.Visible = false; ShowSomethingElse(); }, "Close");
//   someContainer.AddChild(panel);
//   panel.Visible = false;
public partial class SettingsPanel : Panel
{
    private readonly Action _onClose;
    private readonly string _closeLabel;

    private Control _settingsTab;
    private Control _keybindsTab;
    private Button _settingsTabBtn;
    private Button _keybindsTabBtn;

    private string _listeningAction = null;
    private readonly Dictionary<string, Button> _keybindButtons = new();
    private Label _listenHint;

    public SettingsPanel(Action onClose, string closeLabel = "Close")
    {
        _onClose = onClose;
        _closeLabel = closeLabel;
    }

    public override void _Ready()
    {
        // Keep working while the game tree is paused (this panel is used from
        // the pause menu, which pauses the tree behind it).
        ProcessMode = ProcessModeEnum.Always;

        AnchorLeft = 0.5f; AnchorRight = 0.5f;
        AnchorTop  = 0.5f; AnchorBottom = 0.5f;
        OffsetLeft   = -280f; OffsetRight  = 280f;
        OffsetTop    = -260f; OffsetBottom = 260f;

        var style = new StyleBoxFlat();
        style.BgColor          = new Color(0.1f, 0.1f, 0.1f, 0.97f);
        style.BorderColor      = new Color(0.4f, 0.4f, 0.4f);
        style.BorderWidthTop   = 2; style.BorderWidthBottom = 2;
        style.BorderWidthLeft  = 2; style.BorderWidthRight  = 2;
        AddThemeStyleboxOverride("panel", style);

        var outer = new VBoxContainer();
        outer.AnchorRight  = 1f; outer.AnchorBottom = 1f;
        outer.OffsetLeft   = 24f; outer.OffsetRight  = -24f;
        outer.OffsetTop    = 20f; outer.OffsetBottom = -20f;
        outer.AddThemeConstantOverride("separation", 14);
        AddChild(outer);

        var title = new Label();
        title.Text                = "Settings";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 22);
        outer.AddChild(title);

        // Tab row
        var tabRow = new HBoxContainer();
        tabRow.Alignment = BoxContainer.AlignmentMode.Center;
        tabRow.AddThemeConstantOverride("separation", 8);

        _settingsTabBtn = new Button();
        _settingsTabBtn.Text = "Settings";
        _settingsTabBtn.ToggleMode = true;
        _settingsTabBtn.ButtonPressed = true;

        _keybindsTabBtn = new Button();
        _keybindsTabBtn.Text = "Keybinds";
        _keybindsTabBtn.ToggleMode = true;

        tabRow.AddChild(_settingsTabBtn);
        tabRow.AddChild(_keybindsTabBtn);
        outer.AddChild(tabRow);

        _settingsTab = BuildSettingsTab();
        _keybindsTab = BuildKeybindsTab();
        outer.AddChild(_settingsTab);
        outer.AddChild(_keybindsTab);
        _keybindsTab.Visible = false;

        _settingsTabBtn.Pressed += () => ShowTab(true);
        _keybindsTabBtn.Pressed += () => ShowTab(false);

        var closeBtn = MakeButton(_closeLabel);
        closeBtn.Pressed += () => { CancelListening(); _onClose?.Invoke(); };
        outer.AddChild(closeBtn);
    }

    private void ShowTab(bool settings)
    {
        _settingsTab.Visible = settings;
        _keybindsTab.Visible = !settings;
        _settingsTabBtn.ButtonPressed = settings;
        _keybindsTabBtn.ButtonPressed = !settings;
        CancelListening();
    }

    // ── Settings tab ─────────────────────────────────────────────────────────

    private Control BuildSettingsTab()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);

        var sm = SettingsManager.Instance;

        var (sensSlider, sensValue) = AddSlider(vbox, "Mouse Sensitivity", sm.MouseSensitivity, 0f, 100f, 1f);
        var (fovSlider, fovValue)   = AddSlider(vbox, "Field of View", sm.Fov, 50f, 120f, 1f);
        var (volSlider, volValue)   = AddSlider(vbox, "Master Volume", sm.MasterVolume, 0f, 1f, 0.01f);

        var fsRow = new HBoxContainer();
        fsRow.AddThemeConstantOverride("separation", 12);
        var fsLabel = new Label(); fsLabel.Text = "Fullscreen";
        fsLabel.CustomMinimumSize = new Vector2(180f, 0f);
        var fsToggle = new CheckButton();
        fsToggle.ButtonPressed = sm.Fullscreen;
        fsRow.AddChild(fsLabel);
        fsRow.AddChild(fsToggle);
        vbox.AddChild(fsRow);

        sensSlider.ValueChanged += v =>
        {
            SettingsManager.Instance?.SetMouseSensitivity((float)v);
            sensValue.Text = ((int)v).ToString();
        };
        fovSlider.ValueChanged += v =>
        {
            SettingsManager.Instance?.SetFov((float)v);
            fovValue.Text = ((int)v).ToString();
        };
        volSlider.ValueChanged += v =>
        {
            SettingsManager.Instance?.SetMasterVolume((float)v);
            volValue.Text = ((int)(v * 100)).ToString() + "%";
        };
        fsToggle.Toggled += v => SettingsManager.Instance?.SetFullscreen(v);

        return vbox;
    }

    private (HSlider, Label) AddSlider(VBoxContainer parent, string labelText,
        float current, float min, float max, float step)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var lbl = new Label();
        lbl.Text              = labelText;
        lbl.CustomMinimumSize = new Vector2(180f, 0f);

        var slider = new HSlider();
        slider.MinValue            = min;
        slider.MaxValue            = max;
        slider.Step                = step;
        slider.Value               = current;
        slider.CustomMinimumSize   = new Vector2(140f, 24f);
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        string FormatVal(float v) => labelText == "Field of View"
            ? ((int)v).ToString()
            : labelText == "Master Volume"
                ? ((int)(v * 100)).ToString() + "%"
                : ((int)v).ToString();

        var valLbl = new Label();
        valLbl.Text                = FormatVal(current);
        valLbl.CustomMinimumSize   = new Vector2(48f, 0f);
        valLbl.HorizontalAlignment = HorizontalAlignment.Right;

        var valEdit = new LineEdit();
        valEdit.CustomMinimumSize = new Vector2(48f, 0f);
        valEdit.Alignment         = HorizontalAlignment.Right;
        valEdit.Visible           = false;

        valLbl.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                valEdit.Text    = slider.Value.ToString("F0");
                valLbl.Visible  = false;
                valEdit.Visible = true;
                valEdit.GrabFocus();
                valEdit.SelectAll();
            }
        };
        valLbl.MouseFilter = Control.MouseFilterEnum.Stop;

        void CommitEdit()
        {
            if (float.TryParse(valEdit.Text, out float typed))
            {
                typed        = Mathf.Clamp(typed, min, max);
                slider.Value = typed;
                valLbl.Text  = FormatVal(typed);
            }
            valEdit.Visible = false;
            valLbl.Visible  = true;
        }
        valEdit.TextSubmitted += _ => CommitEdit();
        valEdit.FocusExited   += CommitEdit;

        row.AddChild(lbl);
        row.AddChild(slider);
        row.AddChild(valLbl);
        row.AddChild(valEdit);
        parent.AddChild(row);

        return (slider, valLbl);
    }

    // ── Keybinds tab ─────────────────────────────────────────────────────────

    private Control BuildKeybindsTab()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);

        _listenHint = new Label();
        _listenHint.Text                = "";
        _listenHint.HorizontalAlignment = HorizontalAlignment.Center;
        _listenHint.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.3f));
        vbox.AddChild(_listenHint);

        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(0f, 260f);
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(list);
        vbox.AddChild(scroll);

        foreach (var (action, label) in KeybindsManager.Rebindable)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);

            var lbl = new Label();
            lbl.Text              = label;
            lbl.CustomMinimumSize = new Vector2(150f, 0f);
            row.AddChild(lbl);

            var keyBtn = new Button();
            keyBtn.CustomMinimumSize = new Vector2(120f, 0f);
            keyBtn.Text = KeybindsManager.Instance?.GetKeyLabel(action) ?? "—";
            keyBtn.Pressed += () => BeginListening(action, keyBtn);
            row.AddChild(keyBtn);
            _keybindButtons[action] = keyBtn;

            var resetBtn = new Button();
            resetBtn.Text              = "Reset";
            resetBtn.CustomMinimumSize = new Vector2(64f, 0f);
            resetBtn.Pressed += () =>
            {
                KeybindsManager.Instance?.ResetToDefault(action);
                RefreshKeyButtons();
            };
            row.AddChild(resetBtn);

            list.AddChild(row);
        }

        var resetAllBtn = MakeButton("Reset All to Default");
        resetAllBtn.Pressed += () =>
        {
            KeybindsManager.Instance?.ResetAllToDefault();
            RefreshKeyButtons();
        };
        vbox.AddChild(resetAllBtn);

        return vbox;
    }

    private void BeginListening(string action, Button button)
    {
        CancelListening();
        _listeningAction = action;
        button.Text       = "Press a key...";
        _listenHint.Text  = "Press any key to bind it — Esc to cancel.";
    }

    private void CancelListening()
    {
        if (_listeningAction == null) return;
        _listeningAction = null;
        _listenHint.Text = "";
        RefreshKeyButtons();
    }

    private void RefreshKeyButtons()
    {
        foreach (var kvp in _keybindButtons)
            kvp.Value.Text = KeybindsManager.Instance?.GetKeyLabel(kvp.Key) ?? "—";
    }

    // Catches the "next key press" while a rebind button is waiting for one.
    // Consumes the event so it never reaches gameplay or menu shortcuts.
    public override void _Input(InputEvent @event)
    {
        if (_listeningAction == null || !Visible) return;
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            GetViewport().SetInputAsHandled();

            if (key.Keycode == Key.Escape) { CancelListening(); return; }

            var action = _listeningAction;
            _listeningAction = null;
            _listenHint.Text = "";

            Key boundKey = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
            KeybindsManager.Instance?.Rebind(action, boundKey);
            RefreshKeyButtons();
        }
    }

    private Button MakeButton(string text)
    {
        var btn = new Button();
        btn.Text              = text;
        btn.CustomMinimumSize = new Vector2(240f, 44f);
        btn.AddThemeFontSizeOverride("font_size", 16);
        return btn;
    }
}