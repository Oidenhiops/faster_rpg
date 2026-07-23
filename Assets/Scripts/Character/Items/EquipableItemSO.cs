using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipableItem", menuName = "ScriptableObjects/Items/EquipableItem", order = 1)]
public class EquipableItemSO : ItemBaseSO
{
    public override async Awaitable EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel,  bool isFastItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in characterItem.itemStatistics)
        {
            if (character.charactersData[character.characterIndex].statistics.ContainsKey(statistic.Key))
            {
                character.charactersData[character.characterIndex].statistics[statistic.Key].itemValue += statistic.Value.baseValue;
                character.charactersData[character.characterIndex].statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        EquipModelItem(character, characterItem, true, isFastItem);
    }
    public override async Awaitable DesEquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel,  bool isFastItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in characterItem.itemStatistics)
        {
            if (character.charactersData[character.characterIndex].statistics.ContainsKey(statistic.Key))
            {
                character.charactersData[character.characterIndex].statistics[statistic.Key].itemValue -= statistic.Value.baseValue;
                character.charactersData[character.characterIndex].statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        DesEquipModelItem(character, characterItem, true, isFastItem);
    }
}
