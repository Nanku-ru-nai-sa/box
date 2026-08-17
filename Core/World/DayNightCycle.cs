using Godot;

public partial class DayNightCycle : Node3D
{
    [Export] public DirectionalLight3D Sun { get; set; }

    // ============================================================
    // DAY LENGTH
    // ============================================================

    // One complete in-game day.
    // 15 real minutes = 900 seconds.
    [Export]
    public float DayDurationSeconds { get; set; } = 900f;

    // 0.000 = Sunrise
    // 0.250 = Noon
    // 0.500 = Sunset
    // 0.750 = Midnight
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
        new Color(0.06f, 0.06f, 0.11f);

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
    // INTERNAL STATE
    // ============================================================

    private float _currentSunFade = 1f;

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
        // Put this node into a group so the debug menu
        // can find it.
        AddToGroup("day_night_cycle");

        // ========================================================
        // SUN
        // ========================================================

        _sunMesh = new MeshInstance3D();

        var sunSphere = new SphereMesh();

        sunSphere.Radius = 8f;
        sunSphere.Height = 16f;

        _sunMesh.Mesh = sunSphere;

        _sunMat = new StandardMaterial3D();

        _sunMat.AlbedoColor =
            new Color(1.0f, 0.9f, 0.2f);

        _sunMat.EmissionEnabled = true;

        _sunMat.Emission =
            new Color(1.0f, 0.8f, 0.1f);

        _sunMat.EmissionEnergyMultiplier = 2.0f;

        _sunMat.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.Unshaded;

        _sunMat.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        _sunMesh.MaterialOverride = _sunMat;

        // Attach the actual DirectionalLight3D to the sun.
        if (Sun != null)
        {
            Sun.GetParent()?.RemoveChild(Sun);

            _sunMesh.AddChild(Sun);

            Sun.Position =
                Vector3.Zero;

            Sun.RotationDegrees =
                new Vector3(
                    90f,
                    0f,
                    0f
                );
        }

        AddChild(_sunMesh);

