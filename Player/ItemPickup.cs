using Godot;
using System;

// A physical item drop that appears in the world when a block is broken,
// or when the player manually drops an item with Q.
//
// Real (approximated) physics: pops out with velocity, falls under gravity,
// bounces a little on impact, comes to rest — no Minecraft-style idle
// bob/spin once it's settled.
//
// Two visual styles:
//
//   - Block items render as a small 3D cube using the block's texture.
//   - Everything else renders as a small textured pickup.
//
// This does NOT use Godot's physics engine. It manually checks world block
// data through ChunkManager instead.
public partial class ItemPickup : Node3D
{
    // Set these fields right after creating the node, before adding it to
    // the scene tree. See Player.SpawnItemDrop().
    public string ItemId = "";
    public int Count = 1;

    // If set, used as the initial velocity instead of the random
    // "popped out of a broken block" toss.
    public Vector3? TossVelocity = null;


    // ============================================================
    // GAMEPLAY TUNING
    // ============================================================

    private const float PickupRadius = 1.0f;
    private const float MagnetRadius = 2.0f;
    private const float MagnetSpeed = 7f;

    private const float PickupDelay = 0.5f;
    private const float DespawnSeconds = 300f;


    // ============================================================
    // PHYSICS FEEL
    // ============================================================

    private const float Gravity = 18f;

    private const float PopHorizontalMin = 0.4f;
    private const float PopHorizontalMax = 1.0f;

    private const float PopVerticalMin = 1.6f;
    private const float PopVerticalMax = 2.4f;

    private const float Bounciness = 0.35f;
    private const float BounceSettleSpeed = 0.8f;

    private const float AirDrag = 2.2f;


    // ============================================================
    // SPIN
    // ============================================================

    private const float TwistMin = 4f;
    private const float TwistMax = 7f;
    private const float TwistDrag = 3f;


    // ============================================================
    // TEXTURE PATHS
    // ============================================================

    private const string BlockTexturePath =
        "res://Assets/Textures/Blocks/{0}.png";

    private const string ItemTexturePath =
        "res://Assets/Textures/Items/{0}.png";

    // IMPORTANT:
    // Your ore drops live here:
    //
    // res://Assets/Textures/Items/ore/moon_shard.png
    // res://Assets/Textures/Items/ore/sun_shard.png
    //
    private const string OreTexturePath =
        "res://Assets/Textures/Items/ore/{0}.png";

    // Kept as a fallback in case you later put other celestial textures here.
    private const string CelestialTexturePath =
        "res://Assets/Textures/Celestial/{0}.png";


    // ============================================================
    // VISUAL SETTINGS
    // ============================================================

    private const int CropPixels = 4;


    // ============================================================
    // INTERNAL STATE
    // ============================================================

    private MeshInstance3D _visual;

    private float _dropSize;
    private float _rollRadius;

    private Vector3 _velocity;
    private Vector3 _angularVelocity;

    private float _age;

    private bool _settled;
    private bool _collected;
    private bool _isBlock;

    private float _restHeight;

    private Player _player;


    // ============================================================
    // READY
    // ============================================================

