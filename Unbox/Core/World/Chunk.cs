using Godot;
using System.Collections.Generic;

/// <summary>
/// A 16x16x16 chunk of blocks.
/// Handles block storage and mesh generation.
/// </summary>
public partial class Chunk : Node3D
{
    // Chunk dimensions
    public const int SIZE = 16;
    public const int HEIGHT = 16;

    // Block data - 16x16x16 array of block states
    private BlockState[,,] _blocks = new BlockState[SIZE, HEIGHT, SIZE];

    // Mesh generation
    private MeshInstance3D _meshInstance;
    private MeshInstance3D _transparentMeshInstance;
    private StaticBody3D _collisionBody;
    private CollisionShape3D _collisionShape;

    // Chunk position in chunk coordinates
    // Multiply by SIZE to get world position
    public Vector3I ChunkPosition { get; private set; }

    // Is this chunk ready to render
    public bool IsGenerated { get; private set; } = false;

    // Dirty flag - needs mesh rebuild
    private bool _isDirty = false;

    // Block face directions
    private static readonly Vector3I[] Directions = new Vector3I[]
    {
        Vector3I.Up,
        Vector3I.Down,
        Vector3I.Left,
        Vector3I.Right,
        Vector3I.Forward,
        Vector3I.Back
    };

    // UV coordinates for 16x16 pixel textures
    private static readonly Vector2[] FaceUVs = new Vector2[]
    {
        new Vector2(0, 0),
        new Vector2(1, 0),
        new Vector2(1, 1),
        new Vector2(0, 1)
    };

    public override void _Ready()
    {
        // Setup mesh instances
        _meshInstance = new MeshInstance3D();
        AddChild(_meshInstance);

        _transparentMeshInstance = new MeshInstance3D();
        AddChild(_transparentMeshInstance);

        // Setup collision
        _collisionBody = new StaticBody3D();
        AddChild(_collisionBody);

        _collisionShape = new CollisionShape3D();
        _collisionBody.AddChild(_collisionShape);
    }

    // Initialize chunk at a given chunk position
    public void Initialize(Vector3I chunkPosition)
    {
        ChunkPosition = chunkPosition;
        GlobalPosition = new Vector3(
            chunkPosition.X * SIZE,
            chunkPosition.Y * HEIGHT,
            chunkPosition.Z * SIZE
        );
    }

    // Get a block at local chunk coordinates
    public BlockState GetBlock(int x, int y, int z)
    {
        if (!IsInBounds(x, y, z))
            return BlockState.Air;
        return _blocks[x, y, z];
    }

    // Set a block at local chunk coordinates
    public void SetBlock(int x, int y, int z, BlockState state)
    {
        if (!IsInBounds(x, y, z)) return;
        _blocks[x, y, z] = state;
        _isDirty = true;
    }

    // Check if coordinates are within chunk bounds
    private bool IsInBounds(int x, int y, int z)
    {
        return x >= 0 && x < SIZE &&
               y >= 0 && y < HEIGHT &&
               z >= 0 && z < SIZE;
    }

    // Mark chunk as needing mesh rebuild
    public void MarkDirty()
    {
        _isDirty = true;
    }

    // Called every frame
    public override void _Process(double delta)
    {
        if (_isDirty)
        {
            BuildMesh();
            _isDirty = false;
        }
    }

