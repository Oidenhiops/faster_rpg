using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "VoxelTypesDB", menuName = "ScriptableObjects/DB/VoxelTypesDB", order = 1)]
public class VoxelTypesDBSO : ScriptableObject
{
    [Tooltip("Índice en la lista = id del voxel (0 = aire). El generador usa 1-10 en orden: pasto, tierra, piedra, mineral, arena, nieve, tronco, hojas, agua, maleza")]
    public List<VoxelTypeSO> types = new List<VoxelTypeSO>();

    [Tooltip("Qué se genera y qué puede aparecer en cada zona")]
    public SerializedDictionary<TypeZone, ZoneInfo> zones = new SerializedDictionary<TypeZone, ZoneInfo>();

    public enum TypeZone
    {
        None = 0,
        Pradera = 1,
        Bosque = 2,
        Desierto = 3,
        Nevado = 4,
        Pantano = 5,
    }

    [Serializable]
    public class ZoneInfo
    {
        [Tooltip("Parámetros de generación del terreno para esta zona")]
        public VoxelGenerator.Settings generation = new VoxelGenerator.Settings();

        [Header("Bloques de la zona (roles del terreno; vacío = tipo por defecto)")]
        [Tooltip("Bloque superior del terreno (pasto, arena roja, musgo...)")]
        public VoxelTypeSO surface;
        [Tooltip("Capa bajo la superficie (tierra...)")]
        public VoxelTypeSO subsoil;
        [Tooltip("Roca profunda")]
        public VoxelTypeSO stone;
        [Tooltip("Vetas de mineral en la roca")]
        public VoxelTypeSO ore;
        [Tooltip("Orillas y lecho del agua (arena, lodo...)")]
        public VoxelTypeSO beach;
        [Tooltip("El agua de la zona (azul, verde pantano, lava...)")]
        public VoxelTypeSO water;

        [Header("Vegetación (tipos con isPlant y su probabilidad relativa)")]
        public List<PlantSpawn> plants = new List<PlantSpawn>();

        [Header("Árboles de la zona")]
        public List<TreeSpawn> trees = new List<TreeSpawn>();

        [Header("Minerales de la zona (vetas)")]
        public List<OreSpawn> ores = new List<OreSpawn>();

        [Header("Futuro: estructuras y entidades de la zona")]
        public List<GameObject> buildings = new List<GameObject>();
        public List<GameObject> entities = new List<GameObject>();
    }

    [NaughtyAttributes.Button]
    public void RegisterZoneTypes()
    {
        // agrega a la paleta cualquier tipo referenciado por las zonas que aún no esté
        foreach (var zone in zones.Values)
        {
            if (zone == null) continue;
            Register(zone.surface); Register(zone.subsoil); Register(zone.stone);
            Register(zone.ore); Register(zone.beach); Register(zone.water);
            foreach (var p in zone.plants)
                if (p != null) Register(p.plant);
            foreach (var t in zone.trees)
            {
                if (t == null) continue;
                Register(t.trunk); Register(t.leaves);
            }
            foreach (var o in zone.ores)
            {
                if (o == null) continue;
                Register(o.ore); Register(o.host);
            }
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    void Register(VoxelTypeSO type)
    {
        if (type != null && !types.Contains(type)) types.Add(type);
    }

    [Serializable]
    public class PlantSpawn
    {
        public VoxelTypeSO plant;
        [Min(0.01f)] public float weight = 1f;
    }

    [Serializable]
    public class OreSpawn
    {
        public VoxelTypeSO ore;
        [Tooltip("Bloque que la veta reemplaza (vacío = la roca de la zona)")]
        public VoxelTypeSO host;
        [Tooltip("Vetas por área de 16x16 columnas (como 'veins per chunk' de Minecraft)")]
        [Min(0f)] public float veinsPerChunk = 4f;
        [Tooltip("Altura mínima en bloques (0 = fondo del mapa)")]
        public int minHeight = 2;
        [Tooltip("Altura máxima en bloques")]
        public int maxHeight = 48;
        [Tooltip("Bloques de mineral que crecen desde el bloque inicial de la veta")]
        [Min(1)] public int minVeinSize = 3;
        [Min(1)] public int maxVeinSize = 8;
    }

    [Serializable]
    public class TreeSpawn
    {
        public VoxelTypeSO trunk;
        public VoxelTypeSO leaves;
        [Min(0.01f)] public float weight = 1f;
        [Min(1)] public int minTrunk = 3;
        [Min(1)] public int maxTrunk = 5;
    }
}