    public override void _Ready()
    {
        // --------------------------------------------------------
        // Build all possible texture paths.
        // --------------------------------------------------------

        string blockPath =
            string.Format(
                BlockTexturePath,
                ItemId
            );

        string itemPath =
            string.Format(
                ItemTexturePath,
                ItemId
            );

        string orePath =
            string.Format(
                OreTexturePath,
                ItemId
            );

        string celestialPath =
            string.Format(
                CelestialTexturePath,
                ItemId
            );


        Texture2D sourceTexture = null;


        // --------------------------------------------------------
        // 1. BLOCK
        // --------------------------------------------------------

        if (ResourceLoader.Exists(blockPath))
        {
            _isBlock = true;

            sourceTexture =
                ResourceLoader.Load<Texture2D>(
                    blockPath
                );

            GD.Print(
                $"[ItemPickup] Loaded block texture: {blockPath}"
            );
        }


        // --------------------------------------------------------
        // 2. ORE
        //
        // This MUST be checked before normal Items because your
        // celestial drops are stored in Items/ore/.
        // --------------------------------------------------------

        else if (ResourceLoader.Exists(orePath))
        {
            _isBlock = false;

            sourceTexture =
                ResourceLoader.Load<Texture2D>(
                    orePath
                );

            GD.Print(
                $"[ItemPickup] Loaded ore texture: {orePath}"
            );
        }


        // --------------------------------------------------------
        // 3. NORMAL ITEM
        // --------------------------------------------------------

        else if (ResourceLoader.Exists(itemPath))
        {
            _isBlock = false;

            sourceTexture =
                ResourceLoader.Load<Texture2D>(
                    itemPath
                );

            GD.Print(
                $"[ItemPickup] Loaded item texture: {itemPath}"
            );
        }


        // --------------------------------------------------------
        // 4. CELESTIAL FALLBACK
        // --------------------------------------------------------

        else if (ResourceLoader.Exists(celestialPath))
        {
            _isBlock = false;

            sourceTexture =
                ResourceLoader.Load<Texture2D>(
                    celestialPath
                );

            GD.Print(
                $"[ItemPickup] Loaded celestial texture: {celestialPath}"
            );
        }


        // --------------------------------------------------------
        // 5. NOTHING FOUND
        // --------------------------------------------------------

        else
        {
            _isBlock = false;
            sourceTexture = null;

            GD.PrintErr(
                $"[ItemPickup] Could not find texture for item '{ItemId}'."
            );

            GD.PrintErr(
                $"[ItemPickup] Checked:"
            );

            GD.PrintErr(
                $"  {blockPath}"
            );

            GD.PrintErr(
                $"  {orePath}"
            );

            GD.PrintErr(
                $"  {itemPath}"
            );

            GD.PrintErr(
                $"  {celestialPath}"
            );
        }


        // --------------------------------------------------------
        // CROP TEXTURE
        // --------------------------------------------------------

        Texture2D croppedTexture =
            CropCenter(
                sourceTexture,
                CropPixels
            );


        // --------------------------------------------------------
        // WORK OUT PICKUP SIZE
        // --------------------------------------------------------

        int sourceResolution =
            sourceTexture != null
                ? sourceTexture.GetWidth()
                : 16;

        float pixelSize =
            1f /
            Mathf.Max(
                1,
                sourceResolution
            );

        _dropSize =
            CropPixels *
            pixelSize;

        _rollRadius =
            _dropSize *
            0.5f;


        // --------------------------------------------------------
        // MATERIAL
        // --------------------------------------------------------

        var mat =
            new StandardMaterial3D();

        mat.AlbedoTexture =
            croppedTexture;

        mat.TextureFilter =
            BaseMaterial3D.TextureFilterEnum.Nearest;


        // Non-block items use transparency.
        if (!_isBlock)
        {
            mat.Transparency =
                BaseMaterial3D.TransparencyEnum.AlphaScissor;

            mat.AlphaScissorThreshold =
                0.5f;
        }


        // --------------------------------------------------------
        // CREATE VISUAL
        // --------------------------------------------------------

        Mesh cubeMesh =
            BuildUniformFaceCube(
                _dropSize
            );

        _visual =
            new MeshInstance3D();

        _visual.Mesh =
            cubeMesh;

        _restHeight =
            _dropSize *
            0.5f;

        _visual.MaterialOverride =
            mat;

        _visual.Position =
            new Vector3(
                0,
                _restHeight,
                0
            );

        AddChild(_visual);


        // --------------------------------------------------------
        // RANDOMIZATION
        // --------------------------------------------------------

        var rng =
            new RandomNumberGenerator();

        rng.Randomize();


        if (!_isBlock)
        {
            _visual.RotationDegrees =
                new Vector3(
                    0,
                    rng.RandfRange(
                        0f,
                        360f
                    ),
                    0
                );
        }


        // --------------------------------------------------------
        // INITIAL VELOCITY
        // --------------------------------------------------------

        if (TossVelocity.HasValue)
        {
            _velocity =
                TossVelocity.Value;
        }
        else
        {
            float angle =
                rng.RandfRange(
                    0f,
                    Mathf.Tau
                );

            float hSpeed =
                rng.RandfRange(
                    PopHorizontalMin,
                    PopHorizontalMax
                );

            _velocity =
                new Vector3(
                    Mathf.Cos(angle) * hSpeed,
                    rng.RandfRange(
                        PopVerticalMin,
                        PopVerticalMax
                    ),
                    Mathf.Sin(angle) * hSpeed
                );
        }


        // --------------------------------------------------------
        // INITIAL TWIST
        // --------------------------------------------------------

        Vector3 twistAxis =
            new Vector3(
                rng.RandfRange(
                    -1f,
                    1f
                ),
                rng.RandfRange(
                    -1f,
                    1f
                ),
                rng.RandfRange(
                    -1f,
                    1f
                )
            ).Normalized();

        _angularVelocity =
            twistAxis *
            rng.RandfRange(
                TwistMin,
                TwistMax
            );
    }


