using Godot;
using System.Collections.Generic;

public struct BlockState
{
    public string BlockId;
    public byte BitMask;
    public byte Rotation;
    public string[] Features;

    public static readonly BlockState Air = new BlockState
    {
        BlockId = "air",
        BitMask = 0,
        Rotation = 0,
        Features = new string[0]
    };

    public BlockState(string blockId)
    {
        BlockId = blockId;
        BitMask = 0b11111111;
        Rotation = 0;
        Features = new string[0];
    }

    public BlockState(string blockId, string[] features)
    {
        BlockId = blockId;
        BitMask = 0b11111111;
        Rotation = 0;
        Features = features;
    }

    public bool IsAir()
    {
        return BlockId == "air" || BitMask == 0;
    }

    public bool IsFullBlock()
    {
        return BitMask == 0b11111111;
    }

    public bool IsBitActive(int bitIndex)
    {
        return (BitMask & (1 << bitIndex)) != 0;
    }

    public void SetBit(int bitIndex, bool active)
    {
        if (active)
            BitMask |= (byte)(1 << bitIndex);
        else
            BitMask &= (byte)~(1 << bitIndex);
    }

    public bool HasFeature(string featureId)
    {
        if (Features == null) return false;
        foreach (var f in Features)
            if (f == featureId) return true;
        return false;
    }

    public void AddFeature(string featureId)
    {
        if (HasFeature(featureId)) return;
        var newFeatures = new string[(Features?.Length ?? 0) + 1];
        if (Features != null)
            Features.CopyTo(newFeatures, 0);
        newFeatures[newFeatures.Length - 1] = featureId;
        Features = newFeatures;
    }

    public void RemoveFeature(string featureId)
    {
        if (Features == null) return;
        var newList = new List<string>(Features);
        newList.Remove(featureId);
        Features = newList.ToArray();
    }

    public int ActiveBitCount()
    {
        int count = 0;
        for (int i = 0; i < 8; i++)
            if (IsBitActive(i)) count++;
        return count;
    }
}