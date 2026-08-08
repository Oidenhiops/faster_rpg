using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dibuja un outline (wireframe) sobre lo que una herramienta de minado está
/// apuntando.
///
/// - ShowContour: dibuja el borde de cada VoxelWorld.MiningQuad recibida (una
///   cara rectangular ya fusionada con sus vecinas coplanares por greedy
///   meshing). Con esto el outline es el contorno externo real de la forma —
///   por ejemplo, si a un bloque ya minado a medias le quedan unos pocos
///   micro-voxels pegados, se ve el borde de ese bulto, no una rejilla de
///   cubitos sueltos. La usa Pickaxe vía VoxelWorld.PreviewPickaxeContour.
/// - ShowVoxel: atajo para un único cubo (Perfect, siempre un solo
///   micro-voxel) — internamente arma sus 6 caras y las pasa por ShowContour.
/// - ShowSphere: esfera de wireframe (3 anillos ortogonales) para Drill. No
///   se intenta la forma exacta talla-por-talla aquí: un taladro de radio
///   normal puede afectar miles de micro-voxels y recalcular esa forma cada
///   frame solo para la previsualización sería demasiado caro.
///
/// El progreso de rotura (0-1, el daño acumulado guardado en VoxelWorld) tiñe
/// el outline hacia damagedColor y lo engrosa: cuanto más roto, más rojo/grueso.
///
/// Los LineRenderers se crean una sola vez y se reutilizan (pool que crece
/// según haga falta) para no generar basura de mallas cada frame.
/// </summary>
public class VoxelOutlineIndicator : MonoBehaviour
{
    [Header("Apariencia")]
    public Color color = new Color(1f, 1f, 1f, 0.9f);
    [Range(0.001f, 0.05f)] public float lineWidth = 0.015f;
    [Tooltip("Segmentos por círculo del outline de esfera (Drill)")]
    [Range(8, 64)] public int sphereSegments = 24;
    [Tooltip("Máximo de caras dibujadas a la vez en ShowContour, por rendimiento. " +
             "Con greedy meshing casi nunca se necesitan más que un puñado; esto es solo un seguro.")]
    public int maxContourFaces = 512;

    [Header("Daño acumulado (progreso de rotura)")]
    [Tooltip("Color del outline cuando el bloque está a punto de romperse (daño acumulado alto)")]
    public Color damagedColor = new Color(1f, 0.15f, 0.1f, 1f);
    [Tooltip("Cuánto crece el grosor de línea con el daño acumulado (1 = no crece)")]
    [Range(1f, 5f)] public float damagedWidthMultiplier = 2.5f;

    readonly List<LineRenderer[]> facePool = new List<LineRenderer[]>(); // cada elemento = 4 aristas del borde de una cara
    int activeFaces;

    LineRenderer[] sphereRings; // 3 círculos ortogonales (Drill)

    readonly VoxelWorld.MiningQuad[] singleVoxelBuf = new VoxelWorld.MiningQuad[6];

    void Awake()
    {
        sphereRings = new LineRenderer[3];
        for (int i = 0; i < 3; i++)
            sphereRings[i] = MakeLineRenderer($"OutlineRing{i}", sphereSegments + 1);

        HideAll();
    }

