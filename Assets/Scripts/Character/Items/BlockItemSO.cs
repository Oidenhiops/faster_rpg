using UnityEngine;

[CreateAssetMenu(fileName = "BlockItem", menuName = "ScriptableObjects/Items/BlockItem", order = 1)]
public class BlockItemSO : ItemBaseSO
{
    [Header("Dureza")]
    [Tooltip("Daño acumulado necesario para romper el bloque entero (pico). Para el taladro se necesita drillPower >= hardness.")]
    public float hardness = 1f;
    public bool indestructible;

    [Header("Planta (maleza, flores...)")]
    [Tooltip("Se renderiza como quads en X (sin cubo), no colisiona, se rompe entera de un toque y no bloquea caras vecinas")]
    public bool isPlant;

    [Header("Veta de mineral (opcional)")]
    [Tooltip("Si se asigna, este bloque es una 'veta': al minarlo, solo una fracción de lo obtenido cae como este mineral; el resto cae como este bloque anfitrión (ej. Piedra). Vacío = bloque normal, sin reparto.")]
    public BlockItemSO oreHost;
    [Tooltip("Fracción (0-1) de lo minado que realmente cae como este mineral; el resto se convierte en oreHost.")]
    [Range(0f, 1f)] public float oreYield = 0.25f;

    public override async Awaitable UseItem(UseItemInfo useItemInfo)
    {
        if (useItemInfo.character is CharacterPlayer characterPlayer)
        {
            VoxelWorld.Instance.PlaceBlock(characterPlayer.currentHit.point + characterPlayer.currentHit.normal * 0.5f, this);
        }
    }
}
