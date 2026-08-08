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
    // protege microBlocks: la generación (background), las ediciones del jugador
    // (hilo principal) y el remesh (background) pueden tocar el mismo chunk casi al
    // mismo tiempo. Un Dictionary no tolera lectura y escritura estructural concurrentes
    // (Add/Remove mientras otro hilo hace TryGetValue) — sin este lock eso corrompe o
    // tira NullReferenceException dentro de Dictionary.FindEntry.
    public readonly object microLock = new object();

    public Vector3Int coord;
    public bool dirty;
    public bool remeshing;
    public bool edited; // tiene cambios del jugador: sus datos se conservan al descargar
    // true mientras el chunk está cargado (entre GetOrCreateChunk y UnloadColumn), sin
    // importar si ya tiene GameObject. `go` se crea recién cuando el primer remesh
    // encuentra geometría real — muchos chunks (cielo vacío, roca enterrada sin caras
    // visibles) nunca llegan a necesitarlo. Ver VoxelWorld.EnsureChunkObjects.
    public bool loaded;

    public GameObject go;
    public Mesh mesh;
    public MeshFilter filter;
    public MeshCollider collider;

    // malla de agua (transparente, sin collider)
    public GameObject waterGo;
    public Mesh waterMesh;
    public MeshFilter waterFilter;

    // malla de plantas (cutout, sin collider — ver VoxelWorld.DamageBlock/MineVoxel:
    // si un bloque tiene una planta encima, el golpe se la lleva a ella primero)
    public GameObject plantGo;
    public Mesh plantMesh;
    public MeshFilter plantFilter;

    public static int BlockIndex(int x, int y, int z) => x + SIZE * (z + SIZE * y);
    public static int MicroIndex(int x, int y, int z) => x + MICRO * (z + MICRO * y);
}
