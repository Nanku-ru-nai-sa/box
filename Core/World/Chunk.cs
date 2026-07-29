using Godot;
using System.Collections.Generic;

public partial class Chunk : Node3D
{
    public const int SIZE = 16;
    public const int HEIGHT = 16;

    private BlockState[,,] _blocks = new BlockState[SIZE, HEIGHT, SIZE];

    private MeshInstance3D _meshInstance;
    private MeshInstance3D _transparentMeshInstance;
    private StaticBody3D _collisionBody;

    public Vector3I ChunkPosition { get; private set; }
    public bool IsGenerated { get; private set; } = false;
    private ChunkManager _chunkManager; // set in Initialize - used for cross-chunk grass spread
    private bool _isDirty = false;
    private Dictionary<Vector3I, BlockState> _modifiedBlocks = new();
    private float _randomTickTimer = 0f;
    private const float RandomTickInterval = 0.03f; // ~33 ticks per second (was 0.05f / 20 per second)
    private const int RandomTicksPerInterval = 10; // samples per tick (was 3) - higher = faster grass spread, more CPU per chunk per tick

    // Rebuilding a chunk's mesh means re-triangulating its whole 16x16x16
    // volume from scratch - fine for occasional player edits, but systemic
    // tick-driven changes (grass spread, wet sand reacting near a
    // coastline) can flip several blocks in quick succession, and without
    // a limit that meant a full rebuild every single frame during a busy
    // stretch. This cooldown batches rapid changes into far fewer rebuilds.
    private float _meshRebuildCooldown = 0f;
    private const float MeshRebuildMinInterval = 0.1f; // at most ~10 rebuilds/sec per chunk

    private enum FaceDirection
    {
        Top, Bottom, North, South, East, West
    }

    // The 6 direct face neighbors (no diagonals) - used for wet-sand
    // checks instead of a full 26-neighbor 3x3x3 scan, since "touching"
    // only really needs to mean face-adjacent, and this is checked very
    // frequently (every sand/wet-sand random tick sample near any
    // coastline), so cutting 26 checks down to 6 meaningfully reduces cost.
    private static readonly Vector3I[] OrthogonalOffsets = new Vector3I[]
    {
        new Vector3I(1, 0, 0), new Vector3I(-1, 0, 0),
        new Vector3I(0, 1, 0), new Vector3I(0, -1, 0),
        new Vector3I(0, 0, 1), new Vector3I(0, 0, -1)
    };

public override void _Ready()
{
    _meshInstance = new MeshInstance3D();
    AddChild(_meshInstance);

    _transparentMeshInstance = new MeshInstance3D();
    AddChild(_transparentMeshInstance);
}

public void Initialize(Vector3I chunkPosition)
{
    ChunkPosition = chunkPosition;
    GlobalPosition = new Vector3(
        chunkPosition.X * SIZE,
        chunkPosition.Y * HEIGHT,
        chunkPosition.Z * SIZE
    );

    // Seed from chunk position (not fully random) so flower placement is
    // deterministic per-chunk rather than shifting on every reload, while
    // still varying naturally from one chunk to the next.
    _grassRng.Seed = (ulong)(chunkPosition.X * 486187739 ^ chunkPosition.Z * 1300719893 ^ chunkPosition.Y * 668265263);

    // Cached reference to the owning ChunkManager, used so grass spread
    // can reach into neighboring chunks across a chunk boundary. Chunk is
    // always added as a direct child of ChunkManager in LoadChunk, so the
    // parent is already set by the time Initialize runs.
    _chunkManager = GetParent() as ChunkManager;
}

    public BlockState GetBlock(int x, int y, int z)
    {
        if (!IsInBounds(x, y, z))
            return BlockState.Air;
        return _blocks[x, y, z];
    }

