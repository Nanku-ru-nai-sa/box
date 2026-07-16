using Godot;
using System;
using System.Collections.Generic;

public partial class ChunkManager : Node3D
{
    [Export] public int RenderDistance { get; set; } = 7;
    [Export] public int VerticalRenderDistance { get; set; } = 8; // covers +/-8 chunks (+/-128 blocks) around the player - enough to see from ground level up through the sky island band (~Y230) when standing near sea level, without loading the full 18-layer world height at all times
    [Export] public int Seed { get; set; } = 777;

    // ---- CAVE SETTINGS ----
    // How caves work: two noise fields are sampled at every underground
    // block. Where BOTH fields land close to zero at the same time, that
    // spot gets carved into air. Because it's an intersection of two wavy
    // fields instead of one, the carved-out shape comes out as winding,
    // branching tunnels instead of blobby round caverns - which is the
    // classic beta 1.7.3 "worm cave" look.
    [ExportGroup("Caves")]
    [Export] public int CaveMinY = 5;      // caves won't generate below this Y (keeps a solid floor near bedrock)
    [Export] public int CaveMaxY = 150;    // caves won't generate above this Y
    [Export] public float CaveThreshold = 0.12f; // tunnel width - higher = fatter tunnels, lower = thinner. Keep this modest for a beta-accurate look; big values start looking like open caverns instead of worm tunnels.

    // ---- ORE SETTINGS ----
    // Each ore has its own noise field, its own Y range, and its own
    // "Rarity" threshold. Rarity is compared against a noise value that
    // ranges roughly -1 to 1, so a HIGHER rarity number = a smaller slice
    // of that range counts as ore = the ore is RARER. Because each ore now
    // has an independent noise field, they no longer cluster together the
    // way coal and iron used to.
    [ExportGroup("Ore Generation")]
    [Export] public int CoalMinY = 5;
    [Export] public int CoalMaxY = 128;
    [Export] public float CoalRarity = 0.30f;

    [Export] public int IronMinY = 5;
    [Export] public int IronMaxY = 64;
    [Export] public float IronRarity = 0.40f;

    [Export] public int GoldMinY = 5;
    [Export] public int GoldMaxY = 32;
    [Export] public float GoldRarity = 0.46f;

    [Export] public int DiamondMinY = 5;
    [Export] public int DiamondMaxY = 16;
    [Export] public float DiamondRarity = 0.55f;

    [Export] public int ObsidianMinY = 5;  // kept just above the bedrock layer (Y0-3), which is handled separately and never rolls for ore - that's why it wasn't showing up before
    [Export] public int ObsidianMaxY = 20;
    [Export] public float ObsidianRarity = 0.42f;

    [Export] public float RockPatchRarity = 0.52f; // "rock" (cobblestone-style) patches - higher = rarer/smaller patches
    [Export] public float DirtPatchRarity = 0.60f; // rare underground dirt patches - higher = rarer/smaller patches

    public bool IsInitialLoadComplete { get; private set; } = false;
[Signal] public delegate void WorldReadyEventHandler();

    private Dictionary<Vector3I, Chunk> _chunks = new();
    private List<Vector3I> _chunksToLoad = new();
    private Vector3I _lastPlayerChunk = new Vector3I(999, 999, 999);
    private Node3D _player;

    private const int ChunksPerFrame      = 4;   // used after initial load (streaming)
    private const int ChunksPerFrameInit  = 80;

    private const int WaterLevel = 100;
    private const int MaxOceanDepthBelowSeaLevel = 20; // how far below sea level natural water basins are allowed to go; anything carved out deeper than this (i.e. caves) stays dry instead of flooding

    private FastNoiseLite _lowNoise = new FastNoiseLite();
    private FastNoiseLite _highNoise = new FastNoiseLite();
    private FastNoiseLite _selectorNoise = new FastNoiseLite();

    private FastNoiseLite _biomeTemp = new FastNoiseLite();
    private FastNoiseLite _biomeHumid = new FastNoiseLite();

    private FastNoiseLite _oreNoise = new FastNoiseLite();
    private FastNoiseLite _outcropNoise = new FastNoiseLite();
    private FastNoiseLite _cliffNoise = new FastNoiseLite();
    private FastNoiseLite _overhangNoise = new FastNoiseLite();
    private FastNoiseLite _skyIslandNoise = new FastNoiseLite();
    private FastNoiseLite _skyIslandSelector = new FastNoiseLite();

    private FastNoiseLite _caveNoiseA = new FastNoiseLite();
    private FastNoiseLite _caveNoiseB = new FastNoiseLite();
    private FastNoiseLite _caveRadiusNoise = new FastNoiseLite(); // makes tunnel width drift wider/narrower along their length, like a worm's radius changing as it burrows

    private FastNoiseLite _coalNoise = new FastNoiseLite();
    private FastNoiseLite _ironNoise = new FastNoiseLite();
    private FastNoiseLite _goldNoise = new FastNoiseLite();
    private FastNoiseLite _diamondNoise = new FastNoiseLite();
    private FastNoiseLite _obsidianNoise = new FastNoiseLite();
    private FastNoiseLite _rockPatchNoise = new FastNoiseLite();
    private FastNoiseLite _dirtPatchNoise = new FastNoiseLite();

