using Godot;
using System.Collections.Generic;

public partial class OverlayRegistry : Node
{
    public static OverlayRegistry Instance { get; private set; }

    private Dictionary<string, OverlayResource> _overlays = new();

    public override void _Ready()
    {
        Instance = this;

        RegisterOverlays();

        GD.Print($"OverlayRegistry loaded {_overlays.Count} overlays.");
    }

    private void RegisterOverlays()
    {
        // ============================================================
        // ORES
        // ============================================================

        var coalTex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/coal_ore.png");

        var ironTex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/iron_ore.png");

        var goldTex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/gold_ore.png");

        var diamondTex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/diamond_ore.png");

        var tinTex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/tin_ore.png");

        var copperTex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/copper_ore.png");

        var mithrilTex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/mithril_ore.png");


        Register(new OverlayResource
        {
            OverlayId = "ore:coal",
            DisplayName = "Coal",
            OverlayTexture = coalTex,
            ItemId = "coal"
        });

        Register(new OverlayResource
        {
            OverlayId = "ore:iron",
            DisplayName = "Iron",
            OverlayTexture = ironTex,
            ItemId = "iron"
        });

        Register(new OverlayResource
        {
            OverlayId = "ore:tin",
            DisplayName = "Tin",
            OverlayTexture = tinTex,
            ItemId = "tin"
        });

        Register(new OverlayResource
        {
            OverlayId = "ore:copper",
            DisplayName = "Copper",
            OverlayTexture = copperTex,
            ItemId = "copper"
        });

        Register(new OverlayResource
        {
            OverlayId = "ore:gold",
            DisplayName = "Gold",
            OverlayTexture = goldTex,
            ItemId = "gold"
        });

        Register(new OverlayResource
        {
            OverlayId = "ore:diamond",
            DisplayName = "Diamond",
            OverlayTexture = diamondTex,
            ItemId = "diamond"
        });

        Register(new OverlayResource
        {
            OverlayId = "ore:mithril",
            DisplayName = "Mithril",
            OverlayTexture = mithrilTex,
            ItemId = "mithril"
        });


        // ============================================================
        // TREE ROOTS
        // ============================================================

        var root1Tex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/root1.png");

        var root2Tex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/root2.png");

        var root3Tex =
            ResourceLoader.Load<Texture2D>(
                "res://Assets/Textures/Overlay/root3.png");


        Register(new OverlayResource
        {
            OverlayId = "root:root1",
            DisplayName = "Roots",
            OverlayTexture = root1Tex
        });

        Register(new OverlayResource
        {
            OverlayId = "root:root2",
            DisplayName = "Roots",
            OverlayTexture = root2Tex
        });

        Register(new OverlayResource
        {
            OverlayId = "root:root3",
            DisplayName = "Roots",
            OverlayTexture = root3Tex
        });

        GD.Print("Overlays registered.");
    }

    private void Register(OverlayResource overlay)
    {
        _overlays[overlay.OverlayId] = overlay;

        GD.Print($"  Registered overlay: {overlay.OverlayId}");
    }

    public OverlayResource GetOverlay(string overlayId)
    {
        if (_overlays.TryGetValue(overlayId, out OverlayResource overlay))
            return overlay;

        GD.PrintErr(
            $"OverlayRegistry: Overlay not found: {overlayId}");

        return null;
    }

    public bool OverlayExists(string overlayId)
    {
        return _overlays.ContainsKey(overlayId);
    }

    public IEnumerable<OverlayResource> GetAllOverlays()
    {
        return _overlays.Values;
    }

    public OverlayResource GetOverlayFromBlockState(BlockState block)
    {
        if (block.Features == null)
            return null;

        foreach (var feature in block.Features)
        {
            OverlayResource overlay = GetOverlay(feature);

            if (overlay != null)
                return overlay;
        }

        return null;
    }
}