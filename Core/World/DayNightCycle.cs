using Godot;

public partial class DayNightCycle : Node3D
{
    [Export] public DirectionalLight3D Sun { get; set; }
    [Export] public float DayDurationSeconds { get; set; } = 1200f;

    private float _timeOfDay = 0.5f; // starts at sunrise

    private Color _nightColor = new Color(0.001f, 0.001f, 0.005f);
    private Color _sunriseColor = new Color(1.0f, 0.5f, 0.2f);
    private Color _noonColor = new Color(1.0f, 0.95f, 0.8f);
    private Color _sunsetColor = new Color(1.0f, 0.4f, 0.15f);

    private Color _nightSky = new Color(0.06f, 0.06f, 0.11f);
    private Color _sunriseSky = new Color(0.7f, 0.4f, 0.3f);
    private Color _noonSky = new Color(0.4f, 0.6f, 0.9f);
    private Color _sunsetSky = new Color(0.7f, 0.35f, 0.25f);

    private float _nightEnergy = 1.0f;
    private float _sunriseEnergy = 1.2f;
    private float _noonEnergy = 2.0f;
    private float _sunsetEnergy = 1.2f;

    private MeshInstance3D _sunMesh;
    private MeshInstance3D _moonMesh;
    private StandardMaterial3D _sunMat;
    private StandardMaterial3D _moonMat;
    private float _orbitRadius = 200f;

    private Godot.Environment _env;
    private ProceduralSkyMaterial _skyMaterial;

    private float _currentSunFade = 1f;

    // Phase durations in seconds (must sum to DayDurationSeconds = 1200f)
    private const float SunriseDuration = 50f;
    private const float DayDuration     = 600f;
    private const float SunsetDuration  = 50f;
    private const float NightDuration   = 500f;
    private const float TotalDuration   = 1200f;

    // Sunrise centered exactly at the horizon crossing (0.5), sunset centered at (1.0/0.0)
    private const float SunriseStart = (0.5f * TotalDuration - SunriseDuration / 2f) / TotalDuration;
    private const float SunriseMid   = 0.5f;
    private const float SunriseEnd   = (0.5f * TotalDuration + SunriseDuration / 2f) / TotalDuration;

    private const float SunsetStart  = (TotalDuration - SunsetDuration / 2f) / TotalDuration;
    private const float SunsetMid    = 1.0f;
    private const float SunsetEnd    = (SunsetDuration / 2f) / TotalDuration;

    public override void _Ready()
    {
        // Create sun sphere
        _sunMesh = new MeshInstance3D();
        var sunSphere = new SphereMesh();
        sunSphere.Radius = 8f;
        sunSphere.Height = 16f;
        _sunMesh.Mesh = sunSphere;

        _sunMat = new StandardMaterial3D();
        _sunMat.AlbedoColor = new Color(1.0f, 0.9f, 0.2f);
        _sunMat.EmissionEnabled = true;
        _sunMat.Emission = new Color(1.0f, 0.8f, 0.1f);
        _sunMat.EmissionEnergyMultiplier = 2.0f;
        _sunMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _sunMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _sunMesh.MaterialOverride = _sunMat;

        // Attach light as child of sun mesh so it moves with it
        if (Sun != null)
        {
            Sun.GetParent()?.RemoveChild(Sun);
            _sunMesh.AddChild(Sun);
            Sun.Position = Vector3.Zero;
            Sun.RotationDegrees = new Vector3(90f, 0f, 0f);
        }

        AddChild(_sunMesh);

        // Create moon sphere
        _moonMesh = new MeshInstance3D();
        var moonSphere = new SphereMesh();
        moonSphere.Radius = 5f;
        moonSphere.Height = 10f;
        _moonMesh.Mesh = moonSphere;

        _moonMat = new StandardMaterial3D();
        _moonMat.AlbedoColor = new Color(0.9f, 0.9f, 1.0f);
        _moonMat.EmissionEnabled = true;
        _moonMat.Emission = new Color(0.8f, 0.8f, 1.0f);
        _moonMat.EmissionEnergyMultiplier = 0.8f;
        _moonMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _moonMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _moonMesh.MaterialOverride = _moonMat;
        AddChild(_moonMesh);

        // Environment / sky setup
        _env = new Godot.Environment();
        _env.BackgroundMode = Godot.Environment.BGMode.Sky;
        _env.Sky = new Sky();
        _skyMaterial = new ProceduralSkyMaterial();
        _env.Sky.SkyMaterial = _skyMaterial;
        _env.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
        _env.AmbientLightEnergy = 0.5f;

        var worldEnv = new WorldEnvironment();
        worldEnv.Environment = _env;
        AddChild(worldEnv);
    }

    private int _frameCount = 0;

