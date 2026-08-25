using Godot;

public partial class BlockBreakOverlay : Node3D
{
    public const int TotalStages = 8;

    private MeshInstance3D _mesh;
    private StandardMaterial3D _mat;
    private Texture2D[] _stageTextures = new Texture2D[TotalStages];

    private Vector3I _targetBlock =
        new Vector3I(int.MinValue, 0, 0);

    private int _currentStage = -1;

    // ---------------------------------------------------------
    // CELESTIAL MODE
    // ---------------------------------------------------------

    private bool _celestialMode = false;

    private Vector3 _celestialPosition = Vector3.Zero;
    private Vector3 _celestialSize = Vector3.One;
    private Vector3 _celestialRotation = Vector3.Zero;

    public override void _Ready()
    {
        // Build the normal 1x1x1 block overlay.
        _mesh = new MeshInstance3D();

        var boxMesh = new BoxMesh();
        boxMesh.Size = new Vector3(
            1.002f,
            1.002f,
            1.002f
        );

        _mesh.Mesh = boxMesh;

        _mat = new StandardMaterial3D();

        _mat.Transparency =
            BaseMaterial3D.TransparencyEnum.Alpha;

        _mat.ShadingMode =
            BaseMaterial3D.ShadingModeEnum.Unshaded;

        _mat.CullMode =
            BaseMaterial3D.CullModeEnum.Disabled;

        _mat.DepthDrawMode =
            BaseMaterial3D.DepthDrawModeEnum.Disabled;

        _mat.NoDepthTest = false;

        _mat.RenderPriority = 1;

        _mat.TextureFilter =
            BaseMaterial3D.TextureFilterEnum.Nearest;

        // Make the same texture appear correctly on all
        // six faces of the BoxMesh.
        _mat.Uv1Scale =
            new Vector3(3f, 2f, 1f);

        _mesh.MaterialOverride = _mat;

        _mesh.Visible = false;

        AddChild(_mesh);

        // -----------------------------------------------------
        // LOAD BREAK TEXTURES
        // -----------------------------------------------------

        for (int i = 0; i < TotalStages; i++)
        {
            string path =
                $"res://Assets/Textures/Break/break_{i + 1}.png";

            if (ResourceLoader.Exists(path))
            {
                _stageTextures[i] =
                    ResourceLoader.Load<Texture2D>(path);
            }
            else
            {
                GD.PrintErr(
                    $"BlockBreakOverlay: missing texture {path}"
                );
            }
        }
    }

    // ---------------------------------------------------------
    // NORMAL BLOCK POSITION
    // ---------------------------------------------------------

    public void SyncPosition(Vector3I blockWorldPos)
    {
        // Celestial overlays are positioned from their actual
        // collision body's GlobalPosition instead.
        if (_celestialMode)
            return;

        GlobalPosition = new Vector3(
            blockWorldPos.X + 0.5f,
            blockWorldPos.Y + 0.5f,
            blockWorldPos.Z + 0.5f
        );
    }

    // ---------------------------------------------------------
    // CELESTIAL MODE
    // ---------------------------------------------------------

    public void SetCelestialMode(
        Vector3 worldPosition,
        Vector3 size,
        Vector3 rotation)
    {
        _celestialMode = true;

        _celestialPosition = worldPosition;
        _celestialSize = size;
        _celestialRotation = rotation;

        // IMPORTANT:
        // Update the position every time this method is called.
        // This keeps the crack attached to the moving Sun/Moon.
        GlobalPosition = _celestialPosition;

        _mesh.Scale = _celestialSize;
        _mesh.Rotation = _celestialRotation;
    }

    // ---------------------------------------------------------
    // BREAK UPDATE
    // ---------------------------------------------------------

    public bool UpdateBreak(
        Vector3I blockWorldPos,
        int hitCount,
        int miningPower)
    {
        if (miningPower < 1)
            miningPower = 1;

        int pixelsProgressed =
            Mathf.Min(
                hitCount * miningPower,
                TotalStages
            );

        int stage =
            Mathf.Clamp(
                pixelsProgressed - 1,
                0,
                TotalStages - 1
            );

        // Normal blocks use their voxel position.
        // Celestial bodies keep the position supplied by
        // SetCelestialMode().
        SyncPosition(blockWorldPos);

        // Update texture if stage changed or target changed.
        if (
            stage != _currentStage ||
            _targetBlock != blockWorldPos
        )
        {
            _currentStage = stage;
            _targetBlock = blockWorldPos;

            if (_stageTextures[stage] != null)
            {
                _mat.AlbedoTexture =
                    _stageTextures[stage];
            }

            _mat.AlbedoColor =
                new Color(
                    1f,
                    1f,
                    1f,
                    0.75f
                );

            _mesh.Visible = true;
        }

        return pixelsProgressed >= TotalStages;
    }

    // ---------------------------------------------------------
    // HIDE
    // ---------------------------------------------------------

    public void HideOverlay()
    {
        _mesh.Visible = false;

        _currentStage = -1;

        _targetBlock =
            new Vector3I(
                int.MinValue,
                0,
                0
            );

        // Return to normal block mode.
        _celestialMode = false;

        _celestialPosition = Vector3.Zero;

        _celestialSize = Vector3.One;

        _celestialRotation = Vector3.Zero;

        _mesh.Scale = Vector3.One;

        _mesh.Rotation = Vector3.Zero;
    }

    // ---------------------------------------------------------
    // RESET
    // ---------------------------------------------------------

    public void ResetTarget()
    {
        HideOverlay();
    }
}