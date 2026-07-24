using Godot;
using System;

// A physical item drop that appears in the world when a block is broken,
// or when the player manually drops an item with Q.
//
// Real (approximated) physics: pops out with velocity, falls under gravity,
// bounces a little on impact, comes to rest — no Minecraft-style idle
// bob/spin once it's settled.
//
// Two visual styles, both built from just the CENTER 4x4 PIXELS of the
// relevant texture (kept tiny/chunky on purpose):
//   - Block items render as a small 3D cube using the block's texture,
//     and roll/tumble realistically while moving.
//   - Everything else renders as a thin flat "chip" (1 pixel thick) that
//     lies flat on the ground.
//
// This does NOT use Godot's physics engine (RigidBody3D/collision layers) —
// it manually checks world block data through ChunkManager instead, so it
// never needs to guess at your project's collision layer setup and can't
// accidentally push the player around.
public partial class ItemPickup : Node3D
{
    // Set these fields right after creating the node, before adding it to
    // the scene tree. See Player.SpawnItemDrop().
    public string ItemId = "";
    public int Count = 1;
    // If set, used as the initial velocity instead of the random "popped
    // out of a broken block" toss. Used for Q-drops (see Player.DropOneItem).
    public Vector3? TossVelocity = null;

    // ── Gameplay tuning ───────────────────────────────────────────────────
    private const float PickupRadius   = 1.0f;  // walk this close and it's collected instantly
    private const float MagnetRadius   = 2.0f;  // starts drifting toward the player from this far away
    private const float MagnetSpeed    = 7f;
    private const float PickupDelay    = 0.5f;  // can't be collected for this long after spawning
    private const float DespawnSeconds = 300f;  // 5 minutes sitting on the ground and it disappears

    // ── Physics feel ──────────────────────────────────────────────────────
    private const float Gravity          = 18f;
    private const float PopHorizontalMin = 0.4f;  // trimmed down slightly from before — a gentler pop
    private const float PopHorizontalMax = 1.0f;
    private const float PopVerticalMin   = 1.6f;
    private const float PopVerticalMax   = 2.4f;
    private const float Bounciness       = 0.35f; // fraction of vertical speed kept after a bounce
    private const float BounceSettleSpeed = 0.8f; // below this vertical speed, a bounce just stops instead
    private const float AirDrag          = 2.2f;  // horizontal speed lost per second while airborne

    // Independent little "flick" of spin on spawn, separate from the
    // rolling-from-velocity motion — decays away as it flies/settles.
    private const float TwistMin  = 4f;  // rad/s
    private const float TwistMax  = 7f;
    private const float TwistDrag = 3f;  // how fast the twist decays per second

    // ── Visual sizing ─────────────────────────────────────────────────────
    // IMPORTANT: adjust this path if your block textures live somewhere else.
    // Item icons are assumed to already exist at res://Assets/Textures/Items/{id}.png
    // (matching Player.GetItemIcon), so block textures are assumed to mirror
    // that convention one folder over.
    private const string BlockTexturePath = "res://Assets/Textures/Blocks/{0}.png";
    private const string ItemTexturePath  = "res://Assets/Textures/Items/{0}.png";

    private const int CropPixels = 4; // crop just the center 4x4 pixels of the texture

    private MeshInstance3D _visual;
    private float    _dropSize;   // computed in _Ready from the real texture resolution
    private float    _rollRadius;
    private Vector3  _velocity;
    private Vector3  _angularVelocity; // the independent "twist", in addition to rolling
    private float    _age;
    private bool     _settled;
    private bool     _collected;
    private bool     _isBlock;
    private float    _restHeight; // how high the visual's center sits above the ground contact point
    private Player   _player;

