using UnityEngine;

[CreateAssetMenu(fileName = "BlockItem", menuName = "ScriptableObjects/Items/BlockItem", order = 1)]
public class BlockItemSO : ItemBaseSO
{
    [Header("Rotura")]
    [Tooltip("Ticks (golpes) para romper el bloque con el poder justo. El poder que este bloque exige se define en itemStatistics (heredado): ej. una entrada PicaxePower con baseValue 50. Con menos poder que el exigido no recibe daño; con el doble o más (+100%) se rompe al instante; el exceso intermedio reduce los ticks linealmente (a +50% de poder, la mitad de ticks). Sin entradas en itemStatistics, cualquier herramienta lo rompe en estos ticks.")]
    public float ticksPerBreak = 1f;
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
