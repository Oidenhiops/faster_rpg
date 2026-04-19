using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class SkillsBaseSO : ScriptableObject
{
    public int skillId;
    public int skillIdText;
    public Sprite icon;
    public string animationSkillName;
    public string generalAnimationSkillName;
    public TypeSkill typeSkill;
    public bool needSceneAnimation;
    public GameObject skillVFXPrefab;
    public float skillVFXDuration = 1f;
    public SerializedDictionary<int, SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>> statistics = new SerializedDictionary<int, SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>>();
    public ItemBaseSO.TypeWeapon weaponForUseSkill;
    public virtual void UseSkill(CharacterBase characterMakeSkill, CharacterBase characterToMakeSkill) { Debug.LogError("UseSkill non implemented"); }
    public virtual void DiscountMpAfterUseSkill(CharacterBase characterMakeSkill) { Debug.LogError("DiscountMpAfterUseSkill non implemented"); }
    public virtual void LevelUpSkill(CharacterBase character) { Debug.LogError("LevelUpSkill non implemented"); }
    public void AddSkill(CharacterBase character, int characterIndex)
    {

    }
    public bool ValidateCanUseSkill(CharacterBase character, int characterIndex, int skillIndex)
    {
        return character.charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Sp].currentValue - character.charactersData[characterIndex].characterData.skills[skillIndex].skillsBaseSO.statistics[skillIndex][CharacterData.TypeStatistic.Sp].baseValue >= 0;
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
    public enum TypeSkill
    {
        Attack,
        Heal,
        Buff,
        Debuff
    }
}