  public void SetBlock(int x, int y, int z, BlockState block)
{
    _blocks[x, y, z] = block;
    _isDirty = true;
    _modifiedBlocks[new Vector3I(x, y, z)] = block;

    // If a solid, opaque, full block was just placed directly above a
    // grass block, that grass loses its light/air and reverts to dirt -
    // mirrors how grass decays when covered in most voxel games.
    TryDecayGrassBelow(x, y, z, block);

    // Sand has gravity (like Minecraft) - wet_sand1/wet_sand2 deliberately
    // do not, since they're meant to behave like packed/stable ground.
    TryApplyGravity(x, y, z, block);
}

// Checks the block directly below (x, y-1, z). If it's grass and the
// block just placed at (x, y, z) is solid/opaque/full, converts that
// grass back to dirt. Only triggers from SetBlock (real player/gameplay
// actions), never from SetBlockInternal (world generation), so it won't
// interfere with normal terrain/tree/decoration placement during
// GenerateChunk.
private void TryDecayGrassBelow(int x, int y, int z, BlockState placedBlock)
{
    if (placedBlock.IsAir()) return; // breaking a block (placing air) can't cover anything

    BlockResource resource = BlockRegistry.Instance.GetBlock(placedBlock.BlockId);
    if (resource == null) return;
    if (resource.IsTransparent) return;              // glass/water etc. still let light through
    if (resource.IsCross || resource.IsFlatGround) return; // flowers/clovers/carpets don't block light
    if (!placedBlock.IsFullBlock()) return;           // chiseled/partial shapes may not fully cover the top

    int by = y - 1;
    if (!IsInBounds(x, by, z)) return; // below this chunk's bottom slice - not handled cross-chunk

    BlockState below = _blocks[x, by, z];
    if (below.BlockId != "grass_block") return;

    var dirtBlock = new BlockState { BlockId = "dirt", BitMask = 0xFF };
    _blocks[x, by, z] = dirtBlock;
    _modifiedBlocks[new Vector3I(x, by, z)] = dirtBlock;
    _isDirty = true;
}

// Returns true if the given block would hold something up (i.e. counts
// as solid ground for gravity purposes). Excludes air, transparent
// blocks (water, glass), and cross/flat-ground decorations (flowers,
// carpets) - none of those should support a falling sand block.
private bool IsSolidSupport(BlockState block)
{
    if (block.IsAir()) return false;
    if (!block.IsFullBlock()) return false;

    BlockResource resource = BlockRegistry.Instance.GetBlock(block.BlockId);
    if (resource == null) return false;
    if (resource.IsTransparent) return false;
    if (resource.IsCross || resource.IsFlatGround) return false;

    return true;
}

// Sand has gravity, like Minecraft - wet_sand1/wet_sand2 deliberately do
// not (they're meant to behave like stable, packed ground). This is
// called from SetBlock for every real player-driven block change and
// checks two cases:
//   1. The block just placed IS sand, and whatever's below it doesn't
//      support it (e.g. placed straight into open air) - falls right away.
//   2. The block just placed makes THIS position unable to support
//      anything (e.g. breaking the ground out from under a sand block) -
//      checks if sand sits directly above and needs to fall as a result.
private void TryApplyGravity(int x, int y, int z, BlockState placedBlock)
{
    if (placedBlock.BlockId == "sand" && !IsSolidSupport(GetBlockCrossChunk(x, y - 1, z)))
    {
        ApplyGravityFall(x, y, z);
        return;
    }

    if (!IsSolidSupport(placedBlock))
    {
        BlockState above = GetBlockCrossChunk(x, y + 1, z);
        if (above.BlockId == "sand")
            ApplyGravityFall(x, y + 1, z);
    }
}

// Drops the sand block at local (x, y, z) straight down until it lands
// on solid support (or the bottom of the loaded world). Also handles a
// full "domino" cascade: after moving a sand block, it checks the
// position directly above (which may have been resting on the block
// that just moved) and repeats, so a whole stack of sand collapses in
// one pass rather than needing separate trigger events per block.
private void ApplyGravityFall(int x, int y, int z)
{
    int currentY = y;
    int safety = 0; // hard cap so malformed/edge-case data can't cause a runaway loop

    while (safety++ < 512)
    {
        BlockState current = GetBlockCrossChunk(x, currentY, z);
        if (current.BlockId != "sand") break;

        int restY = currentY;
        while (true)
        {
            int worldYBelow = ChunkPosition.Y * HEIGHT + (restY - 1);
            if (worldYBelow < 0) break; // don't fall below the bottom of the world

            if (!GetBlockCrossChunk(x, restY - 1, z).IsAir()) break;
            restY--;
        }

        if (restY != currentY)
        {
            var sandBlock = new BlockState { BlockId = "sand", BitMask = 0xFF };
            WritePersisted(x, restY, z, sandBlock);
            WritePersisted(x, currentY, z, BlockState.Air);
        }

        currentY++; // check what was resting on top of this block next
    }
}

// Writes a block at a LOCAL offset from this chunk's origin, crossing
// into a neighboring chunk via ChunkManager if needed - same as
// SetBlockCrossChunkGrowth, but this variant DOES persist as a save
// modification, since gravity is a real physical change (not ephemeral
// growth like grass/wet-sand spread) and should survive a reload.
private void WritePersisted(int x, int y, int z, BlockState block)
{
    if (IsInBounds(x, y, z))
    {
        _blocks[x, y, z] = block;
        _isDirty = true;
        _modifiedBlocks[new Vector3I(x, y, z)] = block;
        return;
    }

    if (_chunkManager == null) return;

    Vector3I worldPos = new Vector3I(
        ChunkPosition.X * SIZE + x,
        ChunkPosition.Y * HEIGHT + y,
        ChunkPosition.Z * SIZE + z
    );
    _chunkManager.SetBlockAtWorld(worldPos, block);
}

// Used during initial world generation - does NOT mark as modified
public void SetBlockInternal(int x, int y, int z, BlockState block)
{
    _blocks[x, y, z] = block;
}

public Dictionary<Vector3I, BlockState> GetModifications()
{
    return _modifiedBlocks;
}



public void ApplyModifications(Dictionary<Vector3I, BlockState> mods)
{
    foreach (var kvp in mods)
    {
        _blocks[kvp.Key.X, kvp.Key.Y, kvp.Key.Z] = kvp.Value;
    }
    _modifiedBlocks = mods;
    _isDirty = true;
}

    private bool IsInBounds(int x, int y, int z)
    {
        return x >= 0 && x < SIZE &&
               y >= 0 && y < HEIGHT &&
               z >= 0 && z < SIZE;
    }

