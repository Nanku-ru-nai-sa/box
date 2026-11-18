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
    private Vector3 _currentShapeSize = Vector3.One;
    private bool _usingShapeMesh = false;
    private BoxMesh _fullBlockMesh;

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

        _fullBlockMesh = new BoxMesh();
        _fullBlockMesh.Size = new Vector3(1.002f, 1.002f, 1.002f);
        _mesh.Mesh = _fullBlockMesh;

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
        int miningPower,
        BlockResource blockResource = null)
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

        // Custom shapes use a fitted mesh with cropped UVs.
        // This keeps the crack texture's pixel size unchanged:
        // narrow stalks show only the matching narrow part of the
        // 16x16 crack texture instead of stretching all 16 pixels.
        if (!_celestialMode && blockResource != null &&
            blockResource.IsCustomShape)
        {
            Vector3 shapeSize = blockResource.CustomShapeSize;
            shapeSize.X = Mathf.Clamp(shapeSize.X, 0.001f, 1f);
            shapeSize.Y = Mathf.Clamp(shapeSize.Y, 0.001f, 1f);
            shapeSize.Z = Mathf.Clamp(shapeSize.Z, 0.001f, 1f);

            if (!_usingShapeMesh || _currentShapeSize != shapeSize)
            {
                _mesh.Mesh = BuildShapeOverlayMesh(shapeSize);
                _currentShapeSize = shapeSize;
                _usingShapeMesh = true;
            }

            _mesh.Scale = Vector3.One;
            _mesh.Position = new Vector3(0f, (shapeSize.Y - 1f) * 0.5f, 0f);
        }
        else if (!_celestialMode)
        {
            if (_usingShapeMesh)
            {
                _mesh.Mesh = _fullBlockMesh;
                _usingShapeMesh = false;
                _currentShapeSize = Vector3.One;
            }

            _mesh.Scale = Vector3.One;
            _mesh.Position = Vector3.Zero;
        }

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

    private ArrayMesh BuildShapeOverlayMesh(Vector3 size)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        float hx = size.X * 0.5f;
        float hy = size.Y * 0.5f;
        float hz = size.Z * 0.5f;

        float uX0 = (1f - size.X) * 0.5f;
        float uX1 = 1f - uX0;
        float uZ0 = (1f - size.Z) * 0.5f;
        float uZ1 = 1f - uZ0;

        // Top and bottom crop in both horizontal dimensions.
        AddOverlayQuad(surface,
            new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz),
            new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz),
            uX0, uX1, uZ0, uZ1);
        AddOverlayQuad(surface,
            new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz),
            new Vector3(hx, -hy, -hz), new Vector3(-hx, -hy, -hz),
            uX0, uX1, uZ0, uZ1);

        // X-facing sides crop the texture width to the block's X size.
        AddOverlayQuad(surface,
            new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz),
            new Vector3(hx, hy, -hz), new Vector3(-hx, hy, -hz),
            uX0, uX1, 0f, 1f);
        AddOverlayQuad(surface,
            new Vector3(hx, -hy, hz), new Vector3(-hx, -hy, hz),
            new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz),
            uX0, uX1, 0f, 1f);

        // Z-facing sides crop the texture width to the block's Z size.
        AddOverlayQuad(surface,
            new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz),
            new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz),
            uZ0, uZ1, 0f, 1f);
        AddOverlayQuad(surface,
            new Vector3(-hx, -hy, hz), new Vector3(-hx, -hy, -hz),
            new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz),
            uZ0, uZ1, 0f, 1f);

        surface.GenerateNormals();
        return surface.Commit();
    }

    private static void AddOverlayQuad(
        SurfaceTool surface,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        float uMin, float uMax, float vMin, float vMax)
    {
        surface.SetUV(new Vector2(uMin, vMax)); surface.AddVertex(a);
        surface.SetUV(new Vector2(uMax, vMax)); surface.AddVertex(b);
        surface.SetUV(new Vector2(uMax, vMin)); surface.AddVertex(c);

        surface.SetUV(new Vector2(uMin, vMax)); surface.AddVertex(a);
        surface.SetUV(new Vector2(uMax, vMin)); surface.AddVertex(c);
        surface.SetUV(new Vector2(uMin, vMin)); surface.AddVertex(d);
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
        _mesh.Position = Vector3.Zero;
        if (_usingShapeMesh)
        {
            _mesh.Mesh = _fullBlockMesh;
            _usingShapeMesh = false;
            _currentShapeSize = Vector3.One;
        }

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