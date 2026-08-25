using Godot;
using System;

public partial class DayNightCycle : Node3D
{
    [Export]
    public DirectionalLight3D Sun { get; set; }

    // ============================================================
    // DAY / NIGHT
    // ============================================================

    [Export]
    public float DayDurationSeconds { get; set; } = 900f;

    // ============================================================
    // TIME OF DAY
    // ============================================================

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
    // SUN
    // ============================================================

    private Node3D _sunCube;

    private MeshInstance3D _sunFront;
    private MeshInstance3D _sunBack;
    private MeshInstance3D _sunLeft;
    private MeshInstance3D _sunRight;
    private MeshInstance3D _sunTop;
    private MeshInstance3D _sunBottom;

    private StandardMaterial3D _sunFrontMat;
    private StandardMaterial3D _sunBackMat;
    private StandardMaterial3D _sunLeftMat;
    private StandardMaterial3D _sunRightMat;
    private StandardMaterial3D _sunTopMat;
    private StandardMaterial3D _sunBottomMat;

    private AnimatableBody3D _sunCollision;
    private CollisionShape3D _sunCollisionShape;

    private float _sunSize = 16f;
    private float _moonSize = 10f;

    // ============================================================
    // MOON
    // ============================================================

    // IMPORTANT:
    // The moon visual now lives INSIDE the AnimatableBody3D.
    // The body itself moves through the sky.
    private AnimatableBody3D _moonCollision;

    private CollisionShape3D _moonCollisionShape;

    private Node3D _moonCube;

    private MeshInstance3D _moonFront;
    private MeshInstance3D _moonBack;
    private MeshInstance3D _moonLeft;
    private MeshInstance3D _moonRight;
    private MeshInstance3D _moonTop;
    private MeshInstance3D _moonBottom;

    private StandardMaterial3D _moonFrontMat;
    private StandardMaterial3D _moonBackMat;
    private StandardMaterial3D _moonLeftMat;
    private StandardMaterial3D _moonRightMat;
    private StandardMaterial3D _moonTopMat;
    private StandardMaterial3D _moonBottomMat;
    private bool _sunHarvested = false;
    private bool _moonHarvested = false;
    

    private bool _wasSunVisible = false;
    private bool _wasMoonVisible = false;

    
    private bool _sunBroken = false;
private bool _moonBroken = false;

    // ============================================================
    // ORBIT
    // ============================================================

    private float _orbitRadius = 200f;

    // ============================================================
    // CELESTIAL TEXTURE PATH
    // ============================================================

    private const string CelestialTexturePath =
        "res://Assets/Textures/Celestial/";

    // ============================================================
    // ENVIRONMENT
    // ============================================================

    private Godot.Environment _env;
    private ProceduralSkyMaterial _skyMaterial;

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
    // PHYSICS MOON TARGET
    // ============================================================

    // This is the position the moon should occupy during physics.
    // The AnimatableBody3D is moved directly to this position.
    private Vector3 _targetMoonPosition = Vector3.Zero;

    public override void _Ready()
    {
        AddToGroup("day_night_cycle");

        LoadSavedWorldState();

        CreateSun();
        CreateMoon();
        CreateEnvironment();

        SeasonManager seasonManager =
            GetNodeOrNull<SeasonManager>(
                "/root/SeasonManager"
            );

        if (seasonManager != null)
        {
            seasonManager.SeasonChanged +=
                OnSeasonChanged;

            seasonManager.DayChanged +=
                OnDayChanged;
        }

        UpdateCelestialBodies();
        UpdateLightColor();

        // Force the initial moon physics position immediately.
        UpdateMoonPhysicsPosition();
    }

    // ============================================================
    // CREATE SUN
    // ============================================================

