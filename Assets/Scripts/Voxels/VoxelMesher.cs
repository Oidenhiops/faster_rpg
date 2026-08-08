using System.Collections.Concurrent;
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
        public readonly byte[] waterLvl = new byte[P3];  // nivel de agua 1-8 (8 = llena/fuente)

        public static int Idx(int x, int y, int z) => (x + 1) + P * ((z + 1) + P * (y + 1));
    }

    // ---- pools: cada remesh (uno por chunk que cambia) creaba un Snapshot y un
    // BuildResult nuevos y los tiraba a la basura al terminar. Con streaming + ediciones
    // frecuentes eso genera bastante presión de GC (arreglos de ~5800 elementos varias
    // veces por segundo), lo cual se siente como micro-cortes de frame. Reutilizar las
    // mismas instancias entre remeshes elimina esas asignaciones. Son ConcurrentBag
    // porque varios RemeshAsync corren a la vez en hilos de background distintos
    // (ver VoxelWorld.RemeshAsync) y pueden pedir/devolver al mismo tiempo.
    static readonly ConcurrentBag<Snapshot> snapshotPool = new ConcurrentBag<Snapshot>();
    static readonly ConcurrentBag<BuildResult> resultPool = new ConcurrentBag<BuildResult>();

    /// <summary>Toma un Snapshot reciclado del pool, o crea uno nuevo si no hay ninguno libre.
    /// BuildSnapshot sobrescribe sus 3 arreglos por completo (todas las P3 celdas), así que
    /// no hace falta limpiar los datos de un uso anterior antes de reusarlo.</summary>
    public static Snapshot RentSnapshot() => snapshotPool.TryTake(out Snapshot s) ? s : new Snapshot();

    /// <summary>Devuelve un Snapshot al pool. Llamar solo cuando ya nadie más lo va a leer
    /// (Build ya lo consumió por completo, no guarda referencias a sus arreglos).</summary>
    public static void ReturnSnapshot(Snapshot s) => snapshotPool.Add(s);

    static BuildResult RentResult()
    {
        if (resultPool.TryTake(out BuildResult r)) { r.Clear(); return r; }
        return new BuildResult();
    }

    /// <summary>Devuelve un BuildResult al pool. Llamar solo después de que sus vértices ya
    /// se subieron a los Mesh de Unity (Mesh.SetVertices copia los datos; no se queda con
    /// una referencia a estas listas), para no reciclar algo que todavía está en uso.</summary>
    public static void ReturnResult(BuildResult r) => resultPool.Add(r);

    // altura visual del agua según su nivel
    static float WaterHeight(byte lvl) => lvl >= 8 ? WATER_TOP : Mathf.Max((int)lvl, 1) / (float)M;

    public class MeshData
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Vector2> uvs = new List<Vector2>();
        public List<int> triangles = new List<int>();

        // vacía las listas pero conserva su capacidad interna (el arreglo reservado no se
        // libera), así que reusar un MeshData reciclado del pool no vuelve a pagar el costo
        // de ir agrandando las listas desde 0 cada vez.
        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            triangles.Clear();
        }
    }

    public class BuildResult
    {
        public MeshData solid = new MeshData();
        public MeshData water = new MeshData();
        public MeshData plants = new MeshData();

        public void Clear()
        {
            solid.Clear();
            water.Clear();
            plants.Clear();
        }
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

    static bool IsSolid(byte id, byte waterId, bool[] plants) =>
        id != 0 && id != waterId && !plants[id];

    static bool AirLike(byte id, bool[] plants) => id == 0 || plants[id];

    public static BuildResult Build(Snapshot s, Rect[] typeRects, byte waterId, bool[] plants)
    {
        var result = RentResult();
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

                    if (micro == null && plants[t])
                    {
                        // planta: dos quads cruzados, a la malla de plantas
                        AddPlantCross(result.plants, blockPos, typeRects[t]);
                        continue;
                    }

                    if (micro == null && t == waterId)
                    {
                        // ---- bloque de agua ----
                        Rect wRect = typeRects[waterId];

                        // el agua nunca "sube" por tener algo encima: conserva su superficie.
                        // Solo una columna continua de agua (agua sobre agua) la dibuja llena.
                        int ai = Snapshot.Idx(x, y + 1, z);
                        byte[] aboveMicro = s.micro[ai];
                        bool aboveWater = aboveMicro == null && s.types[ai] == waterId;
                        float topH = aboveWater ? 1f : WaterHeight(s.waterLvl[bi]);
                        int topLayer = Mathf.Clamp(Mathf.RoundToInt(topH * M), 1, M); // capas micro visibles

                        for (int f = 0; f < 6; f++)
                        {
                            Vector3Int d = Dirs[f];
                            int ni = Snapshot.Idx(x + d.x, y + d.y, z + d.z);
                            byte nt = s.types[ni];
                            byte[] nm = s.micro[ni];

                            if (f == 0)
                            {
                                if (aboveWater) continue; // columna continua: sin tapa
                                if (aboveMicro == null)
                                {
                                    // aire, planta o sólido encima: la tapa se dibuja igual
                                    // (bajo un bloque queda la ranura de 1/8, visible de lado)
                                    AddWaterTop(water, blockPos, wRect, topH);
                                }
                                else
                                {
                                    // parcial encima: tapa salvo donde su agua micro continúa la columna
                                    for (int mz2 = 0; mz2 < M; mz2++)
                                        for (int mx2 = 0; mx2 < M; mx2++)
                                        {
                                            if (aboveMicro[VoxelChunk.MicroIndex(mx2, 0, mz2)] == waterId) continue;
                                            AddWaterTopCell(water, blockPos, mx2, mz2, wRect, topH);
                                        }
                                }
                                continue;
                            }

                            if (f == 1)
                            {
                                if (nm == null)
                                {
                                    if (AirLike(nt, plants)) AddQuad(water, blockPos, f, 1f, blockPos, wRect, d);
                                }
                                else EmitFaceAgainstPartial(water, s, x, y, z, f, wRect, waterId, plants, true, M);
                                continue;
                            }

                            // ---- caras laterales ----
                            if (nm == null)
                            {
                                if (nt == waterId)
                                {
                                    // vecino agua con nivel más bajo: pared del escalón
                                    int nai = Snapshot.Idx(x + d.x, y + 1, z + d.z);
                                    bool nAboveWater = s.micro[nai] == null && s.types[nai] == waterId;
                                    float nH = nAboveWater ? 1f : WaterHeight(s.waterLvl[ni]);
                                    if (nH < topH)
                                        AddQuad(water, blockPos, f, 1f, blockPos, wRect, d, topH, nH);
                                }
                                else if (AirLike(nt, plants))
                                {
                                    AddQuad(water, blockPos, f, 1f, blockPos, wRect, d, topH);
                                }
                            }
                            else
                            {
                                // vecino parcial: agua visible donde sus micro-celdas están vacías
                                EmitFaceAgainstPartial(water, s, x, y, z, f, wRect, waterId, plants, true, topLayer);
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

                            if (nm == null && IsSolid(nt, waterId, plants)) continue;                           // vecino sólido: oculta
                            if (nm == null) AddQuad(solid, blockPos, f, 1f, blockPos, rect, d);                  // aire/agua/planta: quad de 1m
                            else EmitFaceAgainstPartial(solid, s, x, y, z, f, rect, waterId, plants, false, M);  // parcial: por micro-celda
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
                                        // agua micro: caras contra aire, incluida la franja de aire
                                        // que queda sobre agua vecina de nivel más bajo
                                        for (int f = 0; f < 6; f++)
                                        {
                                            Vector3Int d = Dirs[f];
                                            if (!WaterMicroNeighborOpen(s, x, y, z, mx + d.x, my + d.y, mz + d.z, waterId, plants))
                                                continue;
                                            AddQuad(water, microPos, f, MV, blockPos, rect, d);
                                        }
                                        continue;
                                    }

                                    for (int f = 0; f < 6; f++)
                                    {
                                        Vector3Int d = Dirs[f];
                                        byte nb = MicroAt(s, x, y, z, mx + d.x, my + d.y, mz + d.z);
                                        if (IsSolid(nb, waterId, plants)) continue; // aire, agua o planta: cara visible
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
        Rect rect, byte waterId, bool[] plants, bool waterOnlyAir, int maxLayer)
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

                if (my >= maxLayer) continue; // por encima de la superficie del agua no hay nada
                byte nb = MicroAt(s, x, y, z, mx + d.x, my + d.y, mz + d.z);
                bool hidden = waterOnlyAir ? !AirLike(nb, plants) : IsSolid(nb, waterId, plants);
                if (hidden) continue;
                AddQuad(md, blockPos + new Vector3(mx, my, mz) * MV, f, MV, blockPos, rect, d);
            }
    }

    // ¿está abierta (aire) la celda vecina para una cara de agua micro?
    // A diferencia de MicroAt, si el vecino es un bloque de agua uniforme con nivel
    // bajo, la franja por encima de su superficie cuenta como aire (cara visible)
    static bool WaterMicroNeighborOpen(Snapshot s, int bx, int by, int bz, int mx, int my, int mz, byte waterId, bool[] plants)
    {
        if (mx < 0) { bx--; mx += M; } else if (mx >= M) { bx++; mx -= M; }
        if (my < 0) { by--; my += M; } else if (my >= M) { by++; my -= M; }
        if (mz < 0) { bz--; mz += M; } else if (mz >= M) { bz++; mz -= M; }

        int bi = Snapshot.Idx(bx, by, bz);
        byte[] micro = s.micro[bi];
        if (micro != null) return micro[VoxelChunk.MicroIndex(mx, my, mz)] == 0;

        byte t = s.types[bi];
        if (AirLike(t, plants)) return true;
        if (t != waterId) return false; // sólido

        // vecino agua uniforme: abierto solo por encima de su superficie
        int ai = Snapshot.Idx(bx, by + 1, bz);
        if (s.micro[ai] == null && s.types[ai] == waterId) return false; // columna continua de agua
        int layers = Mathf.Clamp(Mathf.RoundToInt(WaterHeight(s.waterLvl[bi]) * M), 1, M);
        return my >= layers;
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
        float clipTopY = float.PositiveInfinity, float clipBottomY = float.NegativeInfinity)
    {
        int vi = md.vertices.Count;
        Vector3[] corners = Corners[f];
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = origin + corners[i] * size;
            if (p.y - origin.y > clipTopY) p.y = origin.y + clipTopY;       // recorte superior (agua)
            if (p.y - origin.y < clipBottomY) p.y = origin.y + clipBottomY; // recorte inferior (escalones)
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

    // planta: dos quads cruzados en X, hundidos en el suelo para que la maleza
    // "nazca" del terreno aunque el bloque de abajo sea parcial (micro-relieve)
    static void AddPlantCross(MeshData md, Vector3 blockPos, Rect rect)
    {
        const float INSET = 0.15f;  // margen lateral
        const float SINK = 0.9f;    // cuánto se hunde bajo la celda
        const float TOP = 0.85f;    // altura de las puntas

        // quad 1: diagonal \  |  quad 2: diagonal /
        AddPlantQuad(md, blockPos,
            new Vector3(INSET, -SINK, INSET), new Vector3(1f - INSET, -SINK, 1f - INSET), TOP + SINK, rect);
        AddPlantQuad(md, blockPos,
            new Vector3(1f - INSET, -SINK, INSET), new Vector3(INSET, -SINK, 1f - INSET), TOP + SINK, rect);
    }

    static void AddPlantQuad(MeshData md, Vector3 blockPos, Vector3 a, Vector3 b, float height, Rect rect)
    {
        int vi = md.vertices.Count;
        Vector3 up = new Vector3(0f, height, 0f);
        md.vertices.Add(blockPos + a + up);
        md.vertices.Add(blockPos + b + up);
        md.vertices.Add(blockPos + b);
        md.vertices.Add(blockPos + a);
        for (int i = 0; i < 4; i++) md.normals.Add(Vector3.up); // iluminación suave
        md.uvs.Add(new Vector2(rect.xMin, rect.yMax));
        md.uvs.Add(new Vector2(rect.xMax, rect.yMax));
        md.uvs.Add(new Vector2(rect.xMax, rect.yMin));
        md.uvs.Add(new Vector2(rect.xMin, rect.yMin));
        md.triangles.Add(vi);
        md.triangles.Add(vi + 1);
        md.triangles.Add(vi + 2);
        md.triangles.Add(vi);
        md.triangles.Add(vi + 2);
        md.triangles.Add(vi + 3);
    }

    // celda micro de superficie de agua (contra bloques parciales encima)
    static void AddWaterTopCell(MeshData md, Vector3 blockPos, int mx, int mz, Rect rect, float height)
    {
        int vi = md.vertices.Count;
        Vector3[] corners = Corners[0];
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = corners[i];
            float lx = (mx + c.x) * MV, lz = (mz + c.z) * MV;
            md.vertices.Add(blockPos + new Vector3(lx, height, lz));
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

    // superficie del agua a la altura de su nivel
    static void AddWaterTop(MeshData md, Vector3 blockPos, Rect rect, float height)
    {
        int vi = md.vertices.Count;
        Vector3[] corners = Corners[0];
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = corners[i];
            md.vertices.Add(blockPos + new Vector3(c.x, height, c.z));
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
