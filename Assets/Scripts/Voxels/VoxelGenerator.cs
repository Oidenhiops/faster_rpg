using System;
using UnityEngine;

/// <summary>
/// Generación estilo Minecraft: colinas suaves por heightmap (fbm), superficie en
/// escalones de bloques de 1m (sin micro-relieve), capas pasto/tierra/piedra,
/// cuevas y vetas de mineral con ruido 3D, arena en las orillas y árboles.
/// La destrucción sigue siendo con precisión de micro-voxels (estilo DRG).
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
        float oHill = Next(rnd), oCave = Next(rnd), oOre = Next(rnd);

        Vector3Int dims = w.BlockDims;

        // ---- pasada 1: alturas ----
        var heights = new int[dims.x, dims.z];
        for (int x = 0; x < dims.x; x++)
            for (int z = 0; z < dims.z; z++)
            {
                float n = Fbm(oHill + x * s.hillScale, oHill + z * s.hillScale, 3);
                float h = s.baseHeightMeters + (n - 0.5f) * 2f * s.hillAmplitudeMeters;
                heights[x, z] = Mathf.Clamp(Mathf.RoundToInt(h), 2, dims.y - 2);
            }

        // ---- pasada 2: columnas ----
        var surface = new byte[dims.x, dims.z];
        for (int x = 0; x < dims.x; x++)
            for (int z = 0; z < dims.z; z++)
            {
                int hBlocks = heights[x, z];
                bool beach = s.waterLevelMeters > 0f && hBlocks <= s.waterLevelMeters + 0.6f;
                byte surfaceType = beach ? SAND : GRASS;
                byte subType = beach ? SAND : DIRT;
                surface[x, z] = surfaceType;

                for (int y = 0; y < hBlocks; y++)
                {
                    int depth = hBlocks - y;

                    if (depth >= s.minCaveDepthMeters && y >= 1 &&
                        Noise3(oCave + x * s.caveScale, oCave + y * s.caveScale, oCave + z * s.caveScale) > s.caveThreshold)
                        continue; // cueva

                    byte id;
                    if (depth == 1) id = surfaceType;             // bloque superior
                    else if (depth <= 1 + s.dirtDepthMeters) id = subType;
                    else id = STONE;

                    if (id == STONE &&
                        Noise3(oOre + x * s.oreScale, oOre + y * s.oreScale, oOre + z * s.oreScale) > s.oreThreshold)
                        id = ORE;

                    w.SetBlockSilent(x, y, z, id);
                }
            }

        // ---- pasada 3: árboles ----
        if (s.treeDensity > 0f) PlaceTrees(w, s, heights, surface, dims);
    }

    // ------------------------------------------------------------------ árboles

    static void PlaceTrees(VoxelWorld w, Settings s, int[,] heights, byte[,] surface, Vector3Int dims)
    {
        for (int x = 2; x < dims.x - 2; x++)
            for (int z = 2; z < dims.z - 2; z++)
            {
                if (surface[x, z] != GRASS) continue;
                if (Hash01(x, z, s.seed) >= s.treeDensity) continue;

                // suelo real: las cuevas pueden haber vaciado la columna
                int baseY = heights[x, z];
                while (baseY > 1 && w.GetBlockType(x, baseY - 1, z) == 0)
                    baseY--;
                if (w.GetBlockType(x, baseY - 1, z) == 0) continue; // sin soporte

                int trunk = s.minTrunk + (int)(Hash01(x, z, s.seed + 1) * (s.maxTrunk - s.minTrunk + 1));
                trunk = Mathf.Min(trunk, s.maxTrunk);
                if (baseY + trunk + 3 >= dims.y) continue;

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
