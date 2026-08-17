using Godot;

public partial class DayNightCycle : Node3D
{
    // ============================================================
    // SUN
    // ============================================================

    [Export]
    public DirectionalLight3D Sun { get; set; }

    // ============================================================
    // DAY LENGTH
    // ============================================================

    // One complete in-game day.
    // 15 real minutes = 900 seconds.
    [Export]
    public float DayDurationSeconds { get; set; } = 900f;

    // ============================================================
    // TIME
    // ============================================================

    // IMPORTANT:
    //
    // 0.00 = Sunrise
    // 0.25 = Noon
    // 0.50 = Sunset
    // 0.75 = Midnight
    // 1.00 = Next Sunrise
    //
    // Time is saved with the world.
    // New worlds begin at sunrise.

    private float _timeOfDay = 0.0f;

    private bool _timeLoaded = false;

    private float _saveTimer = 0f;

    private const float SaveIntervalSeconds = 10f;

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
    // SUN ENERGY
    // ============================================================

    private float _nightEnergy = 0.0f;

    private float _sunriseEnergy = 1.2f;

    private float _noonEnergy = 2.0f;

    private float _sunsetEnergy = 1.2f;

    // ============================================================
    // MOON ENERGY
    // ============================================================

    // Moonlight is intentionally very weak.
    private float _moonEnergy = 0.10f;

    // ============================================================
    // CELESTIAL OBJECTS
    // ============================================================

    private MeshInstance3D _sunMesh;

    private MeshInstance3D _moonMesh;

    private StandardMaterial3D _sunMat;

    private StandardMaterial3D _moonMat;

    private DirectionalLight3D _moonLight;

    private float _orbitRadius = 200f;

    // ============================================================
    // ENVIRONMENT
    // ============================================================

    private Godot.Environment _env;

    private ProceduralSkyMaterial _skyMaterial;

    // ============================================================
    // CURRENT CELESTIAL FADE
    // ============================================================

    private float _currentSunFade = 0f;

    private float _currentMoonFade = 0f;

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
        // SUN MESH
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

        AddChild(_sunMesh);

        // ========================================================
        // SUN LIGHT
        // ========================================================

        if (Sun != null)
        {
            Node oldParent = Sun.GetParent();

            if (oldParent != null)
                oldParent.RemoveChild(Sun);

            AddChild(Sun);

            Sun.LightEnergy = 0f;
        }

        // ========================================================
        // MOON MESH
        // ========================================================

        _moonMesh = new MeshInstance3D();

        var moonSphere = new SphereMesh();

        moonSphere.Radius = 5f;
        moonSphere.Height = 10f;

        _moonMesh.Mesh = moonSphere;

        _moonMat = new StandardMaterial3D();

        _moonMat.AlbedoColor =
            new Color(0.9f, 0.9f, 1.0f);

        _moonMat.EmissionEnabled = true;

        _moonMat.Emission =
            new Color(0.8f, 0.8f, 1.0f);

        _moonMat.EmissionEnergyMultiplier = 0.8f;

        _moonMat.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.Unshaded;

        _moonMat.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        _moonMesh.MaterialOverride = _moonMat;

        AddChild(_moonMesh);

        // ========================================================
        // MOON LIGHT
        // ========================================================

        _moonLight = new DirectionalLight3D();

        _moonLight.LightColor =
            new Color(0.55f, 0.65f, 1.0f);

        _moonLight.LightEnergy = 0f;

        _moonLight.ShadowEnabled = false;

        AddChild(_moonLight);

        // ========================================================
        // ENVIRONMENT
        // ========================================================

        // We create the environment here so the sky can be
        // controlled by this system.
        //
        // IMPORTANT:
        // We do NOT create a second WorldEnvironment node.
        // This prevents multiple environments from fighting each
        // other and helps eliminate the strange black sky orb.
        //
        // If another WorldEnvironment already exists in the world,
        // this environment should eventually be moved there.

        _env = new Godot.Environment();

        _env.BackgroundMode =
            Godot.Environment.BGMode.Sky;

        _env.Sky = new Sky();

        _skyMaterial =
            new ProceduralSkyMaterial();

        _env.Sky.SkyMaterial =
            _skyMaterial;

        _env.AmbientLightSource =
            Godot.Environment.AmbientSource.Sky;

        _env.AmbientLightEnergy = 0.015f;

        // ========================================================
        // LOAD SAVED TIME
        // ========================================================

        // Defer this until the scene tree/autoloads are fully ready.
        CallDeferred(nameof(LoadSavedTime));

