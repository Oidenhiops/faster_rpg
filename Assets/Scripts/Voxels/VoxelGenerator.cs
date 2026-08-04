using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generación con biomas: las zonas del DB se reparten el mapa en regiones
/// Voronoi (rejilla con jitter). Cada columna usa los bloques de rol, la
/// vegetación, los árboles y los parámetros de su zona; las alturas se mezclan
/// suavemente entre zonas vecinas para evitar acantilados en las fronteras.
/// El nivel y el tipo de agua son globales (zona principal), como un "sea level".
/// Superficie suavizada con los 8³ micro-voxels del bloque superior.
/// Ids clásicos de respaldo: 1=pasto, 2=tierra, 3=piedra, 4=mineral, 5=arena,
/// 7=tronco, 8=hojas, 9=agua, 10=maleza.
/// </summary>
public static class VoxelGenerator
{
    const byte GRASS = 1, DIRT = 2, STONE = 3, ORE = 4, SAND = 5, WOOD = 7, LEAVES = 8, WATER = 9, PLANT = 10;

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

        [Header("Minerales")]
        public float oreScale = 0.2f;
        [Range(0.6f, 0.9f)] public float oreThreshold = 0.74f;

        [Header("Árboles (los tipos y tamaños vienen de la zona en el DB)")]
        [Range(0f, 0.1f)] public float treeDensity = 0.02f;