    public override void _Ready()
    {
        string blockPath = string.Format(BlockTexturePath, ItemId);
        Texture2D sourceTexture;

        if (ResourceLoader.Exists(blockPath))
        {
            _isBlock      = true;
            sourceTexture = ResourceLoader.Load<Texture2D>(blockPath);
        }
        else
        {
            _isBlock = false;
            string itemPath = string.Format(ItemTexturePath, ItemId);
            sourceTexture = ResourceLoader.Exists(itemPath) ? ResourceLoader.Load<Texture2D>(itemPath) : null;
        }

        Texture2D croppedTexture = CropCenter(sourceTexture, CropPixels);

        // Work out how big one texture pixel is in world units from the
        // REAL resolution of the source texture (assumed to span exactly
        // one world unit across a block face, same as the game's blocks).
        // This is what makes "4 pixels" actually mean 4 real texture
        // pixels, whether your art is 16x16, 32x32, or anything else.
        int sourceResolution = sourceTexture != null ? sourceTexture.GetWidth() : 16;
        float pixelSize = 1f / Mathf.Max(1, sourceResolution);
        _dropSize   = CropPixels * pixelSize;
        _rollRadius = _dropSize * 0.5f;

        var mat = new StandardMaterial3D();
        mat.AlbedoTexture = croppedTexture;
        mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        if (!_isBlock)
        {
            // Item icons usually have a transparent background — cut it out
            // rather than showing a solid quad.
            mat.Transparency          = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            mat.AlphaScissorThreshold = 0.5f;
        }

        Mesh cubeMesh = BuildUniformFaceCube(_dropSize);
        _visual = new MeshInstance3D();
        _visual.Mesh             = cubeMesh;
        _restHeight              = _dropSize * 0.5f;
        _visual.MaterialOverride = mat;
        _visual.Position         = new Vector3(0, _restHeight, 0);
        AddChild(_visual);

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        if (!_isBlock)
        {
            // One-time random facing so a pile of dropped items doesn't all
            // line up identically.
            _visual.RotationDegrees = new Vector3(0, rng.RandfRange(0f, 360f), 0);
        }

        if (TossVelocity.HasValue)
        {
            _velocity = TossVelocity.Value;
        }
        else
        {
            float angle  = rng.RandfRange(0f, Mathf.Tau);
            float hSpeed = rng.RandfRange(PopHorizontalMin, PopHorizontalMax);
            _velocity = new Vector3(
                Mathf.Cos(angle) * hSpeed,
                rng.RandfRange(PopVerticalMin, PopVerticalMax),
                Mathf.Sin(angle) * hSpeed);
        }

        // The extra little twist, independent of travel direction.
        Vector3 twistAxis = new Vector3(
            rng.RandfRange(-1f, 1f),
            rng.RandfRange(-1f, 1f),
            rng.RandfRange(-1f, 1f)).Normalized();
        _angularVelocity = twistAxis * rng.RandfRange(TwistMin, TwistMax);
    }

    // Builds a cube where every face gets the FULL texture, identically —
    // unlike Godot's built-in BoxMesh, which unwraps a single texture across
    // all 6 faces like a folded-out cube net (fine for a big detailed
    // texture, but wrong for our tiny uniform 4x4 crop).
    private Mesh BuildUniformFaceCube(float size)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        float h = size / 2f;

        AddFace(st, new Vector3(h, 0, 0),  new Vector3(1, 0, 0),  new Vector3(0, 0, -1), new Vector3(0, 1, 0), size); // +X
        AddFace(st, new Vector3(-h, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1),  new Vector3(0, 1, 0), size); // -X
        AddFace(st, new Vector3(0, h, 0),  new Vector3(0, 1, 0),  new Vector3(1, 0, 0),  new Vector3(0, 0, -1), size); // +Y (top)
        AddFace(st, new Vector3(0, -h, 0), new Vector3(0, -1, 0), new Vector3(1, 0, 0),  new Vector3(0, 0, 1), size);  // -Y (bottom)
        AddFace(st, new Vector3(0, 0, h),  new Vector3(0, 0, 1),  new Vector3(1, 0, 0),  new Vector3(0, 1, 0), size);  // +Z
        AddFace(st, new Vector3(0, 0, -h), new Vector3(0, 0, -1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), size);  // -Z

