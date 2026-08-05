using UnityEngine;

[CreateAssetMenu(fileName = "ToolItem", menuName = "ScriptableObjects/Items/EquipableItem/ToolItem", order = 1)]
public class ToolItemSO : EquipableItemSO
{
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
                Debug.Log("RightHand finish animation");
                break;
            }
            await Awaitable.NextFrameAsync();
        }
        useItemInfo.character.characterAnimator.SetFloat(GetHandLayer(useItemInfo.characterItem.itemBaseSO.animationValueName, useItemInfo.isFastItem), 0);
    }
}
