using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generación por COLUMNAS de chunks (16x16 bloques), pensada para streaming:
/// - Etapa 1 (GenerateColumn): terreno, capas, cuevas, suavizado micro y agua.
/// - Etapa 2 (DecorateColumn): vetas, árboles y maleza; puede escribir a través
///   de los bordes, por eso se ejecuta cuando las 8 columnas vecinas ya tienen
///   terreno (patrón de decoración de Minecraft).
/// Todo es determinista: cada bloque depende solo de la semilla del mundo y de
/// sus coordenadas, así que recargar una columna reproduce el mismo resultado.
/// Las zonas del DB se reparten el mapa en regiones Voronoi con alturas mezcladas.
/// Ids clásicos de respaldo: 1=pasto, 2=tierra, 3=piedra, 4=mineral, 5=arena,
/// 7=tronco, 8=hojas, 9=agua, 10=maleza.
/// </summary>
public static class VoxelGenerator
{
    const byte GRASS = 1, DIRT = 2, STONE = 3, ORE = 4, SAND = 5, WOOD = 7, LEAVES = 8, WATER = 9, PLANT = 10;
    const int C = VoxelChunk.SIZE; // 16: bloques por lado de columna

    [Serializable]
    public class Settings
    {
        [Header("Terreno")]
        public float baseHeightMeters = 46f; // media altura de un mundo de 96
        public float hillAmplitudeMeters = 6f;
        [Tooltip("Frecuencia de las colinas (por metro)")]
        public float hillScale = 0.02f;

        [Header("Suavizado (usa los 8³ internos del bloque superior)")]
        public bool smoothSurface = true;
        [Tooltip("Rugosidad fina adicional, en metros")]
        [Range(0f, 0.5f)] public float microDetailAmplitude = 0.1f;
        public float microDetailScale = 0.5f;

        [Header("Agua (solo cuenta la de la zona principal: nivel global del mapa)")]
        [Tooltip("0 = sin agua. Altura del plano de agua; las orillas usan el bloque de orilla")]
        public float waterLevelMeters = 43f;

        [Header("Capas")]
        public int dirtDepthMeters = 3;

        [Header("Cuevas")]
        public float caveScale = 0.09f;
        [Range(0.5f, 0.8f)] public float caveThreshold = 0.62f;
        public float minCaveDepthMeters = 4f;

        [Header("Minerales por ruido (solo si la zona no define vetas)")]
        public float oreScale = 0.2f;
        [Range(0.6f, 0.9f)] public float oreThreshold = 0.74f;

        [Header("Árboles (los tipos y tamaños vienen de la zona en el DB)")]
        [Range(0f, 0.1f)] public float treeDensity = 0.02f;

        [Header("Maleza")]
        [Range(0f, 0.5f)] public float grassDensity = 0.14f;
    }

    // datos precalculados de una zona: parámetros + ids de rol + tablas de spawn
    internal class ZoneData
    {
        public Settings s;
        public byte grass, dirt, stone, ore, sand;
        public List<byte> plantIds = new List<byte>();
        public List<float> plantWeights = new List<float>();
        public float plantTotal;
        public List<(byte trunk, byte leaves, int min, int max, float weight)> trees =
            new List<(byte, byte, int, int, float)>();
        public float treeTotal;
        public List<(byte ore, byte host, float veins, int minH, int maxH, int minSize, int maxSize)> ores =
            new List<(byte, byte, float, int, int, int, int)>();
    }

    /// <summary>Contexto inmutable de generación (zonas, offsets de ruido, nivel de agua).</summary>
    public class GenContext
    {
        internal Settings gs;
        internal int seed;
        internal byte waterId;
        internal float oHill, oCave, oOre, oDetail;
        internal List<ZoneData> zones;
        internal float cell;
        internal int wl;
        internal Vector3Int dims;
    }