        return st.Commit();
    }

    // Adds one quad face, full 0..1 UV, centered at `center` facing `normal`.
    private void AddFace(SurfaceTool st, Vector3 center, Vector3 normal, Vector3 right, Vector3 up, float size)
    {
        Vector3 r = right * (size / 2f);
        Vector3 u = up * (size / 2f);
        Vector3 p0 = center - r - u;
        Vector3 p1 = center + r - u;
        Vector3 p2 = center + r + u;
        Vector3 p3 = center - r + u;

        // Godot uses CLOCKWISE winding (as viewed from the normal side) for
        // front faces — this order is deliberately reversed from the more
        // "textbook" CCW/OpenGL convention to match that.
        st.SetNormal(normal); st.SetUV(new Vector2(0, 1)); st.AddVertex(p0);
        st.SetNormal(normal); st.SetUV(new Vector2(1, 0)); st.AddVertex(p2);
        st.SetNormal(normal); st.SetUV(new Vector2(1, 1)); st.AddVertex(p1);

        st.SetNormal(normal); st.SetUV(new Vector2(0, 1)); st.AddVertex(p0);
        st.SetNormal(normal); st.SetUV(new Vector2(0, 0)); st.AddVertex(p3);
        st.SetNormal(normal); st.SetUV(new Vector2(1, 0)); st.AddVertex(p2);
    }

    // Loads the source texture's pixel data and returns a new texture
    // containing only the center NxN pixels (clamped if the source is
    // smaller than that).
    private Texture2D CropCenter(Texture2D source, int size)
    {
        if (source == null) return null;

        Image img = source.GetImage();
        img.Convert(Image.Format.Rgba8);

        int w = img.GetWidth();
        int h = img.GetHeight();
        int cropW = Mathf.Min(size, w);
        int cropH = Mathf.Min(size, h);
        int startX = Mathf.Clamp((w - cropW) / 2, 0, Mathf.Max(0, w - cropW));
        int startY = Mathf.Clamp((h - cropH) / 2, 0, Mathf.Max(0, h - cropH));

        Image cropped = img.GetRegion(new Rect2I(startX, startY, cropW, cropH));
        return ImageTexture.CreateFromImage(cropped);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_collected) return;
        float dt = (float)delta;
        _age += dt;

        if (_age >= DespawnSeconds) { QueueFree(); return; }

        if (!_settled)
        {
            var cm = GetTree().Root.FindChild("ChunkManager", true, false) as ChunkManager;

            _velocity.Y -= Gravity * dt;

            float dragMul = Mathf.Max(0f, 1f - AirDrag * dt);
            _velocity.X *= dragMul;
            _velocity.Z *= dragMul;

            Vector3 nextPos = GlobalPosition + _velocity * dt;

            if (cm != null)
            {
                var belowCoord = new Vector3I(
                    Mathf.FloorToInt(nextPos.X),
                    Mathf.FloorToInt(nextPos.Y - _restHeight),
                    Mathf.FloorToInt(nextPos.Z));
                var below = cm.GetBlockAtWorld(belowCoord);
                if (!below.IsAir())
                {
                    nextPos.Y = belowCoord.Y + 1f + _restHeight;

                    if (Mathf.Abs(_velocity.Y) > BounceSettleSpeed)
                    {
                        _velocity.Y = -_velocity.Y * Bounciness;
                    }
                    else
                    {
                        _velocity        = Vector3.Zero;
                        _angularVelocity = Vector3.Zero;
                        _settled         = true;
                    }
                }
            }

            GlobalPosition = nextPos;

            // Blocks roll like a real cube based on how fast they're moving...
            if (_isBlock)
            {
                Vector3 horizVel = new Vector3(_velocity.X, 0f, _velocity.Z);
                float hSpeed = horizVel.Length();
                if (hSpeed > 0.01f)
                {
                    Vector3 axis = Vector3.Up.Cross(horizVel).Normalized();
                    float rollAngle = (hSpeed / _rollRadius) * dt;
                    _visual.Basis = new Basis(axis, rollAngle) * _visual.Basis;
                }
            }

            // ...and everything gets a bit of independent twist on top,
            // fading out as it flies.
            float twistSpeed = _angularVelocity.Length();
            if (twistSpeed > 0.01f)
            {
                _visual.Basis = new Basis(_angularVelocity.Normalized(), twistSpeed * dt) * _visual.Basis;
                _angularVelocity *= Mathf.Max(0f, 1f - TwistDrag * dt);
            }
        }
        // Once settled, nothing moves and nothing rotates — it just sits there.

        if (_age < PickupDelay) return;

        if (_player == null || !IsInstanceValid(_player))
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null) return;

        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (dist <= PickupRadius)
        {
            Collect();
        }
        else if (dist <= MagnetRadius)
        {
            Vector3 toPlayer = (_player.GlobalPosition + new Vector3(0, 0.6f, 0) - GlobalPosition).Normalized();
            GlobalPosition += toPlayer * MagnetSpeed * dt;
            _settled = false; // let it drift freely toward the player instead of sticking to the floor
        }
    }

    private void Collect()
    {
        if (_collected || _player == null) return;
        _collected = true;
        int leftover = _player.CollectPickup(ItemId, Count);
        if (leftover > 0)
        {
            // Inventory's full — leave what didn't fit sitting on the ground
            // instead of deleting it, and allow it to be picked up again shortly.
            Count      = leftover;
            _collected = false;
            _age       = PickupDelay;
            return;
        }
        QueueFree();
    }
}