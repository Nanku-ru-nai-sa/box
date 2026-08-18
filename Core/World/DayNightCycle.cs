using Godot;

public partial class DayNightCycle : Node3D
{
    [Export]
    public DirectionalLight3D Sun { get; set; }

    // ============================================================
    // DAY / NIGHT
    // ============================================================

    // Total real-time length of one complete in-game day.
    // 15 minutes = 900 seconds.
    [Export]
    public float DayDurationSeconds { get; set; } = 900f;

    // ============================================================
    // TIME OF DAY
    // ============================================================

    // IMPORTANT:
    //
    // _timeOfDay is NOT directly the displayed clock.
    //
    // It represents progress from:
    //
    // 0.000 = Sunrise
    // 1.000 = Next Sunrise
    //
    // The displayed clock is converted separately so that:
    //
    // Sunrise = 6:00 AM
    // Noon    = 12:00 PM
    // Sunset  = 6:00 PM
    // Midnight = 12:00 AM
    //
    // Seasonal daylight changes how quickly the clock moves
    // through the daytime and nighttime portions.

    private float _timeOfDay = 0.0f;

    // ============================================================
    // LIGHT COLORS
    // ============================================================

    private Color _nightColor =
        new Color(0.001f, 0.001f, 0.005f);

    private Color _sunriseColor =
        new Color(1.0f, 0.5f, 0.2f);

    private Color _noonColor =
        new Color(1.0f, 0.95f, 0.8f);

    private Color _sunsetColor =
        new Color(1.0f, 0.4f, 0.15f);

    // ============================================================
    // SKY COLORS
    // ============================================================

    private Color _nightSky =
        new Color(0.04f, 0.04f, 0.075f);

    private Color _sunriseSky =
        new Color(0.7f, 0.4f, 0.3f);

    private Color _noonSky =
        new Color(0.4f, 0.6f, 0.9f);

    private Color _sunsetSky =
        new Color(0.7f, 0.35f, 0.25f);

    // ============================================================
    // LIGHT ENERGY
    // ============================================================

    private float _nightEnergy = 0.0f;

    private float _sunriseEnergy = 1.2f;
    private float _noonEnergy = 2.0f;
    private float _sunsetEnergy = 1.2f;

    // ============================================================
    // CELESTIAL OBJECTS
    // ============================================================

    private MeshInstance3D _sunMesh;
    private MeshInstance3D _moonMesh;

    private StandardMaterial3D _sunMat;
    private StandardMaterial3D _moonMat;

    private float _orbitRadius = 200f;

    // ============================================================
    // ENVIRONMENT
    // ============================================================

    private Godot.Environment _env;
    private ProceduralSkyMaterial _skyMaterial;

    // ============================================================
    // DEBUG
    // ============================================================

    private int _frameCount = 0;

    // ============================================================
    // SEASONAL DAYLIGHT
    // ============================================================

    [ExportGroup("Seasonal Daylight")]

    [Export]
    public float SpringDaylightMinutes { get; set; } = 10.0f;

    [Export]
    public float SummerDaylightMinutes { get; set; } = 11.0f;

    [Export]
    public float AutumnDaylightMinutes { get; set; } = 9.0f;

    [Export]
    public float WinterDaylightMinutes { get; set; } = 8.0f;

    [Export]
    public float SunriseDurationSeconds { get; set; } = 60f;

    [Export]
    public float SunsetDurationSeconds { get; set; } = 60f;

    // ============================================================
    // READY
    // ============================================================

