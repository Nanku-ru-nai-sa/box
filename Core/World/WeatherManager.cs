using Godot;
using System;

/// <summary>
/// WeatherManager
///
/// Autoload this script as:
///     WeatherManager
///
/// Handles:
///     - Clear weather
///     - Rain
///     - Snow
///     - Sandstorms
///     - Random thunderstorms during rain
///     - Lightning flashes
///
/// Weather only runs while an actual world/player is active.
/// The weather effects follow the player but are positioned well
/// above them so rain falls from the sky instead of appearing
/// around the player's head.
/// </summary>
public partial class WeatherManager : Node
{
    // ============================================================
    // WEATHER TYPES
    // ============================================================

    public enum WeatherType
    {
        Clear,
        Rain,
        Snow,
        Sandstorm
    }

    // ============================================================
    // PUBLIC STATE
    // ============================================================

    public static WeatherManager Instance { get; private set; }

    public WeatherType CurrentWeather { get; private set; }
        = WeatherType.Clear;

    public bool IsRaining =>
        CurrentWeather == WeatherType.Rain;

    public bool IsSnowing =>
        CurrentWeather == WeatherType.Snow;

    public bool IsSandstorm =>
        CurrentWeather == WeatherType.Sandstorm;

    public bool IsThunderstorm =>
        CurrentWeather == WeatherType.Rain &&
        _thunderstorm;

    public event Action<WeatherType, WeatherType> WeatherChanged;

    public event Action LightningStrike;

    // ============================================================
    // WEATHER DURATION
    // ============================================================

    [ExportGroup("Weather Duration")]

    [Export]
    public float MinimumWeatherHours { get; set; } = 2.0f;

    [Export]
    public float MaximumWeatherHours { get; set; } = 6.0f;

    // How long clear weather must last after bad weather.
    [Export]
    public float MinimumClearHours { get; set; } = 24.0f;

    [Export]
    public float MaximumClearHours { get; set; } = 72.0f;

    // ============================================================
    // WEATHER CHANCES
    // ============================================================

    [ExportGroup("Weather Chances")]

    [Export]
    public float SpringRainChance { get; set; } = 0.45f;

    [Export]
    public float SummerRainChance { get; set; } = 0.15f;

    [Export]
    public float SummerSandstormChance { get; set; } = 0.10f;

    [Export]
    public float AutumnRainChance { get; set; } = 0.40f;

    [Export]
    public float AutumnSandstormChance { get; set; } = 0.05f;

    [Export]
    public float WinterSnowChance { get; set; } = 0.35f;

    // ============================================================
    // THUNDER
    // ============================================================

    [ExportGroup("Thunder")]

    [Export]
    public float ThunderstormChance { get; set; } = 0.15f;

    [Export]
    public float MinimumLightningSeconds { get; set; } = 12.0f;

    [Export]
    public float MaximumLightningSeconds { get; set; } = 35.0f;

    // ============================================================
    // RAIN
    // ============================================================

    [ExportGroup("Rain")]

    [Export]
    public int RainParticleAmount { get; set; } = 1400;

    [Export]
    public float RainParticleLifetime { get; set; } = 1.2f;

    [Export]
    public float RainSpeed { get; set; } = 32f;

    // How far above the player the rain emitter sits.
    [Export]
    public float RainHeightAbovePlayer { get; set; } = 30f;

    // ============================================================
    // SNOW
    // ============================================================

    [ExportGroup("Snow")]

    [Export]
    public int SnowParticleAmount { get; set; } = 700;

    [Export]
    public float SnowParticleLifetime { get; set; } = 5.0f;

    [Export]
    public float SnowSpeed { get; set; } = 3.0f;

    [Export]
    public float SnowHeightAbovePlayer { get; set; } = 20f;

    // ============================================================
    // SANDSTORM
    // ============================================================

    [ExportGroup("Sandstorm")]

    [Export]
    public int SandstormParticleAmount { get; set; } = 1200;

    [Export]
    public float SandstormParticleLifetime { get; set; } = 2.5f;

    [Export]
    public float SandstormSpeed { get; set; } = 12f;

    [Export]
    public float SandstormHeightAbovePlayer { get; set; } = 8f;

    // ============================================================
    // INTERNAL STATE
    // ============================================================

    private RandomNumberGenerator _rng;

    private SeasonManager _seasonManager;
    private DayNightCycle _dayNightCycle;

    private Node3D _player;

