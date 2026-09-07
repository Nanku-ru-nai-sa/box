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

    // Plants are non-solid, but still need an invisible raycast target.
    private StaticBody3D _plantTargetBody;

    // Normal world collision = layer 1.
    // Plant interaction targets = layer 2.
    private const uint WorldCollisionLayer = 1;
    private const uint PlantTargetCollisionLayer = 2;

    public Vector3I ChunkPosition { get; private set; }
    public bool IsGenerated { get; private set; } = false;

    private ChunkManager _chunkManager;
    private bool _isDirty = false;
    private Dictionary<Vector3I, BlockState> _modifiedBlocks = new();

    private float _randomTickTimer = 0f;
    private const float RandomTickInterval = 0.03f;
    private const int RandomTicksPerInterval = 10;

    private float _meshRebuildCooldown = 0f;
    private const float MeshRebuildMinInterval = 0.1f;

    private enum FaceDirection
    {
        Top, Bottom, North, South, East, West
    }

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

    public void SetChunkManager(ChunkManager chunkManager)
    {
        _chunkManager = chunkManager;
    }

    public void Initialize(Vector3I chunkPosition)
    {
        ChunkPosition = chunkPosition;

        GlobalPosition = new Vector3(
            chunkPosition.X * SIZE,
            chunkPosition.Y * HEIGHT,
            chunkPosition.Z * SIZE
        );

        _grassRng.Seed =
            (ulong)(
                chunkPosition.X * 486187739 ^
                chunkPosition.Z * 1300719893 ^
                chunkPosition.Y * 668265263
            );

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

        TryDecayGrassBelow(x, y, z, block);
        TryApplyGravity(x, y, z, block);
    }

    private void TryDecayGrassBelow(
        int x,
        int y,
        int z,
        BlockState placedBlock)
    {
        if (placedBlock.IsAir())
            return;

        BlockResource resource =
            BlockRegistry.Instance.GetBlock(placedBlock.BlockId);

        if (resource == null)
            return;

        if (resource.IsTransparent)
            return;

        if (resource.IsCross || resource.IsFlatGround)
            return;

        if (!placedBlock.IsFullBlock())
            return;

        int by = y - 1;

        if (!IsInBounds(x, by, z))
            return;

        BlockState below = _blocks[x, by, z];

        if (below.BlockId != "grass_block")
            return;

        var dirtBlock = new BlockState
        {
            BlockId = "dirt",
            BitMask = 0xFF
        };

        _blocks[x, by, z] = dirtBlock;
        _modifiedBlocks[new Vector3I(x, by, z)] = dirtBlock;
        _isDirty = true;
    }

    private bool IsSolidSupport(BlockState block)
    {
        if (block.IsAir())
            return false;

        if (!block.IsFullBlock())
            return false;

        BlockResource resource =
            BlockRegistry.Instance.GetBlock(block.BlockId);

        if (resource == null)
            return false;

        if (resource.IsTransparent)
            return false;

        if (resource.IsCross || resource.IsFlatGround)
            return false;

        return true;
    }

    private void TryApplyGravity(
        int x,
        int y,
        int z,
        BlockState placedBlock)
    {
        if (placedBlock.BlockId == "sand" &&
            !IsSolidSupport(GetBlockCrossChunk(x, y - 1, z)))
        {
            ApplyGravityFall(x, y, z);
            return;
        }

        if (!IsSolidSupport(placedBlock))
        {
            BlockState above =
                GetBlockCrossChunk(x, y + 1, z);

            if (above.BlockId == "sand")
                ApplyGravityFall(x, y + 1, z);
        }
    }

    private void ApplyGravityFall(int x, int y, int z)
    {
        int currentY = y;
        int safety = 0;

        while (safety++ < 512)
        {
            BlockState current =
                GetBlockCrossChunk(x, currentY, z);

            if (current.BlockId != "sand")
                break;

            int restY = currentY;

            while (true)
            {
                int worldYBelow =
                    ChunkPosition.Y * HEIGHT + (restY - 1);

                if (worldYBelow < 0)
                    break;

                if (!GetBlockCrossChunk(x, restY - 1, z).IsAir())
                    break;

                restY--;
            }

            if (restY != currentY)
            {
                var sandBlock = new BlockState
                {
                    BlockId = "sand",
                    BitMask = 0xFF
                };

                WritePersisted(x, restY, z, sandBlock);
                WritePersisted(
                    x,
                    currentY,
                    z,
                    BlockState.Air
                );
            }

            currentY++;
        }
    }

    private void WritePersisted(
        int x,
        int y,
        int z,
        BlockState block)
    {
        if (IsInBounds(x, y, z))
        {
            _blocks[x, y, z] = block;
            _isDirty = true;
            _modifiedBlocks[new Vector3I(x, y, z)] = block;
            return;
        }

        if (_chunkManager == null)
            return;

        Vector3I worldPos = new Vector3I(
            ChunkPosition.X * SIZE + x,
            ChunkPosition.Y * HEIGHT + y,
            ChunkPosition.Z * SIZE + z
        );

        _chunkManager.SetBlockAtWorld(worldPos, block);
    }

    public void SetBlockInternal(
        int x,
        int y,
        int z,
        BlockState block)
    {
        _blocks[x, y, z] = block;
    }

    public Dictionary<Vector3I, BlockState> GetModifications()
    {
        return _modifiedBlocks;
    }

    public void ApplyModifications(
        Dictionary<Vector3I, BlockState> mods)
    {
        foreach (var kvp in mods)
        {
            _blocks[
                kvp.Key.X,
                kvp.Key.Y,
                kvp.Key.Z
            ] = kvp.Value;
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

    public void SetBlockNaturalGrowth(
        int x,
        int y,
        int z,
        BlockState block)
    {
        if (!IsInBounds(x, y, z))
            return;

        _blocks[x, y, z] = block;
        _isDirty = true;
    }

    private BlockState GetBlockCrossChunk(
        int x,
        int y,
        int z)
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

    private void SetBlockCrossChunkGrowth(
        int x,
        int y,
        int z,
        BlockState block)
    {
        if (IsInBounds(x, y, z))
        {
            SetBlockNaturalGrowth(x, y, z, block);
            return;
        }

        if (_chunkManager == null)
            return;

        Vector3I worldPos = new Vector3I(
            ChunkPosition.X * SIZE + x,
            ChunkPosition.Y * HEIGHT + y,
            ChunkPosition.Z * SIZE + z
        );

        _chunkManager.SetBlockAtWorldNaturalGrowth(
            worldPos,
            block
        );
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

        if (_isDirty &&
            IsGenerated &&
            _meshRebuildCooldown <= 0f)
        {
            BuildMesh();
            _isDirty = false;
            _meshRebuildCooldown =
                MeshRebuildMinInterval;
        }

        if (!IsGenerated)
            return;

        _randomTickTimer += (float)delta;

        if (_randomTickTimer >= RandomTickInterval)
        {
            _randomTickTimer = 0f;
            RandomTick();
        }
    }

    private const float FlowerOnSpreadChance = 0.12f;

    // Half the previous grass spread chance.
    private const float GrassSpreadChancePerTick = 0.25f;

    private RandomNumberGenerator _grassRng =
        new RandomNumberGenerator();

    private void RandomTick()
    {
        for (int i = 0;
             i < RandomTicksPerInterval;
             i++)
        {
            int rx = GD.RandRange(0, SIZE - 1);
            int ry = GD.RandRange(0, HEIGHT - 1);
            int rz = GD.RandRange(0, SIZE - 1);

            BlockState block =
                _blocks[rx, ry, rz];

            if (block.BlockId == "grass_block")
            {
                if (!GetBlockCrossChunk(
                        rx,
                        ry + 1,
                        rz
                    ).IsAir())
                    continue;

                if (_grassRng.Randf() >
                    GrassSpreadChancePerTick)
                    continue;

                TrySpreadFromGrass(
                    rx,
                    ry,
                    rz
                );
            }
        }
    }

    private void TrySandToWetSand1(
        int x,
        int y,
        int z)
    {
        if (HasNeighborBlock(
                x,
                y,
                z,
                "water"))
        {
            _blocks[x, y, z] =
                new BlockState
                {
                    BlockId = "wet_sand1",
                    BitMask = 0xFF
                };

            _isDirty = true;
        }
    }

    private void TryWetSand2ToWetSand1(
        int x,
        int y,
        int z)
    {
        if (HasNeighborBlock(
                x,
                y,
                z,
                "water"))
        {
            _blocks[x, y, z] =
                new BlockState
                {
                    BlockId = "wet_sand1",
                    BitMask = 0xFF
                };

            _isDirty = true;
        }
    }

    private void TryWetSand1SpreadToSand(
        int x,
        int y,
        int z)
    {
        foreach (var offset in OrthogonalOffsets)
        {
            int nx = x + offset.X;
            int ny = y + offset.Y;
            int nz = z + offset.Z;

            if (GetBlockCrossChunk(
                    nx,
                    ny,
                    nz
                ).BlockId != "sand")
                continue;

            var wetSand2 =
                new BlockState
                {
                    BlockId = "wet_sand2",
                    BitMask = 0xFF
                };

            SetBlockCrossChunkGrowth(
                nx,
                ny,
                nz,
                wetSand2
            );

            _blocks[x, y, z] = wetSand2;
            _isDirty = true;

            return;
        }
    }

    private bool HasNeighborBlock(
        int x,
        int y,
        int z,
        string blockId)
    {
        foreach (var offset in OrthogonalOffsets)
        {
            if (GetBlockCrossChunk(
                    x + offset.X,
                    y + offset.Y,
                    z + offset.Z
                ).BlockId == blockId)
                return true;
        }

        return false;
    }

    private bool TrySpreadFromGrass(
        int rx,
        int ry,
        int rz)
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

                    BlockState neighbor =
                        GetBlockCrossChunk(
                            nx,
                            ny,
                            nz
                        );

                    if (neighbor.BlockId != "dirt")
                        continue;

                    if (!GetBlockCrossChunk(
                            nx,
                            ny + 1,
                            nz
                        ).IsAir())
                        continue;

                    var grassBlock =
                        new BlockState
                        {
                            BlockId = "grass_block",
                            BitMask = 0xFF
                        };

                    SetBlockCrossChunkGrowth(
                        nx,
                        ny,
                        nz,
                        grassBlock
                    );

                    if (_grassRng.Randf() <
                        FlowerOnSpreadChance)
                    {
                        float typeRoll =
                            _grassRng.Randf();

                        string decorId =
                            typeRoll < 0.34f
                                ? "rose"
                                : typeRoll < 0.67f
                                    ? "clover"
                                    : "dandelion";

                        var decorBlock =
                            new BlockState
                            {
                                BlockId = decorId,
                                BitMask = 0xFF
                            };

                        SetBlockCrossChunkGrowth(
                            nx,
                            ny + 1,
                            nz,
                            decorBlock
                        );
                    }

                    return true;
                }
            }
        }

        return false;
    }

    public void BuildMesh()
    {
        var solidSurfaces =
            new Dictionary<Texture2D, SurfaceTool>();

        var transparentSurfaces =
            new Dictionary<Texture2D, SurfaceTool>();

        var cutoutSurfaces =
            new Dictionary<Texture2D, SurfaceTool>();

        // Separate invisible collision geometry used ONLY for raycasting
        // at cross/plant blocks.
        var plantTargetSurface =
            new SurfaceTool();

        plantTargetSurface.Begin(
            Mesh.PrimitiveType.Triangles
        );

        bool hasPlantTargets = false;

        for (int x = 0; x < SIZE; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                for (int z = 0; z < SIZE; z++)
                {
                    BlockState block =
                        _blocks[x, y, z];

                    if (block.IsAir())
                        continue;

                    BlockResource resource =
                        BlockRegistry.Instance.GetBlock(
                            block.BlockId
                        );

                    if (resource == null)
                        continue;

                    var surfaces =
                        resource.IsTransparent
                            ? transparentSurfaces
                            : solidSurfaces;

                    if (resource.IsCross)
                    {
                        // Existing visual stays exactly the same.
                        AddCrossFaces(
                            cutoutSurfaces,
                            resource,
                            x,
                            y,
                            z
                        );

                        // Invisible full-block target.
                        AddPlantTargetCube(
                            plantTargetSurface,
                            x,
                            y,
                            z
                        );

                        hasPlantTargets = true;
                    }
                    else if (resource.IsFlatGround)
                    {
                        if (resource.IsThinItem)
                        {
                            AddThinItemFaces(
                                cutoutSurfaces,
                                resource,
                                x,
                                y,
                                z
                            );
                        }
                        else
                        {
                            AddFlatGroundFace(
                                cutoutSurfaces,
                                resource,
                                x,
                                y,
                                z
                            );
                        }
                    }
                    else if (block.IsFullBlock())
                    {
                        AddFullBlockFaces(
                            surfaces,
                            block,
                            resource,
                            x,
                            y,
                            z
                        );

                        var ore =
                            OreRegistry.Instance?
                                .GetOreFromBlockState(
                                    block
                                );

                        if (ore != null)
                        {
                            AddOreOverlayFaces(
                                cutoutSurfaces,
                                ore.OverlayTexture,
                                resource,
                                x,
                                y,
                                z
                            );
                        }
                    }
                    else
                    {
                        AddChiseledBlockFaces(
                            surfaces,
                            block,
                            resource,
                            x,
                            y,
                            z
                        );
                    }
                }
            }
        }

        var arrayMesh = new ArrayMesh();
        var solidOnlyMesh = new ArrayMesh();
        var plantTargetMesh = new ArrayMesh();

        if (hasPlantTargets)
        {
            plantTargetSurface.GenerateNormals();
            plantTargetSurface.Commit(
                plantTargetMesh
            );
        }

        foreach (var kvp in cutoutSurfaces)
        {
            kvp.Value.GenerateNormals();
            kvp.Value.Commit(arrayMesh);

            int surfIdx =
                arrayMesh.GetSurfaceCount() - 1;

            if (surfIdx >= 0)
            {
                var mat =
                    new StandardMaterial3D();

                mat.TextureFilter =
                    BaseMaterial3D
                        .TextureFilterEnum
                        .Nearest;

                mat.Transparency =
                    BaseMaterial3D
                        .TransparencyEnum
                        .AlphaScissor;

                mat.AlphaScissorThreshold =
                    0.1f;

                mat.RenderPriority = 0;

                if (kvp.Key != null)
                    mat.AlbedoTexture =
                        kvp.Key;

                arrayMesh.SurfaceSetMaterial(
                    surfIdx,
                    mat
                );
            }
        }

        foreach (var kvp in solidSurfaces)
        {
            kvp.Value.GenerateNormals();
            kvp.Value.Commit(arrayMesh);
            kvp.Value.Commit(solidOnlyMesh);

            int surfIdx =
                arrayMesh.GetSurfaceCount() - 1;

            if (surfIdx >= 0)
            {
                var mat =
                    new StandardMaterial3D();

                mat.TextureFilter =
                    BaseMaterial3D
                        .TextureFilterEnum
                        .Nearest;

                if (kvp.Key != null)
                    mat.AlbedoTexture =
                        kvp.Key;

                arrayMesh.SurfaceSetMaterial(
                    surfIdx,
                    mat
                );
            }
        }

        foreach (var kvp in transparentSurfaces)
        {
            kvp.Value.GenerateNormals();
            kvp.Value.Commit(arrayMesh);

            int surfIdx =
                arrayMesh.GetSurfaceCount() - 1;

            if (surfIdx >= 0)
            {
                var mat =
                    new StandardMaterial3D();

                mat.TextureFilter =
                    BaseMaterial3D
                        .TextureFilterEnum
                        .Nearest;

                mat.Transparency =
                    BaseMaterial3D
                        .TransparencyEnum
                        .Alpha;

                if (kvp.Key != null)
                    mat.AlbedoTexture =
                        kvp.Key;

                arrayMesh.SurfaceSetMaterial(
                    surfIdx,
                    mat
                );
            }
        }

        if (arrayMesh.GetSurfaceCount() > 0)
        {
            _meshInstance.Mesh = arrayMesh;
        }

        // Always rebuild both collision systems.
        // This also removes an old plant target body after
        // the last plant in a chunk is broken.
        CallDeferred(
            "BuildCollision",
            solidOnlyMesh
        );

        CallDeferred(
            "BuildPlantTargetCollision",
            plantTargetMesh
        );

        IsGenerated = true;
    }

    private void AddCrossFaces(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        BlockResource resource,
        int x,
        int y,
        int z)
    {
        Texture2D tex =
            resource.TextureSide ??
            resource.TextureTop;

        if (tex == null)
            return;

        if (!surfaces.TryGetValue(
                tex,
                out SurfaceTool st))
        {
            st = new SurfaceTool();
            st.Begin(
                Mesh.PrimitiveType.Triangles
            );

            surfaces[tex] = st;
        }

        // Diagonal 1
        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(x, y + 1, z)
        );

        st.SetUV(new Vector2(1, 0));
        st.AddVertex(
            new Vector3(
                x + 1,
                y + 1,
                z + 1
            )
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(
                x + 1,
                y,
                z + 1
            )
        );

        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(x, y + 1, z)
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(
                x + 1,
                y,
                z + 1
            )
        );

        st.SetUV(new Vector2(0, 1));
        st.AddVertex(
            new Vector3(x, y, z)
        );

        // Back side of diagonal 1
        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(
                x + 1,
                y + 1,
                z + 1
            )
        );

        st.SetUV(new Vector2(1, 0));
        st.AddVertex(
            new Vector3(x, y + 1, z)
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(x, y, z)
        );

        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(
                x + 1,
                y + 1,
                z + 1
            )
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(x, y, z)
        );

        st.SetUV(new Vector2(0, 1));
        st.AddVertex(
            new Vector3(
                x + 1,
                y,
                z + 1
            )
        );

        // Diagonal 2
        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(x + 1, y + 1, z)
        );

        st.SetUV(new Vector2(1, 0));
        st.AddVertex(
            new Vector3(x, y + 1, z + 1)
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(x, y, z + 1)
        );

        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(x + 1, y + 1, z)
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(x, y, z + 1)
        );

        st.SetUV(new Vector2(0, 1));
        st.AddVertex(
            new Vector3(x + 1, y, z)
        );

        // Back side of diagonal 2
        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(x, y + 1, z + 1)
        );

        st.SetUV(new Vector2(1, 0));
        st.AddVertex(
            new Vector3(x + 1, y + 1, z)
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(x + 1, y, z)
        );

        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(x, y + 1, z + 1)
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(x + 1, y, z)
        );

        st.SetUV(new Vector2(0, 1));
        st.AddVertex(
            new Vector3(x, y, z + 1)
        );
    }

    // Creates a full invisible cube around a cross plant.
    // This is NOT added to the normal collision mesh.
    // It exists only so the player's RayCast3D can select the plant.
    private void AddPlantTargetCube(
        SurfaceTool surface,
        int x,
        int y,
        int z)
    {
        AddQuad(
            surface,
            GetFaceVertices(
                x,
                y,
                z,
                FaceDirection.Top,
                1.0f
            )
        );

        AddQuad(
            surface,
            GetFaceVertices(
                x,
                y,
                z,
                FaceDirection.Bottom,
                1.0f
            )
        );

        AddQuad(
            surface,
            GetFaceVertices(
                x,
                y,
                z,
                FaceDirection.North,
                1.0f
            )
        );

        AddQuad(
            surface,
            GetFaceVertices(
                x,
                y,
                z,
                FaceDirection.South,
                1.0f
            )
        );

        AddQuad(
            surface,
            GetFaceVertices(
                x,
                y,
                z,
                FaceDirection.East,
                1.0f
            )
        );

        AddQuad(
            surface,
            GetFaceVertices(
                x,
                y,
                z,
                FaceDirection.West,
                1.0f
            )
        );
    }

    private void AddFlatGroundFace(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        BlockResource resource,
        int x,
        int y,
        int z)
    {
        Texture2D tex = resource.TextureTop;

        if (tex == null)
            return;

        if (!surfaces.TryGetValue(
                tex,
                out SurfaceTool st))
        {
            st = new SurfaceTool();
            st.Begin(
                Mesh.PrimitiveType.Triangles
            );

            surfaces[tex] = st;
        }

        float flatY =
            y + 0.0625f;

        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(x, flatY, z)
        );

        st.SetUV(new Vector2(1, 0));
        st.AddVertex(
            new Vector3(
                x + 1,
                flatY,
                z
            )
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(
                x + 1,
                flatY,
                z + 1
            )
        );

        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(x, flatY, z)
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(
                x + 1,
                flatY,
                z + 1
            )
        );

        st.SetUV(new Vector2(0, 1));
        st.AddVertex(
            new Vector3(
                x,
                flatY,
                z + 1
            )
        );

        // Bottom
        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(
                x,
                flatY,
                z + 1
            )
        );

        st.SetUV(new Vector2(1, 0));
        st.AddVertex(
            new Vector3(
                x + 1,
                flatY,
                z + 1
            )
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(
                x + 1,
                flatY,
                z
            )
        );

        st.SetUV(new Vector2(0, 0));
        st.AddVertex(
            new Vector3(
                x,
                flatY,
                z + 1
            )
        );

        st.SetUV(new Vector2(1, 1));
        st.AddVertex(
            new Vector3(
                x + 1,
                flatY,
                z
            )
        );

        st.SetUV(new Vector2(0, 1));
        st.AddVertex(
            new Vector3(
                x,
                flatY,
                z
            )
        );
    }

    private const float ThinItemHeight = 0.0625f;
    private const int ThinItemGridSize = 16;

    private static readonly Dictionary<
        Texture2D,
        bool[,]
    > _thinItemAlphaCache = new();

    private bool[,] GetOrBuildAlphaMask(
        Texture2D tex)
    {
        if (_thinItemAlphaCache.TryGetValue(
                tex,
                out var cached))
            return cached;

        var mask =
            new bool[
                ThinItemGridSize,
                ThinItemGridSize
            ];

        Image img = tex.GetImage();

        if (img != null)
        {
            img.Convert(Image.Format.Rgba8);

            int w = img.GetWidth();
            int h = img.GetHeight();

            for (
                int v = 0;
                v < ThinItemGridSize;
                v++)
            {
                for (
                    int u = 0;
                    u < ThinItemGridSize;
                    u++)
                {
                    int px = Mathf.Clamp(
                        (int)(
                            (u + 0.5f)
                            / ThinItemGridSize
                            * w
                        ),
                        0,
                        w - 1
                    );

                    int py = Mathf.Clamp(
                        (int)(
                            (v + 0.5f)
                            / ThinItemGridSize
                            * h
                        ),
                        0,
                        h - 1
                    );

                    mask[u, v] =
                        img.GetPixel(
                            px,
                            py
                        ).A > 0.5f;
                }
            }
        }

        _thinItemAlphaCache[tex] =
            mask;

        return mask;
    }

    private void AddQuadWithUV(
        SurfaceTool surface,
        Vector3[] verts,
        Vector2 uv)
    {
        surface.SetUV(uv);
        surface.AddVertex(verts[0]);

        surface.SetUV(uv);
        surface.AddVertex(verts[1]);

        surface.SetUV(uv);
        surface.AddVertex(verts[2]);

        surface.SetUV(uv);
        surface.AddVertex(verts[0]);

        surface.SetUV(uv);
        surface.AddVertex(verts[2]);

        surface.SetUV(uv);
        surface.AddVertex(verts[3]);
    }

    private void AddThinItemFaces(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        BlockResource resource,
        int x,
        int y,
        int z)
    {
        Texture2D tex =
            resource.TextureTop;

        if (tex == null)
            return;

        float topY =
            y + ThinItemHeight;

        SurfaceTool st =
            GetOrCreateSurface(
                surfaces,
                tex
            );

        AddQuad(
            st,
            new Vector3[]
            {
                new Vector3(x, topY, z),
                new Vector3(x + 1, topY, z),
                new Vector3(
                    x + 1,
                    topY,
                    z + 1
                ),
                new Vector3(
                    x,
                    topY,
                    z + 1
                )
            }
        );

        AddQuad(
            st,
            new Vector3[]
            {
                new Vector3(
                    x,
                    y,
                    z + 1
                ),
                new Vector3(
                    x + 1,
                    y,
                    z + 1
                ),
                new Vector3(
                    x + 1,
                    y,
                    z
                ),
                new Vector3(x, y, z)
            }
        );

        bool[,] mask =
            GetOrBuildAlphaMask(tex);

        float step =
            1f / ThinItemGridSize;

        for (
            int v = 0;
            v < ThinItemGridSize;
            v++)
        {
            for (
                int u = 0;
                u < ThinItemGridSize;
                u++)
            {
                if (!mask[u, v])
                    continue;

                float x0 =
                    x + u * step;

                float x1 =
                    x0 + step;

                float z0 =
                    z + v * step;

                float z1 =
                    z0 + step;

                Vector2 swatchUV =
                    new Vector2(
                        (u + 0.5f) * step,
                        1f -
                        (v + 0.5f) * step
                    );

                if (u == 0 ||
                    !mask[u - 1, v])
                {
                    AddQuadWithUV(
                        st,
                        new Vector3[]
                        {
                            new Vector3(
                                x0,
                                y,
                                z1
                            ),
                            new Vector3(
                                x0,
                                y,
                                z0
                            ),
                            new Vector3(
                                x0,
                                topY,
                                z0
                            ),
                            new Vector3(
                                x0,
                                topY,
                                z1
                            )
                        },
                        swatchUV
                    );
                }

                if (u ==
                    ThinItemGridSize - 1 ||
                    !mask[u + 1, v])
                {
                    AddQuadWithUV(
                        st,
                        new Vector3[]
                        {
                            new Vector3(
                                x1,
                                y,
                                z0
                            ),
                            new Vector3(
                                x1,
                                y,
                                z1
                            ),
                            new Vector3(
                                x1,
                                topY,
                                z1
                            ),
                            new Vector3(
                                x1,
                                topY,
                                z0
                            )
                        },
                        swatchUV
                    );
                }

                if (v == 0 ||
                    !mask[u, v - 1])
                {
                    AddQuadWithUV(
                        st,
                        new Vector3[]
                        {
                            new Vector3(
                                x0,
                                y,
                                z0
                            ),
                            new Vector3(
                                x1,
                                y,
                                z0
                            ),
                            new Vector3(
                                x1,
                                topY,
                                z0
                            ),
                            new Vector3(
                                x0,
                                topY,
                                z0
                            )
                        },
                        swatchUV
                    );
                }

                if (v ==
                    ThinItemGridSize - 1 ||
                    !mask[u, v + 1])
                {
                    AddQuadWithUV(
                        st,
                        new Vector3[]
                        {
                            new Vector3(
                                x1,
                                y,
                                z1
                            ),
                            new Vector3(
                                x0,
                                y,
                                z1
                            ),
                            new Vector3(
                                x0,
                                topY,
                                z1
                            ),
                            new Vector3(
                                x1,
                                topY,
                                z1
                            )
                        },
                        swatchUV
                    );
                }
            }
        }
    }

    private void BuildCollision(
        ArrayMesh mesh)
    {
        var faces = mesh.GetFaces();

        if (faces.Length < 3)
            return;

        var newCollisionBody =
            new StaticBody3D();

        newCollisionBody.CollisionLayer =
            WorldCollisionLayer;

        newCollisionBody.CollisionMask =
            WorldCollisionLayer;

        newCollisionBody.SetMeta(
            "chunk",
            this
        );

        var concave =
            new ConcavePolygonShape3D();

        concave.BackfaceCollision = true;
        concave.SetFaces(faces);

        var shape =
            new CollisionShape3D();

        shape.Shape = concave;
        newCollisionBody.AddChild(shape);

        GetParent().AddChild(
            newCollisionBody
        );

        newCollisionBody.GlobalPosition =
            GlobalPosition;

        if (_collisionBody != null &&
            IsInstanceValid(_collisionBody))
        {
            _collisionBody.QueueFree();
        }

        _collisionBody =
            newCollisionBody;
    }

    // Builds the invisible plant interaction collider.
    // Layer 2 means the player can raycast against it without
    // physically colliding with it, assuming the player body
    // only uses the normal world collision layer.
    private void BuildPlantTargetCollision(
        ArrayMesh mesh)
    {
        var faces = mesh.GetFaces();

        if (faces.Length < 3)
        {
            if (_plantTargetBody != null &&
                IsInstanceValid(_plantTargetBody))
            {
                _plantTargetBody.QueueFree();
            }

            _plantTargetBody = null;
            return;
        }

        var newPlantTargetBody =
            new StaticBody3D();

        newPlantTargetBody.CollisionLayer =
            PlantTargetCollisionLayer;

        newPlantTargetBody.CollisionMask = 0;

        newPlantTargetBody.SetMeta(
            "chunk",
            this
        );

        var concave =
            new ConcavePolygonShape3D();

        concave.BackfaceCollision = true;
        concave.SetFaces(faces);

        var shape =
            new CollisionShape3D();

        shape.Shape = concave;

        newPlantTargetBody.AddChild(
            shape
        );

        GetParent().AddChild(
            newPlantTargetBody
        );

        newPlantTargetBody.GlobalPosition =
            GlobalPosition;

        if (_plantTargetBody != null &&
            IsInstanceValid(_plantTargetBody))
        {
            _plantTargetBody.QueueFree();
        }

        _plantTargetBody =
            newPlantTargetBody;
    }

    private bool HasExposedFace(
        int x,
        int y,
        int z)
    {
        return IsAirAt(x + 1, y, z) ||
               IsAirAt(x - 1, y, z) ||
               IsAirAt(x, y + 1, z) ||
               IsAirAt(x, y - 1, z) ||
               IsAirAt(x, y, z + 1) ||
               IsAirAt(x, y, z - 1);
    }

    private bool IsAirAt(
        int x,
        int y,
        int z)
    {
        if (y < 0 || y >= HEIGHT)
            return true;

        if (x < 0 ||
            x >= SIZE ||
            z < 0 ||
            z >= SIZE)
            return false;

        return _blocks[x, y, z].IsAir();
    }

    private SurfaceTool GetOrCreateSurface(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        Texture2D texture)
    {
        if (!surfaces.ContainsKey(texture))
        {
            var st =
                new SurfaceTool();

            st.Begin(
                Mesh.PrimitiveType.Triangles
            );

            surfaces[texture] = st;
        }

        return surfaces[texture];
    }

    private void AddFullBlockFaces(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        BlockState block,
        BlockResource resource,
        int x,
        int y,
        int z)
    {
        if (ShouldDrawFace(
                x,
                y + 1,
                z,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureTop
                ),
                GetFaceVertices(
                    x,
                    y,
                    z,
                    FaceDirection.Top,
                    1.0f
                )
            );
        }

        if (ShouldDrawFace(
                x,
                y - 1,
                z,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureBottom
                ),
                GetFaceVertices(
                    x,
                    y,
                    z,
                    FaceDirection.Bottom,
                    1.0f
                )
            );
        }

        if (ShouldDrawFace(
                x,
                y,
                z - 1,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureSide
                ),
                GetFaceVertices(
                    x,
                    y,
                    z,
                    FaceDirection.North,
                    1.0f
                )
            );
        }

        if (ShouldDrawFace(
                x,
                y,
                z + 1,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureSide
                ),
                GetFaceVertices(
                    x,
                    y,
                    z,
                    FaceDirection.South,
                    1.0f
                )
            );
        }

        if (ShouldDrawFace(
                x - 1,
                y,
                z,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureSide
                ),
                GetFaceVertices(
                    x,
                    y,
                    z,
                    FaceDirection.West,
                    1.0f
                )
            );
        }

        if (ShouldDrawFace(
                x + 1,
                y,
                z,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureSide
                ),
                GetFaceVertices(
                    x,
                    y,
                    z,
                    FaceDirection.East,
                    1.0f
                )
            );
        }
    }

    private static readonly Dictionary<
        FaceDirection,
        Vector3
    > _faceNormals = new()
    {
        {
            FaceDirection.Top,
            new Vector3(0, 1, 0)
        },
        {
            FaceDirection.Bottom,
            new Vector3(0, -1, 0)
        },
        {
            FaceDirection.North,
            new Vector3(0, 0, -1)
        },
        {
            FaceDirection.South,
            new Vector3(0, 0, 1)
        },
        {
            FaceDirection.West,
            new Vector3(-1, 0, 0)
        },
        {
            FaceDirection.East,
            new Vector3(1, 0, 0)
        }
    };

    private const float OreOverlayNudge =
        0.0015f;

    private Vector3[] NudgeVerts(
        Vector3[] verts,
        FaceDirection dir)
    {
        Vector3 offset =
            _faceNormals[dir] *
            OreOverlayNudge;

        var nudged =
            new Vector3[verts.Length];

        for (int i = 0;
             i < verts.Length;
             i++)
        {
            nudged[i] =
                verts[i] + offset;
        }

        return nudged;
    }

    private void AddOreOverlayFaces(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        Texture2D overlayTexture,
        BlockResource resource,
        int x,
        int y,
        int z)
    {
        if (overlayTexture == null)
            return;

        if (ShouldDrawFace(
                x,
                y + 1,
                z,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    overlayTexture
                ),
                NudgeVerts(
                    GetFaceVertices(
                        x,
                        y,
                        z,
                        FaceDirection.Top,
                        1.0f
                    ),
                    FaceDirection.Top
                )
            );
        }

        if (ShouldDrawFace(
                x,
                y - 1,
                z,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    overlayTexture
                ),
                NudgeVerts(
                    GetFaceVertices(
                        x,
                        y,
                        z,
                        FaceDirection.Bottom,
                        1.0f
                    ),
                    FaceDirection.Bottom
                )
            );
        }

        if (ShouldDrawFace(
                x,
                y,
                z - 1,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    overlayTexture
                ),
                NudgeVerts(
                    GetFaceVertices(
                        x,
                        y,
                        z,
                        FaceDirection.North,
                        1.0f
                    ),
                    FaceDirection.North
                )
            );
        }

        if (ShouldDrawFace(
                x,
                y,
                z + 1,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    overlayTexture
                ),
                NudgeVerts(
                    GetFaceVertices(
                        x,
                        y,
                        z,
                        FaceDirection.South,
                        1.0f
                    ),
                    FaceDirection.South
                )
            );
        }

        if (ShouldDrawFace(
                x - 1,
                y,
                z,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    overlayTexture
                ),
                NudgeVerts(
                    GetFaceVertices(
                        x,
                        y,
                        z,
                        FaceDirection.West,
                        1.0f
                    ),
                    FaceDirection.West
                )
            );
        }

        if (ShouldDrawFace(
                x + 1,
                y,
                z,
                resource.IsTransparent))
        {
            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    overlayTexture
                ),
                NudgeVerts(
                    GetFaceVertices(
                        x,
                        y,
                        z,
                        FaceDirection.East,
                        1.0f
                    ),
                    FaceDirection.East
                )
            );
        }
    }

    private void AddChiseledBlockFaces(
        Dictionary<Texture2D, SurfaceTool> surfaces,
        BlockState block,
        BlockResource resource,
        int x,
        int y,
        int z)
    {
        Vector3I[] bitOffsets =
        new Vector3I[]
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

        for (int bit = 0;
             bit < 8;
             bit++)
        {
            if (!block.IsBitActive(bit))
                continue;

            Vector3I offset =
                bitOffsets[bit];

            float bx =
                x + offset.X * 0.5f;

            float by =
                y + offset.Y * 0.5f;

            float bz =
                z + offset.Z * 0.5f;

            float s = 0.5f;

            int topBit =
                bit + 4;

            if (topBit >= 8 ||
                !block.IsBitActive(topBit))
            {
                AddQuad(
                    GetOrCreateSurface(
                        surfaces,
                        resource.TextureTop
                    ),
                    GetFaceVertices(
                        bx,
                        by,
                        bz,
                        FaceDirection.Top,
                        s
                    )
                );
            }

            int bottomBit =
                bit - 4;

            if (bottomBit < 0 ||
                !block.IsBitActive(bottomBit))
            {
                AddQuad(
                    GetOrCreateSurface(
                        surfaces,
                        resource.TextureBottom
                    ),
                    GetFaceVertices(
                        bx,
                        by,
                        bz,
                        FaceDirection.Bottom,
                        s
                    )
                );
            }

            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureSide
                ),
                GetFaceVertices(
                    bx,
                    by,
                    bz,
                    FaceDirection.North,
                    s
                )
            );

            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureSide
                ),
                GetFaceVertices(
                    bx,
                    by,
                    bz,
                    FaceDirection.South,
                    s
                )
            );

            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureSide
                ),
                GetFaceVertices(
                    bx,
                    by,
                    bz,
                    FaceDirection.West,
                    s
                )
            );

            AddQuad(
                GetOrCreateSurface(
                    surfaces,
                    resource.TextureSide
                ),
                GetFaceVertices(
                    bx,
                    by,
                    bz,
                    FaceDirection.East,
                    s
                )
            );
        }
    }

    private bool ShouldDrawFace(
        int nx,
        int ny,
        int nz,
        bool currentIsTransparent)
    {
        if (!IsInBounds(nx, ny, nz))
            return true;

        BlockState neighbor =
            _blocks[nx, ny, nz];

        if (neighbor.IsAir())
            return true;

        BlockResource neighborResource =
            BlockRegistry.Instance.GetBlock(
                neighbor.BlockId
            );

        if (neighborResource == null)
            return true;

        if (neighborResource.IsTransparent &&
            !currentIsTransparent)
            return true;

        if (!neighbor.IsFullBlock())
            return true;

        return false;
    }

    private Vector3[] GetFaceVertices(
        float x,
        float y,
        float z,
        FaceDirection dir,
        float size)
    {
        float s = size;

        return dir switch
        {
            FaceDirection.Top =>
                new Vector3[]
                {
                    new Vector3(
                        x,
                        y + s,
                        z
                    ),
                    new Vector3(
                        x + s,
                        y + s,
                        z
                    ),
                    new Vector3(
                        x + s,
                        y + s,
                        z + s
                    ),
                    new Vector3(
                        x,
                        y + s,
                        z + s
                    )
                },

            FaceDirection.Bottom =>
                new Vector3[]
                {
                    new Vector3(
                        x,
                        y,
                        z + s
                    ),
                    new Vector3(
                        x + s,
                        y,
                        z + s
                    ),
                    new Vector3(
                        x + s,
                        y,
                        z
                    ),
                    new Vector3(
                        x,
                        y,
                        z
                    )
                },

            FaceDirection.North =>
                new Vector3[]
                {
                    new Vector3(
                        x,
                        y,
                        z
                    ),
                    new Vector3(
                        x + s,
                        y,
                        z
                    ),
                    new Vector3(
                        x + s,
                        y + s,
                        z
                    ),
                    new Vector3(
                        x,
                        y + s,
                        z
                    )
                },

            FaceDirection.South =>
                new Vector3[]
                {
                    new Vector3(
                        x + s,
                        y,
                        z + s
                    ),
                    new Vector3(
                        x,
                        y,
                        z + s
                    ),
                    new Vector3(
                        x,
                        y + s,
                        z + s
                    ),
                    new Vector3(
                        x + s,
                        y + s,
                        z + s
                    )
                },

            FaceDirection.East =>
                new Vector3[]
                {
                    new Vector3(
                        x + s,
                        y,
                        z
                    ),
                    new Vector3(
                        x + s,
                        y,
                        z + s
                    ),
                    new Vector3(
                        x + s,
                        y + s,
                        z + s
                    ),
                    new Vector3(
                        x + s,
                        y + s,
                        z
                    )
                },

            FaceDirection.West =>
                new Vector3[]
                {
                    new Vector3(
                        x,
                        y,
                        z + s
                    ),
                    new Vector3(
                        x,
                        y,
                        z
                    ),
                    new Vector3(
                        x,
                        y + s,
                        z
                    ),
                    new Vector3(
                        x,
                        y + s,
                        z + s
                    )
                },

            _ => new Vector3[4]
        };
    }

    private void AddQuad(
        SurfaceTool surface,
        Vector3[] verts)
    {
        Vector2[] uvs =
        new Vector2[]
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

    public void ExplodeAtWorld(
        Vector3I center,
        int radius = 3)
    {
        if (_chunkManager == null)
        {
            GD.PrintErr(
                "[Bomb] ChunkManager reference is null."
            );

            return;
        }

        int radiusSquared =
            radius * radius;

        for (
            int x = center.X - radius;
            x <= center.X + radius;
            x++)
        {
            for (
                int y = center.Y - radius;
                y <= center.Y + radius;
                y++)
            {
                for (
                    int z = center.Z - radius;
                    z <= center.Z + radius;
                    z++)
                {
                    int dx =
                        x - center.X;

                    int dy =
                        y - center.Y;

                    int dz =
                        z - center.Z;

                    if (
                        (dx * dx) +
                        (dy * dy) +
                        (dz * dz)
                        > radiusSquared)
                    {
                        continue;
                    }

                    Vector3I worldPos =
                        new Vector3I(
                            x,
                            y,
                            z
                        );

                    BlockState block =
                        _chunkManager.GetBlockAtWorld(
                            worldPos
                        );

                    if (block.IsAir())
                        continue;

                    if (block.BlockId ==
                        "bedrock")
                        continue;

                    _chunkManager.SetBlockAtWorld(
                        worldPos,
                        BlockState.Air
                    );
                }
            }
        }
    }
}