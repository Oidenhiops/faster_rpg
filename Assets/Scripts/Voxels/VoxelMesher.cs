using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera las mallas de un chunk (16³ bloques de 1m) con dos niveles de detalle:
/// - Bloques uniformes: caras de 1m completas (rápido, 1 quad por cara visible).
/// - Bloques parciales: caras por micro-voxel (1/8 m) solo donde dan al aire.
/// El agua va a una malla aparte (transparente, sin collider); para el terreno
/// sólido el agua cuenta como aire, así el fondo se ve a través de ella.
/// Función pura sobre un Snapshot: puede correr en un hilo de fondo.
/// </summary>
public static class VoxelMesher
{
    public const int P = VoxelChunk.SIZE + 2; // chunk + 1 bloque de borde de los vecinos
    public const int P3 = P * P * P;
    const int M = VoxelChunk.MICRO;
    const float MV = 1f / M;
    // superficie del agua hundida exactamente 1 capa micro (7/8), para que la malla
    // de los bloques de agua uniformes empalme sin costura con el agua micro-voxel
    const float WATER_TOP = 1f - 1f / M;

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

    public class BuildResult
    {
        public MeshData solid = new MeshData();
        public MeshData water = new MeshData();
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

    static bool IsSolid(byte id, byte waterId) => id != 0 && id != waterId;

    public static BuildResult Build(Snapshot s, Rect[] typeRects, byte waterId)
    {
        var result = new BuildResult();
        MeshData solid = result.solid;
        MeshData water = result.water;

        for (int y = 0; y < VoxelChunk.SIZE; y++)
            for (int z = 0; z < VoxelChunk.SIZE; z++)
                for (int x = 0; x < VoxelChunk.SIZE; x++)
                {
                    int bi = Snapshot.Idx(x, y, z);
                    byte t = s.types[bi];
                    byte[] micro = s.micro[bi];
                    if (micro == null && t == 0) continue; // aire

                    var blockPos = new Vector3(x, y, z);

                    if (micro == null && t == waterId)
                    {
                        // ---- bloque de agua ----
                        Rect wRect = typeRects[waterId];

                        // ¿es agua de superficie? (encima no hay otro bloque de agua uniforme)
                        int ai = Snapshot.Idx(x, y + 1, z);
                        bool surfaceWater = !(s.micro[ai] == null && s.types[ai] == waterId);

                        for (int f = 0; f < 6; f++)
                        {
                            Vector3Int d = Dirs[f];
                            int ni = Snapshot.Idx(x + d.x, y + d.y, z + d.z);
                            byte nt = s.types[ni];
                            byte[] nm = s.micro[ni];

                            if (nm == null)
                            {
                                if (nt != 0) continue; // sólido u otra agua: oculta
                                if (f == 0) AddWaterTop(water, blockPos, wRect);
                                // costados del agua superficial recortados a la altura de la superficie
                                else AddQuad(water, blockPos, f, 1f, blockPos, wRect, d,
                                             surfaceWater ? WATER_TOP : 1f);
                            }
                            else if (f == 0)
                            {
                                // superficie contra bloque parcial encima: también hundida,
                                // celda por celda donde la capa inferior del vecino esté vacía
                                for (int mz2 = 0; mz2 < M; mz2++)
                                    for (int mx2 = 0; mx2 < M; mx2++)
                                    {
                                        if (nm[VoxelChunk.MicroIndex(mx2, 0, mz2)] != 0) continue;
                                        AddWaterTopCell(water, blockPos, mx2, mz2, wRect);
                                    }
                            }
                            else
                            {
                                // vecino parcial: agua visible donde sus micro-celdas están vacías
                                // (sin la capa micro superior si es agua de superficie)
                                EmitFaceAgainstPartial(water, s, x, y, z, f, wRect, waterId,
                                    waterOnlyAir: true, skipTopLayer: surfaceWater);
                            }
                        }
                        continue;
                    }

                    if (micro == null)
                    {
                        // ---- bloque sólido uniforme ----
                        Rect rect = typeRects[t];
                        for (int f = 0; f < 6; f++)
                        {
                            Vector3Int d = Dirs[f];
                            int ni = Snapshot.Idx(x + d.x, y + d.y, z + d.z);
                            byte nt = s.types[ni];
                            byte[] nm = s.micro[ni];

                            if (nm == null && IsSolid(nt, waterId)) continue;                              // vecino sólido: oculta
                            if (nm == null) AddQuad(solid, blockPos, f, 1f, blockPos, rect, d);             // aire o agua: quad de 1m
                            else EmitFaceAgainstPartial(solid, s, x, y, z, f, rect, waterId, false, false); // parcial: por micro-celda
                        }
                    }
                    else
                    {
                        // ---- bloque parcial: micro-voxels expuestos ----
                        for (int my = 0; my < M; my++)
                            for (int mz = 0; mz < M; mz++)
                                for (int mx = 0; mx < M; mx++)
                                {
                                    byte id = micro[VoxelChunk.MicroIndex(mx, my, mz)];
                                    if (id == 0) continue;
                                    Rect rect = typeRects[id];
                                    var microPos = blockPos + new Vector3(mx, my, mz) * MV;

                                    if (id == waterId)
                                    {
                                        // agua micro: solo caras contra aire, a la malla de agua
                                        for (int f = 0; f < 6; f++)
                                        {
                                            Vector3Int d = Dirs[f];
                                            if (MicroAt(s, x, y, z, mx + d.x, my + d.y, mz + d.z) != 0) continue;
                                            AddQuad(water, microPos, f, MV, blockPos, rect, d);
                                        }
                                        continue;
                                    }

                                    for (int f = 0; f < 6; f++)
                                    {
                                        Vector3Int d = Dirs[f];
                                        byte nb = MicroAt(s, x, y, z, mx + d.x, my + d.y, mz + d.z);
                                        if (IsSolid(nb, waterId)) continue; // aire o agua: cara visible
                                        AddQuad(solid, microPos, f, MV, blockPos, rect, d);
                                    }
                                }
                    }
                }
        return result;
    }

