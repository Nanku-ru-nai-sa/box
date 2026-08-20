using Godot;

public partial class CalendarPanel : PanelContainer
{
    // ============================================================
    // SETTINGS
    // ============================================================

    private const int PanelWidth = 260;
    private const int PanelHeight = 145;
    private const int Margin = 14;

    // Calendar starts CLOSED.
    private bool _isOpen = false;

    // ============================================================
    // UI
    // ============================================================

    private Label _yearLabel;
    private Label _seasonLabel;
    private Label _timeLabel;
    private Label _moonLabel;

    private VBoxContainer _content;

    // ============================================================
    // MANAGERS
    // ============================================================

    private SeasonManager _seasonManager;
    private DayNightCycle _dayNightCycle;

    // ============================================================
    // READY
    // ============================================================

    public override void _Ready()
    {
        // --------------------------------------------------------
        // Panel setup
        // --------------------------------------------------------

        CustomMinimumSize =
            new Vector2(
                PanelWidth,
                PanelHeight
            );

        AnchorLeft = 1.0f;
        AnchorRight = 1.0f;
        AnchorTop = 0.0f;
        AnchorBottom = 0.0f;

        OffsetLeft = -PanelWidth - Margin;
        OffsetRight = -Margin;

        OffsetTop = Margin;
        OffsetBottom = Margin + PanelHeight;

        // --------------------------------------------------------
        // Background
        // --------------------------------------------------------

        var background = new StyleBoxFlat();

        background.BgColor =
            new Color(
                0.03f,
                0.03f,
                0.04f,
                0.90f
            );

        background.BorderWidthLeft = 2;
        background.BorderWidthTop = 2;
        background.BorderWidthRight = 2;
        background.BorderWidthBottom = 2;

        background.BorderColor =
            new Color(
                0.25f,
                0.25f,
                0.30f,
                0.95f
            );

        background.CornerRadiusTopLeft = 6;
        background.CornerRadiusTopRight = 6;
        background.CornerRadiusBottomLeft = 6;
        background.CornerRadiusBottomRight = 6;

        AddThemeStyleboxOverride(
            "panel",
            background
        );

        // --------------------------------------------------------
        // Content
        // --------------------------------------------------------

        _content = new VBoxContainer();

        _content.AddThemeConstantOverride(
            "separation",
            3
        );

        AddChild(_content);

        // --------------------------------------------------------
        // Year
        // --------------------------------------------------------

        _yearLabel =
            CreateLabel(
                18,
                HorizontalAlignment.Left
            );

        _content.AddChild(_yearLabel);

        // --------------------------------------------------------
        // Season / Day
        // --------------------------------------------------------

        _seasonLabel =
            CreateLabel(
                22,
                HorizontalAlignment.Left
            );

        _content.AddChild(_seasonLabel);

        // --------------------------------------------------------
        // Time
        // --------------------------------------------------------

        _timeLabel =
            CreateLabel(
                20,
                HorizontalAlignment.Left
            );

        _content.AddChild(_timeLabel);

        // --------------------------------------------------------
        // Moon
        // --------------------------------------------------------

        _moonLabel =
            CreateLabel(
                16,
                HorizontalAlignment.Left
            );

        _content.AddChild(_moonLabel);

        // --------------------------------------------------------
        // Calendar starts OFF
        // --------------------------------------------------------

        _isOpen = false;
        Visible = false;

        ClearDisplay();
    }

    // ============================================================
    // PROCESS
    // ============================================================

    public override void _Process(double delta)
    {
        // --------------------------------------------------------
        // Make absolutely sure we only operate inside a world.
        // --------------------------------------------------------

        if (!IsWorldActive())
        {
            if (_isOpen || Visible)
            {
                _isOpen = false;
                Visible = false;
            }

            return;
        }

        // --------------------------------------------------------
        // Toggle calendar
        // --------------------------------------------------------

        if (Input.IsActionJustPressed("toggle_calendar"))
        {
            Toggle();
        }

        // --------------------------------------------------------
        // Update display
        // --------------------------------------------------------

        if (_isOpen)
        {
            UpdateDisplay();
        }
    }

    // ============================================================
    // WORLD CHECK
    // ============================================================