    private Node3D _weatherRoot;

    private GpuParticles3D _rainParticles;
    private GpuParticles3D _snowParticles;
    private GpuParticles3D _sandParticles;

    private OmniLight3D _lightningLight;

    private float _weatherRemainingSeconds = 0f;

    private bool _thunderstorm = false;

    private float _nextLightningSeconds = -1f;

    private bool _lightningActive = false;

    private float _lightningRemainingSeconds = 0f;

    private bool _worldActive = false;

    // ============================================================
    // READY
    // ============================================================

    public override void _Ready()
    {
        Instance = this;

        _rng = new RandomNumberGenerator();
        _rng.Randomize();

        AddToGroup("weather_manager");

        GD.Print(
            "[WeatherManager] Weather system ready. " +
            "Waiting for world..."
        );

        FindManagers();

        BuildWeatherRoot();
        BuildRain();
        BuildSnow();
        BuildSandstorm();
        BuildLightning();

        DisableAllWeather();

        CurrentWeather =
            WeatherType.Clear;

        _weatherRemainingSeconds = 0f;
    }

    // ============================================================
    // PROCESS
    // ============================================================

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        FindPlayer();

        // --------------------------------------------------------
        // NO WORLD / NO PLAYER
        // --------------------------------------------------------

        if (!IsWorldActive())
        {
            if (_worldActive)
            {
                LeaveWorld();
            }

            return;
        }

        // --------------------------------------------------------
        // WORLD JUST STARTED
        // --------------------------------------------------------

        if (!_worldActive)
        {
            EnterWorld();
        }

        // --------------------------------------------------------
        // FOLLOW PLAYER
        // --------------------------------------------------------

        FollowPlayer();

        // --------------------------------------------------------
        // WEATHER TIMER
        // --------------------------------------------------------

        if (_weatherRemainingSeconds > 0f)
        {
            _weatherRemainingSeconds -= dt;
        }
        else
        {
            ChooseNextWeather();
        }

        // --------------------------------------------------------
        // LIGHTNING
        // --------------------------------------------------------

        if (_thunderstorm &&
            CurrentWeather == WeatherType.Rain)
        {
            UpdateLightning(dt);
        }

        // --------------------------------------------------------
        // LIGHTNING FLASH
        // --------------------------------------------------------

