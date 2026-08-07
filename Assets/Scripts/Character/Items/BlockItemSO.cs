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
    public override async Awaitable UseItem(UseItemInfo useItemInfo)
    {
        if (useItemInfo.character is CharacterPlayer characterPlayer)
        {
            VoxelWorld.Instance.PlaceBlock(characterPlayer.currentHit.point + characterPlayer.currentHit.normal * 0.5f, this);
        }
    }
}