    public override void _Ready()
    {
        _lowNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _lowNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _lowNoise.FractalOctaves = 6;
        _lowNoise.FractalLacunarity = 2.0f;
        _lowNoise.FractalGain = 0.5f;
        _lowNoise.Frequency = 0.010f;
        _lowNoise.Seed = Seed;

        _highNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _highNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _highNoise.FractalOctaves = 6;
        _highNoise.FractalLacunarity = 2.3f;
        _highNoise.FractalGain = 0.55f;
        _highNoise.Frequency = 0.018f;
        _highNoise.Seed = Seed + 1;

        _cliffNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _cliffNoise.Frequency = 0.025f;
        _cliffNoise.Seed = Seed + 3;

        _overhangNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _overhangNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _overhangNoise.FractalOctaves = 3;
        _overhangNoise.Frequency = 0.022f;
        _overhangNoise.Seed = Seed + 4;

        _skyIslandNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _skyIslandNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _skyIslandNoise.FractalOctaves = 4;
        _skyIslandNoise.FractalLacunarity = 2.1f;
        _skyIslandNoise.FractalGain = 0.5f;
        _skyIslandNoise.Frequency = 0.016f;
        _skyIslandNoise.Seed = Seed + 5;

        _skyIslandSelector.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _skyIslandSelector.Frequency = 0.006f;
        _skyIslandSelector.Seed = Seed + 6;

        _selectorNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _selectorNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _selectorNoise.FractalOctaves = 2;
        _selectorNoise.Frequency = 0.002f;
        _selectorNoise.Seed = Seed + 2;

        _biomeTemp.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _biomeTemp.Seed = 77777;
        _biomeTemp.Frequency = 0.004f;

        _biomeHumid.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _biomeHumid.Seed = 88888;
        _biomeHumid.Frequency = 0.004f;

        _oreNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _oreNoise.Seed = 55555;
        _oreNoise.Frequency = 0.1f;

        _outcropNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _outcropNoise.Seed = 54321;
        _outcropNoise.Frequency = 0.05f;

        // Caves: two independent fields, medium-low frequency + a couple
        // of octaves so tunnels bend and branch instead of running dead
        // straight.
        _caveNoiseA.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _caveNoiseA.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _caveNoiseA.FractalOctaves = 2;
        _caveNoiseA.Frequency = 0.035f; // low-ish = tunnels wind and bend gradually rather than looking noisy/jittery
        _caveNoiseA.Seed = Seed + 10;

        _caveNoiseB.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _caveNoiseB.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _caveNoiseB.FractalOctaves = 2;
        _caveNoiseB.Frequency = 0.035f;
        _caveNoiseB.Seed = Seed + 11;

        // Slow-moving noise that gently widens/narrows the tunnels as they
        // travel, similar to how a real worm-carved tunnel isn't a perfectly
        // uniform tube the whole way.
        _caveRadiusNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _caveRadiusNoise.Frequency = 0.01f;
        _caveRadiusNoise.Seed = Seed + 12;

        // Ores: each gets its own field/seed/frequency so veins no longer
        // line up with each other.
        _coalNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _coalNoise.Frequency = 0.11f;
        _coalNoise.Seed = Seed + 20;

        _ironNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _ironNoise.Frequency = 0.10f;
        _ironNoise.Seed = Seed + 21;

        _goldNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _goldNoise.Frequency = 0.095f;
        _goldNoise.Seed = Seed + 22;

        _diamondNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _diamondNoise.Frequency = 0.09f;
        _diamondNoise.Seed = Seed + 23;

        _obsidianNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _obsidianNoise.Frequency = 0.12f;
        _obsidianNoise.Seed = Seed + 24;

        _rockPatchNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _rockPatchNoise.Frequency = 0.08f;
        _rockPatchNoise.Seed = Seed + 25;

        _dirtPatchNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _dirtPatchNoise.Frequency = 0.055f;
        _dirtPatchNoise.Seed = Seed + 26;

        var canvasLayer = new CanvasLayer();
        GetTree().Root.CallDeferred("add_child", canvasLayer);

        if (_player == null)
            GD.Print("ChunkManager: No player found");
        else
            GD.Print("ChunkManager: Player found");

        GD.Print("Calling UpdateChunks...");
        UpdateChunks();

        if (_player != null)
            _player.GlobalPosition = new Vector3(8, 120, 8); // sits just above sea level (WaterLevel=100) within the new 0-288 world height range

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_player == null)
        {
            var found = GetTree().Root.FindChild("player", true, false);
            _player = found as Node3D;
            if (_player == null) return;
        }

        // TEMP: chunk streaming-on-move disabled again for testing - causes
        // too much lag. Only the initial set of chunks loaded at spawn will
        // exist. Re-enable the block below once perf is sorted out further.
        // Vector3I currentPlayerChunk = WorldToChunk(_player.GlobalPosition);
        // if (currentPlayerChunk != _lastPlayerChunk)
        // {
        //     _lastPlayerChunk = currentPlayerChunk;
        //     UpdateChunks();
        // }

