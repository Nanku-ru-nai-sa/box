using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class BlockRegistry : Node
{
    // Singleton - accessible from anywhere in the game
    public static BlockRegistry Instance { get; private set; }

    // Master dictionary of all blocks
    private Dictionary<string, BlockResource> _blocks = new();

    // Called when the game starts
    public override void _Ready()
    {
        Instance = this;
        LoadAllBlocks();
        GD.Print($"BlockRegistry loaded {_blocks.Count} blocks.");
    }

    // Loads every BlockResource from the Blocks data folder
    private void LoadAllBlocks()
{
    string path = "res://Assets/Data/Blocks/";

    using var dir = DirAccess.Open(path);
    if (dir == null)
    {
        GD.Print("BlockRegistry: No blocks loaded yet.");
        return;
    }

    dir.ListDirBegin();
    string fileName = dir.GetNext();

    while (fileName != "")
    {
        if (fileName.EndsWith(".tres"))
        {
            string fullPath = path + fileName;
            var block = GD.Load<BlockResource>(fullPath);

            if (block != null && block.BlockId != "")
            {
                _blocks[block.BlockId] = block;
                GD.Print($"  Loaded block: {block.BlockId}");
            }
        }
        fileName = dir.GetNext();
    }
}

    // Get a block by its ID - used by everything in the game
    public BlockResource GetBlock(string blockId)
    {
        if (_blocks.TryGetValue(blockId, out BlockResource block))
            return block;

        GD.PrintErr($"BlockRegistry: Block not found: {blockId}");
        return null;
    }

    // Check if a block exists
    public bool BlockExists(string blockId)
    {
        return _blocks.ContainsKey(blockId);
    }

    // Get all registered blocks - useful for creative mode etc
    public IEnumerable<BlockResource> GetAllBlocks()
    {
        return _blocks.Values;
    }
}