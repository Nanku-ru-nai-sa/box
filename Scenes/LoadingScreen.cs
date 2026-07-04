using Godot;

// LoadingScreen
// Add a Node to your Test scene, attach this script.
// It creates its own CanvasLayer internally so it always renders on top.

public partial class LoadingScreen : Node
{
    private const int   ExtraFrames  = 20;
    private const float FadeDuration = 0.6f;

    private CanvasLayer _layer;
    private ColorRect   _bg;
    private Label       _titleLabel;
    private Label       _statusLabel;
    private ProgressBar _progressBar;
    private Label       _progressLabel;

    private bool  _worldReadyReceived = false;
    private int   _extraFrameCount    = 0;
    private bool  _fading             = false;
    private float _fadeAlpha          = 1f;
    private int   _expectedChunks     = 0;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        // Build CanvasLayer internally so it's guaranteed on top
        _layer            = new CanvasLayer();
        _layer.Layer      = 100;
        _layer.ProcessMode = ProcessModeEnum.Always;
        AddChild(_layer);

        BuildUI();
        GD.Print("LoadingScreen: UI built.");

        FreezePlayer(true);

        var cm = FindChunkManager();
        if (cm != null)
        {
            GD.Print($"LoadingScreen: Found ChunkManager. IsInitialLoadComplete={cm.IsInitialLoadComplete}");
            int rd  = cm.RenderDistance;
            int vrd = cm.VerticalRenderDistance;
            _expectedChunks = (rd * 2 + 1) * (rd * 2 + 1) * (vrd * 2 + 1);
            GD.Print($"LoadingScreen: Expecting ~{_expectedChunks} chunks.");

            cm.WorldReady += OnWorldReady;

            if (cm.IsInitialLoadComplete)
                OnWorldReady();
        }
        else
        {
            GD.PrintErr("LoadingScreen: ChunkManager NOT found! Skipping loading screen.");
            FreezePlayer(false);
            QueueFree();
        }
    }

    private void BuildUI()
    {
        // Full screen dark background
        _bg              = new ColorRect();
        _bg.Color        = new Color(0.05f, 0.05f, 0.07f, 1f);
        _bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_bg);

        // Title
        _titleLabel = new Label();
        _titleLabel.Text = "BOX";
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _titleLabel.OffsetBottom = -200f;
        _titleLabel.AddThemeFontSizeOverride("font_size", 72);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _layer.AddChild(_titleLabel);

        // Progress bar — anchored to centre horizontally, fixed Y position
        _progressBar              = new ProgressBar();
        _progressBar.MinValue     = 0;
        _progressBar.MaxValue     = 100;
        _progressBar.Value        = 0;
        _progressBar.ShowPercentage = false;
        _progressBar.AnchorLeft   = 0.1f;
        _progressBar.AnchorRight  = 0.9f;
        _progressBar.AnchorTop    = 0.65f;
        _progressBar.AnchorBottom = 0.65f;
        _progressBar.OffsetBottom = 28f;
        _layer.AddChild(_progressBar);

        // Chunk count text
        _progressLabel = new Label();
        _progressLabel.Text = "Preparing...";
        _progressLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _progressLabel.AnchorLeft   = 0.1f;
        _progressLabel.AnchorRight  = 0.9f;
        _progressLabel.AnchorTop    = 0.72f;
        _progressLabel.AnchorBottom = 0.72f;
        _progressLabel.OffsetBottom = 24f;
        _progressLabel.AddThemeFontSizeOverride("font_size", 14);
        _progressLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
        _layer.AddChild(_progressLabel);

        // Status line
        _statusLabel = new Label();
        _statusLabel.Text = "Loading world...";
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _statusLabel.AnchorLeft   = 0.1f;
        _statusLabel.AnchorRight  = 0.9f;
        _statusLabel.AnchorTop    = 0.78f;
        _statusLabel.AnchorBottom = 0.78f;
        _statusLabel.OffsetBottom = 24f;
        _statusLabel.AddThemeFontSizeOverride("font_size", 16);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        _layer.AddChild(_statusLabel);
    }

    public override void _Process(double delta)
    {
        if (!_worldReadyReceived)
        {
            var cm = FindChunkManager();
            if (cm != null && _expectedChunks > 0)
            {
                int loaded = CountLoadedChunks(cm);
                float pct  = Mathf.Clamp((float)loaded / _expectedChunks * 100f, 0f, 99f);
                _progressBar.Value   = pct;
                _progressLabel.Text  = $"Chunks: {loaded} / {_expectedChunks}";
            }
            return;
        }

        // Wait extra frames so meshes finish
        if (_extraFrameCount < ExtraFrames)
        {
            _extraFrameCount++;
            _progressBar.Value  = 100f;
            _progressLabel.Text = "Ready!";
            return;
        }

        if (!_fading)
        {
            _fading    = true;
            _fadeAlpha = 1f;
            FreezePlayer(false);
        }

        // Fade out all UI children
        _fadeAlpha -= (float)delta / FadeDuration;
        _fadeAlpha  = Mathf.Max(_fadeAlpha, 0f);

        foreach (Node child in _layer.GetChildren())
            if (child is CanvasItem ci)
                ci.Modulate = new Color(1f, 1f, 1f, _fadeAlpha);

        if (_fadeAlpha <= 0f)
            QueueFree();
    }

    private void OnWorldReady()
    {
        GD.Print("LoadingScreen: WorldReady received.");
        _worldReadyReceived = true;
        _statusLabel.Text   = "Almost there...";
        RestorePlayerPosition();
    }

    private void RestorePlayerPosition()
    {
        var cm = FindChunkManager();
        if (cm == null) return;

        var player = FindPlayer();
        if (player == null) return;

        var savedPos = cm.LoadPlayerPosition();
        if (savedPos.HasValue)
        {
            player.GlobalPosition = savedPos.Value;
            GD.Print($"LoadingScreen: Player position restored to {savedPos.Value}");
        }
        else
        {
            GD.Print("LoadingScreen: No saved position, using default spawn.");
        }
    }

    private void FreezePlayer(bool freeze)
    {
        var player = FindPlayer();
        if (player == null)
        {
            GD.Print($"LoadingScreen: FreezePlayer({freeze}) — player not found yet.");
            return;
        }

        player.ProcessMode = freeze ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;

        if (!freeze && player is CharacterBody3D cb)
            cb.Velocity = Vector3.Zero;

        GD.Print($"LoadingScreen: Player {(freeze ? "frozen" : "unfrozen")}.");
    }

    private ChunkManager FindChunkManager()
        => GetTree().Root.FindChild("ChunkManager", true, false) as ChunkManager;

    private Node3D FindPlayer()
    {
        var found = GetTree().Root.FindChild("player", true, false)
                 ?? GetTree().Root.FindChild("Player", true, false);
        return found as Node3D;
    }

    private int CountLoadedChunks(ChunkManager cm)
    {
        int count = 0;
        foreach (var _ in cm.GetLoadedChunkPositions()) count++;
        return count;
    }
}