    private void CreateSun()
    {
        _sunCollision =
    new AnimatableBody3D();

_sunCollision.Name =
    "SunCollision";

_sunCollision.CollisionLayer = 1;
_sunCollision.CollisionMask = 1;
_sunCollision.SyncToPhysics = true;

AddChild(_sunCollision);

_sunCube = new Node3D();

_sunCube.Name =
    "SunCube";

_sunCollision.AddChild(_sunCube);

        Texture2D front =
            LoadSunTexture("sun_front.png");

        Texture2D back =
            LoadSunTexture("sun_back.png");

        Texture2D left =
            LoadSunTexture("sun_left.png");

        Texture2D right =
            LoadSunTexture("sun_right.png");

        Texture2D top =
            LoadSunTexture("sun_top.png");

        Texture2D bottom =
            LoadSunTexture("sun_bottom.png");

        _sunFrontMat =
            CreateSunMaterial(front);

        _sunBackMat =
            CreateSunMaterial(back);

        _sunLeftMat =
            CreateSunMaterial(left);

        _sunRightMat =
            CreateSunMaterial(right);

        _sunTopMat =
            CreateSunMaterial(top);

        _sunBottomMat =
            CreateSunMaterial(bottom);

        _sunFront =
            CreateSunFace(
                "SunFront",
                _sunFrontMat,
                new Vector3(
                    0f,
                    0f,
                    _sunSize / 2f
                ),
                Vector3.Zero
            );

        _sunBack =
            CreateSunFace(
                "SunBack",
                _sunBackMat,
                new Vector3(
                    0f,
                    0f,
                    -_sunSize / 2f
                ),
                new Vector3(
                    0f,
                    Mathf.Pi,
                    0f
                )
            );

        _sunLeft =
            CreateSunFace(
                "SunLeft",
                _sunLeftMat,
                new Vector3(
                    -_sunSize / 2f,
                    0f,
                    0f
                ),
                new Vector3(
                    0f,
                    -Mathf.Pi / 2f,
                    0f
                )
            );

        _sunRight =
            CreateSunFace(
                "SunRight",
                _sunRightMat,
                new Vector3(
                    _sunSize / 2f,
                    0f,
                    0f
                ),
                new Vector3(
                    0f,
                    Mathf.Pi / 2f,
                    0f
                )
            );

        _sunTop =
            CreateSunFace(
                "SunTop",
                _sunTopMat,
                new Vector3(
                    0f,
                    _sunSize / 2f,
                    0f
                ),
                new Vector3(
                    -Mathf.Pi / 2f,
                    0f,
                    0f
                )
            );

        _sunBottom =
            CreateSunFace(
                "SunBottom",
                _sunBottomMat,
                new Vector3(
                    0f,
                    -_sunSize / 2f,
                    0f
                ),
                new Vector3(
                    Mathf.Pi / 2f,
                    0f,
                    0f
                )
            );

        _sunCube.Rotation =
            new Vector3(
                0f,
                Mathf.Pi / 4f,
                0f
            );

        CreateSunCollision();

        if (Sun != null)
        {
            Node parent =
                Sun.GetParent();

            if (parent != null)
                parent.RemoveChild(Sun);

            _sunCube.AddChild(Sun);

            Sun.Position =
                Vector3.Zero;
        }

        GD.Print(
            "[DayNightCycle] Created physical textured sun cube."
        );
    }

    // ============================================================
    // CREATE SUN COLLISION
    // ============================================================

    private void CreateSunCollision()
{
    _sunCollisionShape =
        new CollisionShape3D();

    _sunCollisionShape.Name =
        "SunCollisionShape";

    var box =
        new BoxShape3D();

    box.Size =
        new Vector3(
            _sunSize,
            _sunSize,
            _sunSize
        );

    _sunCollisionShape.Shape =
        box;

    _sunCollision.AddChild(
        _sunCollisionShape
    );

    _sunCollisionShape.Rotation =
    new Vector3(
        0f,
        Mathf.Pi / 4f,
        0f
    );
}

    // ============================================================
    // LOAD SUN TEXTURE
    // ============================================================

    private Texture2D LoadSunTexture(
        string fileName)
    {
        string path =
            CelestialTexturePath +
            fileName;

        Texture2D texture =
            GD.Load<Texture2D>(path);

        if (texture == null)
        {
            GD.PrintErr(
                $"[DayNightCycle] Sun texture not found: {path}"
            );

            return null;
        }

        return texture;
    }

    // ============================================================
    // CREATE SUN MATERIAL
    // ============================================================

    private StandardMaterial3D CreateSunMaterial(
        Texture2D texture)
    {
        var material =
            new StandardMaterial3D();

        material.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.Unshaded;

        material.EmissionEnabled =
            true;

        material.Emission =
            new Color(
                1.0f,
                0.85f,
                0.25f
            );

        material.EmissionEnergyMultiplier =
            2.0f;

        material.TextureFilter =
            BaseMaterial3D.TextureFilterEnum.Nearest;

        material.CullMode =
            BaseMaterial3D.CullModeEnum.Disabled;

        material.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        if (texture != null)
            material.AlbedoTexture = texture;

        return material;
    }

    // ============================================================
    // CREATE SUN FACE
    // ============================================================

