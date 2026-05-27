using System;
using UnityEngine;

[Flags]
public enum BlockFace
{
    None  = 0,
    Up    = 1 << 0,  // +Y
    Down  = 1 << 1,  // -Y
    North = 1 << 2,  // +Z
    South = 1 << 3,  // -Z
    East  = 1 << 4,  // +X
    West  = 1 << 5,  // -X
    All   = Up | Down | North | South | East | West
}

public static class BlockFaceExtensions
{
    // Mapeo de cara -> offset de vecino en la grilla. El orden coincide con NeighborOffsets para iteración eficiente.
    public static readonly Vector3Int[] NeighborOffsets = new Vector3Int[]
    {
        new Vector3Int( 0,  1,  0), // Up
        new Vector3Int( 0, -1,  0), // Down
        new Vector3Int( 0,  0,  1), // North
        new Vector3Int( 0,  0, -1), // South
        new Vector3Int( 1,  0,  0), // East
        new Vector3Int(-1,  0,  0), // West
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

    // Cara opuesta: si yo salgo por Up, mi vecino me recibe por Down.
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

    // Convierte un offset cardinal unitario en la cara correspondiente.
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
