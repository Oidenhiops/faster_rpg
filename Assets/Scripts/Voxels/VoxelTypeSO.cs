using UnityEngine;

[CreateAssetMenu(fileName = "VoxelType", menuName = "ScriptableObjects/Voxels/VoxelType", order = 1)]
public class VoxelTypeSO : ScriptableObject
{
    public string displayName;

    [Header("Apariencia")]
    [Tooltip("Opcional. Debe tener Read/Write habilitado en el import. Si es null, se usa el color plano.")]
    public Texture2D texture;
    public Color color = Color.white;

    [Header("Dureza")]
    [Tooltip("Daño acumulado necesario para romper el bloque entero (pico). Para el taladro se necesita drillPower >= hardness.")]
    public float hardness = 1f;
    public bool indestructible;

    // futuro: ItemBaseSO dropItem; AudioClip breakSound; GameObject breakVfx;
}