    private MeshInstance3D CreateSunFace(
        string faceName,
        StandardMaterial3D material,
        Vector3 position,
        Vector3 rotation)
    {
        var face =
            new MeshInstance3D();

        face.Name =
            faceName;

        var quad =
            new QuadMesh();

        quad.Size =
            new Vector2(
                _sunSize,
                _sunSize
            );

        face.Mesh =
            quad;

        face.Position =
            position;

        face.Rotation =
            rotation;

        face.MaterialOverride =
            material;

        _sunCube.AddChild(face);

        return face;
    }

    // ============================================================
    // CREATE MOON
    // ============================================================

    private void CreateMoon()
    {
        // ========================================================
        // CREATE PHYSICS BODY FIRST
        // ========================================================

        _moonCollision =
            new AnimatableBody3D();

        _moonCollision.Name =
            "MoonCollision";

        _moonCollision.CollisionLayer = 1;
        _moonCollision.CollisionMask = 1;

        // IMPORTANT:
        // Physics movement is synchronized to the physics engine.
        _moonCollision.SyncToPhysics = true;

        AddChild(_moonCollision);

        // ========================================================
        // CREATE COLLISION SHAPE
        // ========================================================

        _moonCollisionShape =
            new CollisionShape3D();

        _moonCollisionShape.Name =
            "MoonCollisionShape";

        var box =
            new BoxShape3D();

        box.Size =
            new Vector3(
                _moonSize,
                _moonSize,
                _moonSize
            );

        _moonCollisionShape.Shape =
            box;

        _moonCollision.AddChild(
            _moonCollisionShape
        );
        _moonCollisionShape.Rotation =
    new Vector3(
        0f,
        Mathf.Pi / 4f,
        0f
    );

        // ========================================================
        // CREATE VISUAL MOON INSIDE PHYSICS BODY
        // ========================================================

        _moonCube =
            new Node3D();

        _moonCube.Name =
            "MoonCube";

        _moonCollision.AddChild(
            _moonCube
        );

        // IMPORTANT:
        // MoonCube is now LOCAL to MoonCollision.
        // It never moves independently from the collision body.
        _moonCube.Position =
            Vector3.Zero;

        _moonCube.Rotation =
    Vector3.Zero;

_moonCollision.Rotation =
    new Vector3(
        0f,
        Mathf.Pi / 4f,
        0f
    );

_moonCollision.Rotation =
    new Vector3(
        0f,
        Mathf.Pi / 4f,
        0f
    );

        // ========================================================
        // LOAD TEXTURES
        // ========================================================

        Texture2D front =
            LoadMoonTexture("moon_front.png");

        Texture2D back =
            LoadMoonTexture("moon_back.png");

        Texture2D left =
            LoadMoonTexture("moon_left.png");

        Texture2D right =
            LoadMoonTexture("moon_right.png");

        Texture2D top =
            LoadMoonTexture("moon_top.png");

        Texture2D bottom =
            LoadMoonTexture("moon_bottom.png");

        // ========================================================
        // MATERIALS
        // ========================================================

        _moonFrontMat =
            CreateMoonMaterial(front);

        _moonBackMat =
            CreateMoonMaterial(back);

        _moonLeftMat =
            CreateMoonMaterial(left);

        _moonRightMat =
            CreateMoonMaterial(right);

        _moonTopMat =
            CreateMoonMaterial(top);

        _moonBottomMat =
            CreateMoonMaterial(bottom);

        // ========================================================
        // FACES
        // ========================================================

        _moonFront =
            CreateMoonFace(
                "MoonFront",
                _moonFrontMat,
                new Vector3(
                    0f,
                    0f,
                    _moonSize / 2f
                ),
                Vector3.Zero
            );

        _moonBack =
            CreateMoonFace(
                "MoonBack",
                _moonBackMat,
                new Vector3(
                    0f,
                    0f,
                    -_moonSize / 2f
                ),
                new Vector3(
                    0f,
                    Mathf.Pi,
                    0f
                )
            );

        _moonLeft =
            CreateMoonFace(
                "MoonLeft",
                _moonLeftMat,
                new Vector3(
                    -_moonSize / 2f,
                    0f,
                    0f
                ),
                new Vector3(
                    0f,
                    -Mathf.Pi / 2f,
                    0f
                )
            );

        _moonRight =
            CreateMoonFace(
                "MoonRight",
                _moonRightMat,
                new Vector3(
                    _moonSize / 2f,
                    0f,
                    0f
                ),
                new Vector3(
                    0f,
                    Mathf.Pi / 2f,
                    0f
                )
            );

        _moonTop =
            CreateMoonFace(
                "MoonTop",
                _moonTopMat,
                new Vector3(
                    0f,
                    _moonSize / 2f,
                    0f
                ),
                new Vector3(
                    -Mathf.Pi / 2f,
                    0f,
                    0f
                )
            );

        _moonBottom =
            CreateMoonFace(
                "MoonBottom",
                _moonBottomMat,
                new Vector3(
                    0f,
                    -_moonSize / 2f,
                    0f
                ),
                new Vector3(
                    Mathf.Pi / 2f,
                    0f,
                    0f
                )
            );

        // Start with collision disabled until
        // UpdateCelestialBodies determines visibility.
        _moonCollisionShape.Disabled = true;

        GD.Print(
            "[DayNightCycle] Created physical textured moon cube."
        );
    }

