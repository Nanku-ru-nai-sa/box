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

    // 0.00 = Midnight
    // 0.25 = Sunrise
    // 0.50 = Noon
    // 0.75 = Sunset
    // 1.00 = Midnight
    private float _timeOfDay = 0.5f;

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

    // Moonlight is intentionally much weaker than sunlight.
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
    // CURRENT LIGHT FADE
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
    public float SummerDaylightMinutes { get; set; } = 10.5f;

    [Export]
    public float AutumnDaylightMinutes { get; set; } = 9.5f;

    [Export]
    public float WinterDaylightMinutes { get; set; } = 9.0f;

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

        // If a DirectionalLight3D was assigned in the inspector,
        // detach it from its old location and place it directly
        // under this DayNightCycle.
        if (Sun != null)
        {
            Node oldParent = Sun.GetParent();

            if (oldParent != null)
            {
                oldParent.RemoveChild(Sun);
            }

            AddChild(Sun);
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

        _moonLight.LightEnergy = 0.0f;

        _moonLight.ShadowEnabled = true;

        AddChild(_moonLight);

        // ========================================================
        // ENVIRONMENT
        // ========================================================

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

        // Very low base ambient.
        // This prevents nighttime from becoming fake daylight.
        _env.AmbientLightEnergy = 0.03f;

        var worldEnv =
            new WorldEnvironment();

        worldEnv.Environment = _env;

        AddChild(worldEnv);

        // ========================================================
        // INITIAL UPDATE
        // ========================================================

        UpdateCelestialBodies();

        UpdateLightColor();
    }

    // ============================================================
    // PROCESS
    // ============================================================

    public override void _Process(double delta)
    {
        _frameCount++;

        if (_frameCount % 3 != 0)
            return;

        _timeOfDay +=
            (float)delta / DayDurationSeconds;

        if (_timeOfDay >= 1f)
        {
            _timeOfDay -= 1f;

            AdvanceCalendarDay();
        }

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
        float hourProgress =
            1f / 24f;

        _timeOfDay += hourProgress;

        if (_timeOfDay >= 1f)
        {
            _timeOfDay -= 1f;

            AdvanceCalendarDay();
        }

        UpdateCelestialBodies();

        UpdateLightColor();

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
                minutes = SpringDaylightMinutes;
                break;

            case SeasonManager.Season.Summer:
                minutes = SummerDaylightMinutes;
                break;

            case SeasonManager.Season.Autumn:
                minutes = AutumnDaylightMinutes;
                break;

            case SeasonManager.Season.Winter:
                minutes = WinterDaylightMinutes;
                break;

            default:
                minutes = SpringDaylightMinutes;
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
        // 0.00 = Midnight
        // 0.25 = Sunrise
        // 0.50 = Noon
        // 0.75 = Sunset
        // 1.00 = Midnight
        // ========================================================

        float sunAngle =
            _timeOfDay * Mathf.Tau -
            Mathf.Pi / 2f;

        Vector3 sunPosition =
            new Vector3(
                Mathf.Cos(sunAngle) * _orbitRadius,
                Mathf.Sin(sunAngle) * _orbitRadius,
                0f
            );

        // ========================================================
        // VISIBLE SUN
        // ========================================================

        _sunMesh.GlobalPosition =
            GlobalPosition + sunPosition;

        // ========================================================
        // SUN HEIGHT
        // ========================================================

        float sunHeight =
            sunPosition.Y;

        // Sun is visible above the horizon.
        float sunFadeRange = 20f;

        float sunFade =
            Mathf.Clamp(
                sunHeight / sunFadeRange,
                0f,
                1f
            );

        _currentSunFade =
            sunFade;

        // ========================================================
        // SUN LIGHT POSITION + DIRECTION
        // ========================================================

        if (Sun != null)
        {
            // Put the actual light at the EXACT same position
            // as the visible sun.
            Sun.GlobalPosition =
                _sunMesh.GlobalPosition;

            // DirectionalLight3D shines along its -Z axis.
            // Point it from the sun toward the world origin.
            Sun.LookAt(
                GlobalPosition,
                Vector3.Up
            );

            // Sunlight is physically impossible when the sun
            // is below the horizon.
            Sun.LightEnergy =
                Mathf.Max(
                    0f,
                    Sun.LightEnergy
                );
        }

        // ========================================================
        // MOON ORBIT
        // ========================================================

        float moonAngle =
            sunAngle + Mathf.Pi;

        Vector3 moonPosition =
            new Vector3(
                Mathf.Cos(moonAngle) * _orbitRadius,
                Mathf.Sin(moonAngle) * _orbitRadius,
                0f
            );

        // ========================================================
        // VISIBLE MOON
        // ========================================================

        _moonMesh.GlobalPosition =
            GlobalPosition + moonPosition;

        // ========================================================
        // MOON HEIGHT
        // ========================================================

        float moonHeight =
            moonPosition.Y;

        float moonFadeRange = 20f;

        float moonFade =
            Mathf.Clamp(
                moonHeight / moonFadeRange,
                0f,
                1f
            );

        _currentMoonFade =
            moonFade;

        // ========================================================
        // MOON LIGHT POSITION + DIRECTION
        // ========================================================

        if (_moonLight != null)
        {
            // EXACT same position as the visible moon.
            _moonLight.GlobalPosition =
                _moonMesh.GlobalPosition;

            // Point moonlight from moon toward world.
            _moonLight.LookAt(
                GlobalPosition,
                Vector3.Up
            );

            // Moonlight is controlled later in
            // UpdateLightColor().
            _moonLight.LightEnergy = 0f;
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
                2.0f * sunFade;
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
                0.8f * moonFade;
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

        float daylightFraction =
            daylightSeconds /
            DayDurationSeconds;

        float daylightStart =
            0.5f -
            daylightFraction / 2f;

        float daylightEnd =
            0.5f +
            daylightFraction / 2f;

        // ========================================================
        // SUNRISE
        // ========================================================

        float sunriseStart =
            daylightStart -
            (sunriseSeconds /
             DayDurationSeconds) / 2f;

        float sunriseMid =
            daylightStart;

        float sunriseEnd =
            daylightStart +
            (sunriseSeconds /
             DayDurationSeconds) / 2f;

        // ========================================================
        // SUNSET
        // ========================================================

        float sunsetStart =
            daylightEnd -
            (sunsetSeconds /
             DayDurationSeconds) / 2f;

        float sunsetMid =
            daylightEnd;

        float sunsetEnd =
            daylightEnd +
            (sunsetSeconds /
             DayDurationSeconds) / 2f;

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
        // IMPORTANT:
        //
        // The seasonal lighting calculation can NEVER create
        // sunlight if the physical sun is below the horizon.
        // ========================================================

        float actualSunEnergy =
            energy *
            _currentSunFade;

        if (Sun != null)
        {
            Sun.LightColor =
                lightColor;

            Sun.LightEnergy =
                actualSunEnergy;

            // No sunlight at all below the horizon.
            Sun.ShadowEnabled =
                actualSunEnergy > 0.001f;
        }

        // ========================================================
        // MOONLIGHT
        //
        // Moonlight only exists while the moon is physically
        // above the horizon.
        // ========================================================

        if (_moonLight != null)
        {
            _moonLight.LightColor =
                new Color(
                    0.55f,
                    0.65f,
                    1.0f
                );

            _moonLight.LightEnergy =
                _moonEnergy *
                _currentMoonFade;

            _moonLight.ShadowEnabled =
                _currentMoonFade > 0.001f;
        }

        // ========================================================
        // AMBIENT ENVIRONMENT
        // ========================================================

        if (_env != null)
        {
            // The environment itself provides only a tiny amount
            // of light at night.
            //
            // This prevents the world from appearing bright when
            // neither celestial body is providing light.

            float ambientEnergy;

            if (_currentSunFade > 0.01f)
            {
                ambientEnergy =
                    Mathf.Lerp(
                        0.12f,
                        0.35f,
                        energy / _noonEnergy
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
            position / range,
            0f,
            1f
        );
    }
}