using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera la malla de un chunk (16³ bloques de 1m) con dos niveles de detalle:
/// - Bloques uniformes: caras de 1m completas (rápido, 1 quad por cara visible).
/// - Bloques parciales: caras por micro-voxel (1/16 m) solo donde dan al aire.
/// Función pura sobre un Snapshot: puede correr en un hilo de fondo.
/// </summary>
public static class VoxelMesher
{
    public const int P = VoxelChunk.SIZE + 2; // chunk + 1 bloque de borde de los vecinos
    public const int P3 = P * P * P;
    const int M = VoxelChunk.MICRO;
    const float MV = 1f / M;

    /// <summary>Copia inmutable de un chunk + borde de vecinos, a nivel de bloque.</summary>
    public class Snapshot
    {
        public readonly byte[] types = new byte[P3];
        public readonly byte[][] micro = new byte[P3][]; // null = bloque uniforme

        public static int Idx(int x, int y, int z) => (x + 1) + P * ((z + 1) + P * (y + 1));
    }

    public class MeshData
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Vector2> uvs = new List<Vector2>();
        public List<int> triangles = new List<int>();
    }

    static readonly Vector3Int[] Dirs =
    {
        new Vector3Int(0, 1, 0),   // arriba
        new Vector3Int(0, -1, 0),  // abajo
        new Vector3Int(0, 0, 1),   // norte
        new Vector3Int(0, 0, -1),  // sur
        new Vector3Int(1, 0, 0),   // este
        new Vector3Int(-1, 0, 0),  // oeste
    };

    // 4 esquinas por cara, en orden horario visto desde fuera (winding de Unity)
    static readonly Vector3[][] Corners =
    {
        new[] { new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0), new Vector3(0, 1, 0) }, // arriba
        new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) }, // abajo
        new[] { new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1), new Vector3(1, 0, 1) }, // norte
        new[] { new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 0) }, // sur
        new[] { new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1), new Vector3(1, 0, 0) }, // este
        new[] { new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1) }, // oeste
    };

    public static MeshData Build(Snapshot s, Rect[] typeRects)
    {
        var md = new MeshData();
        for (int y = 0; y < VoxelChunk.SIZE; y++)
            for (int z = 0; z < VoxelChunk.SIZE; z++)
                for (int x = 0; x < VoxelChunk.SIZE; x++)
                {
                    int bi = Snapshot.Idx(x, y, z);
                    byte t = s.types[bi];
                    byte[] micro = s.micro[bi];
                    if (micro == null && t == 0) continue; // aire

                    var blockPos = new Vector3(x, y, z);

                    if (micro == null)
                    {
                        // bloque uniforme sólido
                        Rect rect = typeRects[t];
                        for (int f = 0; f < 6; f++)
                        {
                            Vector3Int d = Dirs[f];
                            int ni = Snapshot.Idx(x + d.x, y + d.y, z + d.z);
                            byte nt = s.types[ni];
                            byte[] nm = s.micro[ni];

                            if (nm == null && nt != 0) continue;                            // vecino sólido: oculta
                            if (nm == null) AddQuad(md, blockPos, f, 1f, blockPos, rect, d); // vecino aire: quad de 1m
                            else EmitFaceAgainstPartial(md, s, x, y, z, f, rect);            // vecino parcial: por micro-celda
                        }
                    }
                    else
                    {
                        // bloque parcial: micro-voxels expuestos
                        for (int my = 0; my < M; my++)
                            for (int mz = 0; mz < M; mz++)
                                for (int mx = 0; mx < M; mx++)
                                {
                                    byte id = micro[VoxelChunk.MicroIndex(mx, my, mz)];
                                    if (id == 0) continue;
                                    Rect rect = typeRects[id];
                                    var microPos = blockPos + new Vector3(mx, my, mz) * MV;
                                    for (int f = 0; f < 6; f++)
                                    {
                                        Vector3Int d = Dirs[f];
                                        if (MicroAt(s, x, y, z, mx + d.x, my + d.y, mz + d.z) != 0) continue;
                                        AddQuad(md, microPos, f, MV, blockPos, rect, d);
                                    }
                                }
                    }
                }
        return md;
    }

    // cara de un bloque uniforme contra un vecino parcial: emitir solo las
    // micro-celdas cuya celda opuesta en el vecino está vacía
    static void EmitFaceAgainstPartial(MeshData md, Snapshot s, int x, int y, int z, int f, Rect rect)
    {
        Vector3Int d = Dirs[f];
        var blockPos = new Vector3(x, y, z);
        for (int u = 0; u < M; u++)
            for (int v = 0; v < M; v++)
            {
                int mx, my, mz;
                if (d.x != 0) { mx = d.x > 0 ? M - 1 : 0; my = u; mz = v; }
                else if (d.y != 0) { my = d.y > 0 ? M - 1 : 0; mx = u; mz = v; }
                else { mz = d.z > 0 ? M - 1 : 0; mx = u; my = v; }

                if (MicroAt(s, x, y, z, mx + d.x, my + d.y, mz + d.z) != 0) continue;
                AddQuad(md, blockPos + new Vector3(mx, my, mz) * MV, f, MV, blockPos, rect, d);
            }
    }

    // micro-voxel en coordenadas que pueden salirse del bloque (se normalizan al vecino)
    static byte MicroAt(Snapshot s, int bx, int by, int bz, int mx, int my, int mz)
    {
        if (mx < 0) { bx--; mx += M; } else if (mx >= M) { bx++; mx -= M; }
        if (my < 0) { by--; my += M; } else if (my >= M) { by++; my -= M; }
        if (mz < 0) { bz--; mz += M; } else if (mz >= M) { bz++; mz -= M; }

        int bi = Snapshot.Idx(bx, by, bz);
        byte[] micro = s.micro[bi];
        if (micro == null) return s.types[bi]; // uniforme: todo del tipo (0 = aire)
        return micro[VoxelChunk.MicroIndex(mx, my, mz)];
    }

    // blockBase = esquina del bloque de 1m: los UVs se proyectan según la posición
    // dentro del bloque, así los micro-voxels muestrean su porción del atlas y el
    // "corte" de un bloque taladrado conserva la textura alineada
    static void AddQuad(MeshData md, Vector3 origin, int f, float size, Vector3 blockBase, Rect rect, Vector3Int normal)
    {
        int vi = md.vertices.Count;
        Vector3[] corners = Corners[f];
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = origin + corners[i] * size;
            md.vertices.Add(p);
            md.normals.Add(normal);

            Vector3 l = p - blockBase; // 0..1 dentro del bloque
            float u, v;
            if (normal.y != 0) { u = l.x; v = l.z; }
            else if (normal.z != 0) { u = l.x; v = l.y; }
            else { u = l.z; v = l.y; }
            md.uvs.Add(new Vector2(rect.xMin + u * rect.width, rect.yMin + v * rect.height));
        }
        md.triangles.Add(vi);
        md.triangles.Add(vi + 1);
        md.triangles.Add(vi + 2);
        md.triangles.Add(vi);
        md.triangles.Add(vi + 2);
        md.triangles.Add(vi + 3);
    }
}
