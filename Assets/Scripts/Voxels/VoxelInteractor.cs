using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Herramienta de prueba con dos modos, raycast desde el centro de pantalla:
/// - Pico (por defecto): click izq golpea el bloque apuntado; el daño se acumula
///   hasta superar la hardness y el bloque de 1m se rompe entero.
/// - Taladro (toolMode = Drill): click izq talla una esfera de micro-voxels
///   estilo DRG (solo materiales con hardness <= drillPower).
/// Click derecho construye un bloque de 1m en ambos modos.
/// Cuando funcione, mueve esta lógica a tus ItemBaseSO (pico/taladro como ítems).
/// </summary>
public class VoxelInteractor : MonoBehaviour
{
    public enum ToolMode { Pickaxe, Drill }

    public Camera cam;
    public float maxDistance = 8f;
    public LayerMask hitMask = ~0; // pon aquí solo el layer "Map"
    public ToolMode toolMode = ToolMode.Pickaxe;

    [Header("Pico: golpes a bloques enteros (click izq)")]
    [Tooltip("Daño por golpe. Piedra hardness 3 = 3 golpes con daño 1")]
    public float hitDamage = 1f;
    public float hitInterval = 0.3f;

    [Header("Taladro: micro-voxels (click izq en modo Drill)")]
    public float drillRadius = 1.1f;
    [Tooltip("Talla materiales con hardness <= drillPower")]
    public float drillPower = 5f;
    public float drillInterval = 0.12f;

    [Header("Construir (click derecho)")]
    [Tooltip("Índice en VoxelWorld.types (3 = piedra)")]
    public byte buildType = 3;

    float nextActionTime;

    void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        VoxelWorld world = VoxelWorld.Instance;
        if (mouse == null || cam == null || world == null || !world.Ready) return;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));

        if (mouse.leftButton.isPressed && Time.time >= nextActionTime)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask) &&
                hit.collider.GetComponentInParent<VoxelWorld>() != null)
            {
                if (toolMode == ToolMode.Drill)
                {
                    nextActionTime = Time.time + drillInterval;
                    world.DigSphere(hit.point, drillRadius, drillPower);
                    // el diccionario devuelto trae los micro-voxels por tipo → inventario
                }
                else
                {
                    nextActionTime = Time.time + hitInterval;
                    Vector3Int block = world.WorldToBlock(hit.point - hit.normal * 0.01f);
                    if (world.DamageBlock(block, hitDamage, out var removed))
                    {
                        // removed trae los recursos del bloque roto → inventario
                    }
                }
            }
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask))
            {
                world.PlaceBlock(hit.point + hit.normal * 0.5f, buildType);
            }
        }
    }
}