    // Build the visible mesh for this chunk
    public void BuildMesh()
    {
        var solidSurface = new SurfaceTool();
        var transparentSurface = new SurfaceTool();

        solidSurface.Begin(Mesh.PrimitiveType.Triangles);
        transparentSurface.Begin(Mesh.PrimitiveType.Triangles);

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

                    // Choose surface based on transparency
                    SurfaceTool surface = resource.IsTransparent
                        ? transparentSurface
                        : solidSurface;

                    // Full block rendering
                    if (block.IsFullBlock())
                    {
                        AddFullBlockFaces(surface, block,
                            resource, x, y, z);
                    }
                    else
                    {
                        // Chiseled block - render per bit
                        AddChiseledBlockFaces(surface, block,
                            resource, x, y, z);
                    }
                }
            }
        }

        // Finalize solid mesh
        solidSurface.GenerateNormals();
        var solidMesh = solidSurface.Commit();

        if (solidMesh != null)
        {
            var mat = new StandardMaterial3D();
            mat.TextureFilter =
                BaseMaterial3D.TextureFilterEnum.Nearest;
            solidMesh.SurfaceSetMaterial(0, mat);
            _meshInstance.Mesh = solidMesh;
        }

        // Finalize transparent mesh
        transparentSurface.GenerateNormals();
        var transparentMesh = transparentSurface.Commit();

        if (transparentMesh != null)
        {
            var mat = new StandardMaterial3D();
            mat.TextureFilter =
                BaseMaterial3D.TextureFilterEnum.Nearest;
            mat.Transparency =
                BaseMaterial3D.TransparencyEnum.Alpha;
            transparentMesh.SurfaceSetMaterial(0, mat);
            _transparentMeshInstance.Mesh = transparentMesh;
        }

        // Build collision shape from solid mesh
        if (solidMesh != null)
        {
            _collisionShape.Shape =
                solidMesh.CreateTrimeshShape();
        }

        IsGenerated = true;
    }

    // Add faces for a full solid block
    private void AddFullBlockFaces(SurfaceTool surface,
        BlockState block, BlockResource resource,
        int x, int y, int z)
    {
        // Check all 6 faces
        // Only add face if neighbor is air/transparent
        // Top face
        if (ShouldDrawFace(x, y + 1, z, resource.IsTransparent))
            AddFace(surface, x, y, z,
                FaceDirection.Top, resource.TextureTop);

        // Bottom face
        if (ShouldDrawFace(x, y - 1, z, resource.IsTransparent))
            AddFace(surface, x, y, z,
                FaceDirection.Bottom, resource.TextureBottom);

        // North face
        if (ShouldDrawFace(x, y, z - 1, resource.IsTransparent))
            AddFace(surface, x, y, z,
                FaceDirection.North, resource.TextureSide);

        // South face
        if (ShouldDrawFace(x, y, z + 1, resource.IsTransparent))
            AddFace(surface, x, y, z,
                FaceDirection.South, resource.TextureSide);

        // West face
        if (ShouldDrawFace(x - 1, y, z, resource.IsTransparent))
            AddFace(surface, x, y, z,
                FaceDirection.West, resource.TextureSide);

        // East face
        if (ShouldDrawFace(x + 1, y, z, resource.IsTransparent))
            AddFace(surface, x, y, z,
                FaceDirection.East, resource.TextureSide);
    }

    // Add faces for a chiseled block (per bit)
    private void AddChiseledBlockFaces(SurfaceTool surface,
        BlockState block, BlockResource resource,
        int x, int y, int z)
    {
        // Each bit occupies half a block on each axis
        // Bit positions in local block space:
        // Bottom layer (bits 0-3):
        //   0=(0,0,0) 1=(1,0,0) 2=(0,0,1) 3=(1,0,1)
        // Top layer (bits 4-7):
        //   4=(0,1,0) 5=(1,1,0) 6=(0,1,1) 7=(1,1,1)

        Vector3I[] bitOffsets = new Vector3I[]
        {
            new Vector3I(0, 0, 0), // bit 0
            new Vector3I(1, 0, 0), // bit 1
            new Vector3I(0, 0, 1), // bit 2
            new Vector3I(1, 0, 1), // bit 3
            new Vector3I(0, 1, 0), // bit 4
            new Vector3I(1, 1, 0), // bit 5
            new Vector3I(0, 1, 1), // bit 6
            new Vector3I(1, 1, 1)  // bit 7
        };

        for (int bit = 0; bit < 8; bit++)
        {
            if (!block.IsBitActive(bit)) continue;

            Vector3I offset = bitOffsets[bit];

            // Position of this half-block in world space
            float bx = x + offset.X * 0.5f;
            float by = y + offset.Y * 0.5f;
            float bz = z + offset.Z * 0.5f;

            // Add half-size cube faces at this position
            AddHalfBlockFaces(surface, block, resource,
                bx, by, bz, bit, bitOffsets);
        }
    }

    // Add faces for a single half-block bit
    private void AddHalfBlockFaces(SurfaceTool surface,
        BlockState block, BlockResource resource,
        float bx, float by, float bz,
        int bitIndex, Vector3I[] bitOffsets)
    {
        float s = 0.5f; // half block size

        // Top face - draw if bit above is empty
        int topBit = bitIndex + 4;
        bool topEmpty = topBit >= 8 ||
            !block.IsBitActive(topBit);
        if (topEmpty)
            AddHalfFace(surface, bx, by, bz,
                FaceDirection.Top, s, resource.TextureTop);

        // Bottom face
        int bottomBit = bitIndex - 4;
        bool bottomEmpty = bottomBit < 0 ||
            !block.IsBitActive(bottomBit);
        if (bottomEmpty)
            AddHalfFace(surface, bx, by, bz,
                FaceDirection.Bottom, s, resource.TextureBottom);

        // Side faces - simplified for now
        AddHalfFace(surface, bx, by, bz,
            FaceDirection.North, s, resource.TextureSide);
        AddHalfFace(surface, bx, by, bz,
            FaceDirection.South, s, resource.TextureSide);
        AddHalfFace(surface, bx, by, bz,
            FaceDirection.West, s, resource.TextureSide);
        AddHalfFace(surface, bx, by, bz,
            FaceDirection.East, s, resource.TextureSide);
    }

    // Should we draw a face looking toward neighbor position
    private bool ShouldDrawFace(int nx, int ny, int nz,
        bool currentIsTransparent)
    {
        // Always draw faces at chunk boundary for now
        if (!IsInBounds(nx, ny, nz)) return true;

        BlockState neighbor = _blocks[nx, ny, nz];
        if (neighbor.IsAir()) return true;

        BlockResource neighborResource =
            BlockRegistry.Instance.GetBlock(neighbor.BlockId);
        if (neighborResource == null) return true;

        // Draw if neighbor is transparent and we are not
        if (neighborResource.IsTransparent && !currentIsTransparent)
            return true;

        // Draw if neighbor is not a full block
        if (!neighbor.IsFullBlock()) return true;

        return false;
    }

    // Face direction enum
    private enum FaceDirection
    {
        Top, Bottom, North, South, East, West
    }

    // Add a full size block face
    private void AddFace(SurfaceTool surface,
        int x, int y, int z,
        FaceDirection dir, Texture2D texture)
    {
        // Vertices for each face direction
        Vector3[] verts = GetFaceVertices(x, y, z, dir, 1.0f);
        AddQuad(surface, verts, texture);
    }

    // Add a half size block face
    private void AddHalfFace(SurfaceTool surface,
        float x, float y, float z,
        FaceDirection dir, float size, Texture2D texture)
    {
        Vector3[] verts = GetFaceVertices(x, y, z, dir, size);
        AddQuad(surface, verts, texture);
    }

    // Get the 4 vertices for a face
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

    // Add a quad (2 triangles) to the surface
    private void AddQuad(SurfaceTool surface,
        Vector3[] verts, Texture2D texture)
    {
        // UV coordinates for pixel perfect 16x16 textures
        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        // Triangle 1
        surface.SetUV(uvs[0]);
        surface.AddVertex(verts[0]);
        surface.SetUV(uvs[1]);
        surface.AddVertex(verts[1]);
        surface.SetUV(uvs[2]);
        surface.AddVertex(verts[2]);

        // Triangle 2
        surface.SetUV(uvs[0]);
        surface.AddVertex(verts[0]);
        surface.SetUV(uvs[2]);
        surface.AddVertex(verts[2]);
        surface.SetUV(uvs[3]);
        surface.AddVertex(verts[3]);
    }
}