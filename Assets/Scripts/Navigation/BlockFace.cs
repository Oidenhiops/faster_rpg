using System;
using UnityEngine;

[Flags]
public enum BlockFace
{
    None  = 0,
    Up    = 1 << 0,
    Down  = 1 << 1,
    North = 1 << 2,
    South = 1 << 3,
    East  = 1 << 4,
    West  = 1 << 5,
    All   = Up | Down | North | South | East | West
}

public static class BlockFaceExtensions
{
    public static readonly Vector3Int[] NeighborOffsets = new Vector3Int[]
    {
        new Vector3Int( 0,  1,  0),
        new Vector3Int( 0, -1,  0),
        new Vector3Int( 0,  0,  1),
        new Vector3Int( 0,  0, -1),
        new Vector3Int( 1,  0,  0),
        new Vector3Int(-1,  0,  0),
    };

    public static readonly BlockFace[] FaceOrder = new BlockFace[]
    {
        BlockFace.Up,
        BlockFace.Down,
        BlockFace.North,
        BlockFace.South,
        BlockFace.East,
        BlockFace.West,
    };

    public static BlockFace Opposite(this BlockFace face)
    {
        switch (face)
        {
            case BlockFace.Up:    return BlockFace.Down;
            case BlockFace.Down:  return BlockFace.Up;
            case BlockFace.North: return BlockFace.South;
            case BlockFace.South: return BlockFace.North;
            case BlockFace.East:  return BlockFace.West;
            case BlockFace.West:  return BlockFace.East;
            default: return BlockFace.None;
        }
    }

    public static bool HasFace(this BlockFace mask, BlockFace face)
    {
        return (mask & face) != 0;
    }

    public static BlockFace FromOffset(Vector3Int offset)
    {
        if (offset.y ==  1 && offset.x == 0 && offset.z == 0) return BlockFace.Up;
        if (offset.y == -1 && offset.x == 0 && offset.z == 0) return BlockFace.Down;
        if (offset.z ==  1 && offset.x == 0 && offset.y == 0) return BlockFace.North;
        if (offset.z == -1 && offset.x == 0 && offset.y == 0) return BlockFace.South;
        if (offset.x ==  1 && offset.y == 0 && offset.z == 0) return BlockFace.East;
        if (offset.x == -1 && offset.y == 0 && offset.z == 0) return BlockFace.West;
        return BlockFace.None;
    }
}
