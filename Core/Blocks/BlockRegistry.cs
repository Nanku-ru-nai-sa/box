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
        // Load all textures
        var dirtTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/dirt.png");
        var stoneTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/stone.png");
        var rockTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/rock.png");
        var grassTopTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/grass.png");
        var grassSideTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/grass_side.png");
        var sandTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/sand.png");
        var wet_sand1Tex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/wet_sand1.png");
        var wet_sand2Tex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/wet_sand2.png");
        var logTopTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/log_top.png");
        var logSideTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/log.png");
        var planksTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/planks.png");
        var crafterTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/crafter.png");
        var toolBenchTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/tool_bench.png");
        var leavesTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/leaves.png");
        var waterTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/water.png");
        var gravelTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/gravel.png");
        var clayTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/clay.png");
        var bedrockTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/bedrock.png");
        var obsidianTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/obsidian.png");
        var snowTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/snow.png");
        var melonTopTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/melon_top.png");
        var melonSideTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/melon_side.png");
        var roseTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/rose.png");
        var cloverTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/clover.png");
        var dandelionTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/dandelion.png");

        // Ore Hint Rocks - small flat-ground pebbles, same rendering
        // mechanism as clover. Using the existing Items/ icon art instead of
        // making new dedicated rock textures.
        var rockFlintTex  = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Items/flint.png");
        var rockCoalTex   = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Items/coal_ore.png");
        var rockIronTex   = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Items/iron_ore.png");
        var rockTinTex    = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Items/Tin_ore.png");
        var rockCopperTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Items/Copper_ore.png");

        var air = new BlockResource();
        air.BlockId = "air"; air.DisplayName = "Air";
        air.IsSolid = false; air.CanChisel = false; air.IsTransparent = true;
        Register(air);

        var dirt = new BlockResource();
        dirt.BlockId = "dirt"; dirt.DisplayName = "Dirt";
        dirt.IsSolid = true; dirt.CanChisel = true; dirt.GrassCanGrow = true; dirt.Hardness = 0.5f;
        dirt.TextureTop = dirtTex; dirt.TextureSide = dirtTex; dirt.TextureBottom = dirtTex;
        Register(dirt);

        // Grass Block (full block, no overlay needed)
        var grassBlock = new BlockResource();
        grassBlock.BlockId = "grass_block"; grassBlock.DisplayName = "Grass";
        grassBlock.IsSolid = true; grassBlock.CanChisel = true; grassBlock.GrassCanGrow = false; grassBlock.Hardness = 0.5f;
        grassBlock.TextureTop = grassTopTex; grassBlock.TextureSide = grassSideTex; grassBlock.TextureBottom = dirtTex;
        Register(grassBlock);

        var stone = new BlockResource();
        stone.BlockId = "stone"; stone.DisplayName = "Stone";
        stone.IsSolid = true; stone.CanChisel = true; stone.Hardness = 1.5f;
        stone.TextureTop = stoneTex; stone.TextureSide = stoneTex; stone.TextureBottom = stoneTex;
        Register(stone);

        var rock = new BlockResource();
        rock.BlockId = "rock"; rock.DisplayName = "Rock";
        rock.IsSolid = true; rock.CanChisel = true; rock.Hardness = 1.5f;
        rock.TextureTop = rockTex; rock.TextureSide = rockTex; rock.TextureBottom = rockTex;
        Register(rock);

        var sand = new BlockResource();
        sand.BlockId = "sand"; sand.DisplayName = "Sand";
        sand.IsSolid = true; sand.CanChisel = true; sand.Hardness = 0.5f;
        sand.TextureTop = sandTex; sand.TextureSide = sandTex; sand.TextureBottom = sandTex;
        Register(sand);

        var wet_sand1 = new BlockResource();
        wet_sand1.BlockId = "wet_sand1"; wet_sand1.DisplayName = "Wet Sand";
        wet_sand1.IsSolid = true; wet_sand1.CanChisel = true; wet_sand1.Hardness = 0.5f;
        wet_sand1.TextureTop = wet_sand1Tex; wet_sand1.TextureSide = wet_sand1Tex; wet_sand1.TextureBottom = wet_sand1Tex;
        Register(wet_sand1);

        var wet_sand2 = new BlockResource();
        wet_sand2.BlockId = "wet_sand2"; wet_sand2.DisplayName = "Wet Sand";
        wet_sand2.IsSolid = true; wet_sand2.CanChisel = true; wet_sand2.Hardness = 0.5f;
        wet_sand2.TextureTop = wet_sand2Tex; wet_sand2.TextureSide = wet_sand2Tex; wet_sand2.TextureBottom = wet_sand2Tex;
        Register(wet_sand2);

        var gravel = new BlockResource();
        gravel.BlockId = "gravel"; gravel.DisplayName = "Gravel";
        gravel.IsSolid = true; gravel.CanChisel = true; gravel.Hardness = 0.6f;
        gravel.TextureTop = gravelTex; gravel.TextureSide = gravelTex; gravel.TextureBottom = gravelTex;
        Register(gravel);

        var clay = new BlockResource();
        clay.BlockId = "clay"; clay.DisplayName = "Clay";
        clay.IsSolid = true; clay.CanChisel = true; clay.Hardness = 0.6f;
        clay.TextureTop = clayTex; clay.TextureSide = clayTex; clay.TextureBottom = clayTex;
        Register(clay);

        var log = new BlockResource();
        log.BlockId = "log"; log.DisplayName = "Log";
        log.IsSolid = true; log.CanChisel = true; log.Hardness = 1.0f;
        log.TextureTop = logTopTex; log.TextureSide = logSideTex; log.TextureBottom = logTopTex;
        Register(log);

        var planks = new BlockResource();
        planks.BlockId = "planks"; planks.DisplayName = "Planks";
        planks.IsSolid = true; planks.CanChisel = true; planks.Hardness = 1.0f;
        planks.TextureTop = planksTex; planks.TextureSide = planksTex; planks.TextureBottom = planksTex;
        Register(planks);

        var crafter = new BlockResource();
        crafter.BlockId = "crafter"; crafter.DisplayName = "Crafter";
        crafter.IsSolid = true; crafter.CanChisel = false; crafter.Hardness = 1.0f;
        crafter.TextureTop = crafterTex; crafter.TextureSide = crafterTex; crafter.TextureBottom = crafterTex;
        Register(crafter);

        // Tool Bench — new station block. Same shape as Crafter (full block,
        // no overlay), opened the same way (right-click), but drives the
        // ToolBenchPanel UI instead of CraftingPanel. See Player.cs proximity
        // + TryOpenCraftingTable / OpenStationMenu.
        var toolBench = new BlockResource();
        toolBench.BlockId = "tool_bench"; toolBench.DisplayName = "Tool Bench";
        toolBench.IsSolid = true; toolBench.CanChisel = false; toolBench.Hardness = 1.0f;
        toolBench.TextureTop = toolBenchTex; toolBench.TextureSide = toolBenchTex; toolBench.TextureBottom = toolBenchTex;
        Register(toolBench);

        var leaves = new BlockResource();
        leaves.BlockId = "leaves"; leaves.DisplayName = "Leaves";
        leaves.IsSolid = true; leaves.CanChisel = true; leaves.Hardness = 0.2f; leaves.IsTransparent = false;
        leaves.TextureTop = leavesTex; leaves.TextureSide = leavesTex; leaves.TextureBottom = leavesTex;
        Register(leaves);

        var water = new BlockResource();
        water.BlockId = "water"; water.DisplayName = "Water";
        water.IsSolid = false; water.CanChisel = false; water.IsTransparent = true;
        water.TextureTop = waterTex; water.TextureSide = waterTex; water.TextureBottom = waterTex;
        Register(water);

        var bedrock = new BlockResource();
        bedrock.BlockId = "bedrock"; bedrock.DisplayName = "Bedrock";
        bedrock.IsSolid = true; bedrock.CanChisel = false; bedrock.Hardness = float.MaxValue;
        bedrock.TextureTop = bedrockTex; bedrock.TextureSide = bedrockTex; bedrock.TextureBottom = bedrockTex;
        Register(bedrock);

        var obsidian = new BlockResource();
        obsidian.BlockId = "obsidian"; obsidian.DisplayName = "Obsidian";
        obsidian.IsSolid = true; obsidian.CanChisel = true; obsidian.Hardness = 50f;
        obsidian.TextureTop = obsidianTex; obsidian.TextureSide = obsidianTex; obsidian.TextureBottom = obsidianTex;
        Register(obsidian);

        var snow = new BlockResource();
        snow.BlockId = "snow"; snow.DisplayName = "Snow";
        snow.IsSolid = true; snow.CanChisel = true; snow.Hardness = 0.2f;
        snow.TextureTop = snowTex; snow.TextureSide = snowTex; snow.TextureBottom = snowTex;
        Register(snow);

        var melon = new BlockResource();
        melon.BlockId = "melon"; melon.DisplayName = "Melon";
        melon.IsSolid = false; melon.CanChisel = false; melon.Hardness = 0f;
        melon.TextureTop = melonTopTex; melon.TextureSide = melonSideTex; melon.TextureBottom = melonSideTex;
        Register(melon);

        var rose = new BlockResource();
        rose.BlockId = "rose"; rose.DisplayName = "Rose";
        rose.IsSolid = false; rose.CanChisel = false; rose.IsTransparent = true; rose.IsCross = true; rose.Hardness = 0f;
        rose.TextureTop = roseTex; rose.TextureSide = roseTex; rose.TextureBottom = roseTex;
        Register(rose);

        var dandelion = new BlockResource();
        dandelion.BlockId = "dandelion"; dandelion.DisplayName = "Dandelion";
        dandelion.IsSolid = false; dandelion.CanChisel = false; dandelion.IsTransparent = true; dandelion.IsCross = true; dandelion.Hardness = 0f;
        dandelion.TextureTop = dandelionTex; dandelion.TextureSide = dandelionTex; dandelion.TextureBottom = dandelionTex;
        Register(dandelion);

        var clover = new BlockResource();
        clover.BlockId = "clover"; clover.DisplayName = "Clover";
        clover.IsSolid = false; clover.CanChisel = false; clover.IsTransparent = true; clover.IsFlatGround = true; clover.Hardness = 0f;
        clover.TextureTop = cloverTex; clover.TextureSide = cloverTex; clover.TextureBottom = cloverTex;
        Register(clover);

        // Ore Hint Rocks: flat-ground, instant-break, no chiseling - just a
        // little visual tell. IsFlatGround also makes them placeable by the
        // player through the normal block-placement path for free, same as
        // clover, so "placing rocks/items down for fun" doesn't need any
        // extra placement code.
        var rockFlint = new BlockResource();
        rockFlint.BlockId = "rock_flint"; rockFlint.DisplayName = "Flint";
        rockFlint.IsSolid = false; rockFlint.CanChisel = false; rockFlint.IsTransparent = true; rockFlint.IsFlatGround = true; rockFlint.IsThinItem = true; rockFlint.Hardness = 0f;
        rockFlint.TextureTop = rockFlintTex; rockFlint.TextureSide = rockFlintTex; rockFlint.TextureBottom = rockFlintTex;
        Register(rockFlint);

        var rockCoal = new BlockResource();
        rockCoal.BlockId = "rock_coal"; rockCoal.DisplayName = "Coal Bit";
        rockCoal.IsSolid = false; rockCoal.CanChisel = false; rockCoal.IsTransparent = true; rockCoal.IsFlatGround = true; rockCoal.IsThinItem = true; rockCoal.Hardness = 0f;
        rockCoal.TextureTop = rockCoalTex; rockCoal.TextureSide = rockCoalTex; rockCoal.TextureBottom = rockCoalTex;
        Register(rockCoal);

        var rockIron = new BlockResource();
        rockIron.BlockId = "rock_iron"; rockIron.DisplayName = "Iron Bit";
        rockIron.IsSolid = false; rockIron.CanChisel = false; rockIron.IsTransparent = true; rockIron.IsFlatGround = true; rockIron.IsThinItem = true; rockIron.Hardness = 0f;
        rockIron.TextureTop = rockIronTex; rockIron.TextureSide = rockIronTex; rockIron.TextureBottom = rockIronTex;
        Register(rockIron);

        var rockTin = new BlockResource();
        rockTin.BlockId = "rock_tin"; rockTin.DisplayName = "Tin Bit";
        rockTin.IsSolid = false; rockTin.CanChisel = false; rockTin.IsTransparent = true; rockTin.IsFlatGround = true; rockTin.IsThinItem = true; rockTin.Hardness = 0f;
        rockTin.TextureTop = rockTinTex; rockTin.TextureSide = rockTinTex; rockTin.TextureBottom = rockTinTex;
        Register(rockTin);

        var rockCopper = new BlockResource();
        rockCopper.BlockId = "rock_copper"; rockCopper.DisplayName = "Copper Bit";
        rockCopper.IsSolid = false; rockCopper.CanChisel = false; rockCopper.IsTransparent = true; rockCopper.IsFlatGround = true; rockCopper.IsThinItem = true; rockCopper.Hardness = 0f;
        rockCopper.TextureTop = rockCopperTex; rockCopper.TextureSide = rockCopperTex; rockCopper.TextureBottom = rockCopperTex;
        Register(rockCopper);

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