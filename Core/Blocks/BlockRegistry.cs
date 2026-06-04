using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class BlockRegistry : Node
{
    public static BlockRegistry Instance { get; private set; }
    private Dictionary<string, BlockResource> _blocks = new();

    public override void _Ready()
    {
        Instance = this;
        RegisterBlocks();
        GD.Print($"BlockRegistry loaded {_blocks.Count} blocks.");
    }

   private void RegisterBlocks()
{
    // Load textures
    var dirtTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/dirt.png");
    var stoneTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/stone.png");
    var grassTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/grass.png");
    var grassSideTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/grass_side.png");

    // AIR
    var air = new BlockResource();
    air.BlockId = "air";
    air.DisplayName = "Air";
    air.IsSolid = false;
    air.CanChisel = false;
    air.IsTransparent = true;
    Register(air);

    // DIRT
    var dirt = new BlockResource();
    dirt.BlockId = "dirt";
    dirt.DisplayName = "Dirt";
    dirt.IsSolid = true;
    dirt.CanChisel = true;
    dirt.GrassCanGrow = true;
    dirt.Hardness = 0.5f;
    dirt.TextureTop = dirtTex;
    dirt.TextureSide = dirtTex;
    dirt.TextureBottom = dirtTex;
    Register(dirt);

    // STONE
    var stone = new BlockResource();
    stone.BlockId = "stone";
    stone.DisplayName = "Stone";
    stone.IsSolid = true;
    stone.CanChisel = true;
    stone.GrassCanGrow = false;
    stone.Hardness = 1.5f;
    stone.TextureTop = stoneTex;
    stone.TextureSide = stoneTex;
    stone.TextureBottom = stoneTex;
    Register(stone);

    GD.Print("Blocks registered.");
}

    private void Register(BlockResource block)
    {
        _blocks[block.BlockId] = block;
        GD.Print($"  Registered: {block.BlockId}");
    }

    public BlockResource GetBlock(string blockId)
    {
        if (_blocks.TryGetValue(blockId, out BlockResource block))
            return block;
        GD.PrintErr($"BlockRegistry: Block not found: {blockId}");
        return null;
    }

    public bool BlockExists(string blockId)
    {
        return _blocks.ContainsKey(blockId);
    }

    public IEnumerable<BlockResource> GetAllBlocks()
    {
        return _blocks.Values;
    }
}