using Godot;
using System.Collections.Generic;

/// <summary>
/// Represents the state of a single block in the world.
/// Lightweight struct - millions of these exist at once.
/// </summary>
public struct BlockState
{
    // Which block type this is
    public string BlockId;

    // Chisel bit mask - which of the 8 sub-cubes are active
    // 11111111 = full block
    // 00001111 = bottom slab
    // 11110000 = top slab
    public byte BitMask;

    // Block rotation (0, 90, 180, 270)
    public byte Rotation;

    // Active features on this block
    public string[] Features;

    // Air block constant
    public static readonly BlockState Air = new BlockState
    {
        BlockId = "air",
        BitMask = 0,
        Rotation = 0,
        Features = new string[0]
    };

    // Full solid block constructor
    public BlockState(string blockId)
    {
        BlockId = blockId;
        BitMask = 0b11111111;
        Rotation = 0;
        Features = new string[0];
    }

    // Full block with features constructor
    public BlockState(string blockId, string[] features)
    {
        BlockId = blockId;
        BitMask = 0b11111111;
        Rotation = 0;
        Features = features;
    }

    // Check if this block is air
    public bool IsAir()
    {
        return BlockId == "air" || BitMask == 0;
    }

    // Check if this block is a full solid block
    public bool IsFullBlock()
    {
        return BitMask == 0b11111111;
    }

    // Check if a specific bit is active
    // Top layer:    [4][5]
    //               [6][7]
    // Bottom layer: [0][1]
    //               [2][3]
    public bool IsBitActive(int bitIndex)
    {
        return (BitMask & (1 << bitIndex)) != 0;
    }

    // Set a specific bit active or inactive
    public void SetBit(int bitIndex, bool active)
    {
        if (active)
            BitMask |= (byte)(1 << bitIndex);
        else
            BitMask &= (byte)~(1 << bitIndex);
    }

    // Check if this block has a specific feature
    public bool HasFeature(string featureId)
    {
        if (Features == null) return false;
        foreach (var f in Features)
            if (f == featureId) return true;
        return false;
    }

    // Add a feature to this block
    public void AddFeature(string featureId)
    {
        if (HasFeature(featureId)) return;
        var newFeatures = new string[(Features?.Length ?? 0) + 1];
        if (Features != null)
            Features.CopyTo(newFeatures, 0);
        newFeatures[newFeatures.Length - 1] = featureId;
        Features = newFeatures;
    }

    // Remove a feature from this block
    public void RemoveFeature(string featureId)
    {
        if (Features == null) return;
        var newList = new List<string>(Features);
        newList.Remove(featureId);
        Features = newList.ToArray();
    }

    // Count active bits
    public int ActiveBitCount()
    {
        int count = 0;
        for (int i = 0; i < 8; i++)
            if (IsBitActive(i)) count++;
        return count;
    }
}