using Godot;

// PauseMenu — built entirely in code, added as a child of Player in _Ready.
// Escape toggles it. Game tree is paused while open.
// Add [GlobalClass] isn't needed — Player instantiates this directly.

public partial class PauseMenu : Node
{
    private const string MainMenuScene = "res://Scenes/MainMenu.tscn";

    private CanvasLayer  _layer;
    private Panel        _panel;
    private Control      _settingsPanel;

    public bool IsOpen { get; private set; } = false;

    // Player passes itself in so we can call save
    private Player _player;

    public void Init(Player player)
    {
        _player = player;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        _layer            = new CanvasLayer();
        _layer.Layer      = 50;
        _layer.ProcessMode = ProcessModeEnum.Always;
        AddChild(_layer);

        BuildPausePanel();
        _settingsPanel = new SettingsPanel(() =>
        {
            _settingsPanel.Visible = false;
            _panel.Visible = true;
        }, "Back");
        _layer.AddChild(_settingsPanel);
        _settingsPanel.Visible = false;

        _layer.Visible = false;
    }

    // ── Toggle ───────────────────────────────────────────────────────────────

    public void Open()
    {
        IsOpen             = true;
        _layer.Visible     = true;
        _settingsPanel.Visible = false;
        GetTree().Paused   = true;
        Input.MouseMode    = Input.MouseModeEnum.Visible;
    }

    public void Close()
    {
        IsOpen           = false;
        _layer.Visible   = false;
        GetTree().Paused = false;
        Input.MouseMode  = Input.MouseModeEnum.Captured;
    }

    // ── Build UI ─────────────────────────────────────────────────────────────

    private void BuildPausePanel()
    {
        // Dim overlay
        var overlay = new ColorRect();
        overlay.Color        = new Color(0f, 0f, 0f, 0.55f);
        overlay.AnchorRight  = 1f;
        overlay.AnchorBottom = 1f;
        overlay.MouseFilter  = Control.MouseFilterEnum.Ignore; // visual only, don't eat clicks
        _layer.AddChild(overlay);

        _panel = new Panel();
        _panel.AnchorLeft   = 0.5f; _panel.AnchorRight  = 0.5f;
        _panel.AnchorTop    = 0.5f; _panel.AnchorBottom = 0.5f;
        _panel.OffsetLeft   = -160f; _panel.OffsetRight  = 160f;
        _panel.OffsetTop    = -180f; _panel.OffsetBottom = 180f;

        var style = new StyleBoxFlat();
        style.BgColor          = new Color(0.1f, 0.1f, 0.1f, 0.97f);
        style.BorderColor      = new Color(0.45f, 0.45f, 0.45f);
        style.BorderWidthTop   = 2; style.BorderWidthBottom = 2;
        style.BorderWidthLeft  = 2; style.BorderWidthRight  = 2;
        _panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AnchorRight  = 1f; vbox.AnchorBottom = 1f;
        vbox.OffsetLeft   = 24f; vbox.OffsetRight  = -24f;
        vbox.OffsetTop    = 20f; vbox.OffsetBottom = -20f;
        vbox.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(vbox);

        var title = new Label();
        title.Text                = "Paused";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(title);

        var resumeBtn    = MakeButton("Resume");
        var settingsBtn  = MakeButton("Settings");
        var saveQuitBtn  = MakeButton("Save & Quit to Menu");

        vbox.AddChild(resumeBtn);
        vbox.AddChild(settingsBtn);
        vbox.AddChild(saveQuitBtn);

        resumeBtn.Pressed   += Close;
        settingsBtn.Pressed += () => { _settingsPanel.Visible = true; _panel.Visible = false; };
        saveQuitBtn.Pressed += OnSaveAndQuit;

        _layer.AddChild(_panel);
    }

    private void OnSaveAndQuit()
    {
        // Save world + inventory before leaving
        var cm = GetTree().Root.FindChild("ChunkManager", true, false) as ChunkManager;
        if (cm != null && _player != null)
        {
            cm.Call("SaveModifiedChunks");
            // Access inventory via reflection-safe public method
            _player.SaveInventoryFromPauseMenu(cm);
        }

        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(MainMenuScene);
    }

    // ── Settings panel ───────────────────────────────────────────────────────
    // Settings + Keybinds UI now lives in the shared SettingsPanel class
    // (Scenes/SettingsPanel.cs), so MainMenu and PauseMenu can't drift apart
    // and re-introduce the mouse-sensitivity-range bug.

    private Button MakeButton(string text)
    {
        var btn = new Button();
        btn.Text              = text;
        btn.CustomMinimumSize = new Vector2(240f, 44f);
        btn.AddThemeFontSizeOverride("font_size", 16);
        return btn;
    }
}