    public override void _Ready()
    {
        AddToGroup("day_night_cycle");

        // ========================================================
        // LOAD SAVED WORLD STATE
        // ========================================================

        LoadSavedWorldState();

        // ========================================================
        // SUN VISUAL
        // ========================================================

        _sunMesh =
            new MeshInstance3D();

        var sunSphere =
            new SphereMesh();

        sunSphere.Radius = 8f;
        sunSphere.Height = 16f;

        _sunMesh.Mesh =
            sunSphere;

        _sunMat =
            new StandardMaterial3D();

        _sunMat.AlbedoColor =
            new Color(1.0f, 0.9f, 0.2f);

        _sunMat.EmissionEnabled =
            true;

        _sunMat.Emission =
            new Color(1.0f, 0.8f, 0.1f);

        _sunMat.EmissionEnergyMultiplier =
            2.0f;

        _sunMat.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.Unshaded;

        _sunMat.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        _sunMesh.MaterialOverride =
            _sunMat;

        // ========================================================
        // ATTACH SUN LIGHT
        // ========================================================

        if (Sun != null)
        {
            Node parent =
                Sun.GetParent();

            if (parent != null)
                parent.RemoveChild(Sun);

            _sunMesh.AddChild(Sun);

            Sun.Position =
                Vector3.Zero;
        }

        AddChild(_sunMesh);

        // ========================================================
        // MOON VISUAL
        // ========================================================

        _moonMesh =
            new MeshInstance3D();

        var moonSphere =
            new SphereMesh();

        moonSphere.Radius = 5f;
        moonSphere.Height = 10f;

        _moonMesh.Mesh =
            moonSphere;

        _moonMat =
            new StandardMaterial3D();

        _moonMat.AlbedoColor =
            new Color(
                0.8f,
                0.8f,
                0.9f
            );

        // Moon is visual ONLY.
        // It never illuminates the world.

        _moonMat.EmissionEnabled =
            true;

        _moonMat.Emission =
            new Color(
                0.45f,
                0.45f,
                0.6f
            );

        _moonMat.EmissionEnergyMultiplier =
            0.15f;

        _moonMat.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.Unshaded;

        _moonMat.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        _moonMesh.MaterialOverride =
            _moonMat;

        AddChild(_moonMesh);

        // ========================================================
        // ENVIRONMENT
        // ========================================================

        _env =
            new Godot.Environment();

        _env.BackgroundMode =
            Godot.Environment.BGMode.Sky;

        _env.Sky =
            new Sky();

        _skyMaterial =
            new ProceduralSkyMaterial();

        _env.Sky.SkyMaterial =
            _skyMaterial;

        _env.AmbientLightSource =
            Godot.Environment.AmbientSource.Sky;

        _env.AmbientLightEnergy =
            0.035f;

        var worldEnv =
            new WorldEnvironment();

        worldEnv.Environment =
            _env;

        AddChild(worldEnv);

        // ========================================================
        // SEASON EVENTS
        // ========================================================

        SeasonManager seasonManager =
            GetNodeOrNull<SeasonManager>(
                "/root/SeasonManager"
            );

        if (seasonManager != null)
        {
            seasonManager.SeasonChanged +=
                OnSeasonChanged;
        }

        // ========================================================
        // INITIAL UPDATE
        // ========================================================

        UpdateCelestialBodies();
        UpdateLightColor();
    }

    // ============================================================
    // LOAD SAVED WORLD STATE
    // ============================================================

    private void LoadSavedWorldState()
    {
        if (SaveManager.Instance == null)
        {
            _timeOfDay = 0.0f;

            GD.Print(
                "[DayNightCycle] No SaveManager found. " +
                "Starting at sunrise."
            );

            return;
        }

        // --------------------------------------------------------
        // LOAD TIME
        // --------------------------------------------------------

        _timeOfDay =
            Mathf.PosMod(
                SaveManager.Instance.LoadWorldTime(),
                1.0f
            );

        GD.Print(
            $"[DayNightCycle] Starting at saved time: " +
            $"{_timeOfDay:0.000} ({GetTimeString()})"
        );

        // --------------------------------------------------------
        // LOAD CALENDAR
        // --------------------------------------------------------

        SeasonManager seasonManager =
            GetNodeOrNull<SeasonManager>(
                "/root/SeasonManager"
            );

        if (seasonManager != null)
        {
            bool loaded =
                SaveManager.Instance.LoadWorldSeason(
                    seasonManager
                );

            if (loaded)
            {
                GD.Print(
                    $"[DayNightCycle] Loaded calendar: " +
                    $"{seasonManager.GetCalendarString()}"
                );
            }
            else
            {
                GD.Print(
                    "[DayNightCycle] No saved calendar found. " +
                    "Using SeasonManager defaults."
                );
            }
        }
        else
        {
            GD.Print(
                "[DayNightCycle] SeasonManager not found. " +
                "Calendar state was not loaded."
            );
        }
    }

