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
        Infinite,   // ±131 km: en la práctica, sin borde
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

    [Header("Streaming de chunks")]
    [Tooltip("Objetivo alrededor del cual se carga el mundo (el player). Vacío = cámara principal")]
    public Transform streamTarget;
    [Tooltip("Radio de carga en columnas de chunk (16 m cada una)")]
    public int viewDistanceColumns = 8;
    [Tooltip("Columnas generadas por frame")]
    public int columnsPerFrame = 2;
    [Tooltip("Radio cargado de golpe al arrancar (para que haya piso bajo el player)")]
    public int warmupRadius = 3;

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
        WorldSize.Infinite   => new Vector3Int(1 << 18, WORLD_HEIGHT, 1 << 18),
        _                    => worldSizeMeters,
    };
    /// <summary>Offset local para que el centro del mapa quede en la posición del transform.</summary>
    public Vector3 LocalOrigin => -(Vector3)BlockDims * 0.5f;
    /// <summary>Esquina mínima del mundo en coordenadas de mundo.</summary>
    public Vector3 Origin => transform.position + LocalOrigin;
    public bool Ready { get; private set; }

    readonly Dictionary<Vector3Int, VoxelChunk> chunks = new Dictionary<Vector3Int, VoxelChunk>();
    readonly Queue<VoxelChunk> dirtyQueue = new Queue<VoxelChunk>();

    // streaming
    VoxelGenerator.GenContext genContext;
    readonly Dictionary<Vector2Int, byte> columnState = new Dictionary<Vector2Int, byte>(); // 1=generada 2=decorada
    readonly HashSet<Vector2Int> loadedColumns = new HashSet<Vector2Int>();
    static readonly List<Vector2Int> tmpUnload = new List<Vector2Int>();
    List<Vector2Int> ringOffsets;
    int ringPointer;
    Vector2Int lastTargetCol = new Vector2Int(int.MinValue, int.MinValue);
    int unloadTimer;
    int rescanTimer;
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

        // streaming: el mundo se carga por columnas alrededor del objetivo
        genContext = VoxelGenerator.Prepare(this);
        BuildRingOffsets();
        if (streamTarget == null && Camera.main != null) streamTarget = Camera.main.transform;
        WarmupStreaming();
        Ready = true;
    }

    void Update()
    {
        if (Ready && genContext != null) UpdateStreaming();

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

    VoxelChunk GetOrCreateChunk(Vector3Int cc)
    {
        if (!chunks.TryGetValue(cc, out VoxelChunk c))
        {
            c = new VoxelChunk { coord = cc };
            chunks[cc] = c;
        }
        if (c.go == null) BuildChunkObjects(c);
        return c;
    }

    void BuildChunkObjects(VoxelChunk c)
    {
        c.go = new GameObject($"Chunk {c.coord.x},{c.coord.y},{c.coord.z}") { layer = LayerMask.NameToLayer("Map") };
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
    }

    VoxelChunk ChunkAt(int bx, int by, int bz)
    {
        chunks.TryGetValue(new Vector3Int(bx >> 4, by >> 4, bz >> 4), out VoxelChunk c);
        return c;
    }

    // ------------------------------------------------------------------ acceso a bloques

    public bool InBounds(int bx, int by, int bz) =>
        bx >= 0 && by >= 0 && bz >= 0 && bx < BlockDims.x && by < BlockDims.y && bz < BlockDims.z;

    public byte GetBlockType(int bx, int by, int bz)
    {
        if (!InBounds(bx, by, bz)) return 0;
        VoxelChunk c = ChunkAt(bx, by, bz);
        if (c == null) return 0; // chunk no cargado = aire
        return c.blockTypes[VoxelChunk.BlockIndex(bx & 15, by & 15, bz & 15)];
    }

    /// <summary>Micro-voxels del bloque, o null si el bloque es uniforme.</summary>
    public byte[] GetMicroArray(int bx, int by, int bz)
    {
        if (!InBounds(bx, by, bz)) return null;
        VoxelChunk c = ChunkAt(bx, by, bz);
        if (c == null) return null;
        c.microBlocks.TryGetValue(VoxelChunk.BlockIndex(bx & 15, by & 15, bz & 15), out byte[] micro);
        return micro;
    }

    /// <summary>Convierte el bloque uniforme en parcial (asigna sus 16³ voxels).</summary>
    public byte[] AllocateMicro(int bx, int by, int bz, byte fillType)
    {
        VoxelChunk c = ChunkAt(bx, by, bz);
        if (c == null) return null;
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
        if (c == null) return; // fuera del área cargada
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

    /// <summary>Escritura sin eventos ni remesh. Solo para la generación.
    /// No toca chunks descargados ni chunks con ediciones del jugador.</summary>
    public void SetBlockSilent(int bx, int by, int bz, byte typeId)
    {
        if (!InBounds(bx, by, bz)) return;
        VoxelChunk c = ChunkAt(bx, by, bz);
        if (c == null || c.edited) return;
        int idx = VoxelChunk.BlockIndex(bx & 15, by & 15, bz & 15);
        c.blockTypes[idx] = typeId;
        c.microBlocks.Remove(idx); // un bloque uniforme no debe conservar micro-voxels
    }

    /// <summary>Como AllocateMicro pero sin eventos ni remesh. Para detalle en la generación.</summary>
    public byte[] AllocateMicroSilent(int bx, int by, int bz, byte fillType)
    {
        if (!InBounds(bx, by, bz)) return null;
        VoxelChunk c = ChunkAt(bx, by, bz);
        if (c == null || c.edited) return null;
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
        if (c == null) return;
        c.edited = true; // sus datos se conservarán al descargar la columna
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
        if (c == null || c.dirty || c.go == null) return; // descargado: se mesheará al recargar
        c.dirty = true;
        dirtyQueue.Enqueue(c);
    }

    void MarkDirtyAt(Vector3Int chunkCoord)
    {
        chunks.TryGetValue(chunkCoord, out VoxelChunk c);
        MarkDirty(c);
    }

    // ------------------------------------------------------------------ streaming

    int ColumnsX => BlockDims.x >> 4;
    int ColumnsZ => BlockDims.z >> 4;
    int ChunksY => BlockDims.y >> 4;

    Vector2Int TargetColumn()
    {
        Vector3 pos = streamTarget != null ? streamTarget.position : transform.position;
        Vector3 rel = pos - Origin;
        int bx = Mathf.Clamp(Mathf.FloorToInt(rel.x), 0, BlockDims.x - 1);
        int bz = Mathf.Clamp(Mathf.FloorToInt(rel.z), 0, BlockDims.z - 1);
        return new Vector2Int(bx >> 4, bz >> 4);
    }

    void BuildRingOffsets()
    {
        ringOffsets = new List<Vector2Int>();
        int r = Mathf.Max(2, viewDistanceColumns);
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
                ringOffsets.Add(new Vector2Int(dx, dz));
        ringOffsets.Sort((a, b) => (a.x * a.x + a.y * a.y).CompareTo(b.x * b.x + b.y * b.y));
    }

    bool ColumnInWorld(Vector2Int c2) =>
        c2.x >= 0 && c2.y >= 0 && c2.x < ColumnsX && c2.y < ColumnsZ;

    void UpdateStreaming()
    {
        Vector2Int col = TargetColumn();
        if (col != lastTargetCol) { lastTargetCol = col; ringPointer = 0; }

        int budget = columnsPerFrame;
        while (budget > 0 && ringPointer < ringOffsets.Count)
        {
            Vector2Int c2 = col + ringOffsets[ringPointer];
            if (!ColumnInWorld(c2)) { ringPointer++; continue; }
            if (StreamColumn(c2)) budget--; // hizo trabajo este frame
            else ringPointer++;             // esa columna ya está lista
        }

        // re-escanear de vez en cuando: algunas columnas quedan pendientes de
        // decorar hasta que sus vecinas terminan de generarse
        if (ringPointer >= ringOffsets.Count && ++rescanTimer >= 30)
        {
            rescanTimer = 0;
            ringPointer = 0;
        }

        // descarga periódica de columnas lejanas
        if (++unloadTimer >= 60)
        {
            unloadTimer = 0;
            int limit = viewDistanceColumns + 2;
            tmpUnload.Clear();
            foreach (Vector2Int lc in loadedColumns)
                if (Mathf.Max(Mathf.Abs(lc.x - col.x), Mathf.Abs(lc.y - col.y)) > limit)
                    tmpUnload.Add(lc);
            foreach (Vector2Int lc in tmpUnload) UnloadColumn(lc);
        }
    }

    // devuelve true si generó o decoró algo (consumió presupuesto)
    bool StreamColumn(Vector2Int c2)
    {
        columnState.TryGetValue(c2, out byte st);
        if (!loadedColumns.Contains(c2))
        {
            LoadColumn(c2, st);
            return true;
        }
        if (st < 2 && NeighborsGenerated(c2))
        {
            DecorateColumn(c2);
            return true;
        }
        return false;
    }

    void LoadColumn(Vector2Int c2, byte state)
    {
        for (int cy = 0; cy < ChunksY; cy++)
            GetOrCreateChunk(new Vector3Int(c2.x, cy, c2.y));

        // generar terreno (idempotente: los chunks editados conservados se saltan)
        VoxelGenerator.GenerateColumn(genContext, this, c2.x, c2.y);
        if (state == 0) columnState[c2] = 1;

        // recarga de una columna ya decorada: reaplicar la decoración propia y la
        // de las vecinas decoradas (sus árboles cruzan el borde hacia esta columna)
        if (state >= 2) RedecorateAround(c2);

        loadedColumns.Add(c2);
        MarkColumnDirty(c2, alsoNeighbors: true);
    }

    void RedecorateAround(Vector2Int c2)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var n = new Vector2Int(c2.x + dx, c2.y + dz);
                if (columnState.TryGetValue(n, out byte st) && st >= 2)
                    VoxelGenerator.DecorateColumn(genContext, this, n.x, n.y);
            }
    }

    bool NeighborsGenerated(Vector2Int c2)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var n = new Vector2Int(c2.x + dx, c2.y + dz);
                if (!ColumnInWorld(n)) continue; // el borde del mundo cuenta como listo
                if (!loadedColumns.Contains(n)) return false;
            }
        return true;
    }

    void DecorateColumn(Vector2Int c2)
    {
        VoxelGenerator.DecorateColumn(genContext, this, c2.x, c2.y);
        columnState[c2] = 2;
        MarkColumnDirty(c2, alsoNeighbors: true);
    }

    void MarkColumnDirty(Vector2Int c2, bool alsoNeighbors)
    {
        for (int cy = 0; cy < ChunksY; cy++)
        {
            MarkDirtyAt(new Vector3Int(c2.x, cy, c2.y));
            if (!alsoNeighbors) continue;
            MarkDirtyAt(new Vector3Int(c2.x + 1, cy, c2.y));
            MarkDirtyAt(new Vector3Int(c2.x - 1, cy, c2.y));
            MarkDirtyAt(new Vector3Int(c2.x, cy, c2.y + 1));
            MarkDirtyAt(new Vector3Int(c2.x, cy, c2.y - 1));
        }
    }

    void UnloadColumn(Vector2Int c2)
    {
        for (int cy = 0; cy < ChunksY; cy++)
        {
            var cc = new Vector3Int(c2.x, cy, c2.y);
            if (!chunks.TryGetValue(cc, out VoxelChunk c)) continue;

            if (c.go != null)
            {
                Destroy(c.go); // destruye también los hijos (agua, plantas)
                Destroy(c.mesh);
                Destroy(c.waterMesh);
                Destroy(c.plantMesh);
                c.go = null; c.mesh = null; c.filter = null; c.collider = null;
                c.waterGo = null; c.waterMesh = null; c.waterFilter = null;
                c.plantGo = null; c.plantMesh = null; c.plantFilter = null;
                c.dirty = false;
            }

            // sin ediciones del jugador: los datos se regeneran al volver
            if (!c.edited) chunks.Remove(cc);
        }
        loadedColumns.Remove(c2);
    }

    void WarmupStreaming()
    {
        Vector2Int col = TargetColumn();
        int r = Mathf.Clamp(warmupRadius, 1, viewDistanceColumns);

        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                var c2 = new Vector2Int(col.x + dx, col.y + dz);
                if (!ColumnInWorld(c2)) continue;
                columnState.TryGetValue(c2, out byte st);
                LoadColumn(c2, st);
            }

        for (int dx = -(r - 1); dx <= r - 1; dx++)
            for (int dz = -(r - 1); dz <= r - 1; dz++)
            {
                var c2 = new Vector2Int(col.x + dx, col.y + dz);
                if (!ColumnInWorld(c2)) continue;
                if (columnState.TryGetValue(c2, out byte st) && st < 2 && NeighborsGenerated(c2))
                    DecorateColumn(c2);
            }

        // mesheado inmediato del área inicial: piso garantizado bajo el player
        while (dirtyQueue.Count > 0)
        {
            VoxelChunk c = dirtyQueue.Dequeue();
            c.dirty = false;
            if (c.go != null) RemeshImmediate(c);
        }
    }

    /// <summary>¿La columna bajo esta posición ya tiene terreno decorado y mesheado?</summary>
    public bool IsAreaReady(Vector3 worldPos)
    {
        Vector3 rel = worldPos - Origin;
        var c2 = new Vector2Int(Mathf.FloorToInt(rel.x) >> 4, Mathf.FloorToInt(rel.z) >> 4);
        return columnState.TryGetValue(c2, out byte st) && st >= 2 && loadedColumns.Contains(c2);
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
    /// Modo Perfect: mina un único micro-voxel (de voxel en voxel), la máxima precisión
    /// posible. Usa el punto de impacto empujado ligeramente hacia dentro del bloque
    /// (worldPos = hit.point - hit.normal * 0.01f, igual que DamageBlock) para ubicar
    /// el micro-voxel exacto bajo la mira. Respeta hardness e indestructible.
    /// Devuelve true si quitó algo; removedType trae el tipo obtenido (para recursos).
    /// </summary>
    public bool MineVoxel(Vector3 worldPos, float power, out byte removedType)
    {
        removedType = 0;
        Vector3 rel = worldPos - Origin;
        int bx = Mathf.FloorToInt(rel.x);
        int by = Mathf.FloorToInt(rel.y);
        int bz = Mathf.FloorToInt(rel.z);
        if (!InBounds(bx, by, bz)) return false;

        // cáscara indestructible: suelo y paredes, techo abierto
        if (by < 1 || bx < 1 || bz < 1 || bx >= BlockDims.x - 1 || bz >= BlockDims.z - 1) return false;

        byte t = GetBlockType(bx, by, bz);
        byte[] micro = GetMicroArray(bx, by, bz);
        if (t == 0 && micro == null) return false; // aire

        if (micro == null && IsPlantId(t))
        {
            // la maleza se rompe entera con solo rozarla
            SetBlockUniform(bx, by, bz, 0);
            removedType = t;
            return true;
        }

        const int M = VoxelChunk.MICRO;
        if (micro == null)
        {
            VoxelTypeSO blockVt = types[t];
            if (blockVt.indestructible || blockVt.hardness > power) return false;
            micro = AllocateMicro(bx, by, bz, t);
        }

        // micro-voxel exacto bajo la mira, dentro del bloque (0..M-1 por eje)
        Vector3 local = rel - new Vector3(bx, by, bz); // 0..1
        int mx = Mathf.Clamp(Mathf.FloorToInt(local.x * M), 0, M - 1);
        int my = Mathf.Clamp(Mathf.FloorToInt(local.y * M), 0, M - 1);
        int mz = Mathf.Clamp(Mathf.FloorToInt(local.z * M), 0, M - 1);

        int mi = VoxelChunk.MicroIndex(mx, my, mz);
        byte id = micro[mi];
        if (id == 0) return false; // ya estaba vacío (hueco previo)

        VoxelTypeSO vt = types[id];
        if (vt.indestructible || vt.hardness > power) return false;

        micro[mi] = 0;
        removedType = id;

        bool anyLeft = false;
        for (int i = 0; i < micro.Length; i++)
            if (micro[i] != 0) { anyLeft = true; break; }

        if (!anyLeft) SetBlockUniform(bx, by, bz, 0); // colapsa a aire
        else NotifyBlockEdited(bx, by, bz);
        return true;
    }
    /// <summary>
    /// Parámetros de un golpe de minado. Cada MiningType solo lee los campos
    /// que necesita (los demás se ignoran), así una sola llamada a Mine()
    /// sirve para los tres modos sin ramificar en quien la llama:
    /// - Pickaxe: damage.
    /// - Drill: radius y power.
    /// - Perfect: power.
    /// </summary>
    [Serializable]
    public struct MiningParams
    {
        [Tooltip("Pickaxe: daño por golpe (se acumula hasta superar la hardness)")]
        public float damage;
        [Tooltip("Drill: radio de la esfera en metros")]
        public float radius;
        [Tooltip("Drill/Perfect: mina materiales con hardness <= power")]
        public float power;
    }

    /// <summary>Resultado uniforme de Mine(), sin importar el MiningType usado.</summary>
    public struct MiningResult
    {
        /// <summary>True si el golpe rompió/talló/minó algo.</summary>
        public bool changed;
        /// <summary>Recursos obtenidos por tipo. Null si changed es false.</summary>
        public Dictionary<byte, int> removed;
    }

    /// <summary>
    /// Una celda cúbica en coordenadas de mundo: o un bloque de 1m entero, o un
    /// único micro-voxel. La usa PreviewPerfect para describir la celda exacta
    /// que un golpe se llevaría, sin modificar el mundo.
    /// </summary>
    public struct MiningCell
    {
        public Vector3 min;
        public float size;
    }

    /// <summary>
    /// Una cara rectangular del contorno externo, en coordenadas de mundo. Los
    /// 4 puntos forman el borde en orden (a→b→c→d→a); solo hace falta para
    /// dibujar un outline, no para renderizar (no importa el winding).
    /// </summary>
    public struct MiningQuad
    {
        public Vector3 a, b, c, d;
    }

    /// <summary>
    /// Punto de entrada único para minar: dado el tipo de herramienta y los
    /// parámetros que ese tipo necesita, hace el trabajo correspondiente
    /// (DamageBlock/DigSphere/MineVoxel) y devuelve un resultado uniforme.
    /// Pasa hitPoint/hitNormal tal cual salen del RaycastHit; esta función ya
    /// aplica el pequeño empuje hacia dentro del bloque donde hace falta.
    /// </summary>
    public MiningResult Mine(ToolItemSO.MiningType type, Vector3 hitPoint, Vector3 hitNormal, MiningParams p)
    {
        switch (type)
        {
            case ToolItemSO.MiningType.Sphere:
            {
                Dictionary<byte, int> removed = DigSphere(hitPoint, p.radius, p.power);
                return new MiningResult { changed = removed.Count > 0, removed = removed };
            }
            case ToolItemSO.MiningType.Perfect:
            {
                Vector3 worldPos = hitPoint - hitNormal * 0.01f;
                bool mined = MineVoxel(worldPos, p.power, out byte removedType);
                Dictionary<byte, int> removed = mined ? new Dictionary<byte, int> { [removedType] = 1 } : null;
                return new MiningResult { changed = mined, removed = removed };
            }
            default: // Pickaxe
            {
                Vector3Int block = WorldToBlock(hitPoint - hitNormal * 0.01f);
                bool broken = DamageBlock(block, p.damage, out var removed);
                return new MiningResult { changed = broken, removed = removed };
            }
        }
    }

    // scratch reutilizado por PreviewPickaxeContour para no generar basura cada frame
    bool[,] contourMask;
    bool[,] contourVisited;
    readonly List<(int u0, int v0, int u1, int v1)> contourRects = new List<(int, int, int, int)>(32);
    readonly MiningQuad[] cubeQuadBuf = new MiningQuad[6];

    /// <summary>
    /// Sin modificar el mundo: el contorno externo exacto de lo que el pico se
    /// llevaría de este bloque. Si el bloque está intacto (uniforme, o
    /// materializado pero relleno de punta a punta) devuelve las 6 caras del
    /// cubo de 1m completo. Si ya fue minado a medias por el taladro o el modo
    /// Perfect, fusiona las caras expuestas coplanares en rectángulos (greedy
    /// meshing, igual que hace VoxelMesher para la malla real) en vez de dar
    /// un cubito por cada micro-voxel suelto — así el outline dibuja solo el
    /// borde de la forma real, no una rejilla. Rellena <paramref name="results"/>
    /// (se limpia primero) para no generar basura llamándola cada frame.
    /// </summary>
    public void PreviewPickaxeContour(Vector3Int blockPos, List<MiningQuad> results)
    {
        results.Clear();
        int bx = blockPos.x, by = blockPos.y, bz = blockPos.z;
        if (!InBounds(bx, by, bz)) return;

        // cáscara indestructible: suelo y paredes, techo abierto
        if (by < 1 || bx < 1 || bz < 1 || bx >= BlockDims.x - 1 || bz >= BlockDims.z - 1) return;

        byte t = GetBlockType(bx, by, bz);
        byte[] micro = GetMicroArray(bx, by, bz);
        if (t == 0 && micro == null) return; // aire

        VoxelTypeSO vt = types[t != 0 ? t : (byte)1];
        if (vt.indestructible) return;

        Vector3 bMin = Origin + new Vector3(bx, by, bz);
        const int M = VoxelChunk.MICRO;
        const float MV = 1f / M;

        // bloque uniforme, o materializado pero relleno de punta a punta (p.ej. terreno
        // suavizado que no llegó a colapsar a uniforme): sus 6 caras son las del cubo entero.
        if (micro == null || IsFullyFilled(micro))
        {
            CubeQuads(bMin, 1f, cubeQuadBuf);
            results.AddRange(cubeQuadBuf);
            return;
        }

        contourMask ??= new bool[M, M];
        contourVisited ??= new bool[M, M];

        // +X / -X: capas a lo largo de X; máscara indexada (u = mz, v = my)
        for (int mx = 0; mx < M; mx++)
        {
            BuildFaceMaskX(micro, M, mx, +1, contourMask);
            GreedyMerge(contourMask, contourVisited, M, contourRects);
            foreach (var r in contourRects) results.Add(QuadX(bMin, mx + 1, r, MV));

            BuildFaceMaskX(micro, M, mx, -1, contourMask);
            GreedyMerge(contourMask, contourVisited, M, contourRects);
            foreach (var r in contourRects) results.Add(QuadX(bMin, mx, r, MV));
        }

        // +Y / -Y: capas a lo largo de Y; máscara indexada (u = mx, v = mz)
        for (int my = 0; my < M; my++)
        {
            BuildFaceMaskY(micro, M, my, +1, contourMask);
            GreedyMerge(contourMask, contourVisited, M, contourRects);
            foreach (var r in contourRects) results.Add(QuadY(bMin, my + 1, r, MV));

            BuildFaceMaskY(micro, M, my, -1, contourMask);
            GreedyMerge(contourMask, contourVisited, M, contourRects);
            foreach (var r in contourRects) results.Add(QuadY(bMin, my, r, MV));
        }

        // +Z / -Z: capas a lo largo de Z; máscara indexada (u = mx, v = my)
        for (int mz = 0; mz < M; mz++)
        {
            BuildFaceMaskZ(micro, M, mz, +1, contourMask);
            GreedyMerge(contourMask, contourVisited, M, contourRects);
            foreach (var r in contourRects) results.Add(QuadZ(bMin, mz + 1, r, MV));

            BuildFaceMaskZ(micro, M, mz, -1, contourMask);
            GreedyMerge(contourMask, contourVisited, M, contourRects);
            foreach (var r in contourRects) results.Add(QuadZ(bMin, mz, r, MV));
        }
    }

    static bool IsFullyFilled(byte[] micro)
    {
        for (int i = 0; i < micro.Length; i++)
            if (micro[i] == 0) return false;
        return true;
    }

    static bool MicroFilled(byte[] micro, int M, int mx, int my, int mz) =>
        mx >= 0 && mx < M && my >= 0 && my < M && mz >= 0 && mz < M &&
        micro[VoxelChunk.MicroIndex(mx, my, mz)] != 0;

    // marca expuesta (ocupada Y sin vecino en la dirección dir) en mask[u=mz, v=my]
    static void BuildFaceMaskX(byte[] micro, int M, int mx, int dir, bool[,] mask)
    {
        int nx = mx + dir;
        for (int my = 0; my < M; my++)
            for (int mz = 0; mz < M; mz++)
                mask[mz, my] = MicroFilled(micro, M, mx, my, mz) && !MicroFilled(micro, M, nx, my, mz);
    }

    // mask[u=mx, v=mz]
    static void BuildFaceMaskY(byte[] micro, int M, int my, int dir, bool[,] mask)
    {
        int ny = my + dir;
        for (int mx = 0; mx < M; mx++)
            for (int mz = 0; mz < M; mz++)
                mask[mx, mz] = MicroFilled(micro, M, mx, my, mz) && !MicroFilled(micro, M, mx, ny, mz);
    }

    // mask[u=mx, v=my]
    static void BuildFaceMaskZ(byte[] micro, int M, int mz, int dir, bool[,] mask)
    {
        int nz = mz + dir;
        for (int mx = 0; mx < M; mx++)
            for (int my = 0; my < M; my++)
                mask[mx, my] = MicroFilled(micro, M, mx, my, mz) && !MicroFilled(micro, M, mx, my, nz);
    }

    /// <summary>Fusiona una máscara 2D de celdas expuestas en el mínimo de rectángulos
    /// (greedy meshing estándar). Rellena outRects (se limpia primero).</summary>
    static void GreedyMerge(bool[,] mask, bool[,] visited, int size, List<(int u0, int v0, int u1, int v1)> outRects)
    {
        outRects.Clear();
        for (int v = 0; v < size; v++)
            for (int u = 0; u < size; u++)
                visited[u, v] = false;

        for (int v = 0; v < size; v++)
            for (int u = 0; u < size; u++)
            {
                if (visited[u, v] || !mask[u, v]) continue;

                int w = 1;
                while (u + w < size && !visited[u + w, v] && mask[u + w, v]) w++;

                int h = 1;
                bool canGrow = true;
                while (v + h < size && canGrow)
                {
                    for (int k = 0; k < w; k++)
                        if (visited[u + k, v + h] || !mask[u + k, v + h]) { canGrow = false; break; }
                    if (canGrow) h++;
                }

                for (int dv = 0; dv < h; dv++)
                    for (int du = 0; du < w; du++)
                        visited[u + du, v + dv] = true;

                outRects.Add((u, v, u + w, v + h));
            }
    }

    static MiningQuad QuadX(Vector3 bMin, int layer, (int u0, int v0, int u1, int v1) r, float cell)
    {
        float x = bMin.x + layer * cell;
        float z0 = bMin.z + r.u0 * cell, z1 = bMin.z + r.u1 * cell;
        float y0 = bMin.y + r.v0 * cell, y1 = bMin.y + r.v1 * cell;
        return new MiningQuad
        {
            a = new Vector3(x, y0, z0), b = new Vector3(x, y0, z1),
            c = new Vector3(x, y1, z1), d = new Vector3(x, y1, z0)
        };
    }

    static MiningQuad QuadY(Vector3 bMin, int layer, (int u0, int v0, int u1, int v1) r, float cell)
    {
        float y = bMin.y + layer * cell;
        float x0 = bMin.x + r.u0 * cell, x1 = bMin.x + r.u1 * cell;
        float z0 = bMin.z + r.v0 * cell, z1 = bMin.z + r.v1 * cell;
        return new MiningQuad
        {
            a = new Vector3(x0, y, z0), b = new Vector3(x1, y, z0),
            c = new Vector3(x1, y, z1), d = new Vector3(x0, y, z1)
        };
    }

    static MiningQuad QuadZ(Vector3 bMin, int layer, (int u0, int v0, int u1, int v1) r, float cell)
    {
        float z = bMin.z + layer * cell;
        float x0 = bMin.x + r.u0 * cell, x1 = bMin.x + r.u1 * cell;
        float y0 = bMin.y + r.v0 * cell, y1 = bMin.y + r.v1 * cell;
        return new MiningQuad
        {
            a = new Vector3(x0, y0, z), b = new Vector3(x1, y0, z),
            c = new Vector3(x1, y1, z), d = new Vector3(x0, y1, z)
        };
    }

    /// <summary>Las 6 caras de un cubo de arista size en min, como loops de 4 esquinas.</summary>
    public static void CubeQuads(Vector3 min, float size, MiningQuad[] into6)
    {
        Vector3 max = min + Vector3.one * size;
        into6[0] = new MiningQuad { a = new Vector3(min.x, min.y, min.z), b = new Vector3(min.x, max.y, min.z), c = new Vector3(min.x, max.y, max.z), d = new Vector3(min.x, min.y, max.z) }; // -X
        into6[1] = new MiningQuad { a = new Vector3(max.x, min.y, min.z), b = new Vector3(max.x, min.y, max.z), c = new Vector3(max.x, max.y, max.z), d = new Vector3(max.x, max.y, min.z) }; // +X
        into6[2] = new MiningQuad { a = new Vector3(min.x, min.y, min.z), b = new Vector3(min.x, min.y, max.z), c = new Vector3(max.x, min.y, max.z), d = new Vector3(max.x, min.y, min.z) }; // -Y
        into6[3] = new MiningQuad { a = new Vector3(min.x, max.y, min.z), b = new Vector3(max.x, max.y, min.z), c = new Vector3(max.x, max.y, max.z), d = new Vector3(min.x, max.y, max.z) }; // +Y
        into6[4] = new MiningQuad { a = new Vector3(min.x, min.y, min.z), b = new Vector3(max.x, min.y, min.z), c = new Vector3(max.x, max.y, min.z), d = new Vector3(min.x, max.y, min.z) }; // -Z
        into6[5] = new MiningQuad { a = new Vector3(min.x, min.y, max.z), b = new Vector3(min.x, max.y, max.z), c = new Vector3(max.x, max.y, max.z), d = new Vector3(max.x, min.y, max.z) }; // +Z
    }

    /// <summary>
    /// Sin modificar el mundo: si el modo Perfect pudiera minar el micro-voxel
    /// bajo worldPos con esta power (respeta hardness/indestructible igual que
    /// MineVoxel), devuelve true y la celda exacta de ese micro-voxel.
    /// </summary>
    public bool PreviewPerfect(Vector3 worldPos, float power, out MiningCell cell)
    {
        cell = default;
        Vector3 rel = worldPos - Origin;
        int bx = Mathf.FloorToInt(rel.x);
        int by = Mathf.FloorToInt(rel.y);
        int bz = Mathf.FloorToInt(rel.z);
        if (!InBounds(bx, by, bz)) return false;

        if (by < 1 || bx < 1 || bz < 1 || bx >= BlockDims.x - 1 || bz >= BlockDims.z - 1) return false;

        byte t = GetBlockType(bx, by, bz);
        byte[] micro = GetMicroArray(bx, by, bz);
        if (t == 0 && micro == null) return false; // aire

        const int M = VoxelChunk.MICRO;
        Vector3 local = rel - new Vector3(bx, by, bz);
        int mx = Mathf.Clamp(Mathf.FloorToInt(local.x * M), 0, M - 1);
        int my = Mathf.Clamp(Mathf.FloorToInt(local.y * M), 0, M - 1);
        int mz = Mathf.Clamp(Mathf.FloorToInt(local.z * M), 0, M - 1);

        byte id = micro != null ? micro[VoxelChunk.MicroIndex(mx, my, mz)] : t;
        if (id == 0) return false;

        VoxelTypeSO vt = types[id];
        if (vt.indestructible || vt.hardness > power) return false;

        Vector3 min = Origin + new Vector3(bx, by, bz) + new Vector3(mx, my, mz) / M;
        cell = new MiningCell { min = min, size = 1f / M };
        return true;
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
