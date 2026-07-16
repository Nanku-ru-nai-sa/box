using Godot;

// BlockBreakOverlay — attach to Player or instantiate from Player._Ready().
// Manages the semi-transparent crack mesh that appears on the block being broken.
// Break textures go in:  res://Assets/Textures/Break/break_0.png  through  break_5.png

public partial class BlockBreakOverlay : Node3D
{
    public const int TotalStages = 6; // number of break PNGs (break_0 through break_5)

    private MeshInstance3D  _mesh;
    private StandardMaterial3D _mat;
    private Texture2D[]     _stageTextures = new Texture2D[TotalStages];

    private Vector3I _targetBlock = new Vector3I(int.MinValue, 0, 0); // currently targeted block
    private int      _currentStage = -1; // -1 = hidden

    public override void _Ready()
    {
        // Build a unit cube mesh slightly larger than a block so it sits on top
        _mesh = new MeshInstance3D();
        var boxMesh = new BoxMesh();
        boxMesh.Size = new Vector3(1.002f, 1.002f, 1.002f);
        _mesh.Mesh   = boxMesh;

        _mat = new StandardMaterial3D();
        _mat.Transparency         = BaseMaterial3D.TransparencyEnum.Alpha;
        _mat.ShadingMode          = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _mat.CullMode             = BaseMaterial3D.CullModeEnum.Disabled;
        _mat.DepthDrawMode        = BaseMaterial3D.DepthDrawModeEnum.Disabled;
        _mat.NoDepthTest          = false;
        _mat.RenderPriority       = 1;
        _mat.TextureFilter        = BaseMaterial3D.TextureFilterEnum.Nearest;
        // BoxMesh packs its 6 faces into a single 3x2 UV grid rather than
        // giving each face its own 0-1 UV space - so without correcting for
        // it, each face shows a different slice of the crack texture, which
        // is what made the crack look shifted differently depending on
        // which face of the block you were looking at. Vector3(3,2,1) is
        // Godot's own documented fix to make the same texture appear
        // identically on all faces.
        _mat.Uv1Scale             = new Vector3(3f, 2f, 1f);
        _mesh.MaterialOverride    = _mat;
        _mesh.Visible             = false;

        AddChild(_mesh);

        // Pre-load all break textures
        for (int i = 0; i < TotalStages; i++)
        {
            string path = $"res://Assets/Textures/Break/break_{i}.png";
            if (ResourceLoader.Exists(path))
                _stageTextures[i] = ResourceLoader.Load<Texture2D>(path);
            else
                GD.PrintErr($"BlockBreakOverlay: missing texture {path}");
        }
    }

    // Keeps the overlay glued to whatever block is currently under the
    // crosshair, independent of break-stage progress. Call this every
    // frame while breaking is active (e.g. from Player._PhysicsProcess),
    // so the overlay never lags behind camera movement between swings.
    // Without this, the overlay only moved once per swing interval, which
    // is what made it look "offset" whenever you shifted your aim slightly
    // in between hits.
    public void SyncPosition(Vector3I blockWorldPos)
    {
        GlobalPosition = new Vector3(
            blockWorldPos.X + 0.5f,
            blockWorldPos.Y + 0.5f,
            blockWorldPos.Z + 0.5f
        );
    }

    // Call this when the player hits a block — returns true if the block should break
    // blockWorldPos: integer world position of the block being hit
    // stage: current hit count (0-based)
    // totalHits: total hits needed to break
    public bool UpdateBreak(Vector3I blockWorldPos, int hitCount, int totalHits)
    {
        // Map hit count to stage index
        // hit 1 of 6 → stage 0, hit 6 of 6 → stage 5
        int stage = Mathf.Clamp(
            Mathf.FloorToInt((float)(hitCount - 1) / totalHits * TotalStages),
            0, TotalStages - 1
        );

        // Move overlay to block position (set on the Node3D itself, not the mesh child)
        SyncPosition(blockWorldPos);

        // Update texture if stage changed
        if (stage != _currentStage || _targetBlock != blockWorldPos)
        {
            _currentStage = stage;
            _targetBlock  = blockWorldPos;
            if (_stageTextures[stage] != null)
                _mat.AlbedoTexture = _stageTextures[stage];
            _mat.AlbedoColor = new Color(1f, 1f, 1f, 0.75f);
            _mesh.Visible    = true;
        }

        // Return true when fully broken
        return hitCount >= totalHits;
    }

    // Hide the overlay (called when player stops hitting or switches block)
    public void HideOverlay()
    {
        _mesh.Visible  = false;
        _currentStage  = -1;
        _targetBlock   = new Vector3I(int.MinValue, 0, 0);
    }

    // Reset targeting to a new block (clears hit progress)
    public void ResetTarget()
    {
        HideOverlay();
    }
}