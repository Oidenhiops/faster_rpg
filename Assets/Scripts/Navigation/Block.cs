using System;
using UnityEngine;

public enum BlockType
{
    Block,
    Slope
}

[Serializable]
public class Block
{
    public Vector3Int gridPos;
    public BlockFace openFaces = BlockFace.All;
    public bool isOccupiedOnTop;
    public bool isWalkable = true;
    public float moveCost = 1f;
    public BlockType blockType = BlockType.Block;

    [NonSerialized] public GameObject sourceObject;

    public bool IsTraversable => isWalkable && !isOccupiedOnTop;

    public Block() {}

    public Block(Vector3Int gridPos, BlockFace openFaces, bool isWalkable = true, float moveCost = 1f)
    {
        this.gridPos = gridPos;
        this.openFaces = openFaces;
        this.isWalkable = isWalkable;
        this.moveCost = moveCost;
    }
}