    public override void _Process(double delta)
    {

 _frameCount++;
    if (_frameCount % 3 != 0) return; // only update every 3 frames

        _timeOfDay += (float)delta / DayDurationSeconds;
        if (_timeOfDay >= 1f) _timeOfDay -= 1f;

        UpdateCelestialBodies();
        UpdateLightColor();
    }

    private void UpdateCelestialBodies()
    {
        float sunAngle = _timeOfDay * Mathf.Tau - Mathf.Pi;

        _sunMesh.GlobalPosition = new Vector3(
            Mathf.Cos(sunAngle) * _orbitRadius,
            Mathf.Sin(sunAngle) * _orbitRadius,
            0f
        );

        // Rotate around Z since the orbit lives in the X-Y plane
        _sunMesh.Rotation = new Vector3(0f, 0f, sunAngle + Mathf.Pi / 2f);

        float moonAngle = sunAngle + Mathf.Pi;
        _moonMesh.GlobalPosition = new Vector3(
            Mathf.Cos(moonAngle) * _orbitRadius,
            Mathf.Sin(moonAngle) * _orbitRadius,
            0f
        );

        // Fade sun/moon in and out near the horizon instead of snapping visibility
        float fadeRange = 20f;

        float sunFade = Mathf.Clamp(_sunMesh.GlobalPosition.Y / fadeRange, 0f, 1f);
        float moonFade = Mathf.Clamp(_moonMesh.GlobalPosition.Y / fadeRange, 0f, 1f);

        if (_sunMat != null)
        {
            var c = _sunMat.AlbedoColor;
            c.A = sunFade;
            _sunMat.AlbedoColor = c;
            _sunMat.EmissionEnergyMultiplier = 2.0f * sunFade;
        }

        if (_moonMat != null)
        {
            var c = _moonMat.AlbedoColor;
            c.A = moonFade;
            _moonMat.AlbedoColor = c;
            _moonMat.EmissionEnergyMultiplier = 0.8f * moonFade;
        }

        _sunMesh.Visible = sunFade > 0f;
        _moonMesh.Visible = moonFade > 0f;

        _currentSunFade = sunFade;
    }

    private void UpdateLightColor()
    {
        if (Sun == null) return;

        float t = _timeOfDay;

        Color lightColor;
        Color skyColor;
        float energy;

        if (t >= SunriseStart && t < SunriseMid)
        {
            float lt = (t - SunriseStart) / (SunriseMid - SunriseStart);
            lightColor = _nightColor.Lerp(_sunriseColor, lt);
            skyColor = _nightSky.Lerp(_sunriseSky, lt);
            energy = Mathf.Lerp(_nightEnergy, _sunriseEnergy, lt);
        }
        else if (t >= SunriseMid && t < SunriseEnd)
        {
            float lt = (t - SunriseMid) / (SunriseEnd - SunriseMid);
            lightColor = _sunriseColor.Lerp(_noonColor, lt);
            skyColor = _sunriseSky.Lerp(_noonSky, lt);
            energy = Mathf.Lerp(_sunriseEnergy, _noonEnergy, lt);
        }
        else if (t >= SunriseEnd && t < SunsetStart)
        {
            lightColor = _noonColor;
            skyColor = _noonSky;
            energy = _noonEnergy;
        }
        else if (t >= SunsetStart && t < SunsetMid)
        {
            float lt = (t - SunsetStart) / (SunsetMid - SunsetStart);
            lightColor = _noonColor.Lerp(_sunsetColor, lt);
            skyColor = _noonSky.Lerp(_sunsetSky, lt);
            energy = Mathf.Lerp(_noonEnergy, _sunsetEnergy, lt);
        }
        else if (t >= SunsetMid || t < SunsetEnd) // wraps through 0.0
        {
            float wrappedT = t >= SunsetMid ? t - SunsetMid : t + (1f - SunsetMid);
            float span = (1f - SunsetMid) + SunsetEnd;
            float lt = wrappedT / span;
            lightColor = _sunsetColor.Lerp(_nightColor, lt);
            skyColor = _sunsetSky.Lerp(_nightSky, lt);
            energy = Mathf.Lerp(_sunsetEnergy, _nightEnergy, lt);
        }
        else // SunsetEnd <= t < SunriseStart : full night hold
        {
            lightColor = _nightColor;
            skyColor = _nightSky;
            energy = _nightEnergy;
        }

        Sun.LightColor = lightColor;
        Sun.LightEnergy = energy * _currentSunFade;

        if (_env != null)
        {
            _env.AmbientLightEnergy = Mathf.Lerp(0.12f, 0.35f, energy / _noonEnergy);
        }

        if (_skyMaterial != null)
        {
            _skyMaterial.SkyTopColor = skyColor;
            _skyMaterial.SkyHorizonColor = skyColor;
            _skyMaterial.GroundBottomColor = skyColor;
            _skyMaterial.GroundHorizonColor = skyColor;
        }
    }
}