    public static GenContext Prepare(VoxelWorld w)
    {
        var c = new GenContext
        {
            gs = w.generation,
            seed = w.worldSeed,
            waterId = w.waterTypeId,
            dims = w.BlockDims,
        };
        var rnd = new System.Random(c.seed);
        c.oHill = Next(rnd); c.oCave = Next(rnd); c.oOre = Next(rnd); c.oDetail = Next(rnd);

        c.zones = new List<ZoneData>();
        if (w.multiBiome && w.typesDB != null && w.typesDB.zones != null)
            foreach (var kv in w.typesDB.zones)
                if (kv.Value != null) c.zones.Add(BuildZoneData(w, kv.Value, c.gs));
        if (c.zones.Count == 0) c.zones.Add(BuildZoneData(w, w.ZoneInfo, c.gs));

        c.cell = Mathf.Max(8f, w.biomeCellSizeMeters);
        c.wl = c.gs.waterLevelMeters > 0f ? Mathf.Min(Mathf.FloorToInt(c.gs.waterLevelMeters), c.dims.y - 1) : 0;
        return c;
    }

    // ------------------------------------------------------------------ etapa 1: terreno

    public static void GenerateColumn(GenContext c, VoxelWorld w, int colX, int colZ)
    {
        int bx0 = colX * C, bz0 = colZ * C;
        const int M = VoxelChunk.MICRO;
        const float MV = 1f / M;
        Vector3Int dims = c.dims;

        // alturas 18x18 (con borde, para la bilineal del micro-detalle) y zona 16x16
        var hs = new float[C + 2, C + 2];
        var zidx = new byte[C, C];
        for (int i = -1; i <= C; i++)
            for (int j = -1; j <= C; j++)
            {
                ZoneAndHeight(c, bx0 + i + 0.5f, bz0 + j + 0.5f, out byte zi, out float h);
                hs[i + 1, j + 1] = h;
                if (i >= 0 && i < C && j >= 0 && j < C) zidx[i, j] = zi;
            }

        var microHeights = new float[M * M];
        for (int i = 0; i < C; i++)
            for (int j = 0; j < C; j++)
            {
                int x = bx0 + i, z = bz0 + j;
                if (x >= dims.x || z >= dims.z) continue;

                ZoneData zd = c.zones[zidx[i, j]];
                Settings zs = zd.s;

                float hCenter = hs[i + 1, j + 1];
                bool beach = c.wl > 0 && hCenter <= c.gs.waterLevelMeters + 0.6f;
                byte surfaceType = beach ? zd.sand : zd.grass;
                byte subType = beach ? zd.sand : zd.dirt;

                int hRef = Mathf.RoundToInt(hCenter);

                float minH = hCenter, maxH = hCenter;
                if (zs.smoothSurface)
                {
                    for (int mz = 0; mz < M; mz++)
                        for (int mx = 0; mx < M; mx++)
                        {
                            float lx = i + (mx + 0.5f) * MV;
                            float lz = j + (mz + 0.5f) * MV;
                            float h = SampleHeightLocal(hs, lx, lz);
                            if (zs.microDetailAmplitude > 0f)
                                h += (Mathf.PerlinNoise(c.oDetail + (bx0 + lx) * zs.microDetailScale,
                                                        c.oDetail + (bz0 + lz) * zs.microDetailScale) - 0.5f)
                                     * 2f * zs.microDetailAmplitude;
                            h = Mathf.Clamp(h, 2f, dims.y - 1.01f);
                            microHeights[mx + M * mz] = h;
                            if (h < minH) minH = h;
                            if (h > maxH) maxH = h;
                        }
                }

                int fullTop = zs.smoothSurface ? Mathf.FloorToInt(minH) : hRef;

                // ---- bloques uniformes debajo de la franja suavizada ----
                for (int y = 0; y < fullTop; y++)
                {
                    int depth = hRef - y;

                    if (depth >= zs.minCaveDepthMeters && y >= 1 &&
                        Noise3(c.oCave + x * zs.caveScale, c.oCave + y * zs.caveScale, c.oCave + z * zs.caveScale) > zs.caveThreshold)
                        continue; // cueva

                    byte id;
                    if (!zs.smoothSurface && depth == 1) id = surfaceType;
                    else if (depth <= 1 + zs.dirtDepthMeters) id = subType;
                    else id = zd.stone;

                    // minerales por ruido: solo como respaldo si la zona no define vetas
                    if (id == zd.stone && zd.ores.Count == 0 &&
                        Noise3(c.oOre + x * zs.oreScale, c.oOre + y * zs.oreScale, c.oOre + z * zs.oreScale) > zs.oreThreshold)
                        id = zd.ore;

                    w.SetBlockSilent(x, y, z, id);
                }

                if (zs.smoothSurface)
                {
                    // ---- franja suavizada: bloques parciales con micro-voxels ----
                    int topBlocks = Mathf.Min(Mathf.CeilToInt(maxH), dims.y - 1);
                    for (int y = fullTop; y < topBlocks; y++)
                    {
                        byte[] micro = w.AllocateMicroSilent(x, y, z, 0);
                        if (micro == null) continue;

                        int filled = 0;
                        for (int mz = 0; mz < M; mz++)
                            for (int mx = 0; mx < M; mx++)
                            {
                                float gm = microHeights[mx + M * mz] * M;
                                int gy0 = y * M;
                                int count = Mathf.Clamp(Mathf.RoundToInt(gm) - gy0, 0, M);
                                for (int my = 0; my < count; my++)
                                {
                                    byte id = (gm - (gy0 + my)) <= 2.5f ? surfaceType : subType;
                                    micro[VoxelChunk.MicroIndex(mx, my, mz)] = id;
                                }
                                filled += count;
                            }

                        if (filled == VoxelChunk.MICRO3) w.SetBlockSilent(x, y, z, subType);
                        else if (filled == 0) w.SetBlockSilent(x, y, z, 0);
                    }
                }

                // ---- agua: desde el nivel global hacia abajo hasta tocar terreno ----
                if (c.wl > 0)
                {
                    int surfaceMicro = c.wl * M - 1;
                    for (int y = c.wl - 1; y >= 1; y--)
                    {
                        byte t = w.GetBlockType(x, y, z);
                        byte[] micro = w.GetMicroArray(x, y, z);

                        if (t == 0 && micro == null)
                        {
                            w.SetBlockSilent(x, y, z, c.waterId);
                            continue;
                        }

                        if (micro == null) break; // sólido uniforme: terreno alcanzado

                        int gy0 = y * M;
                        for (int my = 0; my < M && gy0 + my < surfaceMicro; my++)
                            for (int mz = 0; mz < M; mz++)
                                for (int mx = 0; mx < M; mx++)
                                {
                                    int idx = VoxelChunk.MicroIndex(mx, my, mz);
                                    if (micro[idx] == 0) micro[idx] = c.waterId;
                                }

                        bool sealedBelow = true;
                        for (int mz = 0; mz < M && sealedBelow; mz++)
                            for (int mx = 0; mx < M; mx++)
                                if (micro[VoxelChunk.MicroIndex(mx, 0, mz)] == c.waterId)
                                {
                                    sealedBelow = false;
                                    break;
                                }
                        if (sealedBelow) break;
                    }
                }
            }
    }