    // ============================================================
    // LOAD MOON TEXTURE
    // ============================================================

    private Texture2D LoadMoonTexture(
        string fileName)
    {
        string path =
            CelestialTexturePath +
            fileName;

        Texture2D texture =
            GD.Load<Texture2D>(path);

        if (texture == null)
        {
            GD.PrintErr(
                $"[DayNightCycle] Moon texture not found: {path}"
            );

            return null;
        }

        return texture;
    }

    // ============================================================
    // CREATE MOON MATERIAL
    // ============================================================

    private StandardMaterial3D CreateMoonMaterial(
        Texture2D texture)
    {
        var material =
            new StandardMaterial3D();

        material.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.Unshaded;

        material.EmissionEnabled =
            false;

        material.TextureFilter =
            BaseMaterial3D.TextureFilterEnum.Nearest;

        material.CullMode =
            BaseMaterial3D.CullModeEnum.Disabled;

        material.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        material.AlbedoColor =
            new Color(
                0.8f,
                0.8f,
                0.85f,
                1f
            );

        if (texture != null)
            material.AlbedoTexture =
                texture;

        return material;
    }

    // ============================================================
    // CREATE MOON FACE
    // ============================================================

    private MeshInstance3D CreateMoonFace(
        string faceName,
        StandardMaterial3D material,
        Vector3 position,
        Vector3 rotation)
    {
        var face =
            new MeshInstance3D();

        face.Name =
            faceName;

        var quad =
            new QuadMesh();

        quad.Size =
            new Vector2(
                _moonSize,
                _moonSize
            );

        face.Mesh =
            quad;

        face.Position =
            position;

        face.Rotation =
            rotation;

        face.MaterialOverride =
            material;

        _moonCube.AddChild(face);

        return face;
    }

public void HarvestCelestial(bool isSun)
{
    if (isSun)
    {
        _sunHarvested = true;

        if (_sunCube != null)
            _sunCube.Visible = false;

        if (_sunCollisionShape != null)
            _sunCollisionShape.Disabled = true;

        GD.Print("[DayNightCycle] Sun harvested.");
    }
    else
    {
        _moonHarvested = true;

        if (_moonCube != null)
            _moonCube.Visible = false;

        if (_moonCollisionShape != null)
            _moonCollisionShape.Disabled = true;

        GD.Print("[DayNightCycle] Moon harvested.");
    }
}
public void ResetCelestialHarvest()
{
    // =========================================================
    // RESET SUN
    // =========================================================

    _sunBroken = false;

    if (_sunFront != null)
        _sunFront.Visible = true;

    if (_sunBack != null)
        _sunBack.Visible = true;

    if (_sunLeft != null)
        _sunLeft.Visible = true;

    if (_sunRight != null)
        _sunRight.Visible = true;

    if (_sunTop != null)
        _sunTop.Visible = true;

    if (_sunBottom != null)
        _sunBottom.Visible = true;

    if (_sunCollisionShape != null)
        _sunCollisionShape.Disabled = false;

    if (_sunCollision != null)
    {
        _sunCollision.CollisionLayer = 1;
        _sunCollision.CollisionMask = 1;
    }

    // =========================================================
    // RESET MOON
    // =========================================================

    _moonBroken = false;

    // Restore the moon cube itself.
    if (_moonCube != null)
        _moonCube.Visible = true;

    // Restore every moon face in case individual faces were
    // hidden when the moon was broken.
    if (_moonFront != null)
        _moonFront.Visible = true;

    if (_moonBack != null)
        _moonBack.Visible = true;

    if (_moonLeft != null)
        _moonLeft.Visible = true;

    if (_moonRight != null)
        _moonRight.Visible = true;

    if (_moonTop != null)
        _moonTop.Visible = true;

    if (_moonBottom != null)
        _moonBottom.Visible = true;

    // Restore moon collision.
    if (_moonCollisionShape != null)
        _moonCollisionShape.Disabled = false;

    if (_moonCollision != null)
    {
        _moonCollision.CollisionLayer = 1;
        _moonCollision.CollisionMask = 1;
    }

    // Make sure the moon is immediately placed at its
    // correct position for the current time.
    UpdateMoonPhysicsPosition();

    GD.Print(
        "[DayNightCycle] Sun and Moon harvest reset."
    );
}