    // Sets a block due to natural/organic growth (e.g. grass spreading) -
// marks the chunk dirty for a mesh rebuild, but does NOT record it in
// _modifiedBlocks, matching how in-chunk grass spread already behaves
// (keeps save files from bloating with every natural spread event).
// Called directly for same-chunk writes, and via ChunkManager for
// cross-chunk writes (see SetBlockAtWorldNaturalGrowth).
public void SetBlockNaturalGrowth(int x, int y, int z, BlockState block)
{
    if (!IsInBounds(x, y, z)) return;
    _blocks[x, y, z] = block;
    _isDirty = true;
}

// Reads a block at a LOCAL offset from this chunk's origin, crossing
// into a neighboring chunk via ChunkManager if the offset falls outside
// this chunk's own bounds. Falls back to air if no chunk is currently
// loaded there (e.g. player is near the edge of the loaded world) or if
// this chunk has no ChunkManager reference yet.
private BlockState GetBlockCrossChunk(int x, int y, int z)
{
    if (IsInBounds(x, y, z))
        return _blocks[x, y, z];

    if (_chunkManager == null)
        return BlockState.Air;

    Vector3I worldPos = new Vector3I(
        ChunkPosition.X * SIZE + x,
        ChunkPosition.Y * HEIGHT + y,
        ChunkPosition.Z * SIZE + z
    );
    return _chunkManager.GetBlockAtWorld(worldPos);
}

// Writes a block at a LOCAL offset from this chunk's origin, crossing
// into a neighboring chunk via ChunkManager if needed. Used only for
// natural growth (grass spread) - never persisted as a save
// modification, same as same-chunk spread.
private void SetBlockCrossChunkGrowth(int x, int y, int z, BlockState block)
{
    if (IsInBounds(x, y, z))
    {
        SetBlockNaturalGrowth(x, y, z, block);
        return;
    }

    if (_chunkManager == null) return;

    Vector3I worldPos = new Vector3I(
        ChunkPosition.X * SIZE + x,
        ChunkPosition.Y * HEIGHT + y,
        ChunkPosition.Z * SIZE + z
    );
    _chunkManager.SetBlockAtWorldNaturalGrowth(worldPos, block);
}
    public void MarkDirty()
{
    _isDirty = true;
}

public void RequestRebuild()
{
    _isDirty = true;
}

public override void _Process(double delta)
{
    if (_meshRebuildCooldown > 0f)
        _meshRebuildCooldown -= (float)delta;

    if (_isDirty && IsGenerated && _meshRebuildCooldown <= 0f)
    {
        BuildMesh();
        _isDirty = false;
        _meshRebuildCooldown = MeshRebuildMinInterval;
    }

    if (!IsGenerated) return;

    _randomTickTimer += (float)delta;
    if (_randomTickTimer >= RandomTickInterval)
    {
        _randomTickTimer = 0f;
        RandomTick();
    }
}

// Chance that a successful grass spread also grows a flower/clover on
// top of the newly-converted grass block - purely decorative, rolled
// once per successful spread.
private const float FlowerOnSpreadChance = 0.12f;

// Gates how often a grass random-tick sample actually attempts a spread.
// 0.5 = spreads at roughly half the rate it did before. This only throttles
// grass specifically - sand/wet_sand1/wet_sand2 conversions still run at
// full speed off the same shared tick, since slowing RandomTickInterval or
// RandomTicksPerInterval directly would have slowed those down too.
private const float GrassSpreadChancePerTick = 0.5f;

private RandomNumberGenerator _grassRng = new RandomNumberGenerator();

// Random tick: samples random blocks in the chunk each interval. Only
// grass blocks are the "active agent" here - dirt is passive and does
// nothing on its own. This matters for performance: dirt blocks are far
// more numerous near any surface than grass blocks are, so if dirt were
// also actively scanning for nearby grass, that (more expensive) search
// would run on far more samples than the grass-only version does. Only
// running the 3x3x3 neighbor search when the sample happens to land on
// grass keeps the per-tick cost low regardless of how much dirt exists
// in the chunk.
private void RandomTick()
{
    for (int i = 0; i < RandomTicksPerInterval; i++)
    {
        int rx = GD.RandRange(0, SIZE - 1);
        int ry = GD.RandRange(0, HEIGHT - 1);
        int rz = GD.RandRange(0, SIZE - 1);

        BlockState block = _blocks[rx, ry, rz];

        if (block.BlockId == "grass_block")
        {
            // Must have air above - checks across the chunk boundary above if
            // this grass block happens to sit at the very top of its chunk.
            if (!GetBlockCrossChunk(rx, ry + 1, rz).IsAir()) continue;

            if (_grassRng.Randf() > GrassSpreadChancePerTick) continue;

            TrySpreadFromGrass(rx, ry, rz);
        }
        // Sand/wet_sand1/wet_sand2 spreading is disabled for now - it was
        // too laggy (see TrySandToWetSand1 / TryWetSand2ToWetSand1 /
        // TryWetSand1SpreadToSand below, still here for reference when this
        // gets reworked). Sand placed as wet_sand1 at world gen (see
        // ChunkManager.TouchesWaterAtGen) still happens - that's a one-time
        // cost at chunk creation, not a per-tick cost, so it's unaffected.
        //else if (block.BlockId == "sand")
        //{
        //    TrySandToWetSand1(rx, ry, rz);
        //}
        //else if (block.BlockId == "wet_sand2")
        //{
        //    TryWetSand2ToWetSand1(rx, ry, rz);
        //}
        //else if (block.BlockId == "wet_sand1")
        //{
        //    TryWetSand1SpreadToSand(rx, ry, rz);
        //}
        // NOTE: no early return here anymore - a previous version bailed
        // out the instant it found ANY grass block, even if that one
        // failed to find an eligible dirt neighbor, which wasted the rest
        // of this tick's sample budget. Now every sample in the budget
        // gets a real attempt, so multiple grass blocks can successfully
        // spread within the same tick call.
    }
}

// Stage 1 of the wet-sand gradient: a plain "sand" block that touches
// water converts itself to wet_sand1.
private void TrySandToWetSand1(int x, int y, int z)
{
    if (HasNeighborBlock(x, y, z, "water"))
    {
        _blocks[x, y, z] = new BlockState { BlockId = "wet_sand1", BitMask = 0xFF };
        _isDirty = true;
    }
}

// Re-wetting: a "wet_sand2" block (the drier tier) that touches water
// gets pulled back to wet_sand1 (the wetter tier) - water reaching a
// wet_sand2 block re-wets it fully rather than leaving it as-is.
private void TryWetSand2ToWetSand1(int x, int y, int z)
{
    if (HasNeighborBlock(x, y, z, "water"))
    {
        _blocks[x, y, z] = new BlockState { BlockId = "wet_sand1", BitMask = 0xFF };
        _isDirty = true;
    }
}

// Stage 2 of the wet-sand gradient: a "wet_sand1" block that touches a
// plain "sand" neighbor triggers a mutual reaction - the sand neighbor
// converts to wet_sand2, AND this wet_sand1 block converts to wet_sand2
// as well (unlike grass spread, where only the target converts and the
// source stays put). This is the ONLY way wet_sand1 progresses to
// wet_sand2 - touching water directly does NOT push it that direction
// (see TryWetSand2ToWetSand1 above for the reverse relationship).
private void TryWetSand1SpreadToSand(int x, int y, int z)
{
    foreach (var offset in OrthogonalOffsets)
    {
        int nx = x + offset.X;
        int ny = y + offset.Y;
        int nz = z + offset.Z;

        if (GetBlockCrossChunk(nx, ny, nz).BlockId != "sand") continue;

        var wetSand2 = new BlockState { BlockId = "wet_sand2", BitMask = 0xFF };

        // Convert the neighboring sand block (may cross into a
        // neighboring chunk).
        SetBlockCrossChunkGrowth(nx, ny, nz, wetSand2);

        // Convert this wet_sand1 block itself too - (x, y, z) is
        // always local since it came from this chunk's own
        // RandomTick sample.
        _blocks[x, y, z] = wetSand2;
        _isDirty = true;

        return; // one reaction per tick sample
    }
}

// Checks this position's 6 direct face neighbors (not diagonals) for any
// block matching the given id, crossing chunk boundaries via
// GetBlockCrossChunk as needed. Stops as soon as a match is found.
private bool HasNeighborBlock(int x, int y, int z, string blockId)
{
    foreach (var offset in OrthogonalOffsets)
    {
        if (GetBlockCrossChunk(x + offset.X, y + offset.Y, z + offset.Z).BlockId == blockId)
            return true;
    }
    return false;
}

// Search this grass block's 3x3x3 neighborhood for an eligible dirt
// block (exposed to air above) and convert it to grass. On a successful
// conversion, there's also a small chance to grow a flower or clover on
// top of the new grass block, purely for visual variety.
private bool TrySpreadFromGrass(int rx, int ry, int rz)
{
    for (int dx = -1; dx <= 1; dx++)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                int nx = rx + dx;
                int ny = ry + dy;
                int nz = rz + dz;

                // No bounds check/skip here anymore - GetBlockCrossChunk
                // transparently reaches into a neighboring chunk if this
                // offset falls outside SIZE/HEIGHT, which is what lets
                // grass spread across a chunk boundary instead of only
                // within its own 16x16x16 chunk.
                BlockState neighbor = GetBlockCrossChunk(nx, ny, nz);
                if (neighbor.BlockId != "dirt")
                    continue;

                // Must have air above (may also cross into another chunk)
                if (!GetBlockCrossChunk(nx, ny + 1, nz).IsAir()) continue;

                // Spread grass! Writes through the same cross-chunk-aware
                // path so this works whether the target dirt block lives
                // in this chunk or a neighboring one.
                var grassBlock = new BlockState { BlockId = "grass_block", BitMask = 0xFF };
                SetBlockCrossChunkGrowth(nx, ny, nz, grassBlock);

                // Small chance to grow a flower/clover on top of the
                // freshly-spread grass block. Reuses the air slot we
                // already confirmed is empty above (ny + 1).
                if (_grassRng.Randf() < FlowerOnSpreadChance)
                {
                    float typeRoll = _grassRng.Randf();
                    string decorId = typeRoll < 0.34f ? "rose"
                        : typeRoll < 0.67f ? "clover"
                        : "dandelion";

                    var decorBlock = new BlockState { BlockId = decorId, BitMask = 0xFF };
                    SetBlockCrossChunkGrowth(nx, ny + 1, nz, decorBlock);
                }

                return true;
            }
        }
    }
    return false;
}

