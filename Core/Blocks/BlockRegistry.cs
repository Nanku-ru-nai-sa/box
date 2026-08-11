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

        // Geology Layer rocks - fill for ChunkManager's Layers list (the
        // geology/soil-band system). diorite/gabbro previously only had
        // item-icon art (Items/rock/) - now using the proper tileable
        // block-face textures added alongside the rest of this set.
        var basaltTex      = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/basalt.png");
        var graniteTex     = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/granite.png");
        var magmaTex       = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/magma.png");
        var gabbroTex      = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/gabbro.png");
        var subBedrockTex  = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/sub_bedrock.png");
        var dioriteTex     = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/diorite.png");
        var deepStoneTex   = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/deep_stone.png");
        var denserStoneTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/denser_stone.png");
        var slateTex       = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/slate.png");
        var slate2Tex      = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/slate2.png"); // registered but not used in Layers yet - alt texture to try later
        var limestoneTex   = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/limestone.png");
        var shaleTex       = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/shale.png");
        var siltTex        = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/silt.png");
        var mudstoneTex    = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/mudstone.png");
        var softStoneTex   = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/soft_stone.png");
        var chalkTex       = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/chalk.png");

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

        // ---- Geology Layer rocks ----
        // Fill for ChunkManager's Layers list. Hardness roughly climbs
        // with rock "grade" - soft soils lowest, sedimentary next,
        // transitional/metamorphic higher, crystalline/deep igneous
        // highest - loosely mirroring how dirt (0.5) through obsidian
        // (50) are already spread out above. Ordered here shallow-to-deep
        // to match the Layers list.

        var silt = new BlockResource();
        silt.BlockId = "silt"; silt.DisplayName = "Silt";
        silt.IsSolid = true; silt.CanChisel = true; silt.Hardness = 0.5f;
        silt.TextureTop = siltTex; silt.TextureSide = siltTex; silt.TextureBottom = siltTex;
        Register(silt);

        var softStone = new BlockResource();
        softStone.BlockId = "soft_stone"; softStone.DisplayName = "Soft Stone";
        softStone.IsSolid = true; softStone.CanChisel = true; softStone.Hardness = 0.8f;
        softStone.TextureTop = softStoneTex; softStone.TextureSide = softStoneTex; softStone.TextureBottom = softStoneTex;
        Register(softStone);

        var chalk = new BlockResource();
        chalk.BlockId = "chalk"; chalk.DisplayName = "Chalk";
        chalk.IsSolid = true; chalk.CanChisel = true; chalk.Hardness = 0.7f;
        chalk.TextureTop = chalkTex; chalk.TextureSide = chalkTex; chalk.TextureBottom = chalkTex;
        Register(chalk);

        var mudstone = new BlockResource();
        mudstone.BlockId = "mudstone"; mudstone.DisplayName = "Mudstone";
        mudstone.IsSolid = true; mudstone.CanChisel = true; mudstone.Hardness = 0.9f;
        mudstone.TextureTop = mudstoneTex; mudstone.TextureSide = mudstoneTex; mudstone.TextureBottom = mudstoneTex;
        Register(mudstone);

        var shale = new BlockResource();
        shale.BlockId = "shale"; shale.DisplayName = "Shale";
        shale.IsSolid = true; shale.CanChisel = true; shale.Hardness = 1.1f;
        shale.TextureTop = shaleTex; shale.TextureSide = shaleTex; shale.TextureBottom = shaleTex;
        Register(shale);

        var limestone = new BlockResource();
        limestone.BlockId = "limestone"; limestone.DisplayName = "Limestone";
        limestone.IsSolid = true; limestone.CanChisel = true; limestone.Hardness = 1.3f;
        limestone.TextureTop = limestoneTex; limestone.TextureSide = limestoneTex; limestone.TextureBottom = limestoneTex;
        Register(limestone);

        var slate = new BlockResource();
        slate.BlockId = "slate"; slate.DisplayName = "Slate";
        slate.IsSolid = true; slate.CanChisel = true; slate.Hardness = 1.6f;
        slate.TextureTop = slateTex; slate.TextureSide = slateTex; slate.TextureBottom = slateTex;
        Register(slate);

        // Not in the Layers list yet - registered so it's ready to drop
        // in whenever you want to try it (per your note).
        var slate2 = new BlockResource();
        slate2.BlockId = "slate2"; slate2.DisplayName = "Slate (Alt)";
        slate2.IsSolid = true; slate2.CanChisel = true; slate2.Hardness = 1.6f;
        slate2.TextureTop = slate2Tex; slate2.TextureSide = slate2Tex; slate2.TextureBottom = slate2Tex;
        Register(slate2);

        var denserStone = new BlockResource();
        denserStone.BlockId = "denser_stone"; denserStone.DisplayName = "Denser Stone";
        denserStone.IsSolid = true; denserStone.CanChisel = true; denserStone.Hardness = 1.7f;
        denserStone.TextureTop = denserStoneTex; denserStone.TextureSide = denserStoneTex; denserStone.TextureBottom = denserStoneTex;
        Register(denserStone);

        var diorite = new BlockResource();
        diorite.BlockId = "diorite"; diorite.DisplayName = "Diorite";
        diorite.IsSolid = true; diorite.CanChisel = true; diorite.Hardness = 1.9f;
        diorite.TextureTop = dioriteTex; diorite.TextureSide = dioriteTex; diorite.TextureBottom = dioriteTex;
        Register(diorite);

        var granite = new BlockResource();
        granite.BlockId = "granite"; granite.DisplayName = "Granite";
        granite.IsSolid = true; granite.CanChisel = true; granite.Hardness = 2.0f;
        granite.TextureTop = graniteTex; granite.TextureSide = graniteTex; granite.TextureBottom = graniteTex;
        Register(granite);

        var deepStone = new BlockResource();
        deepStone.BlockId = "deep_stone"; deepStone.DisplayName = "Deep Stone";
        deepStone.IsSolid = true; deepStone.CanChisel = true; deepStone.Hardness = 2.1f;
        deepStone.TextureTop = deepStoneTex; deepStone.TextureSide = deepStoneTex; deepStone.TextureBottom = deepStoneTex;
        Register(deepStone);

        var gabbro = new BlockResource();
        gabbro.BlockId = "gabbro"; gabbro.DisplayName = "Gabbro";
        gabbro.IsSolid = true; gabbro.CanChisel = true; gabbro.Hardness = 2.1f;
        gabbro.TextureTop = gabbroTex; gabbro.TextureSide = gabbroTex; gabbro.TextureBottom = gabbroTex;
        Register(gabbro);

        var basalt = new BlockResource();
        basalt.BlockId = "basalt"; basalt.DisplayName = "Basalt";
        basalt.IsSolid = true; basalt.CanChisel = true; basalt.Hardness = 1.8f;
        basalt.TextureTop = basaltTex; basalt.TextureSide = basaltTex; basalt.TextureBottom = basaltTex;
        Register(basalt);

        var subBedrock = new BlockResource();
        subBedrock.BlockId = "sub_bedrock"; subBedrock.DisplayName = "Sub-Bedrock";
        subBedrock.IsSolid = true; subBedrock.CanChisel = true; subBedrock.Hardness = 2.3f;
        subBedrock.TextureTop = subBedrockTex; subBedrock.TextureSide = subBedrockTex; subBedrock.TextureBottom = subBedrockTex;
        Register(subBedrock);

        var magma = new BlockResource();
        magma.BlockId = "magma"; magma.DisplayName = "Magma Rock";
        magma.IsSolid = true; magma.CanChisel = true; magma.Hardness = 2.5f;
        magma.TextureTop = magmaTex; magma.TextureSide = magmaTex; magma.TextureBottom = magmaTex;
        Register(magma);

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