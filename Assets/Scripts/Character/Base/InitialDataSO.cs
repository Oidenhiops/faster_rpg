using AYellowpaper.SerializedCollections;
using UnityEngine;
[CreateAssetMenu(fileName = "InitialData", menuName = "ScriptableObjects/Character/InitialDataSO", order = 1)]
public class InitialDataSO : ScriptableObject
{
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> initialStats = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>
    {
        {CharacterData.TypeStatistic.Hp, new CharacterData.Statistic{ aptitudeValue = 100 }},
        {CharacterData.TypeStatistic.Sp, new CharacterData.Statistic{ aptitudeValue = 100 }},
        {CharacterData.TypeStatistic.Atk, new CharacterData.Statistic{ aptitudeValue = 100 }},
        {CharacterData.TypeStatistic.Int, new CharacterData.Statistic{ aptitudeValue = 100 }},
        {CharacterData.TypeStatistic.Def, new CharacterData.Statistic{ aptitudeValue = 100 }},
        {CharacterData.TypeStatistic.Res, new CharacterData.Statistic{ aptitudeValue = 100 }},
        {CharacterData.TypeStatistic.Spd, new CharacterData.Statistic{ aptitudeValue = 100 }},
        {CharacterData.TypeStatistic.Exp, new CharacterData.Statistic{ aptitudeValue = 100 }},
        {CharacterData.TypeStatistic.Crtv, new CharacterData.Statistic{ aptitudeValue = 100 }},
        {CharacterData.TypeStatistic.Crtd, new CharacterData.Statistic{ aptitudeValue = 100 }},
    };
    public SerializedDictionary<int, CharacterData.CharacterSkillInfo> initialSkills = new SerializedDictionary<int, CharacterData.CharacterSkillInfo>();
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> CloneStatistics()
    {
        var clone = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>();

        foreach (var kvp in initialStats)
        {
            clone[kvp.Key] = new CharacterData.Statistic
            {
                baseValue = kvp.Value.baseValue,
                aptitudeValue = kvp.Value.aptitudeValue,
                itemValue = kvp.Value.itemValue,
                buffValue = kvp.Value.buffValue,
                maxValue = kvp.Value.maxValue,
                currentValue = kvp.Value.currentValue
            };
        }

        return clone;
    }
    public SerializedDictionary<int, CharacterData.CharacterSkillInfo> CloneSkills()
    {
        var skills = new SerializedDictionary<int, CharacterData.CharacterSkillInfo>();
        foreach (var skillKvp in initialSkills)
        {
            skills.Add(skillKvp.Key, new CharacterData.CharacterSkillInfo
            {
                skillId = skillKvp.Value.skillsBaseSO.skillId,
                skillsBaseSO = skillKvp.Value.skillsBaseSO,
                level = skillKvp.Value.level,
            });
        }
        return skills;
    }
}