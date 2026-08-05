using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Publica los globales que consume SphereCutout.hlsl (GeneralMaterial.shadergraph).
///
/// Se pone en el mismo GameObject que la Camera (la que tiene el CinemachineBrain).
///
/// Se publica en beginCameraRendering y NO en LateUpdate a proposito: Cinemachine
/// mueve la camara en su propio LateUpdate, asi que si publicaramos ahi la
/// distancia camara->jugador iria un frame desfasada y la puerta de profundidad
/// dejaria pasar pixeles equivocados justo al girar.
/// </summary>
[DisallowMultipleComponent]
public class CameraSphereCutout : MonoBehaviour
{
    static readonly int SphereId  = Shader.PropertyToID("_CutoutSphere");
    static readonly int ParamsId  = Shader.PropertyToID("_CutoutParams");
    static readonly int EnabledId = Shader.PropertyToID("_CutoutEnabled");

    [Header("Objetivo")]
    [Tooltip("Raiz del jugador (el mismo Transform que usa el pivote de camara).")]
    [SerializeField] Transform player;

    [Tooltip("Offset en MUNDO sobre el pivote del jugador. Apunta al pecho/cabeza, " +
             "no a los pies, o el recorte se come el suelo.")]
    [SerializeField] Vector3 centerOffset = new Vector3(0f, 0.9f, 0f);

    [Header("Forma del recorte")]
    [Tooltip("Radio de la esfera en unidades de mundo (= bloques).")]
    [SerializeField, Min(0.1f)] float radius = 1.6f;

    [Tooltip("Cuanto tiene que estar DELANTE del jugador un fragmento para empezar " +
             "a recortarse. Si se te agujerea el suelo a los pies, sube esto.")]
    [SerializeField, Min(0f)] float depthBias = 0.6f;

    [Tooltip("Suavizado de la puerta de profundidad. Bajo = corte mas seco.")]
    [SerializeField, Min(0.01f)] float depthFeather = 0.35f;

    [Tooltip("Alpha minimo dentro del recorte. 0 = agujero total (veras el skybox " +
             "dentro de roca solida). 0.10-0.20 = fantasma de la pared.")]
    [SerializeField, Range(0f, 0.6f)] float minAlpha = 0.12f;

    [Header("Activacion")]
    [SerializeField] bool active = true;

    [Tooltip("Velocidad con la que el radio entra/sale, para que no aparezca de golpe.")]
    [SerializeField, Min(0.1f)] float rampSpeed = 8f;

    float currentRadius;

    void Awake()
    {
        currentRadius = active ? radius : 0f;

        if (player == null)
            Debug.LogWarning($"[{nameof(CameraSphereCutout)}] Falta asignar 'player'. " +
                             "El recorte queda desactivado.", this);
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        Shader.SetGlobalFloat(EnabledId, 0f);
    }

    void LateUpdate()
    {
        // El ramp se hace una vez por frame, no una vez por camara.
        float target = active ? radius : 0f;
        currentRadius = Mathf.MoveTowards(currentRadius, target, rampSpeed * Time.deltaTime);
    }

    void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        // Solo la camara de juego (y la Scene View, para poder previsualizar).
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView)
            return;

        // Se publica POR CAMARA, no una vez por frame. Si solo publicara la
        // primera camara del frame y esa fuera la Scene View, la camara del juego
        // heredaria un playerD medido desde la Scene View (que puede estar
        // lejisimos): la puerta de profundidad dejaria de proteger nada y
        // apareceria x-ray de verdad. Cada camara mide su propia distancia.
        if (player == null || currentRadius <= 0.001f)
        {
            Shader.SetGlobalFloat(EnabledId, 0f);
            return;
        }

        Vector3 center  = player.position + centerOffset;
        float   playerD = Vector3.Distance(cam.transform.position, center);

        Shader.SetGlobalVector(SphereId, new Vector4(center.x, center.y, center.z, currentRadius));
        Shader.SetGlobalVector(ParamsId, new Vector4(playerD, depthFeather, minAlpha, depthBias));
        Shader.SetGlobalFloat(EnabledId, 1f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Vector3 center = player.position + centerOffset;

        Gizmos.color = new Color(0f, 0.65f, 1f, 1f);
        Gizmos.DrawWireSphere(center, radius);

        // Plano de corte: nada mas alla de esta linea se toca nunca.
        Vector3 camPos = Application.isPlaying && Camera.main != null
            ? Camera.main.transform.position
            : transform.position;
        Vector3 dir = (center - camPos).normalized;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(camPos, center - dir * depthBias);
    }
#endif
}