public void BuildMesh()
{
    var solidSurfaces = new Dictionary<Texture2D, SurfaceTool>();
    var transparentSurfaces = new Dictionary<Texture2D, SurfaceTool>();
    var cutoutSurfaces = new Dictionary<Texture2D, SurfaceTool>(); // flowers, clovers, cross blocks

    for (int x = 0; x < SIZE; x++)
    {
        for (int y = 0; y < HEIGHT; y++)
        {
            for (int z = 0; z < SIZE; z++)
            {
                BlockState block = _blocks[x, y, z];
                if (block.IsAir()) continue;

                BlockResource resource = BlockRegistry.Instance.GetBlock(block.BlockId);
                if (resource == null) continue;

                

                var surfaces = resource.IsTransparent ? transparentSurfaces : solidSurfaces;

if (resource.IsCross)
    AddCrossFaces(cutoutSurfaces, resource, x, y, z);
else if (resource.IsFlatGround)
{
    if (resource.IsThinItem)
        AddThinItemFaces(cutoutSurfaces, resource, x, y, z);
    else
        AddFlatGroundFace(cutoutSurfaces, resource, x, y, z);
}
else if (block.IsFullBlock())
{
    AddFullBlockFaces(surfaces, block, resource, x, y, z);

    // Ore overlay: a second, alpha-scissored pass of the ore's fleck
    // texture, nudged slightly outward so the host block's own texture
    // still shows through the transparent parts of the ore art. Reuses
    // the same cutoutSurfaces pass/material as flowers/clover above -
    // depth testing handles the compositing correctly regardless of
    // draw order, since the discard on transparent pixels means nothing
    // gets written there for the host face behind it to lose to.
    var ore = OreRegistry.Instance?.GetOreFromBlockState(block);
    if (ore != null)
        AddOreOverlayFaces(cutoutSurfaces, ore.OverlayTexture, resource, x, y, z);
}
else
    AddChiseledBlockFaces(surfaces, block, resource, x, y, z);
            }
        }
    }

    var arrayMesh = new ArrayMesh();
    var solidOnlyMesh = new ArrayMesh();

    // Cutout surfaces (flowers, clovers) - AlphaScissor, renders before grass overlay
foreach (var kvp in cutoutSurfaces)
{
    kvp.Value.GenerateNormals();
    kvp.Value.Commit(arrayMesh);
    int surfIdx = arrayMesh.GetSurfaceCount() - 1;
    if (surfIdx >= 0)
    {
        var mat = new StandardMaterial3D();
        mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
        mat.AlphaScissorThreshold = 0.1f;
        mat.RenderPriority = 0;
        if (kvp.Key != null)
            mat.AlbedoTexture = kvp.Key;
        arrayMesh.SurfaceSetMaterial(surfIdx, mat);
    }
}

    foreach (var kvp in solidSurfaces)
    {
        kvp.Value.GenerateNormals();
        kvp.Value.Commit(arrayMesh);
        kvp.Value.Commit(solidOnlyMesh);

        int surfIdx = arrayMesh.GetSurfaceCount() - 1;
        if (surfIdx >= 0)
        {
            var mat = new StandardMaterial3D();
            mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            if (kvp.Key != null)
                mat.AlbedoTexture = kvp.Key;
            arrayMesh.SurfaceSetMaterial(surfIdx, mat);
        }
    }

   

    foreach (var kvp in transparentSurfaces)
    {
        kvp.Value.GenerateNormals();
        kvp.Value.Commit(arrayMesh);
        int surfIdx = arrayMesh.GetSurfaceCount() - 1;
        if (surfIdx >= 0)
        {
            var mat = new StandardMaterial3D();
            mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            if (kvp.Key != null)
                mat.AlbedoTexture = kvp.Key;
            arrayMesh.SurfaceSetMaterial(surfIdx, mat);
        }
    }

    if (arrayMesh.GetSurfaceCount() > 0)
    {
        _meshInstance.Mesh = arrayMesh;
        CallDeferred("BuildCollision", solidOnlyMesh);
    }

    IsGenerated = true;
}

