using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class SkillsBaseSO : ScriptableObject
{
    public string skillId;
    public string skillIdText;
    public string animationSkillName;
    public string generalAnimationSkillName;
    public TypeSkill typeSkill;
    public bool needSceneAnimation;
    public GameObject skillVFXPrefab;
    public float skillVFXDuration = 1f;
    public GameObject floatingTextPrefab;
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> statistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>();
    public ItemBaseSO.TypeWeapon weaponForUseSkill;
    public Vector3Int[] positionsToMakeSkill;
    public int skillRadius = 5;
    [Tooltip("Si se usan posiciones fijas para usar la habilidad")]
    public bool usePositionsToMakeSkill;
    public Vector3Int[] positionsSkillForm;
    public int skillInnerRadius = 5;
    [Tooltip("Permite mover el cursor de forma libre para seleccionar la posicion donde usar la habilidad")]
    public bool isFreeMovementSkill;
    public bool needCharacterToMakeSkill;
    public virtual void UseSkill(CharacterBase characterMakeSkill, CharacterBase characterToMakeSkill) { Debug.LogError("UseSkill non implemented"); }
    public virtual void DiscountMpAfterUseSkill(CharacterBase characterMakeSkill) { Debug.LogError("DiscountMpAfterUseSkill non implemented"); }
    public virtual void LevelUpSkill(CharacterBase character) { Debug.LogError("LevelUpSkill non implemented"); }
    public void AddSkill(CharacterBase character, int characterIndex)
    {
        if (character.charactersData[characterIndex].characterData.skills.ContainsKey(weaponForUseSkill))
        {
            if (!character.charactersData[characterIndex].characterData.skills[weaponForUseSkill].ContainsKey(skillId))
            {
                character.charactersData[characterIndex].characterData.skills[weaponForUseSkill].Add(skillId, new CharacterData.CharacterSkillInfo{skillId = skillId, skillsBaseSO = this, level = 0, statistics = CloneStatistics() });
            }
        }
        else
        {
            character.charactersData[characterIndex].characterData.skills.Add(weaponForUseSkill, new SerializedDictionary<string, CharacterData.CharacterSkillInfo>()
            {
                {skillId, new CharacterData.CharacterSkillInfo { skillId = skillId, skillsBaseSO = this, level = 0, statistics = CloneStatistics() }}
            });
        }
    }
    public bool ValidateCanUseSkill(CharacterBase character, int characterIndex)
    {
        return character.charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Sp].currentValue - character.charactersData[characterIndex].characterData.skills[weaponForUseSkill][skillId].statistics[CharacterData.TypeStatistic.Sp].baseValue > 0;
    }
    public string[] GetSkillDescription (SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> statistics)
    {
        List<string> info = new List<string>();

        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in statistics)
        {
            if (statistic.Key != CharacterData.TypeStatistic.Exp)
            {
                info.Add($"{statistic.Value.baseValue}%");
            }
        }

        return info.ToArray();
    }
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> CloneStatistics()
    {
        var clone = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>();

        foreach (var kvp in statistics)
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
    public enum TypeSkill
    {
        Attack,
        Heal,
        Buff,
        Debuff
    }
}