         // During initial load: load as many as possible each frame (no cap).
// After initial load: cap to ChunksPerFrame so streaming doesn't stutter.
int cap = IsInitialLoadComplete ? ChunksPerFrame : ChunksPerFrameInit;
int loaded = 0;
while (_chunksToLoad.Count > 0 && loaded < cap)
{
    Vector3I chunkPos = _chunksToLoad[0];
    _chunksToLoad.RemoveAt(0);

    // Skip chunks entirely above the world height cap (pure air, nothing generates there)
    int chunkBottomY = chunkPos.Y * Chunk.HEIGHT;
    if (chunkBottomY >= 288) { loaded++; continue; }

    // Skip chunks entirely below Y=0 (nothing generates below bedrock)
    int chunkTopY = (chunkPos.Y + 1) * Chunk.HEIGHT;
    if (chunkTopY <= 0) { loaded++; continue; }

    if (!_chunks.ContainsKey(chunkPos))
        LoadChunk(chunkPos);
    loaded++;
}

if (!IsInitialLoadComplete && _chunksToLoad.Count == 0 && _chunks.Count > 0)
{
    IsInitialLoadComplete = true;
    EmitSignal(SignalName.WorldReady);
    GD.Print("World ready!");
}

    }
    private string SaveDirectory => "user://saves/world1/";

    public IEnumerable<Vector3I> GetLoadedChunkPositions()
    {
        return _chunks.Keys;
    }

    public void SaveModifiedChunks()
    {
        var dir = DirAccess.Open("user://");
        if (!dir.DirExists("saves/world1"))
            dir.MakeDirRecursive("saves/world1");

        int savedCount = 0;
        foreach (var kvp in _chunks)
        {
            var mods = kvp.Value.GetModifications();
            if (mods.Count == 0) continue;

            string fileName = $"{SaveDirectory}chunk_{kvp.Key.X}_{kvp.Key.Y}_{kvp.Key.Z}.json";
            var saveData = new Godot.Collections.Dictionary();

            foreach (var mod in mods)
            {
                string key = $"{mod.Key.X},{mod.Key.Y},{mod.Key.Z}";
                saveData[key] = mod.Value.BlockId;
            }

            var json = Json.Stringify(saveData);
            using var file = FileAccess.Open(fileName, FileAccess.ModeFlags.Write);
            file.StoreString(json);
            savedCount++;
        }

        GD.Print($"Saved {savedCount} modified chunks");
    }

    public void SaveInventory(Inventory inventory)
{
    var dir = DirAccess.Open("user://");
    if (!dir.DirExists("saves/world1"))
        dir.MakeDirRecursive("saves/world1");

    var slots = new Godot.Collections.Array();
    foreach (var slot in inventory.Slots)
    {
        var entry = new Godot.Collections.Dictionary();
        entry["id"] = slot.IsEmpty ? "" : slot.ItemId;
        entry["count"] = slot.IsEmpty ? 0 : slot.Count;
        slots.Add(entry);
    }

    string json = Json.Stringify(slots);
    using var file = FileAccess.Open(SaveDirectory + "inventory.json", FileAccess.ModeFlags.Write);
    file.StoreString(json);
    GD.Print("Inventory saved.");
}

public void LoadInventory(Inventory inventory)
{
    string path = SaveDirectory + "inventory.json";
    if (!FileAccess.FileExists(path)) return;

    using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
    string json = file.GetAsText();

    var parsed = Json.ParseString(json).AsGodotArray();
    for (int i = 0; i < parsed.Count && i < inventory.Slots.Length; i++)
    {
        var entry = parsed[i].AsGodotDictionary();
        string id = (string)entry["id"];
        int count = (int)entry["count"];

        if (string.IsNullOrEmpty(id) || count <= 0)
            inventory.Slots[i].Clear();
        else
        {
            inventory.Slots[i].ItemId = id;
            inventory.Slots[i].Count = count;
        }
    }

    GD.Print("Inventory loaded.");
}

public void SavePlayerPosition(Vector3 position)
{
    var dir = DirAccess.Open("user://");
    if (!dir.DirExists("saves/world1"))
        dir.MakeDirRecursive("saves/world1");

    var data = new Godot.Collections.Dictionary
    {
        ["x"] = position.X,
        ["y"] = position.Y,
        ["z"] = position.Z
    };

    using var file = FileAccess.Open(SaveDirectory + "player.json", FileAccess.ModeFlags.Write);
    file.StoreString(Json.Stringify(data));
    GD.Print($"Player position saved: {position}");
}

