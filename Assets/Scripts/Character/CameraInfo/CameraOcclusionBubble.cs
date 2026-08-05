using UnityEngine;

/// <summary>
/// Cuando algo se interpone entre la camara y el jugador, activa un cascaron
/// esferico alrededor del jugador que oculta todo lo que quede mas alla del radio.
///
/// Reparto de trabajo: el recorte (SphereCutout / CameraSphereCutout) es el que
/// destapa al personaje. Esto solo quita el fondo, para que por el agujero se vea
/// oscuridad en vez de cielo o cavernas lejanas.
///
/// Va en el mismo GameObject que la Camera, junto a CameraSphereCutout.
///
/// La deteccion consulta los datos de voxel (VoxelWorld.GetBlockType), no
/// Physics.Raycast: con streaming activo un chunk puede no tener malla generada
/// todavia y el raycast fallaria sin avisar.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]   // despues del CinemachineBrain
public class CameraOcclusionBubble : MonoBehaviour
{
    static readonly int AmountId = Shader.PropertyToID("_BubbleAmount");

    [Header("Objetivo")]
    [SerializeField] Transform player;

    [Tooltip("Offset en mundo sobre el pivote del jugador. Mismo valor que en " +
             "CameraSphereCutout para que las dos esferas esten concentricas.")]
    [SerializeField] Vector3 centerOffset = new Vector3(0f, 0.9f, 0f);

    [Header("Cascaron")]
    [Tooltip("Radio en bloques. Se fuerza a ser mayor que la distancia de camara, " +
             "porque si la camara quedara FUERA veriamos una bola negra delante.")]
    [SerializeField, Min(1f)] float radius = 4f;

    [Tooltip("Margen que se garantiza entre la camara y el cascaron.")]
    [SerializeField, Min(0.1f)] float cameraClearance = 0.75f;

    [Tooltip("Si se deja vacio se crea uno en runtime con el shader " +
             "Voxels/OcclusionBubble.")]
    [SerializeField] Material bubbleMaterial;

    [Header("Transicion")]
    [Tooltip("Segundos que tarda en aparecer y en irse.")]
    [SerializeField, Min(0.01f)] float fadeTime = 0.15f;

    [Tooltip("Segundos que sigue activo despues de dejar de estar tapado. Evita " +
             "el parpadeo cuando cruzas por detras de un tronco delgado.")]
    [SerializeField, Min(0f)] float holdTime = 0.1f;

    [Header("Deteccion")]
    [Tooltip("Muestras entre camara y jugador. 10 sobra para 2-3 bloques.")]
    [SerializeField, Range(3, 32)] int samples = 10;

    [Tooltip("Cuanto se recorta el final del segmento para no contar el bloque " +
             "donde esta el propio jugador.")]
    [SerializeField, Min(0f)] float playerSkin = 0.35f;

    [Header("Debug")]
    [SerializeField] bool forceOn = false;

    Camera cam;
    Transform shell;
    Renderer shellRenderer;
    float amount;          // 0 = sin burbuja, 1 = opaca
    float holdLeft;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (player == null)
            Debug.LogWarning($"[{nameof(CameraOcclusionBubble)}] Falta asignar " +
                             "'player'. La burbuja queda desactivada.", this);

        BuildShell();
    }

    void BuildShell()
    {
        if (bubbleMaterial == null)
        {
            Shader s = Shader.Find("Voxels/OcclusionBubble");
            if (s == null)
            {
                Debug.LogError($"[{nameof(CameraOcclusionBubble)}] No encuentro el " +
                               "shader 'Voxels/OcclusionBubble'.", this);
                enabled = false;
                return;
            }
            bubbleMaterial = new Material(s) { name = "OcclusionBubble (runtime)" };
        }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "~OcclusionBubble";
        go.hideFlags = HideFlags.DontSave;

        // La primitiva trae collider: fuera, esto es puramente visual.
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        shellRenderer = go.GetComponent<MeshRenderer>();
        shellRenderer.sharedMaterial = bubbleMaterial;
        shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;
        shellRenderer.enabled = false;

        shell = go.transform;
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(AmountId, 0f);
        if (shellRenderer != null) shellRenderer.enabled = false;
    }

    void OnDestroy()
    {
        if (shell != null) Destroy(shell.gameObject);
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || player == null || shell == null)
        {
            Publish(0f);
            return;
        }

        Vector3 center = player.position + centerOffset;
        Vector3 camPos = cam.transform.position;

        // El cascaron tiene que envolver a la camara siempre.
        float r = Mathf.Max(radius, Vector3.Distance(camPos, center) + cameraClearance);

        bool occluded = forceOn || IsOccluded(camPos, center);

        if (occluded) holdLeft = holdTime;
        else holdLeft -= Time.deltaTime;

        float target = (occluded || holdLeft > 0f) ? 1f : 0f;
        amount = Mathf.MoveTowards(amount, target, Time.deltaTime / fadeTime);

        shell.position = center;
        shell.localScale = Vector3.one * (r * 2f);   // la primitiva tiene radio 0.5

        Publish(amount);
    }

    void Publish(float a)
    {
        Shader.SetGlobalFloat(AmountId, a);
        if (shellRenderer != null) shellRenderer.enabled = a > 0.001f;
    }

    /// <summary>
    /// Muestrea el segmento camara -> jugador contra los datos de voxel.
    /// Replica el criterio de VoxelMesher.IsSolid: solido = no aire, no agua,
    /// no planta. La muestra i=0 es la posicion de la camara, asi que este mismo
    /// test cubre el caso de que la camara este metida dentro de un bloque.
    /// </summary>
    bool IsOccluded(Vector3 camPos, Vector3 target)
    {
        VoxelWorld w = VoxelWorld.Instance;
        if (w == null) return false;

        Vector3 seg = target - camPos;
        float len = seg.magnitude;
        if (len < 1e-4f) return false;

        Vector3 dir = seg / len;
        float usable = Mathf.Max(0f, len - playerSkin);

        for (int i = 0; i <= samples; i++)
        {
            Vector3 p = camPos + dir * (usable * i / samples);
            Vector3Int b = w.WorldToBlock(p);
            byte id = w.GetBlockType(b.x, b.y, b.z);

            if (id != 0 && id != w.waterTypeId && !w.IsPlantId(id))
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Vector3 center = player.position + centerOffset;
        Vector3 camPos = cam != null ? cam.transform.position : transform.position;
        float r = Mathf.Max(radius, Vector3.Distance(camPos, center) + cameraClearance);

        Gizmos.color = new Color(1f, 0.55f, 0.1f, 1f);
        Gizmos.DrawWireSphere(center, r);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(camPos, center);
    }
#endif
}
