using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipableItem", menuName = "ScriptableObjects/Items/EquipableItem", order = 1)]
public class EquipableItemSO : ItemBaseSO
{
    public override async Awaitable EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel,  bool isFastItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in characterItem.itemStatistics)
        {
            if (character.characterData.statistics.ContainsKey(statistic.Key))
            {
                character.characterData.statistics[statistic.Key].itemValue += statistic.Value.baseValue;
                character.characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        EquipModelItem(character, characterItem, true, isFastItem);
    }
    public override async Awaitable DesEquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel,  bool isFastItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in characterItem.itemStatistics)
        {
            if (character.characterData.statistics.ContainsKey(statistic.Key))
            {
                character.characterData.statistics[statistic.Key].itemValue -= statistic.Value.baseValue;
                character.characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        DesEquipModelItem(character, characterItem, true, isFastItem);
    }
    public override async Awaitable UseItem(UseItemInfo useItemInfo)
    {
        useItemInfo.character.characterAnimator.SetFloat(GetHandLayer(useItemInfo.characterItem.itemBaseSO.animationValueName, useItemInfo.isFastItem), useItemInfo.characterItem.itemBaseSO.animationValue);
        while (true)
        {
            if (useItemInfo.character.characterAnimator.GetCurrentAnimatorStateInfo(2).IsName("RightHand") ||
                useItemInfo.character.characterAnimator.GetCurrentAnimatorStateInfo(1).IsName("LeftHand"))
            {
                break;
            }
            await Awaitable.NextFrameAsync();
        }
        while (true)
        {
            if (!useItemInfo.isFastItem)
            {
                if (useItemInfo.character.characterAnimator.GetCurrentAnimatorStateInfo(2).normalizedTime > 0.9)
                {
                    Debug.Log("RightHand finish animation");
                    break;
                }
            }
            else
            {
                if (useItemInfo.character.characterAnimator.GetCurrentAnimatorStateInfo(1).normalizedTime > 0.9)
                {
                    Debug.Log("LeftHand finish animation");
                    break;
                }                
            }
            await Awaitable.NextFrameAsync();
        }
        useItemInfo.character.characterAnimator.SetFloat(GetHandLayer(useItemInfo.characterItem.itemBaseSO.animationValueName, useItemInfo.isFastItem), 0);
    }
}
