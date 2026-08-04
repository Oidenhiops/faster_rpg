using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mundo voxel acotado, estilo DRG + Minecraft:
/// - Construcción: bloques de 1m alineados a rejilla (PlaceBlock).
/// - Destrucción: esferas que muerden con precisión de 1/16 m (DigSphere).
/// Cada bloque de 1m está hecho de 16³ micro-voxels, pero solo los bloques
/// parcialmente excavados los materializan en memoria (lazy).
/// Requiere que este transform no tenga rotación ni escala.
/// </summary>
public class VoxelWorld : MonoBehaviour
{
    public static VoxelWorld Instance { get; private set; }

    public enum WorldSize
    {
        Small,      // 96 x 96 x 96
        Medium,     // 160 x 96 x 160
        Large,      // 256 x 96 x 256
        ExtraLarge, // 384 x 96 x 384
        Custom,     // usa World Size Meters
    }

    // altura fija del mundo: centrado queda de -48 a +48 (subsuelo ~46m, cielo ~50m)
    public const int WORLD_HEIGHT = 96;

    [Header("Dimensiones")]
    public WorldSize worldSize = WorldSize.Medium;
    [Tooltip("Solo se usa con Custom (metros = bloques de 1m)")]
    public Vector3Int worldSizeMeters = new Vector3Int(96, 40, 96);

    [Header("Semilla del mundo")]
    [Tooltip("Con esto activo, cada Play genera un mundo distinto (la semilla usada se registra en consola)")]
    public bool randomSeed = true;
    [Tooltip("Semilla fija (usada solo si Random Seed está apagado)")]
    public int worldSeed = 1337;

    [Header("Render")]
    [Tooltip("Opcional. Si es null se crea uno con URP/Lit o Standard. La textura principal se reemplaza por el atlas de los tipos.")]
    public Material voxelMaterial;

    [Header("Tipos y zonas")]
    [Tooltip("DB con los tipos de voxel y las zonas (generación + spawns)")]
    public VoxelTypesDBSO typesDB;
    [Tooltip("Zona principal: define nivel y tipo de agua globales; si Multi Biome está apagado, todo el mundo es esta zona")]
    public VoxelTypesDBSO.TypeZone zone = VoxelTypesDBSO.TypeZone.Pradera;
    [Tooltip("Repartir todas las zonas del DB por el mapa en regiones (Voronoi)")]
    public bool multiBiome = true;
    [Tooltip("Tamaño aproximado de cada región de bioma, en metros")]
    public float biomeCellSizeMeters = 48f;

    // paleta activa (viene del DB; si falta, se crean defaults en memoria)
    List<VoxelTypeSO> types = new List<VoxelTypeSO>();
    public VoxelTypesDBSO.ZoneInfo ZoneInfo { get; private set; }

    [Header("Agua")]
    [Tooltip("Índice del tipo agua en la lista")]
    public byte waterTypeId = 9;
    [Tooltip("Opcional. Si es null se crea uno transparente simple")]
    public Material waterMaterial;

    [Header("Plantas")]
    [Tooltip("Opcional. Si es null se crea uno cutout a partir del material del terreno")]
    public Material plantMaterial;

    [Header("Rendimiento")]
    [Tooltip("Chunks remesheados por frame tras una edición")]
    public int remeshBudgetPerFrame = 4;

    [Header("Flujo de agua")]
    public bool waterFlowEnabled = true;
    [Tooltip("Segundos entre ticks de flujo")]
    public float waterFlowInterval = 0.1f;
    [Tooltip("Celdas procesadas por tick")]
    public int waterFlowBudget = 64;

    [Header("Generación (respaldo si el DB no define la zona)")]
    public VoxelGenerator.Settings generation = new VoxelGenerator.Settings();

    /// <summary>Coordenada del bloque (1m) editado. Útil para pathfinding, recursos, sonido.</summary>
    public event Action<Vector3Int> OnBlockChanged;

    public Vector3Int BlockDims => worldSize switch
    {
        WorldSize.Small      => new Vector3Int(96, WORLD_HEIGHT, 96),
        WorldSize.Medium     => new Vector3Int(160, WORLD_HEIGHT, 160),
        WorldSize.Large      => new Vector3Int(256, WORLD_HEIGHT, 256),
        WorldSize.ExtraLarge => new Vector3Int(384, WORLD_HEIGHT, 384),
        _                    => worldSizeMeters,
    };
    /// <summary>Offset local para que el centro del mapa quede en la posición del transform.</summary>
    public Vector3 LocalOrigin => -(Vector3)BlockDims * 0.5f;
    /// <summary>Esquina mínima del mundo en coordenadas de mundo.</summary>
    public Vector3 Origin => transform.position + LocalOrigin;
    public bool Ready { get; private set; }