    private bool IsWorldActive()
    {
        // --------------------------------------------------------
        // Find a valid SeasonManager.
        // --------------------------------------------------------

        if (!IsInstanceValid(_seasonManager))
        {
            _seasonManager =
                GetNodeOrNull<SeasonManager>(
                    "/root/SeasonManager"
                );
        }

        // --------------------------------------------------------
        // Find a valid DayNightCycle.
        // --------------------------------------------------------

        if (!IsInstanceValid(_dayNightCycle))
        {
            _dayNightCycle = null;

            Node found =
                GetTree()
                    .GetFirstNodeInGroup(
                        "day_night_cycle"
                    );

            if (found is DayNightCycle cycle &&
                IsInstanceValid(cycle) &&
                cycle.IsInsideTree())
            {
                _dayNightCycle = cycle;
            }
        }

        // --------------------------------------------------------
        // A real world needs both systems.
        // --------------------------------------------------------

        bool hasSeasonManager =
            IsInstanceValid(_seasonManager) &&
            _seasonManager.IsInsideTree();

        bool hasDayNight =
            IsInstanceValid(_dayNightCycle) &&
            _dayNightCycle.IsInsideTree();

        return hasSeasonManager && hasDayNight;
    }

    // ============================================================
    // TOGGLE
    // ============================================================

    public void Toggle()
    {
        // Never allow the calendar to open outside a world.
        if (!IsWorldActive())
        {
            _isOpen = false;
            Visible = false;
            return;
        }

        _isOpen = !_isOpen;
        Visible = _isOpen;

        GD.Print(
            $"[CalendarPanel] Calendar " +
            $"{(_isOpen ? "ON" : "OFF")}"
        );

        if (_isOpen)
        {
            UpdateDisplay();
        }
    }

    // ============================================================
    // SET OPEN
    // ============================================================

    public void SetOpen(bool open)
    {
        // Cannot open calendar outside a world.
        if (open && !IsWorldActive())
        {
            _isOpen = false;
            Visible = false;
            return;
        }

        _isOpen = open;
        Visible = _isOpen;

        if (_isOpen)
        {
            UpdateDisplay();
        }
    }

    // ============================================================
    // STATE
    // ============================================================

    public bool IsOpen()
    {
        return _isOpen;
    }

    // ============================================================
    // UPDATE DISPLAY
    // ============================================================

    private void UpdateDisplay()
    {
        // --------------------------------------------------------
        // Safety check.
        // --------------------------------------------------------

        if (!IsWorldActive())
        {
            _isOpen = false;
            Visible = false;
            ClearDisplay();
            return;
        }

        // --------------------------------------------------------
        // Season
        // --------------------------------------------------------

        if (IsInstanceValid(_seasonManager))
        {
            _yearLabel.Text =
                $"YEAR {_seasonManager.CurrentYear}";

            _seasonLabel.Text =
                $"{_seasonManager.GetSeasonName().ToUpper()}  " +
                $"—  DAY {_seasonManager.CurrentDay}/" +
                $"{_seasonManager.DaysPerSeason}";

            _moonLabel.Text =
                $"☾  {_seasonManager.GetMoonPhaseName()}";
        }
        else
        {
            ClearDisplay();
            return;
        }

        // --------------------------------------------------------
        // Time
        // --------------------------------------------------------

        if (IsInstanceValid(_dayNightCycle) &&
            _dayNightCycle.IsInsideTree())
        {
            _timeLabel.Text =
                _dayNightCycle.GetTimeString();
        }
        else
        {
            _timeLabel.Text = "--:--";
        }
    }

    // ============================================================
    // CLEAR DISPLAY
    // ============================================================

    private void ClearDisplay()
    {
        if (_yearLabel != null)
            _yearLabel.Text = "";

        if (_seasonLabel != null)
            _seasonLabel.Text = "";

        if (_timeLabel != null)
            _timeLabel.Text = "";

        if (_moonLabel != null)
            _moonLabel.Text = "";
    }

    // ============================================================
    // LABEL CREATION
    // ============================================================

    private Label CreateLabel(
        int fontSize,
        HorizontalAlignment alignment
    )
    {
        var label = new Label();

        label.HorizontalAlignment =
            alignment;

        label.AddThemeFontSizeOverride(
            "font_size",
            fontSize
        );

        label.SizeFlagsHorizontal =
            Control.SizeFlags.ExpandFill;

        return label;
    }
}