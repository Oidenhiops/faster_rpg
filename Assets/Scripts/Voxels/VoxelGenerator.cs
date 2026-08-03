using System;
using UnityEngine;

/// <summary>
/// Generación estilo Minecraft con superficie suavizada:
/// - Alturas por fbm (colinas), capas pasto/tierra/piedra, cuevas y minerales 3D.
/// - Suavizado: la franja superior de cada columna se construye con los 8³
///   micro-voxels internos, muestreando la altura continua del Perlin a
///   resolución de 1/8 m. Los escalones de 1m se vuelven pendientes suaves.
/// Tipos: 1=pasto, 2=tierra, 3=piedra, 4=mineral, 5=arena, 7=tronco, 8=hojas.
/// </summary>
public static class VoxelGenerator
{
    const byte GRASS = 1, DIRT = 2, STONE = 3, ORE = 4, SAND = 5, WOOD = 7, LEAVES = 8;

    [Serializable]
    public class Settings
    {
        public int seed = 1337;

        [Header("Terreno")]
        public float baseHeightMeters = 14f;
        public float hillAmplitudeMeters = 6f;
        [Tooltip("Frecuencia de las colinas (por metro)")]
        public float hillScale = 0.02f;

        [Header("Suavizado (usa los 8³ internos del bloque superior)")]
        public bool smoothSurface = true;
        [Tooltip("Rugosidad fina adicional, en metros")]
        [Range(0f, 0.5f)] public float microDetailAmplitude = 0.1f;
        public float microDetailScale = 0.5f;

        [Header("Agua")]
        [Tooltip("0 = sin agua. Altura del plano de agua; las orillas son de arena")]
        public float waterLevelMeters = 11f;

        [Header("Capas")]
        public int dirtDepthMeters = 3;

        [Header("Cuevas")]
        public float caveScale = 0.09f;
        [Range(0.5f, 0.8f)] public float caveThreshold = 0.62f;
        public float minCaveDepthMeters = 4f;

        [Header("Minerales")]
        public float oreScale = 0.2f;
        [Range(0.6f, 0.9f)] public float oreThreshold = 0.74f;

        [Header("Árboles")]
        [Range(0f, 0.1f)] public float treeDensity = 0.02f;
        public int minTrunk = 3;
        public int maxTrunk = 5;
    }

    public static void Generate(VoxelWorld w)
    {
        Settings s = w.generation;
        var rnd = new System.Random(s.seed);
        float oHill = Next(rnd), oCave = Next(rnd), oOre = Next(rnd), oDetail = Next(rnd);

        Vector3Int dims = w.BlockDims;
        const int M = VoxelChunk.MICRO;
        const float MV = 1f / M;

        // ---- pasada 1: altura continua en el centro de cada columna ----
        var heights = new float[dims.x, dims.z];
        for (int x = 0; x < dims.x; x++)
            for (int z = 0; z < dims.z; z++)
                heights[x, z] = HeightAt(x + 0.5f, z + 0.5f, s, oHill, dims.y);

        // ---- pasada 2: columnas ----
        var surface = new byte[dims.x, dims.z];
        var microHeights = new float[M * M]; // buffer reutilizado por columna
        for (int x = 0; x < dims.x; x++)
            for (int z = 0; z < dims.z; z++)
            {
                float hCenter = heights[x, z];
                bool beach = s.waterLevelMeters > 0f && hCenter <= s.waterLevelMeters + 0.6f;
                byte surfaceType = beach ? SAND : GRASS;
                byte subType = beach ? SAND : DIRT;
                surface[x, z] = surfaceType;

                int hRef = Mathf.RoundToInt(hCenter); // referencia para capas/cuevas

                float minH = hCenter, maxH = hCenter;
                if (s.smoothSurface)
                {
                    // alturas continuas a resolución micro dentro del bloque
                    for (int mz = 0; mz < M; mz++)
                        for (int mx = 0; mx < M; mx++)
                        {
                            float px = x + (mx + 0.5f) * MV;
                            float pz = z + (mz + 0.5f) * MV;
                            float h = HeightAt(px, pz, s, oHill, dims.y);
                            if (s.microDetailAmplitude > 0f)
                                h += (Mathf.PerlinNoise(oDetail + px * s.microDetailScale,
                                                        oDetail + pz * s.microDetailScale) - 0.5f)
                                     * 2f * s.microDetailAmplitude;
                            h = Mathf.Clamp(h, 2f, dims.y - 1.01f);
                            microHeights[mx + M * mz] = h;
                            if (h < minH) minH = h;
                            if (h > maxH) maxH = h;
                        }
                }

                int fullTop = s.smoothSurface ? Mathf.FloorToInt(minH) : hRef;

                // ---- bloques uniformes debajo de la franja suavizada ----
                for (int y = 0; y < fullTop; y++)
                {
                    int depth = hRef - y;

                    if (depth >= s.minCaveDepthMeters && y >= 1 &&
                        Noise3(oCave + x * s.caveScale, oCave + y * s.caveScale, oCave + z * s.caveScale) > s.caveThreshold)
                        continue; // cueva

                    byte id;
                    if (!s.smoothSurface && depth == 1) id = surfaceType;
                    else if (depth <= 1 + s.dirtDepthMeters) id = subType;
                    else id = STONE;

                    if (id == STONE &&
                        Noise3(oOre + x * s.oreScale, oOre + y * s.oreScale, oOre + z * s.oreScale) > s.oreThreshold)
                        id = ORE;

                    w.SetBlockSilent(x, y, z, id);
                }

                if (!s.smoothSurface) continue;

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

        // ---- pasada 3: árboles ----
        if (s.treeDensity > 0f) PlaceTrees(w, s, heights, surface, dims);
    }

    static float HeightAt(float x, float z, Settings s, float oHill, int maxY)
    {
        float n = Fbm(oHill + x * s.hillScale, oHill + z * s.hillScale, 3);
        float h = s.baseHeightMeters + (n - 0.5f) * 2f * s.hillAmplitudeMeters;
        return Mathf.Clamp(h, 2f, maxY - 1.01f);
    }

    // ------------------------------------------------------------------ árboles

    static void PlaceTrees(VoxelWorld w, Settings s, float[,] heights, byte[,] surface, Vector3Int dims)
    {
        for (int x = 2; x < dims.x - 2; x++)
            for (int z = 2; z < dims.z - 2; z++)
            {
                if (surface[x, z] != GRASS) continue;
                if (Hash01(x, z, s.seed) >= s.treeDensity) continue;

                // suelo real: bajar hasta encontrar algo sólido (cuevas pueden vaciar)
                int baseY = Mathf.CeilToInt(heights[x, z]);
                while (baseY > 1 && w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null)
                    baseY--;
                if (w.GetBlockType(x, baseY - 1, z) == 0 && w.GetMicroArray(x, baseY - 1, z) == null) continue;

                int trunk = s.minTrunk + (int)(Hash01(x, z, s.seed + 1) * (s.maxTrunk - s.minTrunk + 1));
                trunk = Mathf.Min(trunk, s.maxTrunk);
                if (baseY + trunk + 3 >= dims.y) continue;

                // raíz: un bloque hundido que reemplaza al bloque parcial de la
                // superficie suavizada, para que el árbol quede enraizado al suelo
                if (baseY - 1 >= 1) w.SetBlockSilent(x, baseY - 1, z, WOOD);

                for (int y = baseY; y < baseY + trunk; y++)
                    w.SetBlockSilent(x, y, z, WOOD);

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
                            w.SetBlockSilent(lx, ly, lz, LEAVES);
                        }
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
