using UnityEngine;

[CreateAssetMenu(fileName = "ToolItem", menuName = "ScriptableObjects/Items/EquipableItem/ToolItem", order = 1)]
public class ToolItemSO : EquipableItemSO
{
    public MiningType toolMode;
    public override async Awaitable UseItem(UseItemInfo useItemInfo)
    {
        useItemInfo.character.characterAnimator.SetFloat(GetHandLayer(useItemInfo.characterItem.itemBaseSO.animationValueName, useItemInfo.isFastItem), useItemInfo.characterItem.itemBaseSO.animationValue);
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
            VoxelWorld.MiningResult result = VoxelWorld.Instance.Mine(toolMode, characterPlayer.currentHit.point, characterPlayer.currentHit.normal, new VoxelWorld.MiningParams 
            { 
                radius = characterPlayer.GetItemStatistic(CharacterData.TypeStatistic.ItemRadius)?.currentValue ?? 0f,
                power = characterPlayer.GetItemPower(typeWeapon).currentValue,
                damage = 1
            });
            if (result.changed)
            {
                // result.removed trae los recursos obtenidos por tipo → inventario
            }
        }
        useItemInfo.character.characterAnimator.SetFloat(GetHandLayer(useItemInfo.characterItem.itemBaseSO.animationValueName, useItemInfo.isFastItem), 0);
    }
    public enum MiningType
    {
        Block = 0,
        Sphere = 1,
        Perfect = 2
    }
}
