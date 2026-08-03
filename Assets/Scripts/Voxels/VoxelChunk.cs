using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chunk de 16x16x16 bloques de 1m. Cada bloque puede ser uniforme (solo su tipo,
/// 1 byte) o parcial (excavado a medias), en cuyo caso guarda sus 16³ micro-voxels.
/// </summary>
public sealed class VoxelChunk
{
    public const int SIZE = 16;                       // bloques (1m) por eje
    public const int SIZE3 = SIZE * SIZE * SIZE;      // 4096 bloques
    public const int MICRO = 8;                       // micro-voxels por eje dentro de un bloque
    public const int MICRO3 = MICRO * MICRO * MICRO;  // 512 micro-voxels

    public readonly byte[] blockTypes = new byte[SIZE3]; // 0 = aire
    // Solo los bloques parcialmente excavados asignan sus 16³ voxels (4 KB c/u).
    // Clave: BlockIndex local. Si un bloque queda vacío del todo, se elimina de aquí.
    public readonly Dictionary<int, byte[]> microBlocks = new Dictionary<int, byte[]>();

    public Vector3Int coord;
    public bool dirty;
    public bool remeshing;

    public GameObject go;
    public Mesh mesh;
    public MeshFilter filter;
    public MeshCollider collider;

    // malla de agua (transparente, sin collider)
    public GameObject waterGo;
    public Mesh waterMesh;
    public MeshFilter waterFilter;

    public static int BlockIndex(int x, int y, int z) => x + SIZE * (z + SIZE * y);
    public static int MicroIndex(int x, int y, int z) => x + MICRO * (z + MICRO * y);
}