    Vector3Int chunkDims;
    VoxelChunk[] chunks;
    readonly Queue<VoxelChunk> dirtyQueue = new Queue<VoxelChunk>();
    readonly Dictionary<Vector3Int, float> blockDamage = new Dictionary<Vector3Int, float>();
    Material runtimeMaterial;
    Rect[] typeRects;  // región de cada tipo dentro del atlas (índice = id)
    bool[] plantFlags; // qué ids son plantas (índice = id)

    // flujo de agua (niveles 1-8; las celdas del lago original son fuentes = sin entrada en el dict)
    readonly Queue<Vector3Int> flowQueue = new Queue<Vector3Int>();
    readonly HashSet<Vector3Int> flowQueued = new HashSet<Vector3Int>();
    readonly Dictionary<Vector3Int, byte> waterLevels = new Dictionary<Vector3Int, byte>(); // solo celdas en flujo
    float nextFlowTime;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // cargar tipos y zona desde el DB (con respaldos si falta algo)
        if (typesDB != null && typesDB.types != null && typesDB.types.Count > 0)
            types = new List<VoxelTypeSO>(typesDB.types);
        if (typesDB != null && typesDB.zones != null &&
            typesDB.zones.TryGetValue(zone, out VoxelTypesDBSO.ZoneInfo zi) && zi != null)
        {
            ZoneInfo = zi;
            if (zi.generation != null) generation = zi.generation;
        }

        EnsureDefaultTypes();

        // el agua de la zona reemplaza el id de agua global (mesher y flujo lo usan)
        if (ZoneInfo != null && ZoneInfo.water != null)
        {
            int wid = types.IndexOf(ZoneInfo.water);
            if (wid > 0) waterTypeId = (byte)wid;
        }