        if (_lightningActive)
        {
            _lightningRemainingSeconds -= dt;

            if (_lightningRemainingSeconds <= 0f)
            {
                EndLightning();
            }
        }
    }

    // ============================================================
    // WORLD DETECTION
    // ============================================================

    private bool IsWorldActive()
    {
        FindPlayer();

        if (!IsInstanceValid(_player))
            return false;

        if (!_player.IsInsideTree())
            return false;

        return true;
    }

    private void EnterWorld()
    {
        _worldActive = true;

        FindManagers();

        CurrentWeather =
            WeatherType.Clear;

        DisableAllWeather();

        // Start with a guaranteed clear period.
        float clearHours =
            _rng.RandfRange(
                0.5f,
                1.5f
            );

        _weatherRemainingSeconds =
            GameHoursToRealSeconds(
                clearHours
            );

        GD.Print(
            $"[WeatherManager] World entered. " +
            $"Initial clear period: " +
            $"{clearHours:0.0} in-game hours."
        );
    }

    private void LeaveWorld()
    {
        _worldActive = false;

        DisableAllWeather();

        _thunderstorm = false;

        _nextLightningSeconds = -1f;

        _lightningActive = false;

        _weatherRemainingSeconds = 0f;

        CurrentWeather =
            WeatherType.Clear;

        GD.Print(
            "[WeatherManager] World left. " +
            "Weather disabled."
        );
    }

    // ============================================================
    // FIND MANAGERS
    // ============================================================

    private void FindManagers()
    {
        if (!IsInstanceValid(_seasonManager))
        {
            _seasonManager =
                GetNodeOrNull<SeasonManager>(
                    "/root/SeasonManager"
                );
        }

        if (!IsInstanceValid(_dayNightCycle))
        {
            _dayNightCycle =
                GetTree()
                    .GetFirstNodeInGroup(
                        "day_night_cycle"
                    ) as DayNightCycle;
        }
    }

    // ============================================================
    // FIND PLAYER
    // ============================================================

    private void FindPlayer()
    {
        if (IsInstanceValid(_player) &&
            _player.IsInsideTree())
        {
            return;
        }

        _player = null;

        Node found =
            GetTree()
                .GetFirstNodeInGroup(
                    "player"
                );

        if (found is Node3D node &&
            IsInstanceValid(node) &&
            node.IsInsideTree())
        {
            _player = node;

            GD.Print(
                "[WeatherManager] Player found."
            );
        }
    }

    // ============================================================
    // WEATHER ROOT
    // ============================================================

    private void BuildWeatherRoot()
    {
        _weatherRoot =
            new Node3D();

        _weatherRoot.Name =
            "WeatherEffects";

        AddChild(
            _weatherRoot
        );
    }

    // ============================================================
    // RAIN
    // ============================================================

    private void BuildRain()
    {
        _rainParticles =
            new GpuParticles3D();

        _rainParticles.Name =
            "RainParticles";

        _rainParticles.Amount =
            RainParticleAmount;

        _rainParticles.Lifetime =
            RainParticleLifetime;

        _rainParticles.Emitting =
            false;

        _rainParticles.LocalCoords =
            false;

        _rainParticles.VisibilityAabb =
            new Aabb(
                new Vector3(
                    -45f,
                    -40f,
                    -45f
                ),
                new Vector3(
                    90f,
                    80f,
                    90f
                )
            );

        // --------------------------------------------------------
        // Rain mesh
        // --------------------------------------------------------

        var rainMesh =
            new QuadMesh();

        rainMesh.Size =
            new Vector2(
                0.045f,
                1.15f
            );

        var rainMaterial =
            new StandardMaterial3D();

        rainMaterial.AlbedoColor =
            new Color(
                0.55f,
                0.72f,
                1.0f,
                0.72f
            );

        // IMPORTANT:
        // Rain is now affected by the world's lighting.
        // It will no longer glow brightly at night.
        rainMaterial.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.PerPixel;

        rainMaterial.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        rainMaterial.CullMode =
            BaseMaterial3D.CullModeEnum.Disabled;

        // No emission.
        rainMaterial.EmissionEnabled = false;

        rainMesh.Material =
            rainMaterial;

        _rainParticles.DrawPass1 =
            rainMesh;

        // --------------------------------------------------------
        // Rain behavior
        // --------------------------------------------------------

        var material =
            new ParticleProcessMaterial();

        material.EmissionShape =
            ParticleProcessMaterial.EmissionShapeEnum.Box;

        material.EmissionBoxExtents =
            new Vector3(
                38f,
                3f,
                38f
            );

        material.Direction =
            new Vector3(
                0f,
                -1f,
                0f
            );

        material.Spread =
            4f;

        material.InitialVelocityMin =
            RainSpeed * 0.85f;

        material.InitialVelocityMax =
            RainSpeed * 1.15f;

        material.Gravity =
            new Vector3(
                0f,
                -8f,
                0f
            );

        material.Color =
            new Color(
                0.65f,
                0.78f,
                1.0f,
                0.75f
            );

        _rainParticles.ProcessMaterial =
            material;

        _weatherRoot.AddChild(
            _rainParticles
        );
    }

    // ============================================================
    // SNOW
    // ============================================================

    private void BuildSnow()
    {
        _snowParticles =
            new GpuParticles3D();

        _snowParticles.Name =
            "SnowParticles";

        _snowParticles.Amount =
            SnowParticleAmount;

        _snowParticles.Lifetime =
            SnowParticleLifetime;

        _snowParticles.Emitting =
            false;

        _snowParticles.LocalCoords =
            false;

        _snowParticles.VisibilityAabb =
            new Aabb(
                new Vector3(
                    -45f,
                    -30f,
                    -45f
                ),
                new Vector3(
                    90f,
                    60f,
                    90f
                )
            );

        var snowMesh =
            new QuadMesh();

        snowMesh.Size =
            new Vector2(
                0.16f,
                0.16f
            );

        var snowMaterial =
            new StandardMaterial3D();

        snowMaterial.AlbedoColor =
            new Color(
                0.95f,
                0.97f,
                1.0f,
                0.9f
            );

        snowMaterial.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.PerPixel;

        snowMaterial.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        snowMaterial.CullMode =
            BaseMaterial3D.CullModeEnum.Disabled;

        snowMaterial.EmissionEnabled =
            false;

        snowMesh.Material =
            snowMaterial;

        _snowParticles.DrawPass1 =
            snowMesh;

        var material =
            new ParticleProcessMaterial();

        material.EmissionShape =
            ParticleProcessMaterial.EmissionShapeEnum.Box;

        material.EmissionBoxExtents =
            new Vector3(
                38f,
                4f,
                38f
            );

        material.Direction =
            new Vector3(
                0f,
                -1f,
                0f
            );

        material.Spread =
            20f;

        material.InitialVelocityMin =
            SnowSpeed * 0.7f;

        material.InitialVelocityMax =
            SnowSpeed * 1.3f;

        material.Gravity =
            new Vector3(
                0f,
                -1.2f,
                0f
            );

        material.LinearAccelMin =
            -0.8f;

        material.LinearAccelMax =
            0.8f;

        material.Color =
            new Color(
                1f,
                1f,
                1f,
                0.9f
            );

        _snowParticles.ProcessMaterial =
            material;

        _weatherRoot.AddChild(
            _snowParticles
        );
    }

    // ============================================================
    // SANDSTORM
    // ============================================================

    private void BuildSandstorm()
    {
        _sandParticles =
            new GpuParticles3D();

        _sandParticles.Name =
            "SandstormParticles";

        _sandParticles.Amount =
            SandstormParticleAmount;

        _sandParticles.Lifetime =
            SandstormParticleLifetime;

        _sandParticles.Emitting =
            false;

        _sandParticles.LocalCoords =
            false;

        _sandParticles.VisibilityAabb =
            new Aabb(
                new Vector3(
                    -45f,
                    -20f,
                    -45f
                ),
                new Vector3(
                    90f,
                    40f,
                    90f
                )
            );

        var sandMesh =
            new QuadMesh();

        sandMesh.Size =
            new Vector2(
                0.12f,
                0.045f
            );

        var sandMaterial =
            new StandardMaterial3D();

        sandMaterial.AlbedoColor =
            new Color(
                0.72f,
                0.57f,
                0.32f,
                0.55f
            );

        sandMaterial.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.PerPixel;

        sandMaterial.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        sandMaterial.CullMode =
            BaseMaterial3D.CullModeEnum.Disabled;

        sandMaterial.EmissionEnabled =
            false;

        sandMesh.Material =
            sandMaterial;

        _sandParticles.DrawPass1 =
            sandMesh;

        var material =
            new ParticleProcessMaterial();

        material.EmissionShape =
            ParticleProcessMaterial.EmissionShapeEnum.Box;

        material.EmissionBoxExtents =
            new Vector3(
                35f,
                12f,
                35f
            );

        material.Direction =
            new Vector3(
                1f,
                0.05f,
                0.25f
            );

        material.Spread =
            20f;

        material.InitialVelocityMin =
            SandstormSpeed * 0.6f;

        material.InitialVelocityMax =
            SandstormSpeed * 1.4f;

        material.Gravity =
            new Vector3(
                0f,
                -0.2f,
                0f
            );

        material.Color =
            new Color(
                0.75f,
                0.6f,
                0.35f,
                0.55f
            );

        _sandParticles.ProcessMaterial =
            material;

        _weatherRoot.AddChild(
            _sandParticles
        );
    }

    // ============================================================
    // LIGHTNING
    // ============================================================

    private void BuildLightning()
    {
        _lightningLight =
            new OmniLight3D();

        _lightningLight.Name =
            "LightningFlash";

        _lightningLight.LightColor =
            new Color(
                0.85f,
                0.92f,
                1.0f
            );

        _lightningLight.OmniRange =
            120f;

        _lightningLight.LightEnergy =
            0f;

        _lightningLight.ShadowEnabled =
            false;

        _weatherRoot.AddChild(
            _lightningLight
        );
    }

    // ============================================================
    // FOLLOW PLAYER
    // ============================================================

    private void FollowPlayer()
    {
        if (!IsInstanceValid(_player) ||
            !_player.IsInsideTree())
        {
            return;
        }

        if (!IsInstanceValid(_weatherRoot) ||
            !_weatherRoot.IsInsideTree())
        {
            return;
        }

        Vector3 playerPosition =
            _player.GlobalPosition;

        // ========================================================
        // IMPORTANT
        //
        // Weather is intentionally positioned ABOVE the player.
        //
        // Rain:
        //     ~30 blocks above player
        //
        // This allows the drops to fall through the scene
        // naturally instead of appearing around the player's head.
        // ========================================================

        _weatherRoot.GlobalPosition =
            playerPosition +
            new Vector3(
                0f,
                RainHeightAbovePlayer,
                0f
            );

        if (IsInstanceValid(_lightningLight))
        {
            _lightningLight.GlobalPosition =
                playerPosition +
                new Vector3(
                    0f,
                    8f,
                    0f
                );
        }
    }

    // ============================================================
    // CHOOSE WEATHER
    // ============================================================

    private void ChooseNextWeather()
    {
        if (!_worldActive)
            return;

        SeasonManager.Season season =
            GetCurrentSeason();

        WeatherType newWeather =
            WeatherType.Clear;

        float roll =
            _rng.Randf();

        switch (season)
        {
            case SeasonManager.Season.Spring:

                if (roll < SpringRainChance)
                {
                    newWeather =
                        WeatherType.Rain;
                }

                break;

            case SeasonManager.Season.Summer:

                if (roll < SummerRainChance)
                {
                    newWeather =
                        WeatherType.Rain;
                }
                else if (
                    roll <
                    SummerRainChance +
                    SummerSandstormChance
                )
                {
                    newWeather =
                        WeatherType.Sandstorm;
                }

                break;

            case SeasonManager.Season.Autumn:

                if (roll < AutumnRainChance)
                {
                    newWeather =
                        WeatherType.Rain;
                }
                else if (
                    roll <
                    AutumnRainChance +
                    AutumnSandstormChance
                )
                {
                    newWeather =
                        WeatherType.Sandstorm;
                }

                break;

            case SeasonManager.Season.Winter:

                if (roll < WinterSnowChance)
                {
                    newWeather =
                        WeatherType.Snow;
                }

                break;
        }

        SetWeather(
            newWeather
        );
    }

    // ============================================================
    // GET CURRENT SEASON
    // ============================================================

    private SeasonManager.Season GetCurrentSeason()
    {
        if (!IsInstanceValid(_seasonManager))
        {
            _seasonManager =
                GetNodeOrNull<SeasonManager>(
                    "/root/SeasonManager"
                );
        }

        if (!IsInstanceValid(_seasonManager))
        {
            return SeasonManager.Season.Spring;
        }

        return _seasonManager.CurrentSeason;
    }

    // ============================================================
    // SET WEATHER
    // ============================================================

    public void SetWeather(
        WeatherType newWeather
    )
    {
        if (!_worldActive)
            return;

        WeatherType oldWeather =
            CurrentWeather;

        CurrentWeather =
            newWeather;

        _thunderstorm =
            false;

        _nextLightningSeconds =
            -1f;

        // --------------------------------------------------------
        // Thunderstorm
        // --------------------------------------------------------

        if (newWeather ==
            WeatherType.Rain)
        {
            _thunderstorm =
                _rng.Randf() <
                ThunderstormChance;

            if (_thunderstorm)
            {
                ScheduleNextLightning();

                GD.Print(
                    "[WeatherManager] " +
                    "Thunderstorm started."
                );
            }
        }

        // --------------------------------------------------------
        // Particles
        // --------------------------------------------------------

        DisableAllWeather();

        switch (newWeather)
        {
            case WeatherType.Rain:

                if (IsInstanceValid(_rainParticles))
                {
                    _rainParticles.Emitting =
                        true;
                }

                break;

            case WeatherType.Snow:

                if (IsInstanceValid(_snowParticles))
                {
                    _snowParticles.Emitting =
                        true;
                }

                break;

            case WeatherType.Sandstorm:

                if (IsInstanceValid(_sandParticles))
                {
                    _sandParticles.Emitting =
                        true;
                }

                break;

            case WeatherType.Clear:
                break;
        }

        // --------------------------------------------------------
        // Duration
        // --------------------------------------------------------

        float hours;

        if (newWeather ==
            WeatherType.Clear)
        {
            // After rain/snow/sandstorm, guarantee a longer
            // stretch of clear weather.
            hours =
                _rng.RandfRange(
                    MinimumClearHours,
                    MaximumClearHours
                );
        }
        else
        {
            hours =
                _rng.RandfRange(
                    MinimumWeatherHours,
                    MaximumWeatherHours
                );
        }

        _weatherRemainingSeconds =
            GameHoursToRealSeconds(
                hours
            );

        GD.Print(
            $"[WeatherManager] Weather changed: " +
            $"{oldWeather} -> {newWeather} " +
            $"({hours:0.0} in-game hours)"
        );

        WeatherChanged?.Invoke(
            newWeather,
            oldWeather
        );
    }

    // ============================================================
    // DISABLE WEATHER
    // ============================================================

    private void DisableAllWeather()
    {
        if (IsInstanceValid(_rainParticles))
        {
            _rainParticles.Emitting =
                false;
        }

        if (IsInstanceValid(_snowParticles))
        {
            _snowParticles.Emitting =
                false;
        }

        if (IsInstanceValid(_sandParticles))
        {
            _sandParticles.Emitting =
                false;
        }

        if (IsInstanceValid(_lightningLight))
        {
            _lightningLight.LightEnergy =
                0f;
        }
    }

    // ============================================================
    // GAME TIME CONVERSION
    // ============================================================

    private float GameHoursToRealSeconds(
        float gameHours
    )
    {
        if (!IsInstanceValid(_dayNightCycle))
        {
            _dayNightCycle =
                GetTree()
                    .GetFirstNodeInGroup(
                        "day_night_cycle"
                    ) as DayNightCycle;
        }

        if (!IsInstanceValid(_dayNightCycle))
        {
            return gameHours * 37.5f;
        }

        float fullDaySeconds =
            _dayNightCycle.DayDurationSeconds;

        if (fullDaySeconds <= 0f)
            return gameHours * 37.5f;

        return
            (gameHours / 24f) *
            fullDaySeconds;
    }

    // ============================================================
    // LIGHTNING
    // ============================================================

    private void ScheduleNextLightning()
    {
        _nextLightningSeconds =
            _rng.RandfRange(
                MinimumLightningSeconds,
                MaximumLightningSeconds
            );
    }

    private void UpdateLightning(
        float delta
    )
    {
        if (!_worldActive)
            return;

        if (_nextLightningSeconds < 0f)
        {
            ScheduleNextLightning();
            return;
        }

        _nextLightningSeconds -= delta;

        if (_nextLightningSeconds <= 0f)
        {
            StartLightning();

            ScheduleNextLightning();
        }
    }

    // ============================================================
    // START LIGHTNING
    // ============================================================

    private void StartLightning()
    {
        if (!IsInstanceValid(_lightningLight))
            return;

        _lightningActive =
            true;

        _lightningRemainingSeconds =
            _rng.RandfRange(
                0.05f,
                0.12f
            );

        _lightningLight.LightEnergy =
            _rng.RandfRange(
                8f,
                14f
            );

        if (_rng.Randf() < 0.30f)
        {
            _lightningRemainingSeconds =
                0.18f;
        }

        GD.Print(
            "[WeatherManager] LIGHTNING!"
        );

        LightningStrike?.Invoke();
    }

    // ============================================================
    // END LIGHTNING
    // ============================================================

    private void EndLightning()
    {
        _lightningActive =
            false;

        if (IsInstanceValid(_lightningLight))
        {
            _lightningLight.LightEnergy =
                0f;
        }
    }

    // ============================================================
    // PUBLIC WEATHER API
    // ============================================================

    public WeatherType GetWeather()
    {
        return CurrentWeather;
    }

    public string GetWeatherName()
    {
        switch (CurrentWeather)
        {
            case WeatherType.Rain:

                return _thunderstorm
                    ? "Thunderstorm"
                    : "Rain";

            case WeatherType.Snow:
                return "Snow";

            case WeatherType.Sandstorm:
                return "Sandstorm";

            default:
                return "Clear";
        }
    }

    public bool IsClear()
    {
        return CurrentWeather ==
            WeatherType.Clear;
    }

    // ============================================================
    // DEBUG COMMANDS
    // ============================================================

    public void DebugClear()
    {
        if (!_worldActive)
            return;

        SetWeather(
            WeatherType.Clear
        );
    }

    public void DebugRain()
    {
        if (!_worldActive)
            return;

        SetWeather(
            WeatherType.Rain
        );
    }

    public void DebugSnow()
    {
        if (!_worldActive)
            return;

        SetWeather(
            WeatherType.Snow
        );
    }

    public void DebugSandstorm()
    {
        if (!_worldActive)
            return;

        SetWeather(
            WeatherType.Sandstorm
        );
    }

    public void DebugThunderstorm()
    {
        if (!_worldActive)
            return;

        SetWeather(
            WeatherType.Rain
        );

        _thunderstorm =
            true;

        ScheduleNextLightning();
    }
}