        // Initial visual update.
        UpdateCelestialBodies();
        UpdateLightColor();
    }

    // ============================================================
    // LOAD SAVED TIME
    // ============================================================

    private void LoadSavedTime()
    {
        SaveManager saveManager =
            GetNodeOrNull<SaveManager>(
                "/root/SaveManager"
            );

        if (saveManager != null)
        {
            _timeOfDay =
                Mathf.PosMod(
                    saveManager.LoadWorldTime(),
                    1f
                );

            GD.Print(
                $"[DayNightCycle] Loaded time: " +
                $"{_timeOfDay:0.000} " +
                $"Game hour: {GetGameHour():0.00}"
            );
        }
        else
        {
            _timeOfDay = 0.0f;

            GD.Print(
                "[DayNightCycle] SaveManager not found. " +
                "Starting at sunrise."
            );
        }

        _timeLoaded = true;

        UpdateCelestialBodies();
        UpdateLightColor();
    }

    // ============================================================
    // EXIT TREE
    // ============================================================

    public override void _ExitTree()
    {
        SaveCurrentTime();
    }

    // ============================================================
    // PROCESS
    // ============================================================

    public override void _Process(double delta)
    {
        _frameCount++;

        float deltaSeconds =
            (float)delta;

        // Save the current time periodically.
        _saveTimer += deltaSeconds;

        if (_saveTimer >= SaveIntervalSeconds)
        {
            _saveTimer = 0f;

            SaveCurrentTime();
        }

        // Update the clock every third frame like before.
        if (_frameCount % 3 != 0)
            return;

        _timeOfDay +=
            deltaSeconds /
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
    // SAVE CURRENT TIME
    // ============================================================

    private void SaveCurrentTime()
    {
        if (!_timeLoaded)
            return;

        SaveManager saveManager =
            GetNodeOrNull<SaveManager>(
                "/root/SaveManager"
            );

        if (saveManager == null)
            return;

        saveManager.SaveWorldTime(
            _timeOfDay
        );
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

        // Immediately save debug changes.
        SaveCurrentTime();

        GD.Print(
            $"[DayNightCycle] Debug hour advanced. " +
            $"Time of day: {_timeOfDay:0.000} " +
            $"Game hour: {GetGameHour():0.00}"
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
    float gameHour = GetGameHour();

    int hour = Mathf.FloorToInt(gameHour);

    return hour % 24;
}

public int GetDisplayMinute()
{
    float gameHour = GetGameHour();

    float minute =
        (gameHour -
         Mathf.Floor(gameHour)) *
        60f;

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
        // SUN ORBIT
        //
        // 0.00 = Sunrise
        // 0.25 = Noon
        // 0.50 = Sunset
        // 0.75 = Midnight
        // 1.00 = Sunrise
        //
        // This is the single source of truth for the sun position.
        // ========================================================

        float sunAngle =
            _timeOfDay * Mathf.Tau;

        Vector3 sunPosition =
            new Vector3(
                Mathf.Cos(sunAngle) *
                    _orbitRadius,

                Mathf.Sin(sunAngle) *
                    _orbitRadius,

                0f
            );

        _sunMesh.GlobalPosition =
            GlobalPosition +
            sunPosition;

        // ========================================================
        // SUN HEIGHT
        // ========================================================

        float sunHeight =
            sunPosition.Y;

        // The sun fades in/out around the horizon.
        const float sunFadeRange = 20f;

        float sunFade =
            Mathf.Clamp(
                sunHeight /
                sunFadeRange,
                0f,
                1f
            );

        _currentSunFade =
            sunFade;

        // ========================================================
        // SUN LIGHT
        // ========================================================

        if (Sun != null)
        {
            // The directional light is placed at the same location
            // as the visible sun and points toward the world.
            Sun.GlobalPosition =
                _sunMesh.GlobalPosition;

            Sun.LookAt(
                GlobalPosition,
                Vector3.Up
            );
        }

        // ========================================================
        // MOON ORBIT
        //
        // Exactly opposite the sun.
        // ========================================================

        float moonAngle =
            sunAngle + Mathf.Pi;

        Vector3 moonPosition =
            new Vector3(
                Mathf.Cos(moonAngle) *
                    _orbitRadius,

                Mathf.Sin(moonAngle) *
                    _orbitRadius,

                0f
            );

        _moonMesh.GlobalPosition =
            GlobalPosition +
            moonPosition;

        // ========================================================
        // MOON HEIGHT
        // ========================================================

        float moonHeight =
            moonPosition.Y;

        const float moonFadeRange = 20f;

        float moonFade =
            Mathf.Clamp(
                moonHeight /
                moonFadeRange,
                0f,
                1f
            );

        _currentMoonFade =
            moonFade;

        // ========================================================
        // MOON LIGHT
        // ========================================================

        if (_moonLight != null)
        {
            _moonLight.GlobalPosition =
                _moonMesh.GlobalPosition;

            _moonLight.LookAt(
                GlobalPosition,
                Vector3.Up
            );
        }

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

        // ========================================================
        // VISIBILITY
        // ========================================================

        _sunMesh.Visible =
            sunFade > 0f;

        _moonMesh.Visible =
            moonFade > 0f;
    }

    // ============================================================
    // LIGHT / SKY
    // ============================================================

    private void UpdateLightColor()
    {
        float daylightSeconds =
            GetDaylightSeconds();

        float daylightFraction =
            daylightSeconds /
            DayDurationSeconds;

        // ========================================================
        // DAYLIGHT WINDOW
        //
        // Sunrise is ALWAYS 0.0.
        //
        // Example Spring:
        //
        // 0.000 = Sunrise
        // 0.333 = Noon
        // 0.667 = Sunset
        // 0.667 -> 1.000 = Night
        // ========================================================

        float daylightStart =
            0.0f;

        float daylightEnd =
            daylightFraction;

        // ========================================================
        // SUNRISE
        //
        // Sunrise begins slightly before the physical sun reaches
        // the horizon and finishes shortly after.
        //
        // Because this wraps around 1.0 -> 0.0, the helper methods
        // handle it correctly.
        // ========================================================

        float sunriseHalf =
            Mathf.Min(
                SunriseDurationSeconds,
                daylightSeconds * 0.15f
            )
            /
            DayDurationSeconds
            /
            2f;

        float sunriseStart =
            Mathf.PosMod(
                daylightStart -
                sunriseHalf,
                1f
            );

        float sunriseMid =
            Mathf.PosMod(
                daylightStart,
                1f
            );

        float sunriseEnd =
            Mathf.PosMod(
                daylightStart +
                sunriseHalf,
                1f
            );

        // ========================================================
        // SUNSET
        // ========================================================

        float sunsetHalf =
            Mathf.Min(
                SunsetDurationSeconds,
                daylightSeconds * 0.15f
            )
            /
            DayDurationSeconds
            /
            2f;

        float sunsetStart =
            Mathf.PosMod(
                daylightEnd -
                sunsetHalf,
                1f
            );

        float sunsetMid =
            Mathf.PosMod(
                daylightEnd,
                1f
            );

        float sunsetEnd =
            Mathf.PosMod(
                daylightEnd +
                sunsetHalf,
                1f
            );

        float t =
            _timeOfDay;

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
        // SUNLIGHT
        //
        // THIS IS THE IMPORTANT PART.
        //
        // The sun's physical height is the final authority.
        //
        // If the sun is below the horizon:
        //
        //     Sun.LightEnergy = 0
        //
        // No seasonal calculation can override that.
        // ========================================================

        float actualSunEnergy =
            energy *
            _currentSunFade;

        if (_currentSunFade <= 0.001f)
        {
            actualSunEnergy = 0f;
        }

        if (Sun != null)
        {
            Sun.LightColor =
                lightColor;

            Sun.LightEnergy =
                actualSunEnergy;

            Sun.ShadowEnabled =
                actualSunEnergy > 0.001f;
        }

        // ========================================================
        // MOONLIGHT
        //
        // Moonlight can ONLY exist while the moon is above the
        // horizon.
        // ========================================================

        if (_moonLight != null)
        {
            float actualMoonEnergy =
                _moonEnergy *
                _currentMoonFade;

            if (_currentMoonFade <= 0.001f)
            {
                actualMoonEnergy = 0f;
            }

            _moonLight.LightColor =
                new Color(
                    0.55f,
                    0.65f,
                    1.0f
                );

            _moonLight.LightEnergy =
                actualMoonEnergy;

            _moonLight.ShadowEnabled =
                actualMoonEnergy > 0.001f;
        }

        // ========================================================
        // AMBIENT ENVIRONMENT
        // ========================================================

        if (_env != null)
        {
            float ambientEnergy;

            if (_currentSunFade > 0.01f)
            {
                ambientEnergy =
                    Mathf.Lerp(
                        0.12f,
                        0.35f,
                        energy /
                        _noonEnergy
                    );
            }
            else if (_currentMoonFade > 0.01f)
            {
                ambientEnergy =
                    Mathf.Lerp(
                        0.025f,
                        0.06f,
                        _currentMoonFade
                    );
            }
            else
            {
                ambientEnergy =
                    0.015f;
            }

            _env.AmbientLightEnergy =
                ambientEnergy;
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
        float end)
    {
        if (start <= end)
        {
            return value >= start &&
                   value < end;
        }

        return value >= start ||
               value < end;
    }

    private float GetWrappedProgress(
        float value,
        float start,
        float end)
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
            position /
            range,
            0f,
            1f
        );
    }
}