    // ------------------------------------------------------------------ etapa 2: decoración

    public static void DecorateColumn(GenContext c, VoxelWorld w, int colX, int colZ)
    {
        int bx0 = colX * C, bz0 = colZ * C;
        Vector3Int dims = c.dims;

        // ---- vetas de mineral (rng determinista por columna) ----
        var rnd = new System.Random(HashInt(colX, colZ, c.seed ^ 0x00BE5EED));
        for (int zi = 0; zi < c.zones.Count; zi++)
        {
            ZoneData zd = c.zones[zi];
            foreach (var ore in zd.ores)
            {
                int attempts = Mathf.FloorToInt(ore.veins);
                if (rnd.NextDouble() < ore.veins - attempts) attempts++;
                for (int a = 0; a < attempts; a++)
                {
                    int x = bx0 + rnd.Next(C);
                    int z = bz0 + rnd.Next(C);
                    int y0 = rnd.Next(Mathf.Clamp(ore.minH, 1, dims.y - 2),
                                      Mathf.Clamp(ore.maxH, ore.minH, dims.y - 2) + 1);
                    if (c.zones.Count > 1)
                    {
                        ZoneAndHeight(c, x + 0.5f, z + 0.5f, out byte zAt, out _);
                        if (zAt != zi) continue; // la veta no pertenece a este bioma
                    }

                    // la veta crece con un paseo aleatorio, reemplazando la roca anfitriona
                    int size = rnd.Next(ore.minSize, ore.maxSize + 1);
                    int placed = 0, guard = size * 6;
                    var p = new Vector3Int(x, y0, z);
                    while (placed < size && guard-- > 0)
                    {
                        if (w.InBounds(p.x, p.y, p.z) &&
                            w.GetMicroArray(p.x, p.y, p.z) == null &&
                            w.GetBlockType(p.x, p.y, p.z) == ore.host)
                        {
                            w.SetBlockSilent(p.x, p.y, p.z, ore.ore);
                            placed++;
                        }
                        p += VeinSteps[rnd.Next(6)];
                    }
                }
            }
        }

        // ---- árboles y maleza ----
        for (int i = 0; i < C; i++)
            for (int j = 0; j < C; j++)
            {
                int x = bx0 + i, z = bz0 + j;
                if (x < 1 || z < 1 || x >= dims.x - 1 || z >= dims.z - 1) continue;

                ZoneAndHeight(c, x + 0.5f, z + 0.5f, out byte ziCol, out float h);
                ZoneData zd = c.zones[ziCol];
                bool beach = c.wl > 0 && h <= c.gs.waterLevelMeters + 0.6f;
                if (beach) continue; // la vegetación solo crece sobre el bloque de superficie
                // (surfaceType == zd.grass en columnas que no son orilla)

                // --- árbol ---
                if (zd.s.treeDensity > 0f && zd.trees.Count > 0 &&
                    Hash01(x, z, c.seed) < zd.s.treeDensity)
                {
                    PlaceTree(c, w, zd, x, z, h);
                    continue; // el árbol ocupa la celda
                }

                // --- maleza ---
                if (zd.s.grassDensity > 0f && zd.plantIds.Count > 0 &&
                    Hash01(x, z, c.seed + 7) < zd.s.grassDensity)
                {
                    PlacePlant(c, w, zd, x, z, h);
                }
            }
    }