    // ============================================================
    // UNIFORM TEXTURED CUBE
    // ============================================================

    private Mesh BuildUniformFaceCube(float size)
    {
        var st =
            new SurfaceTool();

        st.Begin(
            Mesh.PrimitiveType.Triangles
        );

        float h =
            size /
            2f;


        AddFace(
            st,
            new Vector3(h, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 0, -1),
            new Vector3(0, 1, 0),
            size
        );

        AddFace(
            st,
            new Vector3(-h, 0, 0),
            new Vector3(-1, 0, 0),
            new Vector3(0, 0, 1),
            new Vector3(0, 1, 0),
            size
        );

        AddFace(
            st,
            new Vector3(0, h, 0),
            new Vector3(0, 1, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 0, -1),
            size
        );

        AddFace(
            st,
            new Vector3(0, -h, 0),
            new Vector3(0, -1, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 0, 1),
            size
        );

        AddFace(
            st,
            new Vector3(0, 0, h),
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            size
        );

        AddFace(
            st,
            new Vector3(0, 0, -h),
            new Vector3(0, 0, -1),
            new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0),
            size
        );

        return st.Commit();
    }


    // ============================================================
    // ADD TEXTURED FACE
    // ============================================================

    private void AddFace(
        SurfaceTool st,
        Vector3 center,
        Vector3 normal,
        Vector3 right,
        Vector3 up,
        float size)
    {
        Vector3 r =
            right *
            (size / 2f);

        Vector3 u =
            up *
            (size / 2f);

        Vector3 p0 =
            center -
            r -
            u;

        Vector3 p1 =
            center +
            r -
            u;

        Vector3 p2 =
            center +
            r +
            u;

        Vector3 p3 =
            center -
            r +
            u;


        st.SetNormal(normal);
        st.SetUV(new Vector2(0, 1));
        st.AddVertex(p0);

        st.SetNormal(normal);
        st.SetUV(new Vector2(1, 0));
        st.AddVertex(p2);

        st.SetNormal(normal);
        st.SetUV(new Vector2(1, 1));
        st.AddVertex(p1);


        st.SetNormal(normal);
        st.SetUV(new Vector2(0, 1));
        st.AddVertex(p0);

        st.SetNormal(normal);
        st.SetUV(new Vector2(0, 0));
        st.AddVertex(p3);

        st.SetNormal(normal);
        st.SetUV(new Vector2(1, 0));
        st.AddVertex(p2);
    }


    // ============================================================
    // CROP CENTER OF TEXTURE
    // ============================================================

    private Texture2D CropCenter(
        Texture2D source,
        int size)
    {
        if (source == null)
            return null;

        Image img =
            source.GetImage();

        img.Convert(
            Image.Format.Rgba8
        );

        int w =
            img.GetWidth();

        int h =
            img.GetHeight();

        int cropW =
            Mathf.Min(
                size,
                w
            );

        int cropH =
            Mathf.Min(
                size,
                h
            );

        int startX =
            Mathf.Clamp(
                (w - cropW) / 2,
                0,
                Mathf.Max(
                    0,
                    w - cropW
                )
            );

        int startY =
            Mathf.Clamp(
                (h - cropH) / 2,
                0,
                Mathf.Max(
                    0,
                    h - cropH
                )
            );

        Image cropped =
            img.GetRegion(
                new Rect2I(
                    startX,
                    startY,
                    cropW,
                    cropH
                )
            );

        return ImageTexture.CreateFromImage(
            cropped
        );
    }


    // ============================================================
    // PHYSICS
    // ============================================================

    public override void _PhysicsProcess(
        double delta)
    {
        if (_collected)
            return;

        float dt =
            (float)delta;

        _age += dt;


        if (_age >= DespawnSeconds)
        {
            QueueFree();
            return;
        }


        if (!_settled)
        {
            var cm =
                GetTree()
                    .Root
                    .FindChild(
                        "ChunkManager",
                        true,
                        false
                    ) as ChunkManager;


            _velocity.Y -=
                Gravity *
                dt;


            float dragMul =
                Mathf.Max(
                    0f,
                    1f -
                    AirDrag *
                    dt
                );

            _velocity.X *=
                dragMul;

            _velocity.Z *=
                dragMul;


            Vector3 nextPos =
                GlobalPosition +
                _velocity *
                dt;


            if (cm != null)
            {
                var belowCoord =
                    new Vector3I(
                        Mathf.FloorToInt(
                            nextPos.X
                        ),
                        Mathf.FloorToInt(
                            nextPos.Y -
                            _restHeight
                        ),
                        Mathf.FloorToInt(
                            nextPos.Z
                        )
                    );

                var below =
                    cm.GetBlockAtWorld(
                        belowCoord
                    );


                if (!below.IsAir())
                {
                    nextPos.Y =
                        belowCoord.Y +
                        1f +
                        _restHeight;


                    if (
                        Mathf.Abs(
                            _velocity.Y
                        ) >
                        BounceSettleSpeed
                    )
                    {
                        _velocity.Y =
                            -_velocity.Y *
                            Bounciness;
                    }
                    else
                    {
                        _velocity =
                            Vector3.Zero;

                        _angularVelocity =
                            Vector3.Zero;

                        _settled =
                            true;
                    }
                }
            }


            GlobalPosition =
                nextPos;


            // ----------------------------------------------------
            // BLOCK ROLL
            // ----------------------------------------------------

            if (_isBlock)
            {
                Vector3 horizVel =
                    new Vector3(
                        _velocity.X,
                        0f,
                        _velocity.Z
                    );

                float hSpeed =
                    horizVel.Length();


                if (hSpeed > 0.01f)
                {
                    Vector3 axis =
                        Vector3.Up
                            .Cross(
                                horizVel
                            )
                            .Normalized();

                    float rollAngle =
                        (
                            hSpeed /
                            _rollRadius
                        ) *
                        dt;

                    _visual.Basis =
                        new Basis(
                            axis,
                            rollAngle
                        ) *
                        _visual.Basis;
                }
            }


            // ----------------------------------------------------
            // RANDOM TWIST
            // ----------------------------------------------------

            float twistSpeed =
                _angularVelocity.Length();


            if (twistSpeed > 0.01f)
            {
                _visual.Basis =
                    new Basis(
                        _angularVelocity.Normalized(),
                        twistSpeed * dt
                    ) *
                    _visual.Basis;


                _angularVelocity *=
                    Mathf.Max(
                        0f,
                        1f -
                        TwistDrag *
                        dt
                    );
            }
        }


        // ========================================================
        // PICKUP
        // ========================================================

        if (_age < PickupDelay)
            return;


        if (
            _player == null ||
            !IsInstanceValid(_player)
        )
        {
            _player =
                GetTree()
                    .GetFirstNodeInGroup(
                        "player"
                    ) as Player;
        }


        if (_player == null)
            return;


        float dist =
            GlobalPosition.DistanceTo(
                _player.GlobalPosition
            );


        if (dist <= PickupRadius)
        {
            Collect();
        }
        else if (dist <= MagnetRadius)
        {
            Vector3 toPlayer =
                (
                    _player.GlobalPosition +
                    new Vector3(
                        0,
                        0.6f,
                        0
                    ) -
                    GlobalPosition
                ).Normalized();


            GlobalPosition +=
                toPlayer *
                MagnetSpeed *
                dt;

            _settled = false;
        }
    }


    // ============================================================
    // COLLECT
    // ============================================================

    private void Collect()
    {
        if (
            _collected ||
            _player == null
        )
            return;


        _collected =
            true;


        int leftover =
            _player.CollectPickup(
                ItemId,
                Count
            );


        if (leftover > 0)
        {
            Count =
                leftover;

            _collected =
                false;

            _age =
                PickupDelay;

            return;
        }


        QueueFree();
    }
}