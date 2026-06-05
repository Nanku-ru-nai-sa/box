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
    private bool _isDirty = false;

    private enum FaceDirection
    {
        Top, Bottom, North, South, East, West
    }

    public override void _Ready()
    {
        _meshInstance = new MeshInstance3D();
        AddChild(_meshInstance);

        _transparentMeshInstance = new MeshInstance3D();
        AddChild(_transparentMeshInstance);

        _collisionBody = new StaticBody3D();
        _collisionBody.CollisionLayer = 1;
        _collisionBody.CollisionMask = 1;
        AddChild(_collisionBody);
    }

    public void Initialize(Vector3I chunkPosition)
    {
        ChunkPosition = chunkPosition;
        GlobalPosition = new Vector3(
            chunkPosition.X * SIZE,
            chunkPosition.Y * HEIGHT,
            chunkPosition.Z * SIZE
        );
    }

    public BlockState GetBlock(int x, int y, int z)
    {
        if (!IsInBounds(x, y, z))
            return BlockState.Air;
        return _blocks[x, y, z];
    }

    public void SetBlock(int x, int y, int z, BlockState state)
    {
        if (!IsInBounds(x, y, z)) return;
        _blocks[x, y, z] = state;
        _isDirty = true;
    }

    private bool IsInBounds(int x, int y, int z)
    {
        return x >= 0 && x < SIZE &&
               y >= 0 && y < HEIGHT &&
               z >= 0 && z < SIZE;
    }

    public void MarkDirty()
    {
        _isDirty = true;
    }

    public override void _Process(double delta)
    {
        if (_isDirty)
        {
            BuildMesh();
            _isDirty = false;
        }
    }

    public void BuildMesh()
    {
        var solidSurfaces = new Dictionary<Texture2D, SurfaceTool>();
        var transparentSurfaces = new Dictionary<Texture2D, SurfaceTool>();

        for (int x = 0; x < SIZE; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                for (int z = 0; z < SIZE; z++)
                {
                    BlockState block = _blocks[x, y, z];
                    if (block.IsAir()) continue;

                    BlockResource resource =
                        BlockRegistry.Instance.GetBlock(block.BlockId);
                    if (resource == null) continue;

                    var surfaces = resource.IsTransparent
                        ? transparentSurfaces
                        : solidSurfaces;

                    if (block.IsFullBlock())
                        AddFullBlockFaces(surfaces, block, resource, x, y, z);
                    else
                        AddChiseledBlockFaces(surfaces, block, resource, x, y, z);
                }
            }
        }

        var arrayMesh = new ArrayMesh();

        foreach (var kvp in solidSurfaces)
        {
            kvp.Value.GenerateNormals();
            kvp.Value.Commit(arrayMesh);
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
        }

        // Build box collision per block
        BuildBoxCollision();

        IsGenerated = true;
    }

    private void BuildBoxCollision()
    {
        // Clear old collision shapes
        foreach (Node child in _collisionBody.GetChildren())
            child.QueueFree();

        for (int x = 0; x < SIZE; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                for (int z = 0; z < SIZE; z++)
                {
                    BlockState block = _blocks[x, y, z];
                    if (block.IsAir()) continue;

                    BlockResource resource =
                        BlockRegistry.Instance.GetBlock(block.BlockId);
                    if (resource == null || !resource.IsSolid) continue;

                    var shape = new CollisionShape3D();
                    var box = new BoxShape3D();
                    box.Size = new Vector3(1f, 1f, 1f);
                    shape.Shape = box;
                    shape.Position = new Vector3(
                        x + 0.5f,
                        y + 0.5f,
                        z + 0.5f
                    );
                    _collisionBody.AddChild(shape);
                }
            }
        }
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
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
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