    // ============================================================
    // CREATE ENVIRONMENT
    // ============================================================

    private void CreateEnvironment()
    {
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
    }

    // ============================================================
    // LOAD SAVED WORLD STATE
    // ============================================================

    private void LoadSavedWorldState()
    {
        if (SaveManager.Instance == null)
        {
            _timeOfDay =
                0.0f;

            GD.Print(
                "[DayNightCycle] No SaveManager found. " +
                "Starting at sunrise."
            );

            return;
        }

        _timeOfDay =
            Mathf.PosMod(
                SaveManager.Instance.LoadWorldTime(),
                1.0f
            );

        GD.Print(
            $"[DayNightCycle] Starting at saved time: " +
            $"{_timeOfDay:0.000} ({GetTimeString()})"
        );

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
        }
    }

    // ============================================================
    // PROCESS
    // ============================================================

    public override void _Process(
        double delta)
    {
        _timeOfDay +=
            (float)delta /
            DayDurationSeconds;

        if (_timeOfDay >= 1f)
        {
            _timeOfDay -= 1f;

            AdvanceCalendarDay();

            SaveCurrentWorldState();
        }

        // Visuals and lighting can update every render frame.
        UpdateCelestialBodies();
        UpdateLightColor();
    }

    // ============================================================
    // PHYSICS PROCESS
    // ============================================================

    public override void _PhysicsProcess(
        double delta)
    {
        if (_moonCollision == null)
            return;

        // --------------------------------------------------------
        // THE IMPORTANT FIX
        // --------------------------------------------------------
        //
        // The moon's AnimatableBody3D is now the actual moving
        // object.
        //
        // We do NOT copy MoonCube -> MoonCollision anymore.
        //
        // MoonCube is a child of MoonCollision, so both visual
        // and collision move together perfectly on the physics
        // timestep.
        //

        UpdateMoonPhysicsPosition();
    }

    // ============================================================
    // UPDATE MOON PHYSICS POSITION
    // ============================================================

    private void UpdateMoonPhysicsPosition()
    {
        if (_moonCollision == null)
            return;

        float daylightProgress =
            GetDaylightProgress();

        bool sunUp =
            IsSunUp();

        Vector3 moonPosition;

        // ========================================================
        // SUN POSITION CALCULATION
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
                    Mathf.Cos(
                        sunAngle
                    ) *
                    _orbitRadius,

                    Mathf.Sin(
                        sunAngle
                    ) *
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
                    Mathf.Cos(
                        nightAngle
                    ) *
                    _orbitRadius,

                    -Mathf.Abs(
                        Mathf.Sin(
                            nightAngle
                        ) *
                        _orbitRadius
                    ) - 1f,

                    0f
                );
        }

        // ========================================================
        // MOON IS OPPOSITE THE SUN
        // ========================================================

        moonPosition =
            -sunPosition;

        _targetMoonPosition =
            moonPosition;

        // ========================================================
        // MOVE THE PHYSICAL MOON
        // ========================================================

        _moonCollision.GlobalPosition =
            _targetMoonPosition;

        // ========================================================
        // KEEP MOON VISUAL ROTATION FIXED
        // ========================================================

        if (_moonCube != null)
{
    _moonCube.Position =
        Vector3.Zero;
}
    }
// ============================================================
// BREAK CELESTIAL BODY
// ============================================================