    // cara de un bloque uniforme contra un vecino parcial: emitir solo las
    // micro-celdas cuya celda opuesta en el vecino está vacía
    static void EmitFaceAgainstPartial(MeshData md, Snapshot s, int x, int y, int z, int f,
        Rect rect, byte waterId, bool waterOnlyAir, bool skipTopLayer)
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

                if (skipTopLayer && my == M - 1) continue; // sobre la superficie del agua no hay nada
                byte nb = MicroAt(s, x, y, z, mx + d.x, my + d.y, mz + d.z);
                bool hidden = waterOnlyAir ? nb != 0 : IsSolid(nb, waterId);
                if (hidden) continue;
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
    // dentro del bloque, así los micro-voxels muestrean su porción del atlas
    static void AddQuad(MeshData md, Vector3 origin, int f, float size, Vector3 blockBase, Rect rect, Vector3Int normal,
        float clipTopY = float.PositiveInfinity)
    {
        int vi = md.vertices.Count;
        Vector3[] corners = Corners[f];
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = origin + corners[i] * size;
            if (p.y - origin.y > clipTopY) p.y = origin.y + clipTopY; // recorte para agua superficial
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

    // celda micro de superficie de agua hundida (contra bloques parciales encima)
    static void AddWaterTopCell(MeshData md, Vector3 blockPos, int mx, int mz, Rect rect)
    {
        int vi = md.vertices.Count;
        Vector3[] corners = Corners[0];
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = corners[i];
            float lx = (mx + c.x) * MV, lz = (mz + c.z) * MV;
            md.vertices.Add(blockPos + new Vector3(lx, WATER_TOP, lz));
            md.normals.Add(Vector3.up);
            md.uvs.Add(new Vector2(rect.xMin + lx * rect.width, rect.yMin + lz * rect.height));
        }
        md.triangles.Add(vi);
        md.triangles.Add(vi + 1);
        md.triangles.Add(vi + 2);
        md.triangles.Add(vi);
        md.triangles.Add(vi + 2);
        md.triangles.Add(vi + 3);
    }

    // superficie del agua: cara superior hundida
    static void AddWaterTop(MeshData md, Vector3 blockPos, Rect rect)
    {
        int vi = md.vertices.Count;
        Vector3[] corners = Corners[0];
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = corners[i];
            md.vertices.Add(blockPos + new Vector3(c.x, WATER_TOP, c.z));
            md.normals.Add(Vector3.up);
            md.uvs.Add(new Vector2(rect.xMin + c.x * rect.width, rect.yMin + c.z * rect.height));
        }
        md.triangles.Add(vi);
        md.triangles.Add(vi + 1);
        md.triangles.Add(vi + 2);
        md.triangles.Add(vi);
        md.triangles.Add(vi + 2);
        md.triangles.Add(vi + 3);
    }
}