        if (randomSeed)
        {
            worldSeed = UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2);
            Debug.Log($"VoxelWorld: semilla del mundo = {worldSeed}");
        }

        BuildMaterial();
        AllocateChunks();
        VoxelGenerator.Generate(this);
        for (int i = 0; i < chunks.Length; i++) RemeshImmediate(chunks[i]);
        Ready = true;
    }

    void Update()
    {
        int n = Mathf.Min(remeshBudgetPerFrame, dirtyQueue.Count);
        for (int i = 0; i < n; i++)
        {
            VoxelChunk c = dirtyQueue.Dequeue();
            if (c.remeshing) { dirtyQueue.Enqueue(c); continue; } // ocupado, reintentar
            c.dirty = false;
            _ = RemeshAsync(c);
        }

        // tick de flujo de agua
        if (waterFlowEnabled && Ready && flowQueue.Count > 0 && Time.time >= nextFlowTime)
        {
            nextFlowTime = Time.time + waterFlowInterval;
            int f = Mathf.Min(waterFlowBudget, flowQueue.Count);
            for (int i = 0; i < f; i++)
            {
                Vector3Int p = flowQueue.Dequeue();
                flowQueued.Remove(p);
                ProcessFlow(p);
            }
        }
    }

    // ------------------------------------------------------------------ setup

    /// <summary>Si no hay assets asignados, crea tipos por defecto en memoria para poder probar.</summary>
    void EnsureDefaultTypes()
    {
        if (types == null) types = new List<VoxelTypeSO>();
        if (types.Count == 0)
        {
            Debug.LogWarning("VoxelWorld: sin VoxelTypeSO asignados. Usando 9 tipos por defecto en memoria — crea los assets (Create > ScriptableObjects > Voxels > VoxelType) y asígnalos en orden.");
            (string n, Color c, float h)[] defaults =
            {
                ("Aire",    Color.clear,                        1f),
                ("Pasto",   new Color(0.35f, 0.62f, 0.28f),     1f),
                ("Tierra",  new Color(0.45f, 0.32f, 0.20f),     1f),
                ("Piedra",  new Color(0.50f, 0.50f, 0.52f),     3f),
                ("Mineral", new Color(0.90f, 0.75f, 0.20f),     5f),
                ("Arena",   new Color(0.83f, 0.76f, 0.50f),     1f),
                ("Nieve",   new Color(0.93f, 0.95f, 1.00f),     1f),
                ("Tronco",  new Color(0.40f, 0.28f, 0.16f),     2f),
                ("Hojas",   new Color(0.25f, 0.50f, 0.20f),     0.5f),
                ("Agua",    new Color(0.20f, 0.50f, 0.80f, 0.6f), 999f),
                ("Maleza",  new Color(0.30f, 0.58f, 0.24f),       0.5f),
            };
            foreach (var d in defaults)
            {
                var so = ScriptableObject.CreateInstance<VoxelTypeSO>();
                so.name = d.n; so.displayName = d.n; so.color = d.c; so.hardness = d.h;
                so.indestructible = d.n == "Agua";
                so.isPlant = d.n == "Maleza";
                types.Add(so);
            }
        }
        for (int i = 0; i < types.Count; i++)
        {
            if (types[i] != null) continue;
            var so = ScriptableObject.CreateInstance<VoxelTypeSO>();
            so.name = $"Tipo {i}"; so.color = Color.magenta;
            types[i] = so;
            Debug.LogWarning($"VoxelWorld: types[{i}] estaba vacío; usando magenta como aviso.");
        }
    }

    void BuildMaterial()
    {
        // atlas: una textura por tipo; si el tipo no tiene, se genera una de color plano
        var sources = new Texture2D[types.Count];
        for (int i = 0; i < types.Count; i++)
            sources[i] = types[i].texture != null ? types[i].texture : SolidTexture(types[i].color);

        var atlas = new Texture2D(64, 64, TextureFormat.RGBA32, false)
        {
            name = "VoxelAtlas",
            filterMode = FilterMode.Point
        };
        typeRects = atlas.PackTextures(sources, 2, 4096);
        if (typeRects == null)
        {
            // alguna textura sin Read/Write: caer a colores planos para no romper
            Debug.LogError("VoxelWorld: falló el empaque del atlas (¿texturas sin Read/Write habilitado?). Usando colores planos.");
            for (int i = 0; i < sources.Length; i++) sources[i] = SolidTexture(types[i].color);
            typeRects = atlas.PackTextures(sources, 2, 4096);
        }

        if (voxelMaterial != null)
        {
            runtimeMaterial = voxelMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            runtimeMaterial = new Material(shader) { name = "VoxelMaterial" };
        }
        runtimeMaterial.mainTexture = atlas;

        // material del agua: transparente simple si no se asignó uno
        if (waterMaterial == null)
        {
            waterMaterial = new Material(Shader.Find("Sprites/Default")) { name = "WaterMaterial" };
            waterMaterial.color = waterTypeId < types.Count ? types[waterTypeId].color
                                                            : new Color(0.2f, 0.5f, 0.8f, 0.6f);
        }

        // material de plantas: cutout con el mismo atlas, doble cara
        if (plantMaterial == null)
        {
            plantMaterial = new Material(runtimeMaterial) { name = "PlantMaterial" };
            plantMaterial.SetFloat("_AlphaClip", 1f);
            plantMaterial.SetFloat("_Cutoff", 0.5f);
            plantMaterial.EnableKeyword("_ALPHATEST_ON");
            plantMaterial.SetFloat("_Cull", 0f); // doble cara
        }
        plantMaterial.mainTexture = runtimeMaterial.mainTexture;

        // flags de planta por id
        plantFlags = new bool[types.Count];
        for (int i = 0; i < types.Count; i++) plantFlags[i] = types[i] != null && types[i].isPlant;
    }

    public bool IsPlantId(byte id) => plantFlags != null && id < plantFlags.Length && plantFlags[id];

    /// <summary>Id (índice en la paleta) de un VoxelTypeSO; 0 si no está.</summary>
    public byte IdOf(VoxelTypeSO type)
    {
        int i = types.IndexOf(type);
        return (byte)Mathf.Max(i, 0);
    }

    static Texture2D SolidTexture(Color c)
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    void AllocateChunks()
    {
        chunkDims = new Vector3Int(
            CeilDiv(BlockDims.x, VoxelChunk.SIZE),
            CeilDiv(BlockDims.y, VoxelChunk.SIZE),
            CeilDiv(BlockDims.z, VoxelChunk.SIZE));
        chunks = new VoxelChunk[chunkDims.x * chunkDims.y * chunkDims.z];

        for (int cy = 0; cy < chunkDims.y; cy++)
            for (int cz = 0; cz < chunkDims.z; cz++)
                for (int cx = 0; cx < chunkDims.x; cx++)
                {
                    var c = new VoxelChunk { coord = new Vector3Int(cx, cy, cz) };
                    c.go = new GameObject($"Chunk {cx},{cy},{cz}") { layer = LayerMask.NameToLayer("Map") };
                    c.go.transform.SetParent(transform, false);
                    c.go.transform.localPosition = LocalOrigin + (Vector3)(c.coord * VoxelChunk.SIZE);
                    c.filter = c.go.AddComponent<MeshFilter>();
                    var renderer = c.go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = runtimeMaterial;
                    c.collider = c.go.AddComponent<MeshCollider>();
                    c.mesh = new Mesh
                    {
                        name = c.go.name,
                        indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                    };

                    // malla de agua: hijo sin collider, material transparente
                    c.waterGo = new GameObject("Water");
                    c.waterGo.transform.SetParent(c.go.transform, false);
                    c.waterFilter = c.waterGo.AddComponent<MeshFilter>();
                    c.waterGo.AddComponent<MeshRenderer>().sharedMaterial = waterMaterial;
                    c.waterMesh = new Mesh { name = c.go.name + " Water" };

                    // malla de plantas: hijo sin collider, material cutout
                    c.plantGo = new GameObject("Plants");
                    c.plantGo.transform.SetParent(c.go.transform, false);
                    c.plantFilter = c.plantGo.AddComponent<MeshFilter>();
                    c.plantGo.AddComponent<MeshRenderer>().sharedMaterial = plantMaterial;
                    c.plantMesh = new Mesh { name = c.go.name + " Plants" };

                    chunks[ChunkIndex(cx, cy, cz)] = c;
                }
    }

    static int CeilDiv(int a, int b) => (a + b - 1) / b;
    int ChunkIndex(int cx, int cy, int cz) => cx + chunkDims.x * (cz + chunkDims.z * cy);
    VoxelChunk ChunkAt(int bx, int by, int bz) => chunks[ChunkIndex(bx >> 4, by >> 4, bz >> 4)];

    // ------------------------------------------------------------------ acceso a bloques

    public bool InBounds(int bx, int by, int bz) =>
        bx >= 0 && by >= 0 && bz >= 0 && bx < BlockDims.x && by < BlockDims.y && bz < BlockDims.z;

    public byte GetBlockType(int bx, int by, int bz)
    {
        if (!InBounds(bx, by, bz)) return 0;
        return ChunkAt(bx, by, bz).blockTypes[VoxelChunk.BlockIndex(bx & 15, by & 15, bz & 15)];
    }

    /// <summary>Micro-voxels del bloque, o null si el bloque es uniforme.</summary>
    public byte[] GetMicroArray(int bx, int by, int bz)
    {
        if (!InBounds(bx, by, bz)) return null;
        ChunkAt(bx, by, bz).microBlocks.TryGetValue(
            VoxelChunk.BlockIndex(bx & 15, by & 15, bz & 15), out byte[] micro);
        return micro;
    }

    /// <summary>Convierte el bloque uniforme en parcial (asigna sus 16³ voxels).</summary>
    public byte[] AllocateMicro(int bx, int by, int bz, byte fillType)
    {
        VoxelChunk c = ChunkAt(bx, by, bz);
        int idx = VoxelChunk.BlockIndex(bx & 15, by & 15, bz & 15);
        if (c.microBlocks.TryGetValue(idx, out byte[] existing)) return existing;
        var micro = new byte[VoxelChunk.MICRO3];
        if (fillType != 0)
            for (int i = 0; i < micro.Length; i++) micro[i] = fillType;
        c.microBlocks[idx] = micro;
        return micro;
    }

    /// <summary>Deja el bloque uniforme con el tipo dado (borra sus micro-voxels).</summary>
    public void SetBlockUniform(int bx, int by, int bz, byte typeId)
    {
        if (!InBounds(bx, by, bz)) return;
        VoxelChunk c = ChunkAt(bx, by, bz);
        int idx = VoxelChunk.BlockIndex(bx & 15, by & 15, bz & 15);
        c.blockTypes[idx] = typeId;
        c.microBlocks.Remove(idx);
        var pos = new Vector3Int(bx, by, bz);
        blockDamage.Remove(pos); // el daño acumulado no sobrevive al bloque
        if (typeId != waterTypeId) waterLevels.Remove(pos); // el nivel de flujo tampoco
        NotifyBlockEdited(bx, by, bz);

        // las plantas no flotan: si el soporte desaparece, la maleza de arriba se rompe
        if (typeId == 0 && InBounds(bx, by + 1, bz) &&
            GetMicroArray(bx, by + 1, bz) == null && IsPlantId(GetBlockType(bx, by + 1, bz)))
        {
            SetBlockUniform(bx, by + 1, bz, 0);
        }
    }

    /// <summary>Escritura sin eventos ni remesh. Solo para la generación inicial.</summary>
    public void SetBlockSilent(int bx, int by, int bz, byte typeId)
    {
        if (!InBounds(bx, by, bz)) return;
        VoxelChunk c = ChunkAt(bx, by, bz);
        int idx = VoxelChunk.BlockIndex(bx & 15, by & 15, bz & 15);
        c.blockTypes[idx] = typeId;
        c.microBlocks.Remove(idx); // un bloque uniforme no debe conservar micro-voxels
    }

    /// <summary>Como AllocateMicro pero sin eventos ni remesh. Para detalle en la generación.</summary>
    public byte[] AllocateMicroSilent(int bx, int by, int bz, byte fillType)
    {
        if (!InBounds(bx, by, bz)) return null;
        VoxelChunk c = ChunkAt(bx, by, bz);
        int idx = VoxelChunk.BlockIndex(bx & 15, by & 15, bz & 15);
        if (c.microBlocks.TryGetValue(idx, out byte[] existing)) return existing;
        var micro = new byte[VoxelChunk.MICRO3];
        if (fillType != 0)
            for (int i = 0; i < micro.Length; i++) micro[i] = fillType;
        c.microBlocks[idx] = micro;
        return micro;
    }

    public void NotifyBlockEdited(int bx, int by, int bz)
    {
        VoxelChunk c = ChunkAt(bx, by, bz);
        MarkDirty(c);
        // un bloque en el borde del chunk cambia las caras visibles del chunk vecino
        int lx = bx & 15, ly = by & 15, lz = bz & 15;
        if (lx == 0) MarkDirtyAt(c.coord + Vector3Int.left);
        if (lx == VoxelChunk.SIZE - 1) MarkDirtyAt(c.coord + Vector3Int.right);
        if (ly == 0) MarkDirtyAt(c.coord + Vector3Int.down);
        if (ly == VoxelChunk.SIZE - 1) MarkDirtyAt(c.coord + Vector3Int.up);
        if (lz == 0) MarkDirtyAt(c.coord + new Vector3Int(0, 0, -1));
        if (lz == VoxelChunk.SIZE - 1) MarkDirtyAt(c.coord + new Vector3Int(0, 0, 1));
        OnBlockChanged?.Invoke(new Vector3Int(bx, by, bz));

        // cualquier edición despierta la simulación de agua en la vecindad
        if (waterFlowEnabled && Ready) EnqueueFlowAround(new Vector3Int(bx, by, bz));
    }

    // ------------------------------------------------------------------ flujo de agua

    void EnqueueFlowAround(Vector3Int p)
    {
        EnqueueFlow(p);
        EnqueueFlow(p + Vector3Int.up);
        EnqueueFlow(p + Vector3Int.down);
        EnqueueFlow(p + Vector3Int.left);
        EnqueueFlow(p + Vector3Int.right);
        EnqueueFlow(p + new Vector3Int(0, 0, 1));
        EnqueueFlow(p + new Vector3Int(0, 0, -1));
    }

    void EnqueueFlow(Vector3Int p)
    {
        if (!InBounds(p.x, p.y, p.z)) return;
        if (flowQueued.Add(p)) flowQueue.Enqueue(p);
    }

    public bool IsWaterCell(int bx, int by, int bz)
    {
        byte[] micro = GetMicroArray(bx, by, bz);
        if (micro == null) return GetBlockType(bx, by, bz) == waterTypeId;
        foreach (byte id in micro)
            if (id == waterTypeId) return true;
        return false;
    }

    /// <summary>0 = sin agua. Bloques parciales con agua y fuentes = 8; celdas en flujo, su nivel.</summary>
    int EffectiveWaterLevel(Vector3Int p)
    {
        if (!IsWaterCell(p.x, p.y, p.z)) return 0;
        if (GetMicroArray(p.x, p.y, p.z) != null) return 8; // agua micro (orillas): estática, cuenta llena
        return waterLevels.TryGetValue(p, out byte l) ? l : 8; // sin entrada = fuente
    }

    static readonly Vector3Int[] FlowSides =
    {
        Vector3Int.left, Vector3Int.right, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    void ProcessFlow(Vector3Int p)
    {
        int lvl = EffectiveWaterLevel(p);
        if (lvl == 0) return;

        byte[] microP = GetMicroArray(p.x, p.y, p.z);
        bool waterAbove = EffectiveWaterLevel(p + Vector3Int.up) > 0;

        // ---- celdas en flujo: recomputar nivel según su alimentación ----
        if (microP == null && waterLevels.ContainsKey(p))
        {
            int support;
            if (waterAbove) support = 8; // alimentada desde arriba (columna de caída)
            else
            {
                int maxSide = 0;
                foreach (Vector3Int d in FlowSides)
                {
                    int nl = EffectiveWaterLevel(p + d);
                    if (nl > maxSide) maxSide = nl;
                }
                support = maxSide - 1;
            }

            // consolidarse como fuente: 2+ fuentes vecinas y piso firme (estilo Minecraft)
            int sourceNeighbors = 0;
            foreach (Vector3Int d in FlowSides)
            {
                Vector3Int n = p + d;
                if (GetMicroArray(n.x, n.y, n.z) == null &&
                    GetBlockType(n.x, n.y, n.z) == waterTypeId &&
                    !waterLevels.ContainsKey(n)) sourceNeighbors++;
            }
            Vector3Int below = p + Vector3Int.down;
            bool firmFloor = !InBounds(below.x, below.y, below.z) ||
                             GetBlockType(below.x, below.y, below.z) != 0 ||
                             GetMicroArray(below.x, below.y, below.z) != null;

            if (sourceNeighbors >= 2 && firmFloor)
            {
                waterLevels.Remove(p); // ahora es fuente
                lvl = 8;
                NotifyBlockEdited(p.x, p.y, p.z);
            }
            else if (support <= 0)
            {
                waterLevels.Remove(p);
                SetBlockUniform(p.x, p.y, p.z, 0); // se seca (notifica y despierta vecinos)
                return;
            }
            else if (support != lvl)
            {
                waterLevels[p] = (byte)support;
                lvl = support;
                NotifyBlockEdited(p.x, p.y, p.z);
            }
        }

        // ---- bloque parcial con agua: rellenar sus propios huecos ----
        if (microP != null) TryFlow(p, waterAbove ? 8 : 7, waterAbove);

        // ---- caer; si está bloqueado abajo, expandirse a los lados ----
        if (!TryFlow(p + Vector3Int.down, 8, true) && lvl > 1)
        {
            foreach (Vector3Int d in FlowSides)
                TryFlow(p + d, lvl - 1, false);
        }
    }

    // intenta meter agua en la celda; devuelve true si fluyó algo
    bool TryFlow(Vector3Int t, int newLvl, bool falling)
    {
        if (!InBounds(t.x, t.y, t.z) || t.y < 1) return false;

        byte bt = GetBlockType(t.x, t.y, t.z);
        byte[] micro = GetMicroArray(t.x, t.y, t.z);

        if (micro == null)
        {
            if (bt == waterTypeId)
            {
                // ya hay agua: si es de menor nivel, su recomputo la subirá (vecinos en cola)
                return false;
            }
            if (bt != 0) return false; // sólido

            SetBlockUniform(t.x, t.y, t.z, waterTypeId); // notifica → remesh + vecinos a la cola
            waterLevels[t] = (byte)Mathf.Clamp(newLvl, 1, 8); // toda agua nueva nace en flujo
            return true;
        }

        // bloque parcial: se llena hasta la superficie estándar (7/8), porque para la
        // simulación y el render un parcial con agua cuenta como celda llena; llenarlo
        // a medias según el nivel entrante producía alturas inconsistentes en la orilla
        int maxMy = falling ? VoxelChunk.MICRO : VoxelChunk.MICRO - 1;
        bool changed = false;
        for (int my = 0; my < maxMy; my++)
            for (int mz = 0; mz < VoxelChunk.MICRO; mz++)
                for (int mx = 0; mx < VoxelChunk.MICRO; mx++)
                {
                    int idx = VoxelChunk.MicroIndex(mx, my, mz);
                    if (micro[idx] != 0) continue;
                    micro[idx] = waterTypeId;
                    changed = true;
                }
        if (changed) NotifyBlockEdited(t.x, t.y, t.z); // remesh + re-despertar vecinos
        return changed;
    }

    void MarkDirty(VoxelChunk c)
    {
        if (c.dirty) return;
        c.dirty = true;
        dirtyQueue.Enqueue(c);
    }

    void MarkDirtyAt(Vector3Int chunkCoord)
    {
        if (chunkCoord.x < 0 || chunkCoord.y < 0 || chunkCoord.z < 0 ||
            chunkCoord.x >= chunkDims.x || chunkCoord.y >= chunkDims.y || chunkCoord.z >= chunkDims.z) return;
        MarkDirty(chunks[ChunkIndex(chunkCoord.x, chunkCoord.y, chunkCoord.z)]);
    }

    // ------------------------------------------------------------------ excavar / construir

    /// <summary>
    /// Excava una esfera estilo DRG con precisión de 1/16 m. Respeta hardness e
    /// indestructible, y deja una cáscara de 1 bloque en suelo y paredes (techo abierto).
    /// Devuelve micro-voxels quitados por tipo (4096 = un bloque entero) para dar recursos.
    /// </summary>
    public Dictionary<byte, int> DigSphere(Vector3 center, float radiusMeters, float digPower = 1f)
    {
        var removed = new Dictionary<byte, int>();
        Vector3 rel = center - Origin;
        int minBx = Mathf.FloorToInt(rel.x - radiusMeters);
        int minBy = Mathf.FloorToInt(rel.y - radiusMeters);
        int minBz = Mathf.FloorToInt(rel.z - radiusMeters);
        int maxBx = Mathf.FloorToInt(rel.x + radiusMeters);
        int maxBy = Mathf.FloorToInt(rel.y + radiusMeters);
        int maxBz = Mathf.FloorToInt(rel.z + radiusMeters);
        float r2 = radiusMeters * radiusMeters;
        const int M = VoxelChunk.MICRO;
        const float MV = 1f / M;

        for (int by = minBy; by <= maxBy; by++)
            for (int bz = minBz; bz <= maxBz; bz++)
                for (int bx = minBx; bx <= maxBx; bx++)
                {
                    // cáscara indestructible: 1 bloque en suelo y paredes, techo abierto
                    if (by < 1 || by >= BlockDims.y) continue;
                    if (bx < 1 || bx >= BlockDims.x - 1) continue;
                    if (bz < 1 || bz >= BlockDims.z - 1) continue;

                    byte t = GetBlockType(bx, by, bz);
                    byte[] micro = GetMicroArray(bx, by, bz);
                    if (t == 0 && micro == null) continue;

                    Vector3 bMin = new Vector3(bx, by, bz);
                    if (AabbDist2(rel, bMin, bMin + Vector3.one) > r2) continue; // fuera de la esfera

                    if (micro == null)
                    {
                        if (IsPlantId(t))
                        {
                            // la maleza se rompe entera con solo rozarla
                            SetBlockUniform(bx, by, bz, 0);
                            AddCount(removed, t, 1);
                            continue;
                        }
                        VoxelTypeSO vt = types[t];
                        if (vt.indestructible || vt.hardness > digPower) continue;
                        if (FarthestCorner2(rel, bMin) <= r2)
                        {
                            // bloque completamente dentro: quitarlo entero sin materializar
                            SetBlockUniform(bx, by, bz, 0);
                            AddCount(removed, t, VoxelChunk.MICRO3);
                            continue;
                        }
                        micro = AllocateMicro(bx, by, bz, t);
                    }

                    int remaining = 0, cut = 0;
                    for (int my = 0; my < M; my++)
                        for (int mz = 0; mz < M; mz++)
                            for (int mx = 0; mx < M; mx++)
                            {
                                int mi = VoxelChunk.MicroIndex(mx, my, mz);
                                byte id = micro[mi];
                                if (id == 0) continue;
                                VoxelTypeSO vt = types[id];
                                Vector3 p = bMin + new Vector3((mx + 0.5f) * MV, (my + 0.5f) * MV, (mz + 0.5f) * MV);
                                if (!vt.indestructible && vt.hardness <= digPower &&
                                    (p - rel).sqrMagnitude <= r2)
                                {
                                    micro[mi] = 0;
                                    cut++;
                                    AddCount(removed, id, 1);
                                }
                                else remaining++;
                            }

                    if (cut == 0) continue;
                    if (remaining == 0) SetBlockUniform(bx, by, bz, 0); // colapsa a aire
                    else NotifyBlockEdited(bx, by, bz);
                }
        return removed;
    }

    /// <summary>
    /// Convierte una posición de mundo en coordenadas de bloque.
    /// Con un RaycastHit usa: WorldToBlock(hit.point - hit.normal * 0.01f).
    /// </summary>
    public Vector3Int WorldToBlock(Vector3 worldPos)
    {
        Vector3 rel = worldPos - Origin;
        return new Vector3Int(Mathf.FloorToInt(rel.x), Mathf.FloorToInt(rel.y), Mathf.FloorToInt(rel.z));
    }

    /// <summary>
    /// Golpea un bloque entero (modo por defecto: el bloque de 1m se rompe completo,
    /// con todos sus micro-voxels juntos). El daño se acumula entre golpes hasta
    /// superar la hardness del tipo. Devuelve true si el bloque se rompió; en ese
    /// caso removed trae los micro-voxels obtenidos por tipo (para recursos).
    /// </summary>
    public bool DamageBlock(Vector3Int blockPos, float damage, out Dictionary<byte, int> removed)
    {
        removed = null;
        int bx = blockPos.x, by = blockPos.y, bz = blockPos.z;
        if (!InBounds(bx, by, bz)) return false;

        // cáscara indestructible: suelo y paredes, techo abierto
        if (by < 1 || bx < 1 || bz < 1 || bx >= BlockDims.x - 1 || bz >= BlockDims.z - 1) return false;

        byte t = GetBlockType(bx, by, bz);
        byte[] micro = GetMicroArray(bx, by, bz);
        if (t == 0 && micro == null) return false; // aire

        // el tipo del bloque se conserva en blockTypes aunque esté parcial
        VoxelTypeSO vt = types[t != 0 ? t : (byte)1];
        if (vt.indestructible) return false;

        blockDamage.TryGetValue(blockPos, out float total);
        total += damage;
        if (total < vt.hardness)
        {
            blockDamage[blockPos] = total;
            return false; // dañado pero entero (aquí puedes disparar VFX de grietas)
        }

        blockDamage.Remove(blockPos);
        removed = new Dictionary<byte, int>();
        bool hasWater = false;
        if (micro == null)
        {
            removed[t] = VoxelChunk.MICRO3;
        }
        else
        {
            foreach (byte id in micro)
            {
                if (id == 0) continue;
                if (id == waterTypeId) { hasWater = true; continue; } // el agua no es recurso
                removed.TryGetValue(id, out int count);
                removed[id] = count + 1;
            }
        }
        // si el bloque contenía agua, el hueco queda inundado en vez de seco
        SetBlockUniform(bx, by, bz, hasWater ? waterTypeId : (byte)0);
        return true;
    }

    /// <summary>
    /// Coloca un bloque de 1m (estilo Minecraft) en la celda que contiene worldPos.
    /// Solo en celdas completamente vacías. Devuelve true si colocó.
    /// </summary>
    public bool PlaceBlock(Vector3 worldPos, byte typeId)
    {
        if (typeId == 0 || typeId >= types.Count) return false;
        Vector3 rel = worldPos - Origin;
        int bx = Mathf.FloorToInt(rel.x);
        int by = Mathf.FloorToInt(rel.y);
        int bz = Mathf.FloorToInt(rel.z);
        if (!InBounds(bx, by, bz)) return false;
        byte current = GetBlockType(bx, by, bz);
        // solo celdas vacías, con agua o con maleza (construir las desplaza)
        if ((current != 0 && current != waterTypeId && !IsPlantId(current)) ||
            GetMicroArray(bx, by, bz) != null) return false;
        SetBlockUniform(bx, by, bz, typeId);
        return true;
    }

    static void AddCount(Dictionary<byte, int> dict, byte key, int amount)
    {
        dict.TryGetValue(key, out int count);
        dict[key] = count + amount;
    }

    // distancia² de un punto al AABB
    static float AabbDist2(Vector3 p, Vector3 min, Vector3 max)
    {
        float dx = Mathf.Max(min.x - p.x, 0f, p.x - max.x);
        float dy = Mathf.Max(min.y - p.y, 0f, p.y - max.y);
        float dz = Mathf.Max(min.z - p.z, 0f, p.z - max.z);
        return dx * dx + dy * dy + dz * dz;
    }

    // distancia² del punto a la esquina más lejana del bloque de 1m en bMin
    static float FarthestCorner2(Vector3 p, Vector3 bMin)
    {
        float dx = Mathf.Max(Mathf.Abs(p.x - bMin.x), Mathf.Abs(p.x - (bMin.x + 1f)));
        float dy = Mathf.Max(Mathf.Abs(p.y - bMin.y), Mathf.Abs(p.y - (bMin.y + 1f)));
        float dz = Mathf.Max(Mathf.Abs(p.z - bMin.z), Mathf.Abs(p.z - (bMin.z + 1f)));
        return dx * dx + dy * dy + dz * dz;
    }

    // ------------------------------------------------------------------ remesh

    void RemeshImmediate(VoxelChunk c)
    {
        Apply(c, VoxelMesher.Build(CopySnapshot(c), typeRects, waterTypeId, plantFlags));
    }

    async Awaitable RemeshAsync(VoxelChunk c)
    {
        c.remeshing = true;
        try
        {
            VoxelMesher.Snapshot snapshot = CopySnapshot(c); // en main thread
            await Awaitable.BackgroundThreadAsync();
            VoxelMesher.BuildResult result = VoxelMesher.Build(snapshot, typeRects, waterTypeId, plantFlags);
            await Awaitable.MainThreadAsync();
            if (this == null || c.go == null) return;
            Apply(c, result);
        }
        finally
        {
            c.remeshing = false;
        }
    }

    VoxelMesher.Snapshot CopySnapshot(VoxelChunk c)
    {
        var s = new VoxelMesher.Snapshot();
        Vector3Int b0 = c.coord * VoxelChunk.SIZE;
        int i = 0;
        for (int y = -1; y <= VoxelChunk.SIZE; y++)
            for (int z = -1; z <= VoxelChunk.SIZE; z++)
                for (int x = -1; x <= VoxelChunk.SIZE; x++)
                {
                    int bx = b0.x + x, by = b0.y + y, bz = b0.z + z;
                    s.types[i] = GetBlockType(bx, by, bz);
                    byte[] micro = GetMicroArray(bx, by, bz);
                    s.micro[i] = micro != null ? (byte[])micro.Clone() : null;
                    s.waterLvl[i] = 8;
                    if (micro == null && s.types[i] == waterTypeId &&
                        waterLevels.TryGetValue(new Vector3Int(bx, by, bz), out byte lvl))
                        s.waterLvl[i] = lvl;
                    i++;
                }
        return s;
    }

    void Apply(VoxelChunk c, VoxelMesher.BuildResult result)
    {
        // terreno sólido (con collider)
        VoxelMesher.MeshData md = result.solid;
        Mesh mesh = c.mesh;
        mesh.Clear();
        if (md.vertices.Count > 0)
        {
            mesh.SetVertices(md.vertices);
            mesh.SetNormals(md.normals);
            mesh.SetUVs(0, md.uvs);
            mesh.SetTriangles(md.triangles, 0);
            mesh.RecalculateBounds();
            c.filter.sharedMesh = mesh;
            c.collider.sharedMesh = null; // forzar re-cook
            c.collider.sharedMesh = mesh;
        }
        else
        {
            c.filter.sharedMesh = mesh;
            c.collider.sharedMesh = null;
        }

        // agua (sin collider)
        VoxelMesher.MeshData wd = result.water;
        Mesh waterMesh = c.waterMesh;
        waterMesh.Clear();
        if (wd.vertices.Count > 0)
        {
            waterMesh.SetVertices(wd.vertices);
            waterMesh.SetNormals(wd.normals);
            waterMesh.SetUVs(0, wd.uvs);
            waterMesh.SetTriangles(wd.triangles, 0);
            waterMesh.RecalculateBounds();
        }
        c.waterFilter.sharedMesh = waterMesh;

        // plantas (sin collider)
        VoxelMesher.MeshData pd = result.plants;
        Mesh plantMesh = c.plantMesh;
        plantMesh.Clear();
        if (pd.vertices.Count > 0)
        {
            plantMesh.SetVertices(pd.vertices);
            plantMesh.SetNormals(pd.normals);
            plantMesh.SetUVs(0, pd.uvs);
            plantMesh.SetTriangles(pd.triangles, 0);
            plantMesh.RecalculateBounds();
        }
        c.plantFilter.sharedMesh = plantMesh;
    }
}
