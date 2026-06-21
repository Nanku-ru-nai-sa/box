using Godot;
using System.Collections.Generic;

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
        var dirtTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/dirt.png");
        var stoneTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/stone.png");
        var grassTopTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/grass.png");
        var grassSideTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/grass_side.png");
        var sandTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/sand.png");
        var logTopTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/log_top.png");
        var logSideTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/log.png");
        var leavesTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/leaves.png");
        var waterTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/water.png");

        var air = new BlockResource();
        air.BlockId = "air";
        air.DisplayName = "Air";
        air.IsSolid = false;
        air.CanChisel = false;
        air.IsTransparent = true;
        Register(air);

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

        var sand = new BlockResource();
        sand.BlockId = "sand";
        sand.DisplayName = "Sand";
        sand.IsSolid = true;
        sand.CanChisel = true;
        sand.GrassCanGrow = false;
        sand.Hardness = 0.5f;
        sand.TextureTop = sandTex;
        sand.TextureSide = sandTex;
        sand.TextureBottom = sandTex;
        Register(sand);

        var log = new BlockResource();
        log.BlockId = "log";
        log.DisplayName = "Log";
        log.IsSolid = true;
        log.CanChisel = true;
        log.GrassCanGrow = false;
        log.Hardness = 1.0f;
        log.TextureTop = logTopTex;
        log.TextureSide = logSideTex;
        log.TextureBottom = logTopTex;
        Register(log);

        var leaves = new BlockResource();
        leaves.BlockId = "leaves";
        leaves.DisplayName = "Leaves";
        leaves.IsSolid = true;
        leaves.CanChisel = true;
        leaves.GrassCanGrow = false;
        leaves.Hardness = 0.2f;
        leaves.TextureTop = leavesTex;
        leaves.TextureSide = leavesTex;
        leaves.TextureBottom = leavesTex;
        Register(leaves);

        var water = new BlockResource();
        water.BlockId = "water";
        water.DisplayName = "Water";
        water.IsSolid = false;
        water.CanChisel = false;
        water.GrassCanGrow = false;
        water.Hardness = 0f;
        water.IsTransparent = true;
        water.TextureTop = waterTex;
        water.TextureSide = waterTex;
        water.TextureBottom = waterTex;
        Register(water);
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