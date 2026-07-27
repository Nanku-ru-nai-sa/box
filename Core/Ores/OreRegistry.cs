using Godot;
using System.Collections.Generic;

// Registers every ore overlay: its display name, its transparent overlay
// texture, and which item it drops (in addition to whatever the host
// block itself drops) when a block carrying its "ore:<id>" Feature is
// mined. Mirrors BlockRegistry/ItemRegistry's pattern.
//
// NOTE: this needs to be added as an autoload yourself (Project Settings >
// Autoload > add res://Core/Ores/OreRegistry.cs, name it "OreRegistry",
// same as BlockRegistry/ItemRegistry) - I can't safely add the autoload
// entry from outside the editor since Godot assigns it a UID on first load.
public partial class OreRegistry : Node
{
    public static OreRegistry Instance { get; private set; }
    private Dictionary<string, OreResource> _ores = new();

    public override void _Ready()
    {
        Instance = this;
        RegisterOres();
        GD.Print($"OreRegistry loaded {_ores.Count} ores.");
    }

    private void RegisterOres()
    {
        // coal/iron/gold/diamond reuse the existing Blocks/ textures -
        // you said you're redoing all of these as transparent overlay PNGs,
        // so once that art lands these paths don't need to change.
        var coalTex    = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/coal_ore.png");
        var ironTex    = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/iron_ore.png");
        var goldTex    = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/gold_ore.png");
        var diamondTex = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Blocks/diamond_ore.png");

        // NOTE: tin/copper/mithril don't have Blocks/ overlay art yet, only
        // the Items/ icon (and with capitalized filenames, unlike everything
        // else). Pointing at those as a stopgap so these ores render with
        // *something* right away - move/rename them into Blocks/ as
        // tin_ore.png / copper_ore.png / mithril_ore.png (transparent PNG,
        // lowercase, matching the others) whenever you get proper overlay art.
        var tinTex      = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Items/Tin_ore.png");
        var copperTex   = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Items/Copper_ore.png");
        var mithrilTex  = ResourceLoader.Load<Texture2D>("res://Assets/Textures/Items/Mithril_ore.png");

        Register(new OreResource { OreId = "coal", DisplayName = "Coal", OverlayTexture = coalTex, ItemId = "coal" });
        Register(new OreResource { OreId = "iron", DisplayName = "Iron", OverlayTexture = ironTex, ItemId = "iron" });
        Register(new OreResource { OreId = "tin", DisplayName = "Tin", OverlayTexture = tinTex, ItemId = "tin" });
        Register(new OreResource { OreId = "copper", DisplayName = "Copper", OverlayTexture = copperTex, ItemId = "copper" });
        Register(new OreResource { OreId = "gold", DisplayName = "Gold", OverlayTexture = goldTex, ItemId = "gold" });
        Register(new OreResource { OreId = "diamond", DisplayName = "Diamond", OverlayTexture = diamondTex, ItemId = "diamond" });
        Register(new OreResource { OreId = "mithril", DisplayName = "Mithril", OverlayTexture = mithrilTex, ItemId = "mithril" });

        GD.Print("Ores registered.");
    }

    private void Register(OreResource ore)
    {
        _ores[ore.OreId] = ore;
        GD.Print($"  Registered ore: {ore.OreId}");
    }

    public OreResource GetOre(string oreId)
    {
        if (_ores.TryGetValue(oreId, out OreResource ore))
            return ore;
        GD.PrintErr($"OreRegistry: Ore not found: {oreId}");
        return null;
    }

    public bool OreExists(string oreId)
    {
        return _ores.ContainsKey(oreId);
    }

    public IEnumerable<OreResource> GetAllOres()
    {
        return _ores.Values;
    }

    // Reads a BlockState's Features looking for an "ore:<id>" tag and
    // returns the matching OreResource, or null if it isn't ore-logged.
    public OreResource GetOreFromBlockState(BlockState block)
    {
        if (block.Features == null) return null;
        foreach (var f in block.Features)
        {
            if (f.StartsWith("ore:"))
                return GetOre(f.Substring(4));
        }
        return null;
    }
}