private void AddCrossFaces(Dictionary<Texture2D, SurfaceTool> surfaces,
    BlockResource resource, int x, int y, int z)
{
    Texture2D tex = resource.TextureSide ?? resource.TextureTop;
    if (tex == null) return;

    if (!surfaces.TryGetValue(tex, out SurfaceTool st))
    {
        st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        surfaces[tex] = st;
    }

    // Two diagonal planes crossing in the middle
    // Diagonal 1: (-0.5, 0, -0.5) to (0.5, 0, 0.5)
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x,       y + 1, z));
    st.SetUV(new Vector2(1, 0)); st.AddVertex(new Vector3(x + 1,   y + 1, z + 1));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x + 1,   y,     z + 1));
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x,       y + 1, z));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x + 1,   y,     z + 1));
    st.SetUV(new Vector2(0, 1)); st.AddVertex(new Vector3(x,       y,     z));

    // Back side of diagonal 1
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x + 1,   y + 1, z + 1));
    st.SetUV(new Vector2(1, 0)); st.AddVertex(new Vector3(x,       y + 1, z));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x,       y,     z));
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x + 1,   y + 1, z + 1));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x,       y,     z));
    st.SetUV(new Vector2(0, 1)); st.AddVertex(new Vector3(x + 1,   y,     z + 1));

    // Diagonal 2: (0.5, 0, -0.5) to (-0.5, 0, 0.5)
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x + 1,   y + 1, z));
    st.SetUV(new Vector2(1, 0)); st.AddVertex(new Vector3(x,       y + 1, z + 1));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x,       y,     z + 1));
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x + 1,   y + 1, z));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x,       y,     z + 1));
    st.SetUV(new Vector2(0, 1)); st.AddVertex(new Vector3(x + 1,   y,     z));

    // Back side of diagonal 2
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x,       y + 1, z + 1));
    st.SetUV(new Vector2(1, 0)); st.AddVertex(new Vector3(x + 1,   y + 1, z));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x + 1,   y,     z));
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x,       y + 1, z + 1));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x + 1,   y,     z));
    st.SetUV(new Vector2(0, 1)); st.AddVertex(new Vector3(x,       y,     z + 1));
}

private void AddFlatGroundFace(Dictionary<Texture2D, SurfaceTool> surfaces,
    BlockResource resource, int x, int y, int z)
{
    Texture2D tex = resource.TextureTop;
    if (tex == null) return;

    if (!surfaces.TryGetValue(tex, out SurfaceTool st))
    {
        st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        surfaces[tex] = st;
    }

    float flatY = y + 0.0625f; // 1/16 of a block high

    // Top face
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x,     flatY, z));
    st.SetUV(new Vector2(1, 0)); st.AddVertex(new Vector3(x + 1, flatY, z));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x + 1, flatY, z + 1));
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x,     flatY, z));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x + 1, flatY, z + 1));
    st.SetUV(new Vector2(0, 1)); st.AddVertex(new Vector3(x,     flatY, z + 1));

    // Bottom face (so visible from below too)
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x,     flatY, z + 1));
    st.SetUV(new Vector2(1, 0)); st.AddVertex(new Vector3(x + 1, flatY, z + 1));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x + 1, flatY, z));
    st.SetUV(new Vector2(0, 0)); st.AddVertex(new Vector3(x,     flatY, z + 1));
    st.SetUV(new Vector2(1, 1)); st.AddVertex(new Vector3(x + 1, flatY, z));
    st.SetUV(new Vector2(0, 1)); st.AddVertex(new Vector3(x,     flatY, z));
}

