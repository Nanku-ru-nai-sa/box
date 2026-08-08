using Godot;

// One ore's vein-placement settings, as an entry in ChunkManager's Ores
// list. Adding a new ore is now just adding a new entry to that list in
// the Inspector - no new [Export] fields or code changes needed for
// placement. (You'll still need a matching entry in OreRegistry.cs with
// its overlay texture and drop item before it can actually show up and
// be minND though - this only controls WHERE it generates.)
//
// List order matters: entries are placed in the order they appear, and
// once a block is claimed by one ore's vein it can't be claimed by
// another - so if two ores' Y ranges overlap, whichever is listed FIRST
// wins that spot. Keep rarer ores near the top of the list (mithril,
// diamond, ...) and common ones near the bottom (coal) so rare ores
// don't get crowded out, same idea as it worked before this was a list.
[GlobalClass]
public partial class OreVeinDef : Resource
{
    // Must match an OreId registered in OreRegistry.cs.
    [Export] public string OreId = "coal";

    [Export] public int StartY = 128;        // highest Y this ore can appear at
    [Export] public int Depth = 50;          // how far below StartY it keeps appearing (StartY - Depth = lowest Y)
    [Export] public int VeinSize = 8;        // roughly how many blocks are in one pocket
    [Export] public float VeinsPerChunk = 1f; // vein attempts per chunk - can be fractional (0.25 = roughly 1 attempt every 4 chunks)
}