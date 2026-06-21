using Godot;
using System.Collections.Generic;

public partial class ChunkManager : Node3D
{
    [Export] public int RenderDistance { get; set; } = 4;
    [Export] public int Seed { get; set; } = 12345;

    private Dictionary<Vector3I, Chunk> _chunks = new();
    private Vector3I _lastPlayerChunk = new Vector3I(999, 999, 999);
    private Node3D _player;

    public override void _Ready()
    {
        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _noise.Seed = 12345;
        _noise.Frequency = 0.02f;
        _player = GetNodeOrNull<Node3D>("/root/TestWorld/Player");
        
        var canvasLayer = new CanvasLayer();
GetTree().Root.CallDeferred("add_child", canvasLayer);

var crosshair = new ColorRect();
crosshair.Color = new Color(1, 1, 1);
crosshair.Size = new Vector2(2, 2);
crosshair.PivotOffset = new Vector2(1, 1);
crosshair.MouseFilter = Control.MouseFilterEnum.Ignore;

crosshair.AnchorLeft = 0.5f;
crosshair.AnchorRight = 0.5f;
crosshair.AnchorTop = 0.5f;
crosshair.AnchorBottom = 0.5f;
crosshair.OffsetLeft = -1;
crosshair.OffsetTop = -1;
crosshair.OffsetRight = 1;
crosshair.OffsetBottom = 1;

canvasLayer.CallDeferred("add_child", crosshair);

        if (_player == null)
            GD.Print("ChunkManager: No player found");
        else
            GD.Print("ChunkManager: Player found");

        GD.Print("Calling UpdateChunks...");
        UpdateChunks();

        if (_player != null)
    _player.GlobalPosition = new Vector3(8, 60, 8);
    }

   public override void _Process(double delta)
{
    if (_player == null) return;

    Vector3I currentChunk = WorldToChunk(_player.GlobalPosition);
    
    // Only trigger on X/Z change, ignore Y (player falling changes Y constantly)
    Vector3I currentChunkXZ = new Vector3I(currentChunk.X, 0, currentChunk.Z);
    Vector3I lastChunkXZ = new Vector3I(_lastPlayerChunk.X, 0, _lastPlayerChunk.Z);
    
    if (currentChunkXZ != lastChunkXZ)
    {
        _lastPlayerChunk = currentChunk;
        UpdateChunks();
    }
}
    public Chunk GetChunk(Vector3I chunkPos)
{
    _chunks.TryGetValue(chunkPos, out Chunk chunk);
    return chunk;
}

    public Vector3I WorldToChunk(Vector3 worldPos)
    {
        return new Vector3I(
            Mathf.FloorToInt(worldPos.X / Chunk.SIZE),
            Mathf.FloorToInt(worldPos.Y / Chunk.HEIGHT),
            Mathf.FloorToInt(worldPos.Z / Chunk.SIZE)
        );
    }

    private void UpdateChunks()
    {
        Vector3I playerChunk = _player != null
            ? WorldToChunk(_player.GlobalPosition)
            : Vector3I.Zero;

        GD.Print($"UpdateChunks - player chunk: {playerChunk}");

        for (int x = -RenderDistance; x <= RenderDistance; x++)
        {
            for (int z = -RenderDistance; z <= RenderDistance; z++)
            {
                for (int y = 0; y < 6; y++)
                {
                    Vector3I chunkPos = new Vector3I(
                        playerChunk.X + x,
                        y,
                        playerChunk.Z + z
                    );

                    if (!_chunks.ContainsKey(chunkPos))
                        LoadChunk(chunkPos);
                }
            }
        }

        var toUnload = new List<Vector3I>();
        foreach (var pos in _chunks.Keys)
        {
            int dx = Mathf.Abs(pos.X - playerChunk.X);
            int dz = Mathf.Abs(pos.Z - playerChunk.Z);
            if (dx > RenderDistance + 1 || dz > RenderDistance + 1)
                toUnload.Add(pos);
        }

        foreach (var pos in toUnload)
            UnloadChunk(pos);

        GD.Print($"Total chunks: {_chunks.Count}");
    }

    private void LoadChunk(Vector3I chunkPos)
{
    if (_chunks.ContainsKey(chunkPos)) return;

    var chunk = new Chunk();
    AddChild(chunk);
    chunk.Initialize(chunkPos);
    GenerateChunk(chunk, chunkPos);
    chunk.BuildMesh();
    _chunks[chunkPos] = chunk;
}

    private void UnloadChunk(Vector3I chunkPos)
    {
        if (!_chunks.TryGetValue(chunkPos, out Chunk chunk)) return;
        chunk.QueueFree();
        _chunks.Remove(chunkPos);
    }

    private FastNoiseLite _noise = new FastNoiseLite();

private void GenerateChunk(Chunk chunk, Vector3I chunkPos)
{
    for (int x = 0; x < Chunk.SIZE; x++)
    {
        for (int z = 0; z < Chunk.SIZE; z++)
        {
            int worldX = chunkPos.X * Chunk.SIZE + x;
            int worldZ = chunkPos.Z * Chunk.SIZE + z;

            float noiseValue = _noise.GetNoise2D(worldX, worldZ);
            int terrainHeight = (int)((noiseValue + 1f) * 0.5f * 32f + 16f);

            for (int y = 0; y < Chunk.HEIGHT; y++)
            {
                int worldY = chunkPos.Y * Chunk.HEIGHT + y;

                BlockState block;
                if (worldY == terrainHeight)
                {
                    block = new BlockState { BlockId = "dirt", BitMask = 0xFF, Features = new[] { "grass" } };
                }
                else if (worldY < terrainHeight)
                {
                    block = new BlockState { BlockId = "stone", BitMask = 0xFF };
                }
                else
                {
                    block = BlockState.Air;
                }

                chunk.SetBlock(x, y, z, block);
            }
        }
    }
}

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