        // ========================================================
        // MOON
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
                0.9f,
                0.9f,
                1.0f
            );

        _moonMat.EmissionEnabled =
            true;

        _moonMat.Emission =
            new Color(
                0.8f,
                0.8f,
                1.0f
            );

        _moonMat.EmissionEnergyMultiplier =
            0.8f;

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
            0.5f;

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
    // PROCESS
    // ============================================================

    public override void _Process(
        double delta
    )
    {
        _frameCount++;

        // Update every third frame.
        if (_frameCount % 3 != 0)
            return;

        _timeOfDay +=
            (float)delta /
            DayDurationSeconds;

        if (_timeOfDay >= 1f)
        {
            _timeOfDay -= 1f;

            AdvanceCalendarDay();
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
    }

    // ============================================================
    // SEASON CHANGE
    // ============================================================

    private void OnSeasonChanged(
        SeasonManager.Season newSeason,
        SeasonManager.Season oldSeason
    )
    {
        GD.Print(
            $"[DayNightCycle] Season changed: " +
            $"{oldSeason} -> {newSeason}"
        );

        // Immediately recalculate the lighting using
        // the new season's daylight duration.
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
        // One in-game hour is 1/24 of a complete day.
        float hourProgress =
            1f / 24f;

        _timeOfDay +=
            hourProgress;

        if (_timeOfDay >= 1f)
        {
            _timeOfDay -= 1f;

            AdvanceCalendarDay();
        }

        UpdateCelestialBodies();
        UpdateLightColor();

        GD.Print(
            $"[DayNightCycle] Debug hour advanced. " +
            $"Time of day: {_timeOfDay:0.000}"
        );
    }

    // ============================================================
    // CURRENT TIME
    // ============================================================

    public float GetTimeOfDay()
    {
        return _timeOfDay;
    }

    public float GetGameHour()
    {
        return _timeOfDay * 24f;
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

    public string GetTimeOfDayName()
    {
        float hour =
            GetGameHour();

        // Sunrise
        if (hour >= 5.0f &&
            hour < 7.0f)
        {
            return "SUNRISE";
        }

        // Morning
        if (hour >= 7.0f &&
            hour < 11.0f)
        {
            return "MORNING";
        }

        // Midday
        if (hour >= 11.0f &&
            hour < 14.0f)
        {
            return "MIDDAY";
        }

        // Afternoon
        if (hour >= 14.0f &&
            hour < 17.0f)
        {
            return "AFTERNOON";
        }

        // Sunset
        if (hour >= 17.0f &&
            hour < 20.0f)
        {
            return "SUNSET";
        }

        // Night
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
    // CELESTIAL BODIES
    // ============================================================

    private void UpdateCelestialBodies()
    {
        // ========================================================
        // SUN
        // ========================================================

        float sunAngle =
            _timeOfDay *
            Mathf.Tau -
            Mathf.Pi;

        _sunMesh.GlobalPosition =
            new Vector3(
                Mathf.Cos(sunAngle) *
                    _orbitRadius,

                Mathf.Sin(sunAngle) *
                    _orbitRadius,

                0f
            );

        _sunMesh.Rotation =
            new Vector3(
                0f,
                0f,
                sunAngle +
                    Mathf.Pi / 2f
            );

        // ========================================================
        // MOON
        // ========================================================

        // Moon is always opposite the sun.
        float moonAngle =
            sunAngle +
            Mathf.Pi;

        _moonMesh.GlobalPosition =
            new Vector3(
                Mathf.Cos(moonAngle) *
                    _orbitRadius,

                Mathf.Sin(moonAngle) *
                    _orbitRadius,

                0f
            );

        // ========================================================
        // SUN / MOON VISIBILITY
        // ========================================================

        float fadeRange =
            20f;

        float sunFade =
            Mathf.Clamp(
                _sunMesh.GlobalPosition.Y /
                    fadeRange,
                0f,
                1f
            );

        float moonFade =
            Mathf.Clamp(
                _moonMesh.GlobalPosition.Y /
                    fadeRange,
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
                0.8f *
                moonFade;
        }

        _sunMesh.Visible =
            sunFade > 0f;

        _moonMesh.Visible =
            moonFade > 0f;

        _currentSunFade =
            sunFade;
    }

    // ============================================================
    // LIGHT / SKY
    // ============================================================

    private void UpdateLightColor()
    {
        if (Sun == null)
            return;

        float daylightSeconds =
            GetDaylightSeconds();

        float sunriseSeconds =
            Mathf.Min(
                SunriseDurationSeconds,
                daylightSeconds * 0.15f
            );

        float sunsetSeconds =
            Mathf.Min(
                SunsetDurationSeconds,
                daylightSeconds * 0.15f
            );

        // ========================================================
        // DAYLIGHT WINDOW
        // ========================================================

        float daylightProgress =
            daylightSeconds /
            DayDurationSeconds;

        float daylightStart =
            0.5f -
            daylightProgress / 2f;

        float daylightEnd =
            0.5f +
            daylightProgress / 2f;

        // ========================================================
        // SUNRISE
        // ========================================================

        float sunriseStart =
            daylightStart -
            (
                sunriseSeconds /
                DayDurationSeconds
            ) / 2f;

        float sunriseMid =
            daylightStart;

        float sunriseEnd =
            daylightStart +
            (
                sunriseSeconds /
                DayDurationSeconds
            ) / 2f;

        // ========================================================
        // SUNSET
        // ========================================================

        float sunsetStart =
            daylightEnd -
            (
                sunsetSeconds /
                DayDurationSeconds
            ) / 2f;

        float sunsetMid =
            daylightEnd;

        float sunsetEnd =
            daylightEnd +
            (
                sunsetSeconds /
                DayDurationSeconds
            ) / 2f;

        // ========================================================
        // WRAP VALUES
        // ========================================================

        sunriseStart =
            Mathf.PosMod(
                sunriseStart,
                1f
            );

        sunriseMid =
            Mathf.PosMod(
                sunriseMid,
                1f
            );

        sunriseEnd =
            Mathf.PosMod(
                sunriseEnd,
                1f
            );

        sunsetStart =
            Mathf.PosMod(
                sunsetStart,
                1f
            );

        sunsetMid =
            Mathf.PosMod(
                sunsetMid,
                1f
            );

        sunsetEnd =
            Mathf.PosMod(
                sunsetEnd,
                1f
            );

        float t =
            _timeOfDay;

        // ========================================================
        // LIGHT VALUES
        // ========================================================

        Color lightColor;
        Color skyColor;

        float energy;

        // ========================================================
        // SUNRISE
        // ========================================================

        if (IsBetweenWrapped(
            t,
            sunriseStart,
            sunriseMid))
        {
            float lt =
                GetWrappedProgress(
                    t,
                    sunriseStart,
                    sunriseMid
                );

            lightColor =
                _nightColor.Lerp(
                    _sunriseColor,
                    lt
                );

            skyColor =
                _nightSky.Lerp(
                    _sunriseSky,
                    lt
                );

            energy =
                Mathf.Lerp(
                    _nightEnergy,
                    _sunriseEnergy,
                    lt
                );
        }

        // ========================================================
        // MORNING
        // ========================================================

        else if (IsBetweenWrapped(
            t,
            sunriseMid,
            sunriseEnd))
        {
            float lt =
                GetWrappedProgress(
                    t,
                    sunriseMid,
                    sunriseEnd
                );

            lightColor =
                _sunriseColor.Lerp(
                    _noonColor,
                    lt
                );

            skyColor =
                _sunriseSky.Lerp(
                    _noonSky,
                    lt
                );

            energy =
                Mathf.Lerp(
                    _sunriseEnergy,
                    _noonEnergy,
                    lt
                );
        }

        // ========================================================
        // DAY
        // ========================================================

        else if (IsBetweenWrapped(
            t,
            sunriseEnd,
            sunsetStart))
        {
            lightColor =
                _noonColor;

            skyColor =
                _noonSky;

            energy =
                _noonEnergy;
        }

        // ========================================================
        // SUNSET
        // ========================================================

        else if (IsBetweenWrapped(
            t,
            sunsetStart,
            sunsetMid))
        {
            float lt =
                GetWrappedProgress(
                    t,
                    sunsetStart,
                    sunsetMid
                );

            lightColor =
                _noonColor.Lerp(
                    _sunsetColor,
                    lt
                );

            skyColor =
                _noonSky.Lerp(
                    _sunsetSky,
                    lt
                );

            energy =
                Mathf.Lerp(
                    _noonEnergy,
                    _sunsetEnergy,
                    lt
                );
        }

        // ========================================================
        // EVENING
        // ========================================================

        else if (IsBetweenWrapped(
            t,
            sunsetMid,
            sunsetEnd))
        {
            float lt =
                GetWrappedProgress(
                    t,
                    sunsetMid,
                    sunsetEnd
                );

            lightColor =
                _sunsetColor.Lerp(
                    _nightColor,
                    lt
                );

            skyColor =
                _sunsetSky.Lerp(
                    _nightSky,
                    lt
                );

            energy =
                Mathf.Lerp(
                    _sunsetEnergy,
                    _nightEnergy,
                    lt
                );
        }

        // ========================================================
        // NIGHT
        // ========================================================

        else
        {
            lightColor =
                _nightColor;

            skyColor =
                _nightSky;

            energy =
                _nightEnergy;
        }

        // ========================================================
        // DIRECTIONAL SUNLIGHT
        // ========================================================

        Sun.LightColor =
            lightColor;

        Sun.LightEnergy =
            energy *
            _currentSunFade;

        // ========================================================
        // AMBIENT LIGHT
        // ========================================================

        if (_env != null)
        {
            _env.AmbientLightEnergy =
                Mathf.Lerp(
                    0.02f,
                    0.35f,
                    energy /
                    _noonEnergy
                );
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

    // ============================================================
    // WRAPPED TIME HELPERS
    // ============================================================

    private bool IsBetweenWrapped(
        float value,
        float start,
        float end
    )
    {
        if (start <= end)
        {
            return
                value >= start &&
                value < end;
        }

        return
            value >= start ||
            value < end;
    }

    private float GetWrappedProgress(
        float value,
        float start,
        float end
    )
    {
        float range =
            Mathf.PosMod(
                end - start,
                1f
            );

        if (range <= 0.0001f)
            return 0f;

        float position =
            Mathf.PosMod(
                value - start,
                1f
            );

        return Mathf.Clamp(
            position / range,
            0f,
            1f
        );
    }
}