private const float ThinItemHeight = 0.0625f; // 1/16 block - real 3D extrusion thickness, not just a Y offset
private const int ThinItemGridSize = 16;       // matches a standard 16x16 texture, 1 texel = 1 world-space "pixel"

// One 16x16 opacity mask per unique texture, built once from the actual
// image data and reused for every rock/item placed with it - this is the
// expensive part (reading pixels), so it only happens once per texture,
// not once per placed block.
private static readonly Dictionary<Texture2D, bool[,]> _thinItemAlphaCache = new();

private bool[,] GetOrBuildAlphaMask(Texture2D tex)
{
    if (_thinItemAlphaCache.TryGetValue(tex, out var cached)) return cached;

    var mask = new bool[ThinItemGridSize, ThinItemGridSize];
    Image img = tex.GetImage();
    if (img != null)
    {
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        for (int v = 0; v < ThinItemGridSize; v++)
        {
            for (int u = 0; u < ThinItemGridSize; u++)
            {
                // Sample the pixel at the middle of this texel's cell, scaled
                // to the source image's actual size (in case it isn't 16x16).
                int px = Mathf.Clamp((int)((u + 0.5f) / ThinItemGridSize * w), 0, w - 1);
                int py = Mathf.Clamp((int)((v + 0.5f) / ThinItemGridSize * h), 0, h - 1);
                mask[u, v] = img.GetPixel(px, py).A > 0.5f;
            }
        }
    }
    // If img came back null (can happen if the texture's import "Compress
    // Mode" doesn't allow CPU readback), this silently falls back to an
    // all-transparent mask, so you'd just get no side walls. If that
    // happens, set that texture's Compress Mode to Lossless/Uncompressed
    // in the Import tab and reimport.
    _thinItemAlphaCache[tex] = mask;
    return mask;
}

private void AddQuadWithUV(SurfaceTool surface, Vector3[] verts, Vector2 uv)
{
    surface.SetUV(uv); surface.AddVertex(verts[0]);
    surface.SetUV(uv); surface.AddVertex(verts[1]);
    surface.SetUV(uv); surface.AddVertex(verts[2]);

    surface.SetUV(uv); surface.AddVertex(verts[0]);
    surface.SetUV(uv); surface.AddVertex(verts[2]);
    surface.SetUV(uv); surface.AddVertex(verts[3]);
}

// Real thin 3D box: full-icon top/bottom faces, plus pixel-perfect side
// walls generated from the texture's actual silhouette, the same way
// Minecraft's own generated item models (and TFC) work - not a stretched
// copy of the face texture. For every opaque texel, check its 4
// neighbors in texture space; wherever a neighbor is transparent (or off
// the edge of the image), emit a tiny 1-texel-wide wall sampling ONLY
// that texel's color, so the silhouette edge (little notches, chips,
// speckles and all) reads correctly from the side.
private void AddThinItemFaces(Dictionary<Texture2D, SurfaceTool> surfaces,
    BlockResource resource, int x, int y, int z)
{
    Texture2D tex = resource.TextureTop;
    if (tex == null) return;

    float topY = y + ThinItemHeight;
    SurfaceTool st = GetOrCreateSurface(surfaces, tex);

    // Top face - the full icon, standard UV.
    AddQuad(st, new Vector3[]
    {
        new Vector3(x,     topY, z),
        new Vector3(x + 1, topY, z),
        new Vector3(x + 1, topY, z + 1),
        new Vector3(x,     topY, z + 1)
    });

    // Bottom face - same icon, facing down.
    AddQuad(st, new Vector3[]
    {
        new Vector3(x,     y, z + 1),
        new Vector3(x + 1, y, z + 1),
        new Vector3(x + 1, y, z),
        new Vector3(x,     y, z)
    });

    bool[,] mask = GetOrBuildAlphaMask(tex);
    float step = 1f / ThinItemGridSize;

    for (int v = 0; v < ThinItemGridSize; v++)
    {
        for (int u = 0; u < ThinItemGridSize; u++)
        {
            if (!mask[u, v]) continue;

            float x0 = x + u * step, x1 = x0 + step;
            float z0 = z + v * step, z1 = z0 + step;

            // Sample from the middle of this texel, same UV for all 4
            // corners of every wall segment on it - a flat 1-pixel swatch
            // of color, not a gradient.
            Vector2 swatchUV = new Vector2((u + 0.5f) * step, 1f - (v + 0.5f) * step);

            // West wall (-X): emitted if the texel to the left is empty or off the image edge
            if (u == 0 || !mask[u - 1, v])
                AddQuadWithUV(st, new Vector3[]
                {
                    new Vector3(x0, y,    z1),
                    new Vector3(x0, y,    z0),
                    new Vector3(x0, topY, z0),
                    new Vector3(x0, topY, z1)
                }, swatchUV);

            // East wall (+X)
            if (u == ThinItemGridSize - 1 || !mask[u + 1, v])
                AddQuadWithUV(st, new Vector3[]
                {
                    new Vector3(x1, y,    z0),
                    new Vector3(x1, y,    z1),
                    new Vector3(x1, topY, z1),
                    new Vector3(x1, topY, z0)
                }, swatchUV);

            // North wall (-Z, toward the texel above it)
            if (v == 0 || !mask[u, v - 1])
                AddQuadWithUV(st, new Vector3[]
                {
                    new Vector3(x0, y,    z0),
                    new Vector3(x1, y,    z0),
                    new Vector3(x1, topY, z0),
                    new Vector3(x0, topY, z0)
                }, swatchUV);

            // South wall (+Z, toward the texel below it)
            if (v == ThinItemGridSize - 1 || !mask[u, v + 1])
                AddQuadWithUV(st, new Vector3[]
                {
                    new Vector3(x1, y,    z1),
                    new Vector3(x0, y,    z1),
                    new Vector3(x0, topY, z1),
                    new Vector3(x1, topY, z1)
                }, swatchUV);
        }
    }
}
private void BuildCollision(ArrayMesh mesh)
{
    var faces = mesh.GetFaces();
    if (faces.Length < 3) return;

    // Build the NEW collision body first
    var newCollisionBody = new StaticBody3D();
    newCollisionBody.CollisionLayer = 1;
    newCollisionBody.CollisionMask = 1;
    newCollisionBody.SetMeta("chunk", this);

    var concave = new ConcavePolygonShape3D();
    concave.BackfaceCollision = true;
    concave.SetFaces(faces);

    var shape = new CollisionShape3D();
    shape.Shape = concave;
    newCollisionBody.AddChild(shape);

    GetParent().AddChild(newCollisionBody);
    newCollisionBody.GlobalPosition = GlobalPosition;

    // NOW remove the old one, after the new one is already active
    if (_collisionBody != null && IsInstanceValid(_collisionBody))
        _collisionBody.QueueFree();

    _collisionBody = newCollisionBody;
}