public void BreakCelestialBody(string celestialId)
{
    if (celestialId == "sun")
{
    _sunBroken = true;

    // Hide every Sun visual face.
    if (_sunFront != null)
        _sunFront.Visible = false;

    if (_sunBack != null)
        _sunBack.Visible = false;

    if (_sunLeft != null)
        _sunLeft.Visible = false;

    if (_sunRight != null)
        _sunRight.Visible = false;

    if (_sunTop != null)
        _sunTop.Visible = false;

    if (_sunBottom != null)
        _sunBottom.Visible = false;

    // Disable Sun collision.
    if (_sunCollisionShape != null)
        _sunCollisionShape.Disabled = true;

    if (_sunCollision != null)
    {
        _sunCollision.CollisionLayer = 0;
        _sunCollision.CollisionMask = 0;
    }

    GD.Print(
        "[DayNightCycle] Sun broken until next sunrise."
    );
    }
    else if (celestialId == "moon")
    {
        _moonBroken = true;

        // Hide every moon visual face.
if (_moonFront != null)
    _moonFront.Visible = false;

if (_moonBack != null)
    _moonBack.Visible = false;

if (_moonLeft != null)
    _moonLeft.Visible = false;

if (_moonRight != null)
    _moonRight.Visible = false;

if (_moonTop != null)
    _moonTop.Visible = false;

if (_moonBottom != null)
    _moonBottom.Visible = false;

        if (_moonCollisionShape != null)
            _moonCollisionShape.Disabled = true;

        if (_moonCollision != null)
        {
            _moonCollision.CollisionLayer = 0;
            _moonCollision.CollisionMask = 0;
        }

        GD.Print(
            "[DayNightCycle] Moon broken until next moonrise."
        );
    }
}
    // ============================================================
    // UPDATE CELESTIAL BODIES
    // ============================================================

    private void UpdateCelestialBodies()
    {
        if (_sunCube == null ||
            _moonCollision == null ||
            _moonCube == null)
            return;

        float daylightProgress =
            GetDaylightProgress();

        bool sunUp =
            IsSunUp();

        Vector3 sunPosition;

        // ========================================================
        // SUN POSITION
        // ========================================================

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
                    Mathf.Cos(
                        sunAngle
                    ) *
                    _orbitRadius,

                    Mathf.Sin(
                        sunAngle
                    ) *
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
                    Mathf.Cos(
                        nightAngle
                    ) *
                    _orbitRadius,

                    -Mathf.Abs(
                        Mathf.Sin(
                            nightAngle
                        ) *
                        _orbitRadius
                    ) - 1f,

                    0f
                );
        }

        // ========================================================
        // SUN POSITION
        // ========================================================

        _sunCollision.GlobalPosition =
    sunPosition;

        // ========================================================
        // SUN LIGHT ROTATION
        // ========================================================

        if (Sun != null)
        {
            Vector3 direction =
                (
                    Vector3.Zero -
                    _sunCube.GlobalPosition
                ).Normalized();

            if (
                _sunCube.GlobalPosition.LengthSquared()
                > 0.0001f
            )
            {
                Vector3 target =
                    _sunCube.GlobalPosition +
                    direction;

                if (
                    !_sunCube.GlobalPosition
                        .IsEqualApprox(target)
                )
                {
                    Vector3 up =
                        Vector3.Forward;

                    if (
                        Mathf.Abs(
                            direction.Dot(up)
                        ) > 0.98f
                    )
                    {
                        up =
                            Vector3.Right;
                    }

                    Sun.LookAt(
                        target,
                        up
                    );
                }
            }
        }

        // ========================================================
        // MOON POSITION
        // ========================================================
        //
        // IMPORTANT:
        // We do NOT move MoonCube here anymore.
        //
        // The AnimatableBody3D is moved in _PhysicsProcess().
        // MoonCube follows automatically because it is its child.
        //

        Vector3 moonPosition =
            -sunPosition;

        _targetMoonPosition =
            moonPosition;

        // ========================================================
        // MOON VISUAL ROTATION
        // ========================================================

        _moonCube.Position =
            Vector3.Zero;

        _moonCube.Rotation =
            new Vector3(
                0f,
                Mathf.Pi / 4f,
                0f
            );

        // ========================================================
        // HEIGHT
        // ========================================================

        float sunHeight =
            _sunCube.GlobalPosition.Y /
            _orbitRadius;

        float moonHeight =
            _targetMoonPosition.Y /
            _orbitRadius;

        // ========================================================
        // SUN FADE
        // ========================================================

        float sunFade =
            Mathf.Clamp(
                sunHeight * 10f,
                0f,
                1f
            );

        // ========================================================
        // MOON FADE
        // ========================================================

        float moonFade =
            Mathf.Clamp(
                (moonHeight + 0.02f) * 10f,
                0f,
                1f
            );

        // ========================================================
        // SUN MATERIAL ALPHA
        // ========================================================

        UpdateSunMaterialAlpha(
            _sunFrontMat,
            sunFade
        );

        UpdateSunMaterialAlpha(
            _sunBackMat,
            sunFade
        );

        UpdateSunMaterialAlpha(
            _sunLeftMat,
            sunFade
        );

        UpdateSunMaterialAlpha(
            _sunRightMat,
            sunFade
        );

        UpdateSunMaterialAlpha(
            _sunTopMat,
            sunFade
        );

        UpdateSunMaterialAlpha(
            _sunBottomMat,
            sunFade
        );

        // ========================================================
        // MOON MATERIAL ALPHA
        // ========================================================

        UpdateMoonMaterialAlpha(
            _moonFrontMat,
            moonFade
        );

        UpdateMoonMaterialAlpha(
            _moonBackMat,
            moonFade
        );

        UpdateMoonMaterialAlpha(
            _moonLeftMat,
            moonFade
        );

        UpdateMoonMaterialAlpha(
            _moonRightMat,
            moonFade
        );

        UpdateMoonMaterialAlpha(
            _moonTopMat,
            moonFade
        );

        UpdateMoonMaterialAlpha(
            _moonBottomMat,
            moonFade
        );

        // ========================================================
