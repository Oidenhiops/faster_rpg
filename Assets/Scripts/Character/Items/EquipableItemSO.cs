using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipableItem", menuName = "ScriptableObjects/Items/EquipableItem", order = 1)]
public class EquipableItemSO : ItemBaseSO
{
    public override async Awaitable EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in characterItem.itemStatistics)
        {
            if (character.charactersData[character.characterIndex].characterData.statistics.ContainsKey(statistic.Key))
            {
                character.charactersData[character.characterIndex].characterData.statistics[statistic.Key].itemValue += statistic.Value.baseValue;
                character.charactersData[character.characterIndex].characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
    }
    public override async Awaitable DesEquipItem(CharacterBase character, CharacterData.CharacterItem characterItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in characterItem.itemStatistics)
        {
            if (character.charactersData[character.characterIndex].characterData.statistics.ContainsKey(statistic.Key))
            {
                character.charactersData[character.characterIndex].characterData.statistics[statistic.Key].itemValue -= statistic.Value.baseValue;
                character.charactersData[character.characterIndex].characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
    }

    public void UseEquipableItem(CharacterBase character, CharacterData.CharacterItem characterItem)
    {
        throw new System.NotImplementedException();
    }
}
