using Godot;
using System;

// A physical item drop that appears in the world when a block is broken.
// Real (approximated) physics: pops out with velocity, falls under gravity,
// bounces a little on impact like a real object, and comes to rest — no
// Minecraft-style idle bob/spin once it's settled.
//
// Two visual styles:
//   - Block items render as an actual small 3D cube using the block's
//     texture, and roll/tumble realistically while moving.
//   - Everything else renders as a thin flat "chip" (icon texture, 1 pixel
//     thick) that lies flat on the ground.
//
// This does NOT use Godot's physics engine (RigidBody3D/collision layers) —
// it manually checks world block data through ChunkManager instead, so it
// never needs to guess at your project's collision layer setup and can't
// accidentally push the player around.
public partial class ItemPickup : Node3D
{
    // Set these two fields right after creating the node, before adding it
    // to the scene tree. See Player.SpawnItemDrop().
    public string ItemId = "";
    public int Count = 1;

    // ── Gameplay tuning ───────────────────────────────────────────────────
    private const float PickupRadius   = 1.0f;  // walk this close and it's collected instantly
    private const float MagnetRadius   = 2.0f;  // starts drifting toward the player from this far away
    private const float MagnetSpeed    = 7f;
    private const float PickupDelay    = 0.5f;  // can't be collected for this long after spawning
    private const float DespawnSeconds = 300f;  // 5 minutes sitting on the ground and it disappears

    // ── Physics feel ──────────────────────────────────────────────────────
    private const float Gravity          = 18f;
    private const float PopHorizontalMin = 0.6f;  // gentle real-world "kicked out of the block" scatter
    private const float PopHorizontalMax = 1.4f;
    private const float PopVerticalMin   = 2.0f;
    private const float PopVerticalMax   = 3.0f;
    private const float Bounciness       = 0.35f; // fraction of vertical speed kept after a bounce
    private const float BounceSettleSpeed = 0.8f; // below this vertical speed, a bounce just stops instead
    private const float AirDrag          = 2.2f;  // horizontal speed lost per second while airborne

    // ── Visual sizing ─────────────────────────────────────────────────────
    // IMPORTANT: adjust this path if your block textures live somewhere else.
    // Item icons are assumed to already exist at res://Assets/Textures/Items/{id}.png
    // (matching Player.GetItemIcon), so block textures are assumed to mirror
    // that convention one folder over.
    private const string BlockTexturePath = "res://Assets/Textures/Blocks/{0}.png";
    private const string ItemTexturePath  = "res://Assets/Textures/Items/{0}.png";

    private const float BlockDropSize    = 0.4f;   // size of the little rolling block cube, in world units
    private const float PixelSize        = 0.024f; // world units per icon texture pixel (matches your earlier sprite scale)
    private const float FlatItemThickness = PixelSize; // "1 pixel thick", as requested
    private const float RollRadius       = BlockDropSize * 0.5f;

    private MeshInstance3D _visual;
    private Vector3  _velocity;
    private float    _age;
    private bool     _settled;
    private bool     _collected;
    private bool     _isBlock;
    private float    _restHeight; // how high the visual's center sits above the ground contact point
    private Player   _player;

    public override void _Ready()
    {
        string blockPath = string.Format(BlockTexturePath, ItemId);
        Texture2D texture;

        if (ResourceLoader.Exists(blockPath))
        {
            _isBlock = true;
            texture  = ResourceLoader.Load<Texture2D>(blockPath);
        }
        else
        {
            _isBlock = false;
            string itemPath = string.Format(ItemTexturePath, ItemId);
            texture = ResourceLoader.Exists(itemPath) ? ResourceLoader.Load<Texture2D>(itemPath) : null;
        }

        var mat = new StandardMaterial3D();
        mat.AlbedoTexture = texture;
        mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        if (!_isBlock)
        {
            // Item icons usually have a transparent background — cut it out
            // rather than showing a solid quad.
            mat.Transparency         = BaseMaterial3D.TransparencyEnum.AlphaScissor;
            mat.AlphaScissorThreshold = 0.5f;
            mat.CullMode             = BaseMaterial3D.CullModeEnum.Disabled;
        }

        var box = new BoxMesh();
        if (_isBlock)
        {
            box.Size    = new Vector3(BlockDropSize, BlockDropSize, BlockDropSize);
            _restHeight = BlockDropSize * 0.5f;
        }
        else
        {
            float w = texture != null ? texture.GetWidth()  * PixelSize : 0.3f;
            float d = texture != null ? texture.GetHeight() * PixelSize : 0.3f;
            box.Size    = new Vector3(w, FlatItemThickness, d);
            _restHeight = FlatItemThickness * 0.5f;
        }

        _visual = new MeshInstance3D();
        _visual.Mesh             = box;
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

        float angle  = rng.RandfRange(0f, Mathf.Tau);
        float hSpeed = rng.RandfRange(PopHorizontalMin, PopHorizontalMax);
        _velocity = new Vector3(
            Mathf.Cos(angle) * hSpeed,
            rng.RandfRange(PopVerticalMin, PopVerticalMax),
            Mathf.Sin(angle) * hSpeed);
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
                        _velocity = Vector3.Zero;
                        _settled  = true;
                    }
                }
            }

            GlobalPosition = nextPos;

            // Blocks roll like a real cube while they're moving; flat items
            // don't rotate in flight, they just land flat.
            if (_isBlock)
            {
                Vector3 horizVel = new Vector3(_velocity.X, 0f, _velocity.Z);
                float hSpeed = horizVel.Length();
                if (hSpeed > 0.01f)
                {
                    Vector3 axis = Vector3.Up.Cross(horizVel).Normalized();
                    float rollAngle = (hSpeed / RollRadius) * dt;
                    _visual.Basis = new Basis(axis, rollAngle) * _visual.Basis;
                }
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