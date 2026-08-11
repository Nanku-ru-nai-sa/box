using Godot;

// One "geology" band in ChunkManager's Layers list. Each entry lines up
// with ONE vertical chunk row (16 blocks tall) - index 0 sits just above
// bedrock, index 1 is the chunk row above that, and so on going up. This
// only controls the FALLBACK rock (what a block becomes if nothing else -
// ore veins, gravel/rock/dirt patches, obsidian - already claimed it), so
// bedrock itself and all the existing patch systems are untouched.
//
// BlockOptions takes 1-3 BlockIds:
//   1 entry  -> that layer is just that one block, same as before.
//   2-3 entries -> the layer generates as big, blobby PATCHES of each
//                  option mixed together (not per-block random noise -
//                  noise that fine-grained looks like static/salt-and-
//                  pepper, not natural material pockets). Good for
//                  things like "this band is a mix of sand, silt, and
//                  clay" rather than one uniform block. Use BlockWeights
//                  below to make the mix uneven (e.g. 70% one, 15/15 the
//                  other two) instead of a straight even split.
//
// Every BlockId here must already be registered in BlockRegistry.cs (or
// be an existing block like "stone"/"gravel"/"clay"/"sand"/"dirt"/"rock")
// - an unregistered id silently renders as invisible at that spot rather
// than crashing, so a "missing" layer usually means a typo or a block
// that hasn't been added to BlockRegistry yet.
[GlobalClass]
public partial class GeologyLayerDef : Resource
{
    // Just a label for keeping track of which entry is which in the
    // Inspector list - doesn't affect generation at all.
    [Export] public string LayerName = "New Layer";

    [Export] public string[] BlockOptions = new string[] { "stone" };

    // Relative weights for each BlockOptions entry, SAME LENGTH and SAME
    // ORDER as BlockOptions - e.g. {50, 25, 25} or {70, 15, 15}. Don't
    // need to add up to 100, they're normalized automatically (so {2,1,1}
    // behaves the same as {50,25,25}). Leave this empty, or a different
    // length than BlockOptions, for an even split - that's the default.
    //
    // Heads up: this steers proportions, it doesn't guarantee them down
    // to the decimal - the patches are still noise-based, not a literal
    // dice roll, so a "50%" option will look CLOSE to half but not exact.
    [Export] public float[] BlockWeights = new float[] { };

    // Patch size for this layer, only matters with 2-3 BlockOptions.
    // LOWER = bigger/smoother patches. HIGHER = smaller, more frequent
    // patches. Try 0.02-0.03 for large sweeping pockets, 0.08-0.12 for
    // small frequent ones. Each layer can be tuned independently.
    [Export] public float PatchScale = 0.05f;
}