    LineRenderer MakeLineRenderer(string name, int positions)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.positionCount = positions;
        lr.widthMultiplier = lineWidth;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        return lr;
    }

    /// <summary>Las 4 aristas (LineRenderers) del borde de la cara número index, creándolas si hace falta.</summary>
    LineRenderer[] GetFaceSlot(int index)
    {
        while (facePool.Count <= index)
        {
            var edges = new LineRenderer[4];
            for (int i = 0; i < 4; i++)
                edges[i] = MakeLineRenderer($"OutlineFace{facePool.Count}_{i}", 2);
            facePool.Add(edges);
        }
        return facePool[index];
    }

    public void Hide() => HideAll();

    void HideAll()
    {
        for (int i = 0; i < activeFaces; i++)
            foreach (LineRenderer lr in facePool[i]) lr.enabled = false;
        activeFaces = 0;

        if (sphereRings != null)
            foreach (LineRenderer lr in sphereRings) lr.enabled = false;
    }

    /// <summary>
    /// Dibuja el borde de cada cara recibida. Si vienen de PreviewPickaxeContour,
    /// ya están fusionadas por greedy meshing, así que esto traza el contorno
    /// externo real de la forma en vez de una rejilla de micro-voxels sueltos.
    /// damageRatio01 (0-1, opcional): progreso de rotura del bloque
    /// (VoxelWorld.GetBlockDamageRatio01) — a más daño, el outline se pone más
    /// rojo y más grueso, así se nota que el bloque está a punto de romperse.
    /// </summary>
    public void ShowContour(IReadOnlyList<VoxelWorld.MiningQuad> faces, float damageRatio01 = 0f)
    {
        HideAll();
        if (faces == null || faces.Count == 0) return;

        Color c = Color.Lerp(color, damagedColor, damageRatio01);
        float w = Mathf.Lerp(lineWidth, lineWidth * damagedWidthMultiplier, damageRatio01);

        int count = Mathf.Min(faces.Count, maxContourFaces);
        for (int i = 0; i < count; i++)
            DrawFaceAt(GetFaceSlot(i), faces[i], c, w);
        activeFaces = count;
    }

    void DrawFaceAt(LineRenderer[] edges, VoxelWorld.MiningQuad q, Color c, float w)
    {
        SetEdge(edges[0], q.a, q.b, c, w);
        SetEdge(edges[1], q.b, q.c, c, w);
        SetEdge(edges[2], q.c, q.d, c, w);
        SetEdge(edges[3], q.d, q.a, c, w);
    }

    static void SetEdge(LineRenderer lr, Vector3 p0, Vector3 p1, Color c, float w)
    {
        lr.enabled = true;
        lr.SetPosition(0, p0);
        lr.SetPosition(1, p1);
        lr.startColor = lr.endColor = c;
        lr.widthMultiplier = w;
    }

    /// <summary>Atajo para un único cubo (Perfect: siempre un solo micro-voxel).
    /// progress01: progreso de rotura de ese voxel — tiñe y engrosa el contorno.</summary>
    public void ShowVoxel(Vector3 min, float size, float progress01 = 0f)
    {
        if (size <= 0f) { Hide(); return; }
        VoxelWorld.CubeQuads(min, size, singleVoxelBuf);
        ShowContour(singleVoxelBuf, progress01);
    }

    /// <summary>Esfera de wireframe (Drill): centro y radio en metros.
    /// progress01: progreso de rotura del bloque bajo la mira — los anillos se
    /// tiñen y engrosan a medida que el área se acerca a romperse.</summary>
    public void ShowSphere(Vector3 center, float radius, float progress01 = 0f)
    {
        HideAll();
        if (radius <= 0f) return;

        Color rc = Color.Lerp(color, damagedColor, progress01);
        float rw = Mathf.Lerp(lineWidth, lineWidth * damagedWidthMultiplier, progress01);

        for (int i = 0; i < 3; i++)
        {
            LineRenderer lr = sphereRings[i];
            lr.enabled = true;
            lr.startColor = lr.endColor = rc;
            lr.widthMultiplier = rw;
            if (lr.positionCount != sphereSegments + 1) lr.positionCount = sphereSegments + 1;

            for (int s = 0; s <= sphereSegments; s++)
            {
                float t = (s / (float)sphereSegments) * Mathf.PI * 2f;
                float a = Mathf.Cos(t) * radius;
                float b = Mathf.Sin(t) * radius;
                Vector3 p = i == 0 ? new Vector3(a, b, 0f)  // plano XY
                          : i == 1 ? new Vector3(a, 0f, b)  // plano XZ
                                   : new Vector3(0f, a, b); // plano YZ
                lr.SetPosition(s, center + p);
            }
        }
    }
}