private bool HasExposedFace(int x, int y, int z)
{
    return IsAirAt(x + 1, y, z) || IsAirAt(x - 1, y, z) ||
           IsAirAt(x, y + 1, z) || IsAirAt(x, y - 1, z) ||
           IsAirAt(x, y, z + 1) || IsAirAt(x, y, z - 1);
}

private bool IsAirAt(int x, int y, int z)
{
    // Y out of bounds = treat as air (so top/bottom surfaces still get collision)
    if (y < 0 || y >= HEIGHT)
        return true;

    // X/Z out of bounds = treat as solid (neighboring chunk likely fills it)
    if (x < 0 || x >= SIZE || z < 0 || z >= SIZE)
        return false;

    return _blocks[x, y, z].IsAir();
}
    private SurfaceTool GetOrCreateSurface(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        Texture2D texture)
    {
        if (!surfaces.ContainsKey(texture))
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            surfaces[texture] = st;
        }
        return surfaces[texture];
    }

    private void AddFullBlockFaces(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        BlockState block, BlockResource resource,
        int x, int y, int z)
    {
        if (ShouldDrawFace(x, y + 1, z, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, resource.TextureTop),
                GetFaceVertices(x, y, z, FaceDirection.Top, 1.0f));

        if (ShouldDrawFace(x, y - 1, z, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, resource.TextureBottom),
                GetFaceVertices(x, y, z, FaceDirection.Bottom, 1.0f));

        if (ShouldDrawFace(x, y, z - 1, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, resource.TextureSide),
                GetFaceVertices(x, y, z, FaceDirection.North, 1.0f));

        if (ShouldDrawFace(x, y, z + 1, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, resource.TextureSide),
                GetFaceVertices(x, y, z, FaceDirection.South, 1.0f));

        if (ShouldDrawFace(x - 1, y, z, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, resource.TextureSide),
                GetFaceVertices(x, y, z, FaceDirection.West, 1.0f));

        if (ShouldDrawFace(x + 1, y, z, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, resource.TextureSide),
                GetFaceVertices(x, y, z, FaceDirection.East, 1.0f));
    }

    private static readonly Dictionary<FaceDirection, Vector3> _faceNormals = new()
    {
        { FaceDirection.Top,    new Vector3(0, 1, 0) },
        { FaceDirection.Bottom, new Vector3(0, -1, 0) },
        { FaceDirection.North,  new Vector3(0, 0, -1) },
        { FaceDirection.South,  new Vector3(0, 0, 1) },
        { FaceDirection.West,   new Vector3(-1, 0, 0) },
        { FaceDirection.East,   new Vector3(1, 0, 0) },
    };

    private const float OreOverlayNudge = 0.0015f; // pushes the overlay quad slightly out from the host face so they don't z-fight

    private Vector3[] NudgeVerts(Vector3[] verts, FaceDirection dir)
    {
        Vector3 offset = _faceNormals[dir] * OreOverlayNudge;
        var nudged = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            nudged[i] = verts[i] + offset;
        return nudged;
    }

    // Draws the ore's transparent fleck texture as a second quad on top of
    // each visible face of a full block, same face-visibility rules as
    // AddFullBlockFaces. Only supports full blocks for now - chiseled
    // (partial) blocks don't get an ore overlay.
    private void AddOreOverlayFaces(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        Texture2D overlayTexture, BlockResource resource,
        int x, int y, int z)
    {
        if (overlayTexture == null) return;

        if (ShouldDrawFace(x, y + 1, z, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, overlayTexture),
                NudgeVerts(GetFaceVertices(x, y, z, FaceDirection.Top, 1.0f), FaceDirection.Top));

        if (ShouldDrawFace(x, y - 1, z, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, overlayTexture),
                NudgeVerts(GetFaceVertices(x, y, z, FaceDirection.Bottom, 1.0f), FaceDirection.Bottom));

        if (ShouldDrawFace(x, y, z - 1, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, overlayTexture),
                NudgeVerts(GetFaceVertices(x, y, z, FaceDirection.North, 1.0f), FaceDirection.North));

        if (ShouldDrawFace(x, y, z + 1, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, overlayTexture),
                NudgeVerts(GetFaceVertices(x, y, z, FaceDirection.South, 1.0f), FaceDirection.South));

        if (ShouldDrawFace(x - 1, y, z, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, overlayTexture),
                NudgeVerts(GetFaceVertices(x, y, z, FaceDirection.West, 1.0f), FaceDirection.West));

        if (ShouldDrawFace(x + 1, y, z, resource.IsTransparent))
            AddQuad(GetOrCreateSurface(surfaces, overlayTexture),
                NudgeVerts(GetFaceVertices(x, y, z, FaceDirection.East, 1.0f), FaceDirection.East));
    }

    private void AddChiseledBlockFaces(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        BlockState block, BlockResource resource,
        int x, int y, int z)
    {
        Vector3I[] bitOffsets = new Vector3I[]
        {
            new Vector3I(0, 0, 0),
            new Vector3I(1, 0, 0),
            new Vector3I(0, 0, 1),
            new Vector3I(1, 0, 1),
            new Vector3I(0, 1, 0),
            new Vector3I(1, 1, 0),
            new Vector3I(0, 1, 1),
            new Vector3I(1, 1, 1)
        };

        for (int bit = 0; bit < 8; bit++)
        {
            if (!block.IsBitActive(bit)) continue;

            Vector3I offset = bitOffsets[bit];
            float bx = x + offset.X * 0.5f;
            float by = y + offset.Y * 0.5f;
            float bz = z + offset.Z * 0.5f;
            float s = 0.5f;

            int topBit = bit + 4;
            if (topBit >= 8 || !block.IsBitActive(topBit))
                AddQuad(GetOrCreateSurface(surfaces, resource.TextureTop),
                    GetFaceVertices(bx, by, bz, FaceDirection.Top, s));

            int bottomBit = bit - 4;
            if (bottomBit < 0 || !block.IsBitActive(bottomBit))
                AddQuad(GetOrCreateSurface(surfaces, resource.TextureBottom),
                    GetFaceVertices(bx, by, bz, FaceDirection.Bottom, s));

            AddQuad(GetOrCreateSurface(surfaces, resource.TextureSide),
                GetFaceVertices(bx, by, bz, FaceDirection.North, s));
            AddQuad(GetOrCreateSurface(surfaces, resource.TextureSide),
                GetFaceVertices(bx, by, bz, FaceDirection.South, s));
            AddQuad(GetOrCreateSurface(surfaces, resource.TextureSide),
                GetFaceVertices(bx, by, bz, FaceDirection.West, s));
            AddQuad(GetOrCreateSurface(surfaces, resource.TextureSide),
                GetFaceVertices(bx, by, bz, FaceDirection.East, s));
        }
    }

    private bool ShouldDrawFace(int nx, int ny, int nz,
        bool currentIsTransparent)
    {
        if (!IsInBounds(nx, ny, nz)) return true;

        BlockState neighbor = _blocks[nx, ny, nz];
        if (neighbor.IsAir()) return true;

        BlockResource neighborResource =
            BlockRegistry.Instance.GetBlock(neighbor.BlockId);
        if (neighborResource == null) return true;

        if (neighborResource.IsTransparent && !currentIsTransparent)
            return true;

        if (!neighbor.IsFullBlock()) return true;

        return false;
    }

    private Vector3[] GetFaceVertices(float x, float y, float z,
        FaceDirection dir, float size)
    {
        float s = size;
        return dir switch
        {
            FaceDirection.Top => new Vector3[]
            {
                new Vector3(x,     y + s, z    ),
                new Vector3(x + s, y + s, z    ),
                new Vector3(x + s, y + s, z + s),
                new Vector3(x,     y + s, z + s)
            },
            FaceDirection.Bottom => new Vector3[]
            {
                new Vector3(x,     y,     z + s),
                new Vector3(x + s, y,     z + s),
                new Vector3(x + s, y,     z    ),
                new Vector3(x,     y,     z    )
            },
            FaceDirection.North => new Vector3[]
            {
                new Vector3(x,     y,     z    ),
                new Vector3(x + s, y,     z    ),
                new Vector3(x + s, y + s, z    ),
                new Vector3(x,     y + s, z    )
            },
            FaceDirection.South => new Vector3[]
            {
                new Vector3(x + s, y,     z + s),
                new Vector3(x,     y,     z + s),
                new Vector3(x,     y + s, z + s),
                new Vector3(x + s, y + s, z + s)
            },
            FaceDirection.East => new Vector3[]
            {
                new Vector3(x + s, y,     z    ),
                new Vector3(x + s, y,     z + s),
                new Vector3(x + s, y + s, z + s),
                new Vector3(x + s, y + s, z    )
            },
            FaceDirection.West => new Vector3[]
            {
                new Vector3(x,     y,     z + s),
                new Vector3(x,     y,     z    ),
                new Vector3(x,     y + s, z    ),
                new Vector3(x,     y + s, z + s)
            },
            _ => new Vector3[4]
        };
    }

    private void AddQuad(SurfaceTool surface, Vector3[] verts)
{
    Vector2[] uvs = new Vector2[]
    {
        new Vector2(0, 1),
        new Vector2(1, 1),
        new Vector2(1, 0),
        new Vector2(0, 0)
    };

    surface.SetUV(uvs[0]);
    surface.AddVertex(verts[0]);
    surface.SetUV(uvs[1]);
    surface.AddVertex(verts[1]);
    surface.SetUV(uvs[2]);
    surface.AddVertex(verts[2]);

    surface.SetUV(uvs[0]);
    surface.AddVertex(verts[0]);
    surface.SetUV(uvs[2]);
    surface.AddVertex(verts[2]);
    surface.SetUV(uvs[3]);
    surface.AddVertex(verts[3]);
}
}