public Vector3? LoadPlayerPosition()
{
    string path = SaveDirectory + "player.json";
    if (!FileAccess.FileExists(path)) return null;

    using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
    var parsed = Json.ParseString(file.GetAsText()).AsGodotDictionary();
    if (parsed == null) return null;

    return new Vector3(
        (float)parsed["x"],
        (float)parsed["y"],
        (float)parsed["z"]
    );
}

    public void LoadChunkModifications(Chunk chunk, Vector3I chunkPos)
    {
        string fileName = $"{SaveDirectory}chunk_{chunkPos.X}_{chunkPos.Y}_{chunkPos.Z}.json";
        if (!FileAccess.FileExists(fileName)) return;

        using var file = FileAccess.Open(fileName, FileAccess.ModeFlags.Read);
        string json = file.GetAsText();

        var parsed = Json.ParseString(json).AsGodotDictionary();
        var mods = new Dictionary<Vector3I, BlockState>();

        foreach (var key in parsed.Keys)
        {
            string[] parts = ((string)key).Split(',');
            int x = int.Parse(parts[0]);
            int y = int.Parse(parts[1]);
            int z = int.Parse(parts[2]);

            string blockId = (string)parsed[key];
            mods[new Vector3I(x, y, z)] = new BlockState { BlockId = blockId, BitMask = 0xFF };
        }

        chunk.ApplyModifications(mods);
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
                // Vertical loading centered on the PLAYER's current chunk Y
                // (clamped so it never goes below world chunk Y=0), instead
                // of always loading a fixed 0-to-VerticalRenderDistance
                // range. This is what makes vertical chunks follow the
                // player as they go up/down, rather than always loading
                // near the bottom of the world regardless of where the
                // player actually is - which was cutting the world off
                // right around water level before.
                int minY = Mathf.Max(0, playerChunk.Y - VerticalRenderDistance);
                int maxY = playerChunk.Y + VerticalRenderDistance;

                for (int y = minY; y <= maxY; y++)
                {
                    Vector3I chunkPos = new Vector3I(
                        playerChunk.X + x,
                        y,
                        playerChunk.Z + z
                    );

                    if (!_chunks.ContainsKey(chunkPos) && !_chunksToLoad.Contains(chunkPos))
                        _chunksToLoad.Add(chunkPos);
                }
            }
        }

        // Load chunks closest to the player first so you don't fall into
        // the void waiting for distant chunks to finish.
        _chunksToLoad.Sort((a, b) =>
        {
            float distA = (a - playerChunk).LengthSquared();
            float distB = (b - playerChunk).LengthSquared();
            return distA.CompareTo(distB);
        });

        var toUnload = new List<Vector3I>();
        foreach (var pos in _chunks.Keys)
        {
            int dx = Mathf.Abs(pos.X - playerChunk.X);
            int dz = Mathf.Abs(pos.Z - playerChunk.Z);
            int dy = Mathf.Abs(pos.Y - playerChunk.Y);
            if (dx > RenderDistance + 1 || dz > RenderDistance + 1 || dy > VerticalRenderDistance + 1)
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
        LoadChunkModifications(chunk, chunkPos);
        chunk.BuildMesh();
        _chunks[chunkPos] = chunk;
    }

    private void UnloadChunk(Vector3I chunkPos)
    {
        if (!_chunks.TryGetValue(chunkPos, out Chunk chunk)) return;
        chunk.QueueFree();
        _chunks.Remove(chunkPos);
    }

    private enum Biome { Plains, Forest, Desert, Tundra, Beach }

    private Biome GetBiome(float temp, float humid, int surfaceY)
    {
        if (surfaceY <= WaterLevel + 2 && surfaceY >= WaterLevel - 16) return Biome.Beach;
        if (temp > 0.4f && humid < -0.1f) return Biome.Desert;
        if (temp < -0.3f) return Biome.Tundra;
        if (humid > 0.1f) return Biome.Forest;
        return Biome.Plains;
    }

    // Returns true if this exact block should be carved into a cave.
    // baseDensity is passed in so we only carve where there's enough solid
    // rock around the point already (avoids punching paper-thin holes
    // right at the surface).
    private bool IsCave(int worldX, int worldY, int worldZ, float baseDensity)
    {
        if (worldY < CaveMinY || worldY > CaveMaxY) return false;
        if (baseDensity < 4f) return false;

        // Fade caves out as they approach CaveMinY so the very bottom of
        // the world stays solid instead of turning into swiss cheese right
        // above bedrock.
        float bottomFade = Mathf.Clamp((worldY - CaveMinY) / 10f, 0f, 1f);

        // Tunnel width drifts gently along its length instead of staying a
        // perfectly uniform tube - keeps it feeling like a burrowed worm
        // path rather than a pipe, without ever opening into a full room.
        float radiusMod = 0.65f + 0.7f * Mathf.Clamp((_caveRadiusNoise.GetNoise3D(worldX, worldY, worldZ) + 1f) * 0.5f, 0f, 1f);

        // Winding tunnels: carved where two noise fields both land near
        // zero at the same spot - this intersection is what gives the
        // snaking, branching worm-tunnel shape.
        float caveA = _caveNoiseA.GetNoise3D(worldX, worldY * 1.6f, worldZ);
        float caveB = _caveNoiseB.GetNoise3D(worldX, worldY * 1.6f, worldZ);
        float caveValue = Mathf.Abs(caveA) + Mathf.Abs(caveB);

        return caveValue < CaveThreshold * bottomFade * radiusMod;
    }

    private float GetDensity(int worldX, int worldY, int worldZ)
    {
        float low = _lowNoise.GetNoise3D(worldX, worldY * 0.5f, worldZ);
        float lowFlattened = Mathf.Sign(low) * Mathf.Pow(Mathf.Abs(low), 2.2f);

        float high = _highNoise.GetNoise3D(worldX, worldY * 0.5f, worldZ) * 0.6f;
        float selector = _selectorNoise.GetNoise2D(worldX, worldZ);

        float blend = Mathf.Clamp((selector + 1f) * 0.5f, 0f, 1f);
        blend = Mathf.Pow(blend, 1.6f);
        blend = blend * blend * (3f - 2f * blend);

        float shape = Mathf.Lerp(lowFlattened, high, blend);

        const float terrainCenter = 107f;
        const float terrainAmplitude = 160f; // was 300f, lowered so peaks (terrainCenter + amplitude ~= 267) stay safely under the new 288 height cap instead of getting clipped flat at the top

        // Slightly gentler cliffs than before (higher threshold, smaller
        // push) - part of smoothing the terrain out toward a beta 1.7.3
        // feel, and it means we don't need a post-process spike remover
        // anymore.
        float cliff = _cliffNoise.GetNoise3D(worldX, worldY * 0.3f, worldZ);
        if (Mathf.Abs(cliff) > 0.7f)
            shape += cliff * 0.6f;

        float scaledShape = shape * terrainAmplitude;
        float density = scaledShape - (worldY - terrainCenter);

        // Overhang strength reduced (was 35f) - still gives some ledges and
        // little roof pockets, but far less likely to leave floating
        // single blocks behind.
        float overhang = _overhangNoise.GetNoise3D(worldX, worldY, worldZ);
        float surfaceCloseness = Mathf.Clamp(1f - Mathf.Abs(density) / 60f, 0f, 1f);
        density += overhang * 20f * surfaceCloseness;

        const int seaFloorLimit = WaterLevel - MaxOceanDepthBelowSeaLevel;
        if (worldY < seaFloorLimit)
        {
            density = Mathf.Max(density, 0.3f);
        }

        if (Mathf.Abs(density) < 0.3f)
            density = density > 0 ? 0.3f : -0.3f;

        // ---- CAVES ----
        // Carved AFTER the ocean-floor clamp above, so tunnels can still
        // wind underneath the ocean floor. Whether that carved-out cave
        // shows up as air or gets flooded is decided later in
        // GenerateChunk (only shallow pockets near sea level flood; deep
        // caves stay dry).
        if (IsCave(worldX, worldY, worldZ, density))
            density = -1f;

        const float skyIslandCenter = 230f; // was 550f, lowered to fit within the new 288 world height cap (leaves ~58 blocks of clearance to the cap, plus room below for ground mountains up to ~280)
        const float skyIslandAmplitude = 60f;
        const float skyIslandThickness = 40f;

        float skySelector = _skyIslandSelector.GetNoise2D(worldX, worldZ);
        if (skySelector > 0.25f)
        {
            float skyShape = _skyIslandNoise.GetNoise3D(worldX, worldY * 0.6f, worldZ);
            float skyDistanceFromCenter = Mathf.Abs(worldY - skyIslandCenter);
            float skyDensity = (skyShape * skyIslandAmplitude) - (skyDistanceFromCenter - skyIslandThickness);

            float skyOverhang = _overhangNoise.GetNoise3D(worldX, worldY * 1.3f, worldZ + 5000f);
            float skySurfaceCloseness = Mathf.Clamp(1f - Mathf.Abs(skyDensity) / 50f, 0f, 1f);
            skyDensity += skyOverhang * 30f * skySurfaceCloseness;

            float patchFade = Mathf.Clamp((skySelector - 0.25f) / 0.15f, 0f, 1f);
            skyDensity = Mathf.Lerp(-10f, skyDensity, patchFade);

            density = Mathf.Max(density, skyDensity);
        }

        // ---- HARD WORLD HEIGHT CAP ----
        // World capped at 288 (16 x 18 = 288), the nearest clean multiple of
        // Chunk.HEIGHT at or below the requested 300, so the world ends
        // exactly on a chunk boundary with no partial chunk wasted at the
        // top. Nothing - ground, overhangs, sky islands - generates above
        // this, keeping total loaded volume predictable.
        const int worldHeightCap = 288;
        if (worldY >= worldHeightCap)
            return -10f;

        return density;
    }

    private int GetSurfaceHeight(int worldX, int worldZ, int searchTop = 288, int searchBottom = 0)
    {
        for (int y = searchTop; y >= searchBottom; y--)
        {
            if (GetDensity(worldX, y, worldZ) > 0f)
                return y;
        }
        return searchBottom;
    }

    private void GenerateChunk(Chunk chunk, Vector3I chunkPos)
    {
        const int bedrockHeight = 4;
        const int waterFloor = WaterLevel - MaxOceanDepthBelowSeaLevel; // non-solid pockets below this Y stay dry air instead of flooding - this is what keeps deep caves from turning into giant underground lakes

        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int z = 0; z < Chunk.SIZE; z++)
            {
                int worldX = chunkPos.X * Chunk.SIZE + x;
                int worldZ = chunkPos.Z * Chunk.SIZE + z;

                float temp = _biomeTemp.GetNoise2D(worldX, worldZ);
                float humid = _biomeHumid.GetNoise2D(worldX, worldZ);

                // Real top-of-terrain height for this column. Used below so
                // cave ceilings/walls don't get mistaken for the actual
                // ground surface and coated in dirt/grass - that mix-up was
                // why caves were coming out full of dirt.
                int trueSurfaceY = GetSurfaceHeight(worldX, worldZ);

                int approxSurface = -1;

                for (int y = 0; y < Chunk.HEIGHT; y++)
                {
                    int worldY = chunkPos.Y * Chunk.HEIGHT + y;
                    float density = GetDensity(worldX, worldY, worldZ);
                    bool isSolid = density > 0f;

                    BlockState block;

                    if (worldY == 0)
                    {
                        block = new BlockState { BlockId = "bedrock", BitMask = 0xFF };
                    }
                    else if (worldY < bedrockHeight)
                    {
                        float bedrockNoise = _oreNoise.GetNoise3D(worldX, worldY, worldZ);
                        if (!isSolid)
                        {
                            block = BlockState.Air;
                        }
                        else
                        {
                            block = bedrockNoise > 0f
                                ? new BlockState { BlockId = "bedrock", BitMask = 0xFF }
                                : new BlockState { BlockId = "stone", BitMask = 0xFF };
                        }
                    }
                    else if (!isSolid)
                    {
                        block = (worldY <= WaterLevel && worldY >= waterFloor)
                            ? new BlockState { BlockId = "water", BitMask = 0xFF }
                            : BlockState.Air;
                    }
                    else
                    {
                        bool aboveIsAir = GetDensity(worldX, worldY + 1, worldZ) <= 0f;
                        bool nearSurface = false;

                        int depthToAir = 0;
                        for (int checkY = worldY; checkY <= worldY + 5; checkY++)
                        {
                            if (GetDensity(worldX, checkY, worldZ) <= 0f)
                            {
                                depthToAir = checkY - worldY;
                                nearSurface = true;
                                break;
                            }
                        }

                        if (approxSurface < 0 && aboveIsAir)
                            approxSurface = worldY;

                        // Only treat this as "the ground surface" if it's
                        // actually near the real top of the column. Without
                        // this check, a stone block sitting right next to a
                        // cave air pocket 40 blocks underground would also
                        // count as "near surface" and get coated in dirt -
                        // which is exactly what was happening before.
                        bool isRealSurface = worldY >= trueSurfaceY - 6;

                        if (nearSurface && depthToAir <= 4 && isRealSurface)
                        {
                            Biome biome = GetBiome(temp, humid, worldY);

                            if (depthToAir <= 1)
                            {
                                switch (biome)
                                {
                                    case Biome.Desert:
                                        block = new BlockState { BlockId = "sand", BitMask = 0xFF };
                                        break;
                                    case Biome.Beach:
                                        float clayNoise = _outcropNoise.GetNoise2D(worldX * 3f, worldZ * 3f);
                                        block = clayNoise > 0.6f
                                            ? new BlockState { BlockId = "clay", BitMask = 0xFF }
                                            : new BlockState { BlockId = "sand", BitMask = 0xFF };
                                        break;
                                    case Biome.Tundra:
                                        block = new BlockState { BlockId = "snow", BitMask = 0xFF };
                                        break;
                                    default:
                                        block = worldY <= WaterLevel
                                            ? new BlockState { BlockId = "dirt", BitMask = 0xFF }
                                            : new BlockState { BlockId = "grass_block", BitMask = 0xFF };
                                        break;
                                }
                            }
                            else
                            {
                                switch (biome)
                                {
                                    case Biome.Desert:
                                        block = new BlockState { BlockId = "sand", BitMask = 0xFF };
                                        break;
                                    case Biome.Beach:
                                        block = depthToAir <= 3
                                            ? new BlockState { BlockId = "sand", BitMask = 0xFF }
                                            : new BlockState { BlockId = "stone", BitMask = 0xFF };
                                        break;
                                    default:
                                        block = new BlockState { BlockId = "dirt", BitMask = 0xFF };
                                        break;
                                }
                            }
                        }
                        else
                        {
                            block = GetUndergroundBlock(worldX, worldY, worldZ);
                        }
                    }

                    chunk.SetBlockInternal(x, y, z, block);
                }
            }
        }

        GenerateTrees(chunk, chunkPos);
        SpawnMelons(chunk, chunkPos);
        SpawnDecorations(chunk, chunkPos);
        chunk.MarkDirty();
    }

    // Deep underground block choice: ores (each with their own noise/Y
    // range/rarity), then rare "rock" patches, then gravel patches, then
    // plain stone as the fallback.
    private BlockState GetUndergroundBlock(int worldX, int worldY, int worldZ)
    {
        if (worldY >= DiamondMinY && worldY <= DiamondMaxY &&
            _diamondNoise.GetNoise3D(worldX, worldY, worldZ) > DiamondRarity)
            return new BlockState { BlockId = "diamond_ore", BitMask = 0xFF };

        if (worldY >= GoldMinY && worldY <= GoldMaxY &&
            _goldNoise.GetNoise3D(worldX, worldY, worldZ) > GoldRarity)
            return new BlockState { BlockId = "gold_ore", BitMask = 0xFF };

        if (worldY >= IronMinY && worldY <= IronMaxY &&
            _ironNoise.GetNoise3D(worldX, worldY, worldZ) > IronRarity)
            return new BlockState { BlockId = "iron_ore", BitMask = 0xFF };

        if (worldY >= CoalMinY && worldY <= CoalMaxY &&
            _coalNoise.GetNoise3D(worldX, worldY, worldZ) > CoalRarity)
            return new BlockState { BlockId = "coal_ore", BitMask = 0xFF };

        if (worldY >= ObsidianMinY && worldY <= ObsidianMaxY &&
            _obsidianNoise.GetNoise3D(worldX, worldY, worldZ) > ObsidianRarity)
            return new BlockState { BlockId = "obsidian", BitMask = 0xFF };

        float gravelVal = _outcropNoise.GetNoise3D(worldX, worldY * 2f, worldZ);
        if (gravelVal > 0.45f)
            return new BlockState { BlockId = "gravel", BitMask = 0xFF };

        // Small, uncommon "rock" patches (your cobblestone-style block).
        // NOTE: swap "rock" below for whatever block id you actually use
        // for that block if it's registered under a different name.
        float rockVal = _rockPatchNoise.GetNoise3D(worldX, worldY * 2f, worldZ + 9000f);
        if (rockVal > RockPatchRarity)
            return new BlockState { BlockId = "rock", BitMask = 0xFF };

        // Rare underground dirt patches, separate from the surface
        // topsoil layer - most cave walls should still read as stone, this
        // is just an occasional pocket to stumble across.
        float dirtVal = _dirtPatchNoise.GetNoise3D(worldX + 4000f, worldY * 2f, worldZ);
        if (dirtVal > DirtPatchRarity)
            return new BlockState { BlockId = "dirt", BitMask = 0xFF };

        return new BlockState { BlockId = "stone", BitMask = 0xFF };
    }

    private void SpawnMelons(Chunk chunk, Vector3I chunkPos)
    {
        _melonRng.Seed = (ulong)(chunkPos.X * 23456789 ^ chunkPos.Z * 98765432 ^ chunkPos.Y * 11111111);

        // Occasionally spawn a small cluster of melons together, in
        // addition to the usual lone scattered melons below.
        if (_melonRng.Randf() < 0.10f)
        {
            int centerX = _melonRng.RandiRange(2, Chunk.SIZE - 3);
            int centerZ = _melonRng.RandiRange(2, Chunk.SIZE - 3);
            int patchSize = _melonRng.RandiRange(2, 4);
            int placed = 0;
            int attempts = 0;

            while (placed < patchSize && attempts < patchSize * 5)
            {
                attempts++;
                int ox = centerX + _melonRng.RandiRange(-2, 2);
                int oz = centerZ + _melonRng.RandiRange(-2, 2);
                if (ox < 1 || ox >= Chunk.SIZE - 1 || oz < 1 || oz >= Chunk.SIZE - 1) continue;
                if (TrySpawnMelonAt(chunk, chunkPos, ox, oz))
                    placed++;
            }
        }

        for (int x = 1; x < Chunk.SIZE - 1; x++)
        {
            for (int z = 1; z < Chunk.SIZE - 1; z++)
            {
                if (_melonRng.Randf() > 0.004f) continue;
                TrySpawnMelonAt(chunk, chunkPos, x, z);
            }
        }
    }

    private bool TrySpawnMelonAt(Chunk chunk, Vector3I chunkPos, int x, int z)
    {
        int worldX = chunkPos.X * Chunk.SIZE + x;
        int worldZ = chunkPos.Z * Chunk.SIZE + z;

        int surfaceY = GetSurfaceHeight(worldX, worldZ);
        int localSurfaceY = surfaceY - (chunkPos.Y * Chunk.HEIGHT);
        if (localSurfaceY < 0 || localSurfaceY >= Chunk.HEIGHT) return false;

        BlockState surfaceBlock = chunk.GetBlock(x, localSurfaceY, z);
        if (surfaceBlock.BlockId != "grass_block" && surfaceBlock.BlockId != "dirt") return false;

        float worldSurfaceY = chunkPos.Y * Chunk.HEIGHT + localSurfaceY + 1.5f;

        var melon = new Melon();
        GetParent().CallDeferred("add_child", melon);
        melon.SetDeferred("global_position", new Vector3(worldX + 0.5f, worldSurfaceY, worldZ + 0.5f));
        return true;
    }

    private RandomNumberGenerator _treeRng = new RandomNumberGenerator();
    private RandomNumberGenerator _melonRng = new RandomNumberGenerator();
    private RandomNumberGenerator _decorRng = new RandomNumberGenerator();

    private void SpawnDecorations(Chunk chunk, Vector3I chunkPos)
    {
        _decorRng.Seed = (ulong)(chunkPos.X * 11111111 ^ chunkPos.Z * 77777777 ^ chunkPos.Y * 33333333);

        // Flowers now spawn as small patches instead of one independent
        // roll per block. Each patch picks ONE flower type and only places
        // that type, so you get proper little rose patches / clover
        // patches instead of a random mix of species side by side. Patch
        // attempts are rare, so flower fields stay a nice find rather than
        // being everywhere.
        int patchAttempts = 2;

        for (int p = 0; p < patchAttempts; p++)
        {
            if (_decorRng.Randf() > 0.15f) continue;

            int centerX = _decorRng.RandiRange(2, Chunk.SIZE - 3);
            int centerZ = _decorRng.RandiRange(2, Chunk.SIZE - 3);

            int worldX = chunkPos.X * Chunk.SIZE + centerX;
            int worldZ = chunkPos.Z * Chunk.SIZE + centerZ;

            int surfaceY = GetSurfaceHeight(worldX, worldZ);
            int localSurfaceY = surfaceY - (chunkPos.Y * Chunk.HEIGHT);
            if (localSurfaceY < 0 || localSurfaceY >= Chunk.HEIGHT - 1) continue;
            if (surfaceY < WaterLevel) continue;

            BlockState centerSurface = chunk.GetBlock(centerX, localSurfaceY, centerZ);
            if (centerSurface.BlockId != "grass_block") continue;

            float typeRoll = _decorRng.Randf();
            string decorId = typeRoll < 0.34f ? "rose"
                : typeRoll < 0.67f ? "clover"
                : "dandelion";

            int clusterSize = _decorRng.RandiRange(3, 7);
            int placed = 0;
            int attempts = 0;

            while (placed < clusterSize && attempts < clusterSize * 4)
            {
                attempts++;
                int ox = centerX + _decorRng.RandiRange(-2, 2);
                int oz = centerZ + _decorRng.RandiRange(-2, 2);
                if (ox < 0 || ox >= Chunk.SIZE || oz < 0 || oz >= Chunk.SIZE) continue;

                int wx = chunkPos.X * Chunk.SIZE + ox;
                int wz = chunkPos.Z * Chunk.SIZE + oz;
                int sY = GetSurfaceHeight(wx, wz);
                int lY = sY - (chunkPos.Y * Chunk.HEIGHT);
                if (lY < 0 || lY >= Chunk.HEIGHT - 1) continue;
                if (sY < WaterLevel) continue;

                BlockState surfaceBlock = chunk.GetBlock(ox, lY, oz);
                if (surfaceBlock.BlockId != "grass_block") continue;

                BlockState above = chunk.GetBlock(ox, lY + 1, oz);
                if (!above.IsAir()) continue;

                chunk.SetBlockInternal(ox, lY + 1, oz, new BlockState { BlockId = decorId, BitMask = 0xFF });
                placed++;
            }
        }

        // On top of the patches above, also scatter occasional lone
        // flowers anywhere on grass, each rolling its own type - this
        // brings back the old "random flower here and there" feel
        // alongside the new same-type clusters.
        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int z = 0; z < Chunk.SIZE; z++)
            {
                if (_decorRng.Randf() > 0.012f) continue;

                int worldX = chunkPos.X * Chunk.SIZE + x;
                int worldZ = chunkPos.Z * Chunk.SIZE + z;

                int surfaceY = GetSurfaceHeight(worldX, worldZ);
                int localSurfaceY = surfaceY - (chunkPos.Y * Chunk.HEIGHT);
                if (localSurfaceY < 0 || localSurfaceY >= Chunk.HEIGHT - 1) continue;
                if (surfaceY < WaterLevel) continue;

                BlockState surfaceBlock = chunk.GetBlock(x, localSurfaceY, z);
                if (surfaceBlock.BlockId != "grass_block") continue;

                BlockState above = chunk.GetBlock(x, localSurfaceY + 1, z);
                if (!above.IsAir()) continue;

                float typeRoll = _decorRng.Randf();
                string decorId = typeRoll < 0.34f ? "rose"
                    : typeRoll < 0.67f ? "clover"
                    : "dandelion";

                chunk.SetBlockInternal(x, localSurfaceY + 1, z,
                    new BlockState { BlockId = decorId, BitMask = 0xFF });
            }
        }
    }

    private void GenerateTrees(Chunk chunk, Vector3I chunkPos)
    {
        _treeRng.Seed = (ulong)(chunkPos.X * 73856093 ^ chunkPos.Z * 19349663 ^ chunkPos.Y * 83492791);

        for (int x = 2; x < Chunk.SIZE - 2; x++)
        {
            for (int z = 2; z < Chunk.SIZE - 2; z++)
            {
                if (_treeRng.Randf() > 0.02f) continue;

                int worldX = chunkPos.X * Chunk.SIZE + x;
                int worldZ = chunkPos.Z * Chunk.SIZE + z;

                int surfaceY = GetSurfaceHeight(worldX, worldZ);

                float temp = _biomeTemp.GetNoise2D(worldX, worldZ);
                float humid = _biomeHumid.GetNoise2D(worldX, worldZ);
                Biome biome = GetBiome(temp, humid, surfaceY);

                if (biome == Biome.Desert || biome == Biome.Beach || biome == Biome.Tundra) continue;
                if (surfaceY <= WaterLevel) continue;

                int localSurfaceY = surfaceY - (chunkPos.Y * Chunk.HEIGHT);
                if (localSurfaceY < 1 || localSurfaceY >= Chunk.HEIGHT - 8) continue;

                BlockState surface = chunk.GetBlock(x, localSurfaceY, z);
                if (surface.BlockId != "grass_block") continue;

                int trunkHeight = 4 + _treeRng.RandiRange(0, 2);

                for (int ty = 1; ty <= trunkHeight; ty++)
                {
                    int y = localSurfaceY + ty;
                    if (y >= Chunk.HEIGHT) break;
                    chunk.SetBlockInternal(x, y, z, new BlockState { BlockId = "log", BitMask = 0xFF });
                }

                int canopyBaseY = localSurfaceY + trunkHeight - 1;
                int[] layerRadius = { 2, 2, 1, 1 };

                for (int layer = 0; layer < layerRadius.Length; layer++)
                {
                    int radius = layerRadius[layer];
                    int ly = canopyBaseY + layer;
                    if (ly < 0 || ly >= Chunk.HEIGHT) continue;

                    for (int lx = -radius; lx <= radius; lx++)
                    {
                        for (int lz = -radius; lz <= radius; lz++)
                        {
                            int bx = x + lx;
                            int bz = z + lz;
                            if (bx < 0 || bx >= Chunk.SIZE || bz < 0 || bz >= Chunk.SIZE) continue;
                            if (Mathf.Abs(lx) == radius && Mathf.Abs(lz) == radius && radius > 1) continue;

                            if (lx == 0 && lz == 0)
                                chunk.SetBlockInternal(bx, ly, bz, new BlockState { BlockId = "log", BitMask = 0xFF });
                            else
                                chunk.SetBlockInternal(bx, ly, bz, new BlockState { BlockId = "leaves", BitMask = 0xFF });
                        }
                    }
                }

                int trunkTopY = localSurfaceY + trunkHeight + 2;
                if (trunkTopY < Chunk.HEIGHT)
                    chunk.SetBlockInternal(x, trunkTopY, z, new BlockState { BlockId = "leaves", BitMask = 0xFF });

                int topY = canopyBaseY + layerRadius.Length;
                if (topY < Chunk.HEIGHT)
                    chunk.SetBlockInternal(x, topY, z, new BlockState { BlockId = "leaves", BitMask = 0xFF });
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

    // Used for organic/natural block changes (e.g. grass spreading across
    // a chunk boundary) that should NOT be persisted as a save
    // modification - consistent with how grass spreading within a single
    // chunk already behaves (natural growth regenerates on its own rather
    // than bloating save files with every spread event). If no chunk is
    // currently loaded at the target world position (e.g. right at the
    // edge of the loaded world), this silently does nothing.
    public void SetBlockAtWorldNaturalGrowth(Vector3I worldPos, BlockState state)
    {
        Vector3I chunkPos = WorldToChunk(worldPos);
        if (!_chunks.TryGetValue(chunkPos, out Chunk chunk))
            return;

        int localX = worldPos.X - chunkPos.X * Chunk.SIZE;
        int localY = worldPos.Y - chunkPos.Y * Chunk.HEIGHT;
        int localZ = worldPos.Z - chunkPos.Z * Chunk.SIZE;

        chunk.SetBlockNaturalGrowth(localX, localY, localZ, state);
    }
}