// VISIBILITY
// ========================================================

// Respawn harvested celestial bodies when they rise again.
bool sunVisibleNow =
    sunFade > 0.001f;

bool moonVisibleNow =
    moonFade > 0.001f;

if (sunVisibleNow && !_wasSunVisible)
    _sunHarvested = false;

if (moonVisibleNow && !_wasMoonVisible)
    _moonHarvested = false;

_wasSunVisible = sunVisibleNow;
_wasMoonVisible = moonVisibleNow;

_sunCube.Visible =
    sunVisibleNow &&
    !_sunHarvested;

_moonCube.Visible =
    moonVisibleNow &&
    !_moonHarvested;

        // ========================================================
        // COLLISION VISIBILITY
        // ========================================================

        if (_sunCollisionShape != null)
        {
            _sunCollisionShape.Disabled =
                !_sunCube.Visible;
        }

        if (_moonCollisionShape != null)
        {
            _moonCollisionShape.Disabled =
                !_moonCube.Visible;
        }
    }

    // ============================================================
    // UPDATE SUN MATERIAL ALPHA
    // ============================================================

    private void UpdateSunMaterialAlpha(
        StandardMaterial3D material,
        float alpha)
    {
        if (material == null)
            return;

        Color color =
            material.AlbedoColor;

        color.A =
            alpha;

        material.AlbedoColor =
            color;

        material.EmissionEnergyMultiplier =
            2.0f *
            alpha;
    }

    // ============================================================
    // UPDATE MOON MATERIAL ALPHA
    // ============================================================

    private void UpdateMoonMaterialAlpha(
        StandardMaterial3D material,
        float alpha)
    {
        if (material == null)
            return;

        Color color =
            material.AlbedoColor;

        color.A =
            alpha;

        material.AlbedoColor =
            color;
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

        if (!sunUp)
        {
            lightColor =
                _nightColor;

            skyColor =
                _nightSky;

            energy =
                0f;
        }
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

            if (
                daylightPosition <
                sunriseLength
            )
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
            else if (
                daylightPosition >
                1f -
                sunsetLength
            )
            {
                float t =
                    (
                        daylightPosition -
                        (
                            1f -
                            sunsetLength
                        )
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
            else
            {
                float noonDistance =
                    Mathf.Abs(
                        daylightPosition -
                        0.5f
                    ) *
                    2f;

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
        // NIGHT = ZERO SUNLIGHT
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
    // DAY CHANGE
    // ============================================================

   private void OnDayChanged(
    int newDay,
    SeasonManager.Season currentSeason)
{
    // Update the Sun and Moon for the new calendar day first.
    UpdateCelestialBodies();

    // New calendar day = harvested celestial bodies reset.
    _sunBroken = false;
    _moonBroken = false;

    // Restore Sun visuals.
    if (_sunFront != null)
        _sunFront.Visible = true;

    if (_sunBack != null)
        _sunBack.Visible = true;

    if (_sunLeft != null)
        _sunLeft.Visible = true;

    if (_sunRight != null)
        _sunRight.Visible = true;

    if (_sunTop != null)
        _sunTop.Visible = true;

    if (_sunBottom != null)
        _sunBottom.Visible = true;

    // Restore Sun collision.
    if (_sunCollisionShape != null)
        _sunCollisionShape.Disabled = false;

    if (_sunCollision != null)
    {
        _sunCollision.CollisionLayer = 1;
        _sunCollision.CollisionMask = 1;
    }

    // Restore Moon visual.
    if (_moonCube != null)
        _moonCube.Visible = true;

    // Restore Moon collision.
    if (_moonCollisionShape != null)
        _moonCollisionShape.Disabled = false;

    if (_moonCollision != null)
    {
        _moonCollision.CollisionLayer = 1;
        _moonCollision.CollisionMask = 1;
    }

    GD.Print(
        $"[DayNightCycle] NEW DAY {newDay} - Sun and Moon reset."
    );
}

    // ============================================================
    // ADVANCE CALENDAR DAY
    // ============================================================

    private void AdvanceCalendarDay()
{
    GD.Print("========================================");
    GD.Print("[DayNightCycle] ADVANCE CALENDAR DAY CALLED");
    GD.Print("========================================");

    if (Godot.Engine.IsEditorHint())
        return;

    SeasonManager seasonManager =
        GetNodeOrNull<SeasonManager>(
            "/root/SeasonManager"
        );

    if (seasonManager != null)
    {
        GD.Print(
            $"[DayNightCycle] Before AdvanceDay: " +
            $"Day {seasonManager.CurrentDay}, " +
            $"{seasonManager.GetSeasonName()}"
        );

        seasonManager.AdvanceDay();

        GD.Print(
            $"[DayNightCycle] After AdvanceDay: " +
            $"Day {seasonManager.CurrentDay}, " +
            $"{seasonManager.GetSeasonName()}"
        );
    }
    else
    {
        GD.PrintErr(
            "[DayNightCycle] Could not find /root/SeasonManager!"
        );
    }
}

    // ============================================================
    // DEBUG: ADVANCE ONE HOUR
    // ============================================================

    public void AdvanceDebugHour()
{
    float currentHour =
        GetGameHour();

    currentHour += 1f;

    // Our game clock runs from 6:00 AM to 6:00 AM.
    // 30.0 means the next sunrise / new game day.
    bool newDay =
        currentHour >= 30f;

    if (newDay)
        currentHour -= 24f;

    _timeOfDay =
        GameHourToNormalizedTime(
            currentHour
        );

    // ---------------------------------------------------------
    // NEW GAME DAY
    // ---------------------------------------------------------

    if (newDay)
    {
        GD.Print(
            "[DayNightCycle] New game day reached."
        );

        AdvanceCalendarDay();
    }

    UpdateCelestialBodies();
    UpdateLightColor();

    // Immediately synchronize the physics bodies after
    // manually changing the time.
    UpdateMoonPhysicsPosition();

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
    // CURRENT TIME
    // ============================================================

    public float GetTimeOfDay()
    {
        return _timeOfDay;
    }

    // ============================================================
    // NORMALIZED -> GAME CLOCK
    // ============================================================

    public float GetGameHour()
    {
        float daylightProgress =
            GetDaylightProgress();

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
    // GAME CLOCK -> NORMALIZED
    // ============================================================

    private float GameHourToNormalizedTime(
        float gameHour)
    {
        float daylightProgress =
            GetDaylightProgress();

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

        float normalizedNightHour;

        if (gameHour >= 18f)
        {
            normalizedNightHour =
                gameHour -
                18f;
        }
        else
        {
            normalizedNightHour =
                gameHour +
                6f;
        }

        float nightProgress =
            normalizedNightHour /
            12f;

        return
            daylightProgress +
            (
                nightProgress *
                (
                    1f -
                    daylightProgress
                )
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

        return hour % 24;
    }

    public int GetDisplayMinute()
    {
        float gameHour =
            GetGameHour();

        float minute =
            (
                gameHour -
                Mathf.Floor(
                    gameHour
                )
            ) *
            60f;

        return Mathf.Clamp(
            Mathf.FloorToInt(
                minute
            ),
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

        if (hour >= 24f)
            hour -= 24f;

        if (hour >= 5f &&
            hour < 8f)
            return "SUNRISE";

        if (hour >= 8f &&
            hour < 12f)
            return "MORNING";

        if (hour >= 12f &&
            hour < 17f)
            return "AFTERNOON";

        if (hour >= 17f &&
            hour < 20f)
            return "SUNSET";

        return "NIGHT";
    }

    // ============================================================
    // CURRENT SEASON
    // ============================================================

    private SeasonManager.Season GetCurrentSeason()
    {
        if (!IsInsideTree())
            return SeasonManager.Season.Spring;

        SceneTree tree =
            GetTree();

        if (tree == null)
            return SeasonManager.Season.Spring;

        Node root =
            tree.Root;

        if (root == null)
            return SeasonManager.Season.Spring;

        Node managerNode =
            root.GetNodeOrNull(
                "SeasonManager"
            );

        if (managerNode is SeasonManager seasonManager)
            return seasonManager.CurrentSeason;

        var found =
            tree.GetFirstNodeInGroup(
                "season_manager"
            );

        if (found is SeasonManager manager)
            return manager.CurrentSeason;

        return SeasonManager.Season.Spring;
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

            seasonManager.DayChanged -=
                OnDayChanged;
        }

        SaveCurrentWorldState();
    }
}