    static void PlaceTree(GenContext c, VoxelWorld w, ZoneData zd, int x, int z, float h)
    {
        // elegir especie por peso
        float r = Hash01(x, z, c.seed + 3) * zd.treeTotal;
        var kind = zd.trees[0];
        for (int i = 0; i < zd.trees.Count; i++)
        {
            r -= zd.trees[i].weight;
            if (r <= 0f) { kind = zd.trees[i]; break; }
        }

        // suelo real: bajar hasta encontrar algo sólido (cuevas pueden vaciar)
        int baseY = Mathf.CeilToInt(h);
        while (baseY > 1 && w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null)
            baseY--;
        if (w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null) return;

        int trunk = kind.min + (int)(Hash01(x, z, c.seed + 1) * (kind.max - kind.min + 1));
        trunk = Mathf.Min(trunk, kind.max);
        if (baseY + trunk + 3 >= c.dims.y) return;

        // raíz hundida para quedar enraizado en la superficie suavizada
        if (baseY - 1 >= 1) w.SetBlockSilent(x, baseY - 1, z, kind.trunk);
        for (int y = baseY; y < baseY + trunk; y++)
            w.SetBlockSilent(x, y, z, kind.trunk);

        // copa de hojas (puede cruzar a columnas vecinas, ya generadas)
        int cy = baseY + trunk;
        for (int dy = -1; dy <= 2; dy++)
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (dx * dx + dy * dy + dz * dz > 5) continue;
                    int lx = x + dx, ly = cy + dy, lz = z + dz;
                    if (!w.InBounds(lx, ly, lz)) continue;
                    if (w.GetBlockType(lx, ly, lz) != 0) continue;
                    if (w.GetMicroArray(lx, ly, lz) != null) continue;
                    w.SetBlockSilent(lx, ly, lz, kind.leaves);
                }
    }

    static void PlacePlant(GenContext c, VoxelWorld w, ZoneData zd, int x, int z, float h)
    {
        int baseY = Mathf.CeilToInt(h);
        while (baseY > 1 && w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null)
            baseY--;
        if (w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null) return;
        if (baseY >= c.dims.y) return;
        if (w.GetBlockType(x, baseY, z) != 0 || w.GetMicroArray(x, baseY, z) != null) return;

        float r = Hash01(x, z, c.seed + 8) * zd.plantTotal;
        byte chosen = zd.plantIds[0];
        for (int i = 0; i < zd.plantIds.Count; i++)
        {
            r -= zd.plantWeights[i];
            if (r <= 0f) { chosen = zd.plantIds[i]; break; }
        }
        w.SetBlockSilent(x, baseY, z, chosen);
    }

    // ------------------------------------------------------------------ zonas y alturas

    static ZoneData BuildZoneData(VoxelWorld w, VoxelTypesDBSO.ZoneInfo info, Settings fallback)
    {
        var zd = new ZoneData
        {
            s = info?.generation ?? fallback ?? new Settings(),
            grass = Resolve(w, info?.surface, GRASS),
            dirt = Resolve(w, info?.subsoil, DIRT),
            stone = Resolve(w, info?.stone, STONE),
            ore = Resolve(w, info?.ore, ORE),
            sand = Resolve(w, info?.beach, SAND),
        };

        if (info != null)
        {
            foreach (var o in info.ores)
            {
                if (o == null || o.ore == null) continue;
                byte oreId = w.IdOf(o.ore);
                if (oreId == 0) continue;
                byte hostId = o.host != null ? w.IdOf(o.host) : zd.stone;
                if (hostId == 0) hostId = zd.stone;
                zd.ores.Add((oreId, hostId, o.veinsPerChunk,
                    Mathf.Min(o.minHeight, o.maxHeight), Mathf.Max(o.minHeight, o.maxHeight),
                    Mathf.Min(o.minVeinSize, o.maxVeinSize), Mathf.Max(o.minVeinSize, o.maxVeinSize)));
            }
            foreach (var sp in info.plants)
            {
                if (sp == null || sp.plant == null || !sp.plant.isPlant) continue;
                byte id = w.IdOf(sp.plant);
                if (id == 0) continue;
                zd.plantIds.Add(id);
                zd.plantWeights.Add(sp.weight);
                zd.plantTotal += sp.weight;
            }
            foreach (var t in info.trees)
            {
                if (t == null || t.trunk == null || t.leaves == null) continue;
                byte trunkId = w.IdOf(t.trunk);
                byte leavesId = w.IdOf(t.leaves);
                if (trunkId == 0 || leavesId == 0) continue;
                int min = Mathf.Min(t.minTrunk, t.maxTrunk);
                int max = Mathf.Max(t.minTrunk, t.maxTrunk);
                zd.trees.Add((trunkId, leavesId, min, max, t.weight));
                zd.treeTotal += t.weight;
            }
        }

        if (zd.plantIds.Count == 0) { zd.plantIds.Add(PLANT); zd.plantWeights.Add(1f); zd.plantTotal = 1f; }
        if (zd.trees.Count == 0 && zd.s.treeDensity > 0f) { zd.trees.Add((WOOD, LEAVES, 3, 5, 1f)); zd.treeTotal = 1f; }
        return zd;
    }

    static void ZoneAndHeight(GenContext c, float px, float pz, out byte zoneIndex, out float height)
    {
        if (c.zones.Count == 1)
        {
            zoneIndex = 0;
            height = HeightAt(px, pz, c.zones[0].s, c.oHill, c.dims.y);
            return;
        }
        ComputeZoneAndHeight(px, pz, c.cell, c.zones, c.seed, c.oHill, c.dims.y, out zoneIndex, out height);
    }

    // región Voronoi (rejilla con jitter): zona de la celda más cercana y altura
    // mezclada entre las celdas vecinas para suavizar las fronteras
    static void ComputeZoneAndHeight(float px, float pz, float cell, List<ZoneData> zones,
        int seed, float oHill, int maxY, out byte zoneIndex, out float height)
    {
        int cx = Mathf.FloorToInt(px / cell);
        int cz = Mathf.FloorToInt(pz / cell);

        float bestD = float.MaxValue;
        int best = 0;
        float hSum = 0f, wSum = 0f;

        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int ccx = cx + dx, ccz = cz + dz;
                float jx = Hash01(ccx, ccz, seed + 11);
                float jz = Hash01(ccx, ccz, seed + 12);
                float centerX = (ccx + 0.25f + 0.5f * jx) * cell;
                float centerZ = (ccz + 0.25f + 0.5f * jz) * cell;
                int zi = (int)(Hash01(ccx, ccz, seed + 13) * zones.Count) % zones.Count;

                float ddx = px - centerX, ddz = pz - centerZ;
                float d = Mathf.Sqrt(ddx * ddx + ddz * ddz);
                if (d < bestD) { bestD = d; best = zi; }

                float t = Mathf.Clamp01(1f - d / (cell * 1.1f));
                float wgt = t * t;
                if (wgt <= 0f) continue;
                hSum += wgt * HeightAt(px, pz, zones[zi].s, oHill, maxY);
                wSum += wgt;
            }

        zoneIndex = (byte)best;
        height = wSum > 0f ? hSum / wSum : HeightAt(px, pz, zones[best].s, oHill, maxY);
        height = Mathf.Clamp(height, 2f, maxY - 1.01f);
    }

    static float HeightAt(float x, float z, Settings s, float oHill, int maxY)
    {
        float n = Fbm(oHill + x * s.hillScale, oHill + z * s.hillScale, 3);
        float h = s.baseHeightMeters + (n - 0.5f) * 2f * s.hillAmplitudeMeters;
        return Mathf.Clamp(h, 2f, maxY - 1.01f);
    }

    // bilineal sobre la rejilla local 18x18 (centros de columna en i+0.5)
    static float SampleHeightLocal(float[,] hs, float lx, float lz)
    {
        float fx = Mathf.Clamp(lx - 0.5f, -1f, C - 0.001f);
        float fz = Mathf.Clamp(lz - 0.5f, -1f, C - 0.001f);
        int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
        float tx = fx - x0, tz = fz - z0;
        int gx = x0 + 1, gz = z0 + 1;
        float h0 = Mathf.Lerp(hs[gx, gz], hs[gx + 1, gz], tx);
        float h1 = Mathf.Lerp(hs[gx, gz + 1], hs[gx + 1, gz + 1], tx);
        return Mathf.Lerp(h0, h1, tz);
    }

    static byte Resolve(VoxelWorld w, VoxelTypeSO type, byte fallback)
    {
        if (type == null) return fallback;
        byte id = w.IdOf(type);
        return id != 0 ? id : fallback;
    }

    // ------------------------------------------------------------------ utilidades

    static readonly Vector3Int[] VeinSteps =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up,
        Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    static float Next(System.Random rnd) => (float)(rnd.NextDouble() * 1000.0);

    static float Fbm(float x, float y, int octaves)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }

    static float Noise3(float x, float y, float z) =>
        (Mathf.PerlinNoise(x, y) + Mathf.PerlinNoise(y, z) + Mathf.PerlinNoise(z, x)) / 3f;

    static int HashInt(int x, int z, int seed)
    {
        unchecked
        {
            int h = x * 73856093 ^ z * 19349663 ^ seed * 83492791;
            return (h ^ (h >> 13)) * 1274126177;
        }
    }

    static float Hash01(int x, int z, int seed)
    {
        unchecked
        {
            int h = HashInt(x, z, seed);
            return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f;
        }
    }
}