        [Header("Maleza")]
        [Range(0f, 0.5f)] public float grassDensity = 0.14f;
    }

    // datos precalculados de una zona: parámetros + ids de rol + tablas de spawn
    class ZoneData
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

    public static void Generate(VoxelWorld w)
    {
        Settings gs = w.generation;   // global: nivel de agua del mapa
        int seed = w.worldSeed;       // la semilla es del mundo, no de la zona
        byte waterId = w.waterTypeId; // ya resuelto por VoxelWorld
        var rnd = new System.Random(seed);
        float oHill = Next(rnd), oCave = Next(rnd), oOre = Next(rnd), oDetail = Next(rnd);

        // ---- zonas disponibles ----
        var zoneList = new List<ZoneData>();
        if (w.multiBiome && w.typesDB != null && w.typesDB.zones != null)
            foreach (var kv in w.typesDB.zones)
                if (kv.Value != null) zoneList.Add(BuildZoneData(w, kv.Value, gs));
        if (zoneList.Count == 0) zoneList.Add(BuildZoneData(w, w.ZoneInfo, gs));

        Vector3Int dims = w.BlockDims;
        const int M = VoxelChunk.MICRO;
        const float MV = 1f / M;
        float cell = Mathf.Max(8f, w.biomeCellSizeMeters);

        // ---- pasada 1: zona y altura (mezclada) por columna ----
        var zoneIdx = new byte[dims.x, dims.z];
        var heights = new float[dims.x, dims.z];
        for (int x = 0; x < dims.x; x++)
            for (int z = 0; z < dims.z; z++)
            {
                if (zoneList.Count == 1)
                {
                    zoneIdx[x, z] = 0;
                    heights[x, z] = HeightAt(x + 0.5f, z + 0.5f, zoneList[0].s, oHill, dims.y);
                }
                else
                {
                    ComputeZoneAndHeight(x + 0.5f, z + 0.5f, cell, zoneList, seed, oHill, dims.y,
                        out byte zi, out float h);
                    zoneIdx[x, z] = zi;
                    heights[x, z] = h;
                }
            }

        // ---- pasada 2: columnas ----
        int wl = gs.waterLevelMeters > 0f ? Mathf.Min(Mathf.FloorToInt(gs.waterLevelMeters), dims.y - 1) : 0;
        var surface = new byte[dims.x, dims.z];
        var microHeights = new float[M * M]; // buffer reutilizado por columna
        for (int x = 0; x < dims.x; x++)
            for (int z = 0; z < dims.z; z++)
            {
                ZoneData zd = zoneList[zoneIdx[x, z]];
                Settings zs = zd.s;

                float hCenter = heights[x, z];
                bool beach = wl > 0 && hCenter <= gs.waterLevelMeters + 0.6f;
                byte surfaceType = beach ? zd.sand : zd.grass;
                byte subType = beach ? zd.sand : zd.dirt;
                surface[x, z] = surfaceType;

                int hRef = Mathf.RoundToInt(hCenter); // referencia para capas/cuevas

                float minH = hCenter, maxH = hCenter;
                if (zs.smoothSurface)
                {
                    // alturas a resolución micro: interpolación del heightmap + ruido fino
                    for (int mz = 0; mz < M; mz++)
                        for (int mx = 0; mx < M; mx++)
                        {
                            float px = x + (mx + 0.5f) * MV;
                            float pz = z + (mz + 0.5f) * MV;
                            float h = SampleHeight(heights, dims, px, pz);
                            if (zs.microDetailAmplitude > 0f)
                                h += (Mathf.PerlinNoise(oDetail + px * zs.microDetailScale,
                                                        oDetail + pz * zs.microDetailScale) - 0.5f)
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
                        Noise3(oCave + x * zs.caveScale, oCave + y * zs.caveScale, oCave + z * zs.caveScale) > zs.caveThreshold)
                        continue; // cueva

                    byte id;
                    if (!zs.smoothSurface && depth == 1) id = surfaceType;
                    else if (depth <= 1 + zs.dirtDepthMeters) id = subType;
                    else id = zd.stone;

                    // minerales por ruido: solo como respaldo si la zona no define vetas
                    if (id == zd.stone && zd.ores.Count == 0 &&
                        Noise3(oOre + x * zs.oreScale, oOre + y * zs.oreScale, oOre + z * zs.oreScale) > zs.oreThreshold)
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
                                float gm = microHeights[mx + M * mz] * M; // altura en unidades micro
                                int gy0 = y * M;
                                int count = Mathf.Clamp(Mathf.RoundToInt(gm) - gy0, 0, M);
                                for (int my = 0; my < count; my++)
                                {
                                    // los 2 micro-voxels superiores de la columna son superficie
                                    byte id = (gm - (gy0 + my)) <= 2.5f ? surfaceType : subType;
                                    micro[VoxelChunk.MicroIndex(mx, my, mz)] = id;
                                }
                                filled += count;
                            }

                        // optimización: bloque totalmente lleno o vacío vuelve a ser uniforme
                        if (filled == VoxelChunk.MICRO3) w.SetBlockSilent(x, y, z, subType);
                        else if (filled == 0) w.SetBlockSilent(x, y, z, 0);
                    }
                }

                // ---- agua: desde el nivel global hacia abajo hasta tocar terreno ----
                // (solo lagos/ríos abiertos; se detiene en la superficie, no inunda cuevas)
                if (wl > 0)
                {
                    int surfaceMicro = wl * M - 1; // la capa micro superior queda vacía (superficie hundida)
                    for (int y = wl - 1; y >= 1; y--)
                    {
                        byte t = w.GetBlockType(x, y, z);
                        byte[] micro = w.GetMicroArray(x, y, z);

                        if (t == 0 && micro == null)
                        {
                            w.SetBlockSilent(x, y, z, waterId);
                            continue;
                        }

                        if (micro == null) break; // sólido uniforme: terreno alcanzado

                        // bloque parcial: el agua rellena sus huecos
                        int gy0 = y * M;
                        for (int my = 0; my < M && gy0 + my < surfaceMicro; my++)
                            for (int mz = 0; mz < M; mz++)
                                for (int mx = 0; mx < M; mx++)
                                {
                                    int idx = VoxelChunk.MicroIndex(mx, my, mz);
                                    if (micro[idx] == 0) micro[idx] = waterId;
                                }

                        // si la capa inferior quedó con agua, hay paso hacia abajo:
                        // seguir llenando (laderas con dos parciales apilados);
                        // si es todo terreno, el bloque sella la columna
                        bool sealedBelow = true;
                        for (int mz = 0; mz < M && sealedBelow; mz++)
                            for (int mx = 0; mx < M; mx++)
                                if (micro[VoxelChunk.MicroIndex(mx, 0, mz)] == waterId)
                                {
                                    sealedBelow = false;
                                    break;
                                }
                        if (sealedBelow) break;
                    }
                }
            }

        // ---- pasada 2.5: vetas de mineral ----
        PlaceOres(w, seed, dims, zoneList, zoneIdx);

        // ---- pasada 3: árboles ----
        PlaceTrees(w, seed, heights, surface, dims, zoneList, zoneIdx);

        // ---- pasada 4: maleza ----
        PlacePlants(w, seed, heights, surface, dims, zoneList, zoneIdx);
    }

    // ------------------------------------------------------------------ zonas

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

        // respaldos clásicos si la zona no define nada
        if (zd.plantIds.Count == 0) { zd.plantIds.Add(PLANT); zd.plantWeights.Add(1f); zd.plantTotal = 1f; }
        if (zd.trees.Count == 0 && zd.s.treeDensity > 0f) { zd.trees.Add((WOOD, LEAVES, 3, 5, 1f)); zd.treeTotal = 1f; }
        return zd;
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

                // peso suave para mezclar alturas entre biomas vecinos
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

    // interpolación bilineal del heightmap por columnas (para el micro-detalle)
    static float SampleHeight(float[,] heights, Vector3Int dims, float px, float pz)
    {
        float fx = Mathf.Clamp(px - 0.5f, 0f, dims.x - 1.001f);
        float fz = Mathf.Clamp(pz - 0.5f, 0f, dims.z - 1.001f);
        int x0 = (int)fx, z0 = (int)fz;
        int x1 = Mathf.Min(x0 + 1, dims.x - 1), z1 = Mathf.Min(z0 + 1, dims.z - 1);
        float tx = fx - x0, tz = fz - z0;
        float h0 = Mathf.Lerp(heights[x0, z0], heights[x1, z0], tx);
        float h1 = Mathf.Lerp(heights[x0, z1], heights[x1, z1], tx);
        return Mathf.Lerp(h0, h1, tz);
    }

    // id del rol definido por la zona, o el tipo clásico si no está asignado
    static byte Resolve(VoxelWorld w, VoxelTypeSO type, byte fallback)
    {
        if (type == null) return fallback;
        byte id = w.IdOf(type);
        return id != 0 ? id : fallback;
    }

    // ------------------------------------------------------------------ minerales

    static readonly Vector3Int[] VeinSteps =
    {
        Vector3Int.right, Vector3Int.left, Vector3Int.up,
        Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    static void PlaceOres(VoxelWorld w, int seed, Vector3Int dims, List<ZoneData> zones, byte[,] zoneIdx)
    {
        var rnd = new System.Random(seed ^ 0x00BE5EED);
        int chunkCols = Mathf.Max(1, (dims.x * dims.z) / 256); // áreas de 16x16 columnas

        for (int zi = 0; zi < zones.Count; zi++)
        {
            ZoneData zd = zones[zi];
            foreach (var ore in zd.ores)
            {
                // intentos sobre todo el mapa; los que caen fuera del bioma se descartan,
                // así la densidad queda proporcional al área que ocupa la zona
                int attempts = Mathf.RoundToInt(ore.veins * chunkCols);
                for (int a = 0; a < attempts; a++)
                {
                    int x = rnd.Next(1, dims.x - 1);
                    int z = rnd.Next(1, dims.z - 1);
                    if (zones.Count > 1 && zoneIdx[x, z] != zi) continue;

                    int minY = Mathf.Clamp(ore.minH, 1, dims.y - 2);
                    int maxY = Mathf.Clamp(ore.maxH, minY, dims.y - 2);
                    int y = rnd.Next(minY, maxY + 1);

                    // la veta crece con un paseo aleatorio desde el bloque inicial,
                    // reemplazando solo la roca anfitriona
                    int size = rnd.Next(ore.minSize, ore.maxSize + 1);
                    int placed = 0, guard = size * 6;
                    var p = new Vector3Int(x, y, z);
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
    }

    // ------------------------------------------------------------------ árboles

    static void PlaceTrees(VoxelWorld w, int seed, float[,] heights, byte[,] surface,
        Vector3Int dims, List<ZoneData> zones, byte[,] zoneIdx)
    {
        for (int x = 2; x < dims.x - 2; x++)
            for (int z = 2; z < dims.z - 2; z++)
            {
                ZoneData zd = zones[zoneIdx[x, z]];
                if (zd.s.treeDensity <= 0f || zd.trees.Count == 0) continue;
                if (surface[x, z] != zd.grass) continue;
                if (Hash01(x, z, seed) >= zd.s.treeDensity) continue;

                // elegir especie por peso
                float r = Hash01(x, z, seed + 3) * zd.treeTotal;
                var kind = zd.trees[0];
                for (int i = 0; i < zd.trees.Count; i++)
                {
                    r -= zd.trees[i].weight;
                    if (r <= 0f) { kind = zd.trees[i]; break; }
                }

                // suelo real: bajar hasta encontrar algo sólido (cuevas pueden vaciar)
                int baseY = Mathf.CeilToInt(heights[x, z]);
                while (baseY > 1 && w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null)
                    baseY--;
                if (w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null) continue;

                int trunk = kind.min + (int)(Hash01(x, z, seed + 1) * (kind.max - kind.min + 1));
                trunk = Mathf.Min(trunk, kind.max);
                if (baseY + trunk + 3 >= dims.y) continue;

                // raíz: un bloque hundido que reemplaza al parcial de la superficie
                if (baseY - 1 >= 1) w.SetBlockSilent(x, baseY - 1, z, kind.trunk);

                for (int y = baseY; y < baseY + trunk; y++)
                    w.SetBlockSilent(x, y, z, kind.trunk);

                // copa de hojas
                int cy = baseY + trunk;
                for (int dy = -1; dy <= 2; dy++)
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            if (dx * dx + dy * dy + dz * dz > 5) continue;
                            int lx = x + dx, ly = cy + dy, lz = z + dz;
                            if (!w.InBounds(lx, ly, lz)) continue;
                            if (w.GetBlockType(lx, ly, lz) != 0) continue; // solo aire
                            if (w.GetMicroArray(lx, ly, lz) != null) continue;
                            w.SetBlockSilent(lx, ly, lz, kind.leaves);
                        }
            }
    }

    // ------------------------------------------------------------------ maleza

    static void PlacePlants(VoxelWorld w, int seed, float[,] heights, byte[,] surface,
        Vector3Int dims, List<ZoneData> zones, byte[,] zoneIdx)
    {
        for (int x = 1; x < dims.x - 1; x++)
            for (int z = 1; z < dims.z - 1; z++)
            {
                ZoneData zd = zones[zoneIdx[x, z]];
                if (zd.s.grassDensity <= 0f || zd.plantIds.Count == 0) continue;
                if (surface[x, z] != zd.grass) continue;
                if (Hash01(x, z, seed + 7) >= zd.s.grassDensity) continue;

                // suelo real, igual que los árboles
                int baseY = Mathf.CeilToInt(heights[x, z]);
                while (baseY > 1 && w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null)
                    baseY--;
                if (w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null) continue;
                if (baseY >= dims.y) continue;

                // solo en celdas totalmente vacías (los árboles tienen prioridad)
                if (w.GetBlockType(x, baseY, z) != 0 || w.GetMicroArray(x, baseY, z) != null) continue;

                // elegir planta por peso
                float r = Hash01(x, z, seed + 8) * zd.plantTotal;
                byte chosen = zd.plantIds[0];
                for (int i = 0; i < zd.plantIds.Count; i++)
                {
                    r -= zd.plantWeights[i];
                    if (r <= 0f) { chosen = zd.plantIds[i]; break; }
                }
                w.SetBlockSilent(x, baseY, z, chosen);
            }
    }

    // ------------------------------------------------------------------ utilidades

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

    // Unity no trae Perlin 3D; combinación de 3 muestras 2D, suficiente para cuevas/vetas
    static float Noise3(float x, float y, float z) =>
        (Mathf.PerlinNoise(x, y) + Mathf.PerlinNoise(y, z) + Mathf.PerlinNoise(z, x)) / 3f;

    static float Hash01(int x, int z, int seed)
    {
        unchecked
        {
            int h = x * 73856093 ^ z * 19349663 ^ seed * 83492791;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f;
        }
    }
}
