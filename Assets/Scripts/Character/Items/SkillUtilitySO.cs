using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "SkillUtilityItem", menuName = "ScriptableObjects/Items/SkillUtilityItem", order = 1)]
public class SkillUtilitySO : ItemBaseSO
{
    public SkillsBaseSO skillsBaseSO;
    public override void EquipItem(CharacterData characterData, CharacterData.CharacterItem characterItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in itemStatistics)
        {
            if (characterData.statistics.ContainsKey(statistic.Key))
            {
                characterData.statistics[statistic.Key].itemValue += statistic.Value.baseValue;
                characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        characterData.skills[0].skillsBaseSO = skillsBaseSO;
        characterData.skills[0].skillId = skillsBaseSO.skillId;
    }
    public override void DesEquipItem(CharacterData characterData, CharacterData.CharacterItem characterItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in itemStatistics)
        {
            if (characterData.statistics.ContainsKey(statistic.Key))
            {
                characterData.statistics[statistic.Key].itemValue -= statistic.Value.baseValue;
                characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        characterData.skills[0] = new CharacterData.CharacterSkillInfo();
    }
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
        character.charactersData[character.characterIndex].characterData.skills[0].skillsBaseSO = skillsBaseSO;
        character.charactersData[character.characterIndex].characterData.skills[0].skillId = skillsBaseSO.skillId;
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
        character.charactersData[character.characterIndex].characterData.skills[0] = new CharacterData.CharacterSkillInfo();
    }
}
