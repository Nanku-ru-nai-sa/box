using Godot;
using System.Collections.Generic;

/// <summary>
/// Manages chunk loading and unloading around the player.
/// </summary>
public partial class ChunkManager : Node3D
{
    // How many chunks to load in each direction from player
    [Export] public int RenderDistance { get; set; } = 4;

    // Reference to player node
    [Export] public NodePath PlayerPath { get; set; }
    private Node3D _player;

    // All currently loaded chunks
    private Dictionary<Vector3I, Chunk> _chunks = new();

    // Chunk scene to instance
    private PackedScene _chunkScene;

    // Player's last known chunk position
    private Vector3I _lastPlayerChunk = Vector3I.Zero;

    // World generation seed
    [Export] public int Seed { get; set; } = 12345;

    public override void _Ready()
    {
        // Create chunk scene dynamically
        _chunkScene = null;

        // Get player reference
        if (PlayerPath != null)
            _player = GetNode<Node3D>(PlayerPath);

        // Initial chunk load
        UpdateChunks();

        GD.Print("ChunkManager ready.");
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;

        // Check if player moved to a new chunk
        Vector3I currentChunk = WorldToChunk(_player.GlobalPosition);

        if (currentChunk != _lastPlayerChunk)
        {
            _lastPlayerChunk = currentChunk;
            UpdateChunks();
        }
    }

    // Convert world position to chunk coordinates
    public Vector3I WorldToChunk(Vector3 worldPos)
    {
        return new Vector3I(
            Mathf.FloorToInt(worldPos.X / Chunk.SIZE),
            Mathf.FloorToInt(worldPos.Y / Chunk.HEIGHT),
            Mathf.FloorToInt(worldPos.Z / Chunk.SIZE)
        );
    }

    // Convert chunk coordinates to world position
    public Vector3 ChunkToWorld(Vector3I chunkPos)
    {
        return new Vector3(
            chunkPos.X * Chunk.SIZE,
            chunkPos.Y * Chunk.HEIGHT,
            chunkPos.Z * Chunk.SIZE
        );
    }

    // Load chunks around player, unload far chunks
    private void UpdateChunks()
    {
        Vector3I playerChunk = _player != null
            ? WorldToChunk(_player.GlobalPosition)
            : Vector3I.Zero;

        // Find chunks to load
        List<Vector3I> chunksToLoad = new();

        for (int x = -RenderDistance; x <= RenderDistance; x++)
        {
            for (int z = -RenderDistance; z <= RenderDistance; z++)
            {
                // For now just one layer of chunks vertically
                // We'll expand this when we add proper world height
                for (int y = 0; y < 4; y++)
                {
                    Vector3I chunkPos = new Vector3I(
                        playerChunk.X + x,
                        y,
                        playerChunk.Z + z
                    );

                    if (!_chunks.ContainsKey(chunkPos))
                        chunksToLoad.Add(chunkPos);
                }
            }
        }

        // Load new chunks
        foreach (var pos in chunksToLoad)
            LoadChunk(pos);

        // Unload distant chunks
        List<Vector3I> chunksToUnload = new();

        foreach (var pos in _chunks.Keys)
        {
            int dx = Mathf.Abs(pos.X - playerChunk.X);
            int dz = Mathf.Abs(pos.Z - playerChunk.Z);

            if (dx > RenderDistance + 1 || dz > RenderDistance + 1)
                chunksToUnload.Add(pos);
        }

        foreach (var pos in chunksToUnload)
            UnloadChunk(pos);
    }

    // Load and generate a single chunk
    private void LoadChunk(Vector3I chunkPos)
    {
        if (_chunks.ContainsKey(chunkPos)) return;

        var chunk = new Chunk();
        AddChild(chunk);
        chunk.Initialize(chunkPos);

        // Generate terrain for this chunk
        GenerateChunk(chunk, chunkPos);

        // Build the visual mesh
        chunk.BuildMesh();

        _chunks[chunkPos] = chunk;
    }

    // Unload a chunk and free its memory
    private void UnloadChunk(Vector3I chunkPos)
    {
        if (!_chunks.TryGetValue(chunkPos, out Chunk chunk)) return;

        chunk.QueueFree();
        _chunks.Remove(chunkPos);
    }

    // Generate terrain for a chunk using noise
    private void GenerateChunk(Chunk chunk, Vector3I chunkPos)
    {
        // Setup noise for terrain generation
        var noise = new FastNoiseLite();
        noise.Seed = Seed;
        noise.Frequency = 0.02f;
        noise.FractalOctaves = 4;

        int worldX = chunkPos.X * Chunk.SIZE;
        int worldY = chunkPos.Y * Chunk.HEIGHT;
        int worldZ = chunkPos.Z * Chunk.SIZE;

        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int z = 0; z < Chunk.SIZE; z++)
            {
                // Get terrain height at this x,z position
                float noiseVal = noise.GetNoise2D(
                    worldX + x,
                    worldZ + z
                );

                // Convert noise (-1 to 1) to terrain height
                int terrainHeight = Mathf.RoundToInt(
                    (noiseVal + 1f) * 0.5f * 32f + 16f
                );
                // Range: 16 to 48 blocks high

                for (int y = 0; y < Chunk.HEIGHT; y++)
                {
                    int globalY = worldY + y;

                    if (globalY < terrainHeight - 4)
                    {
                        // Deep underground - stone
                        chunk.SetBlock(x, y, z,
                            new BlockState("stone"));
                    }
                    else if (globalY < terrainHeight - 1)
                    {
                        // Near surface - dirt
                        chunk.SetBlock(x, y, z,
                            new BlockState("dirt"));
                    }
                    else if (globalY == terrainHeight - 1)
                    {
                        // Surface - dirt with grass feature
                        chunk.SetBlock(x, y, z,
                            new BlockState("dirt",
                                new string[] { "grass" }));
                    }
                    else
                    {
                        // Above terrain - air
                        chunk.SetBlock(x, y, z, BlockState.Air);
                    }
                }
            }
        }
    }

    // Get a block at world coordinates
    public BlockState GetBlockAtWorld(Vector3I worldPos)
    {
        Vector3I chunkPos = WorldToChunk(worldPos);

        if (!_chunks.TryGetValue(chunkPos, out Chunk chunk))
            return BlockState.Air;

        int localX = worldPos.X - chunkPos.X * Chunk.SIZE;
        int localY = worldPos.Y - chunkPos.Y * Chunk.HEIGHT;
        int localZ = worldPos.Z - chunkPos.Z * Chunk.SIZE;

        return chunk.GetBlock(localX, localY, localZ);
    }

    // Set a block at world coordinates
    public void SetBlockAtWorld(Vector3I worldPos, BlockState state)
    {
        Vector3I chunkPos = WorldToChunk(worldPos);

        if (!_chunks.TryGetValue(chunkPos, out Chunk chunk))
            return;

        int localX = worldPos.X - chunkPos.X * Chunk.SIZE;
        int localY = worldPos.Y - chunkPos.Y * Chunk.HEIGHT;
        int localZ = worldPos.Z - chunkPos.Z * Chunk.SIZE;

        chunk.SetBlock(localX, localY, localZ, state);
    }
}