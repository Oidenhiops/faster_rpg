using UnityEngine;

[CreateAssetMenu(fileName = "ToolItem", menuName = "ScriptableObjects/Items/EquipableItem/ToolItem", order = 1)]
public class ToolItemSO : EquipableItemSO
{
    public TypeWeapon typeWeapon;
    public MiningInfo miningInfo;
    public override async Awaitable UseItem(UseItemInfo useItemInfo)
    {
        useItemInfo.character.characterAnimator.SetFloat(GetHandLayer(useItemInfo.isFastItem), useItemInfo.characterItem.itemBaseSO.animationValue);
        useItemInfo.character.characterData.statistics[CharacterData.TypeStatistic.Str].currentValue -= useItemInfo.character.characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemBaseSO.costPerUse;
        useItemInfo.character.characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Str);
        while (true)
        {
            if (useItemInfo.isFastItem && useItemInfo.character.characterAnimator.GetCurrentAnimatorStateInfo(1).IsName("LeftHand")) break;
            else if (!useItemInfo.isFastItem && useItemInfo.character.characterAnimator.GetCurrentAnimatorStateInfo(2).IsName("RightHand")) break;
            await Awaitable.NextFrameAsync();
        }
        while (true)
        {
            if (useItemInfo.character.characterAnimator.GetCurrentAnimatorStateInfo(useItemInfo.isFastItem ? 1 : 2).normalizedTime > 0.9)
            {
                break;
            }
            await Awaitable.NextFrameAsync();
        }
        if (useItemInfo.character is CharacterPlayer characterPlayer)
        {
            VoxelWorld.MiningResult result = VoxelWorld.Instance.Mine(miningInfo.toolMode, characterPlayer.currentHit.point, characterPlayer.currentHit.normal, new VoxelWorld.MiningParams
            {
                radius = characterPlayer.GetItemStatistic(CharacterData.TypeStatistic.ItemRadius)?.currentValue ?? 0f,
                damage = 1, // ticks por golpe
                miner = characterPlayer // sus estadísticas se comparan con las que cada bloque exige (itemStatistics)
            });
            if (result.changed)
            {
                // result.removed trae los recursos obtenidos por tipo → inventario
            }
        }
        useItemInfo.character.characterAnimator.SetFloat(GetHandLayer(useItemInfo.isFastItem), 0);
    }
    [System.Serializable]
    public class MiningInfo
    {
        public MiningType toolMode;
        public Vector3[] freeModePoints;
    }
    public enum MiningType
    {
        Block = 0,
        Sphere = 1,
        Perfect = 2,
        Free = 3
    }
}