    // ============================================================
    // PROCESS
    // ============================================================

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount % 3 != 0)
            return;

        // ========================================================
        // TIME ADVANCEMENT
        // ========================================================

        _timeOfDay +=
            (float)delta /
            DayDurationSeconds;

        if (_timeOfDay >= 1f)
        {
            _timeOfDay -= 1f;

            AdvanceCalendarDay();

            SaveCurrentWorldState();
        }

        UpdateCelestialBodies();
        UpdateLightColor();
    }

    // ============================================================
    // EXIT TREE
    // ============================================================

    public override void _ExitTree()
    {
        SeasonManager seasonManager =
            GetNodeOrNull<SeasonManager>(
                "/root/SeasonManager"
            );

        if (seasonManager != null)
        {
            seasonManager.SeasonChanged -=
                OnSeasonChanged;
        }

        SaveCurrentWorldState();
    }

    // ============================================================
    // SEASON CHANGE
    // ============================================================

    private void OnSeasonChanged(
        SeasonManager.Season newSeason,
        SeasonManager.Season oldSeason)
    {
        GD.Print(
            $"[DayNightCycle] Season changed: " +
            $"{oldSeason} -> {newSeason}"
        );

        UpdateCelestialBodies();
        UpdateLightColor();
    }

    // ============================================================
    // ADVANCE CALENDAR DAY
    // ============================================================

    private void AdvanceCalendarDay()
    {
        if (Godot.Engine.IsEditorHint())
            return;

        SeasonManager seasonManager =
            GetNodeOrNull<SeasonManager>(
                "/root/SeasonManager"
            );

        if (seasonManager != null)
        {
            seasonManager.AdvanceDay();
        }
    }

    // ============================================================
    // DEBUG: ADVANCE ONE HOUR
    // ============================================================

    public void AdvanceDebugHour()
    {
        // Advance exactly one displayed game hour.
        //
        // We convert the current clock to a game hour,
        // add one hour, then convert it back into our
        // sunrise-based normalized clock.

        float currentHour =
            GetGameHour();

        currentHour += 1f;

        if (currentHour >= 30f)
            currentHour -= 24f;

        _timeOfDay =
            GameHourToNormalizedTime(
                currentHour
            );

        // Sunrise = beginning of the calendar day.
        if (Mathf.IsZeroApprox(_timeOfDay))
        {
            AdvanceCalendarDay();
        }

        UpdateCelestialBodies();
        UpdateLightColor();

        SaveCurrentWorldState();

        GD.Print(
            $"[DayNightCycle] Debug hour advanced. " +
            $"Time of day: {_timeOfDay:0.000} " +
            $"({GetTimeString()})"
        );
    }

    // ============================================================
    // SAVE WORLD STATE
    // ============================================================

    private void SaveCurrentWorldState()
    {
        if (SaveManager.Instance == null)
            return;

        SaveManager.Instance.SaveWorldTime(
            _timeOfDay
        );

        SeasonManager seasonManager =
            GetNodeOrNull<SeasonManager>(
                "/root/SeasonManager"
            );

        if (seasonManager != null)
        {
            SaveManager.Instance.SaveWorldSeason(
                seasonManager
            );
        }
    }

    // ============================================================
    // SAVE TIME ONLY
    // ============================================================

    private void SaveCurrentWorldTime()
    {
        if (SaveManager.Instance == null)
            return;

        SaveManager.Instance.SaveWorldTime(
            _timeOfDay
        );
    }

    // ============================================================
    // CURRENT TIME
    // ============================================================

    public float GetTimeOfDay()
    {
        return _timeOfDay;
    }

    // ============================================================
    // CONVERT NORMALIZED TIME -> GAME CLOCK
    // ============================================================

    public float GetGameHour()
    {
        float daylightProgress =
            GetDaylightProgress();

        // --------------------------------------------------------
        // DAYTIME
        // --------------------------------------------------------
        //
        // 0.0 normalized = 6 AM
        // daylightProgress = 6 PM
        //
        // Therefore:
        //
        // normalized 0.0 -> 6
        // normalized daylightProgress -> 18

        if (_timeOfDay < daylightProgress)
        {
            float daylightPosition =
                _timeOfDay /
                Mathf.Max(
                    daylightProgress,
                    0.0001f
                );

            return Mathf.Lerp(
                6f,
                18f,
                daylightPosition
            );
        }

        // --------------------------------------------------------
        // NIGHTTIME
        // --------------------------------------------------------
        //
        // daylightProgress = 6 PM
        // 1.0 = next 6 AM
        //
        // Therefore:
        //
        // sunset -> 18
        // midnight -> 24
        // next sunrise -> 30
        //
        // We allow 24-30 internally because the calendar day
        // begins at sunrise.

        float nightProgress =
            (
                _timeOfDay -
                daylightProgress
            ) /
            Mathf.Max(
                1f -
                daylightProgress,
                0.0001f
            );

        return Mathf.Lerp(
            18f,
            30f,
            nightProgress
        );
    }

    // ============================================================
    // CONVERT GAME CLOCK -> NORMALIZED TIME
    // ============================================================

    private float GameHourToNormalizedTime(
        float gameHour)
    {
        float daylightProgress =
            GetDaylightProgress();

        // --------------------------------------------------------
        // 6 AM -> 6 PM
        // --------------------------------------------------------

        if (gameHour >= 6f &&
            gameHour < 18f)
        {
            float daylightPosition =
                (
                    gameHour -
                    6f
                ) / 12f;

            return
                daylightPosition *
                daylightProgress;
        }

        // --------------------------------------------------------
        // 6 PM -> NEXT 6 AM
        // --------------------------------------------------------

        float normalizedNightHour;

        if (gameHour >= 18f)
        {
            normalizedNightHour =
                gameHour - 18f;
        }
        else
        {
            // 12 AM through 6 AM
            normalizedNightHour =
                gameHour + 6f;
        }

        float nightProgress =
            normalizedNightHour /
            12f;

        return
            daylightProgress +
            (
                nightProgress *
                (1f - daylightProgress)
            );
    }

    // ============================================================
    // DISPLAY TIME
    // ============================================================

    public int GetDisplayHour()
    {
        float gameHour =
            GetGameHour();

        int hour =
            Mathf.FloorToInt(
                gameHour
            );

        // Convert internal 24-30 range into normal 0-23 clock.

        return hour % 24;
    }

    public int GetDisplayMinute()
    {
        float gameHour =
            GetGameHour();

        float minute =
            (
                gameHour -
                Mathf.Floor(gameHour)
            ) * 60f;

        return Mathf.Clamp(
            Mathf.FloorToInt(minute),
            0,
            59
        );
    }

    public string GetTimeString()
    {
        int hour =
            GetDisplayHour();

        int minute =
            GetDisplayMinute();

        string period =
            hour >= 12
                ? "PM"
                : "AM";

        int displayHour =
            hour % 12;

        if (displayHour == 0)
            displayHour = 12;

        return
            $"{displayHour:00}:{minute:00} {period}";
    }

    // ============================================================
    // TIME OF DAY NAME
    // ============================================================

    public string GetTimeOfDayName()
    {
        float hour =
            GetGameHour();

        // Convert 24-30 internal range into the normal
        // 0-6 AM range for comparisons.

        if (hour >= 24f)
            hour -= 24f;

        if (hour >= 5f &&
            hour < 8f)
        {
            return "SUNRISE";
        }

        if (hour >= 8f &&
            hour < 12f)
        {
            return "MORNING";
        }

        if (hour >= 12f &&
            hour < 17f)
        {
            return "AFTERNOON";
        }

        if (hour >= 17f &&
            hour < 20f)
        {
            return "SUNSET";
        }

        return "NIGHT";
    }

    // ============================================================
    // CURRENT SEASON
    // ============================================================

    private SeasonManager.Season GetCurrentSeason()
    {
        SeasonManager seasonManager =
            GetNodeOrNull<SeasonManager>(
                "/root/SeasonManager"
            );

        if (seasonManager == null)
            return SeasonManager.Season.Spring;

        return seasonManager.CurrentSeason;
    }

    // ============================================================
    // DAYLIGHT LENGTH
    // ============================================================

    private float GetDaylightSeconds()
    {
        float minutes;

        switch (GetCurrentSeason())
        {
            case SeasonManager.Season.Spring:
                minutes =
                    SpringDaylightMinutes;
                break;

            case SeasonManager.Season.Summer:
                minutes =
                    SummerDaylightMinutes;
                break;

            case SeasonManager.Season.Autumn:
                minutes =
                    AutumnDaylightMinutes;
                break;

            case SeasonManager.Season.Winter:
                minutes =
                    WinterDaylightMinutes;
                break;

            default:
                minutes =
                    SpringDaylightMinutes;
                break;
        }

        return Mathf.Clamp(
            minutes * 60f,
            60f,
            DayDurationSeconds - 60f
        );
    }

    // ============================================================
    // NIGHT LENGTH
    // ============================================================

    private float GetNightSeconds()
    {
        return Mathf.Max(
            60f,
            DayDurationSeconds -
            GetDaylightSeconds()
        );
    }

    // ============================================================
    // NORMALIZED DAYLIGHT LENGTH
    // ============================================================

    private float GetDaylightProgress()
    {
        return Mathf.Clamp(
            GetDaylightSeconds() /
            DayDurationSeconds,
            0.01f,
            0.99f
        );
    }

    // ============================================================
    // IS SUN UP
    // ============================================================

    private bool IsSunUp()
    {
        float daylightProgress =
            GetDaylightProgress();

        return
            _timeOfDay >= 0f &&
            _timeOfDay < daylightProgress;
    }

    // ============================================================
    // CELESTIAL BODIES
    // ============================================================

    private void UpdateCelestialBodies()
    {
        float daylightProgress =
            GetDaylightProgress();

        bool sunUp =
            IsSunUp();

        // ========================================================
        // SUN
        // ========================================================

        Vector3 sunPosition;

        if (sunUp)
        {
            float daylightPosition =
                _timeOfDay /
                Mathf.Max(
                    daylightProgress,
                    0.0001f
                );

            float sunAngle =
                Mathf.Pi -
                (
                    daylightPosition *
                    Mathf.Pi
                );

            sunPosition =
                new Vector3(
                    Mathf.Cos(sunAngle) *
                        _orbitRadius,

                    Mathf.Sin(sunAngle) *
                        _orbitRadius,

                    0f
                );
        }
        else
        {
            float nightProgress =
                (
                    _timeOfDay -
                    daylightProgress
                ) /
                Mathf.Max(
                    1f -
                    daylightProgress,
                    0.0001f
                );

            nightProgress =
                Mathf.Clamp(
                    nightProgress,
                    0f,
                    1f
                );

            float nightAngle =
                Mathf.Lerp(
                    0f,
                    Mathf.Pi,
                    nightProgress
                );

            sunPosition =
                new Vector3(
                    Mathf.Cos(nightAngle) *
                        _orbitRadius,

                    -Mathf.Abs(
                        Mathf.Sin(nightAngle) *
                        _orbitRadius
                    ) - 1f,

                    0f
                );
        }

        _sunMesh.GlobalPosition =
            sunPosition;

        // ========================================================
        // SUN LIGHT ROTATION
        // ========================================================

        if (Sun != null)
        {
            Vector3 direction =
                (
                    Vector3.Zero -
                    _sunMesh.GlobalPosition
                ).Normalized();

            Vector3 up =
                Vector3.Forward;

            if (Mathf.Abs(
                direction.Dot(up)
            ) > 0.98f)
            {
                up =
                    Vector3.Right;
            }

            Sun.LookAt(
                _sunMesh.GlobalPosition +
                direction,
                up
            );
        }

        // ========================================================
        // MOON
        // ========================================================

        _moonMesh.GlobalPosition =
            -_sunMesh.GlobalPosition;

        // ========================================================
        // VISIBILITY
        // ========================================================

        float sunHeight =
            _sunMesh.GlobalPosition.Y /
            _orbitRadius;

        float moonHeight =
            _moonMesh.GlobalPosition.Y /
            _orbitRadius;

        float sunFade =
            Mathf.Clamp(
                sunHeight * 10f,
                0f,
                1f
            );

        float moonFade =
            Mathf.Clamp(
                moonHeight * 10f,
                0f,
                1f
            );

        // ========================================================
        // SUN MATERIAL
        // ========================================================

        if (_sunMat != null)
        {
            Color c =
                _sunMat.AlbedoColor;

            c.A =
                sunFade;

            _sunMat.AlbedoColor =
                c;

            _sunMat.EmissionEnergyMultiplier =
                2.0f *
                sunFade;
        }

        // ========================================================
        // MOON MATERIAL
        // ========================================================

        if (_moonMat != null)
        {
            Color c =
                _moonMat.AlbedoColor;

            c.A =
                moonFade;

            _moonMat.AlbedoColor =
                c;

            _moonMat.EmissionEnergyMultiplier =
                0.15f *
                moonFade;
        }

        _sunMesh.Visible =
            sunFade > 0.001f;

        _moonMesh.Visible =
            moonFade > 0.001f;
    }

    // ============================================================
    // LIGHT / SKY
    // ============================================================

    private void UpdateLightColor()
    {
        if (Sun == null)
            return;

        bool sunUp =
            IsSunUp();

        float daylightProgress =
            GetDaylightProgress();

        float daylightPosition =
            sunUp
                ? _timeOfDay /
                  Mathf.Max(
                      daylightProgress,
                      0.0001f
                  )
                : 0f;

        Color lightColor;
        Color skyColor;
        float energy;

        // ========================================================
        // NIGHT
        // ========================================================

        if (!sunUp)
        {
            lightColor =
                _nightColor;

            skyColor =
                _nightSky;

            energy =
                0f;
        }

        // ========================================================
        // DAY
        // ========================================================

        else
        {
            float daylightSeconds =
                GetDaylightSeconds();

            float sunriseLength =
                SunriseDurationSeconds /
                daylightSeconds;

            float sunsetLength =
                SunsetDurationSeconds /
                daylightSeconds;

            sunriseLength =
                Mathf.Clamp(
                    sunriseLength,
                    0.01f,
                    0.25f
                );

            sunsetLength =
                Mathf.Clamp(
                    sunsetLength,
                    0.01f,
                    0.25f
                );

            // ====================================================
            // SUNRISE
            // ====================================================

            if (daylightPosition <
                sunriseLength)
            {
                float t =
                    daylightPosition /
                    sunriseLength;

                lightColor =
                    _nightColor.Lerp(
                        _sunriseColor,
                        t
                    );

                skyColor =
                    _nightSky.Lerp(
                        _sunriseSky,
                        t
                    );

                energy =
                    Mathf.Lerp(
                        _nightEnergy,
                        _sunriseEnergy,
                        t
                    );
            }

            // ====================================================
            // SUNSET
            // ====================================================

            else if (
                daylightPosition >
                1f - sunsetLength)
            {
                float t =
                    (
                        daylightPosition -
                        (1f - sunsetLength)
                    ) /
                    sunsetLength;

                lightColor =
                    _noonColor.Lerp(
                        _sunsetColor,
                        t
                    );

                skyColor =
                    _noonSky.Lerp(
                        _sunsetSky,
                        t
                    );

                energy =
                    Mathf.Lerp(
                        _noonEnergy,
                        _sunsetEnergy,
                        t
                    );
            }

            // ====================================================
            // NORMAL DAY
            // ====================================================

            else
            {
                float noonDistance =
                    Mathf.Abs(
                        daylightPosition -
                        0.5f
                    ) * 2f;

                float noonFactor =
                    1f -
                    Mathf.Clamp(
                        noonDistance,
                        0f,
                        1f
                    );

                lightColor =
                    _sunriseColor.Lerp(
                        _noonColor,
                        noonFactor
                    );

                skyColor =
                    _sunriseSky.Lerp(
                        _noonSky,
                        noonFactor
                    );

                energy =
                    Mathf.Lerp(
                        _sunriseEnergy,
                        _noonEnergy,
                        noonFactor
                    );
            }
        }

        // ========================================================
        // FINAL SUNLIGHT SAFETY
        // ========================================================

        if (!sunUp)
        {
            energy =
                0f;

            lightColor =
                _nightColor;
        }

        Sun.LightColor =
            lightColor;

        Sun.LightEnergy =
            energy;

        // ========================================================
        // AMBIENT
        // ========================================================

        if (_env != null)
        {
            if (!sunUp)
            {
                _env.AmbientLightEnergy =
                    0.015f;
            }
            else
            {
                float ambientFactor =
                    Mathf.Clamp(
                        energy /
                        _noonEnergy,
                        0f,
                        1f
                    );

                _env.AmbientLightEnergy =
                    Mathf.Lerp(
                        0.02f,
                        0.35f,
                        ambientFactor
                    );
            }
        }

        // ========================================================
        // SKY
        // ========================================================

        if (_skyMaterial != null)
        {
            _skyMaterial.SkyTopColor =
                skyColor;

            _skyMaterial.SkyHorizonColor =
                skyColor;

            _skyMaterial.GroundBottomColor =
                skyColor;

            _skyMaterial.GroundHorizonColor =
                skyColor;
        }
    }
}