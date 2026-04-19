using System;
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
    public SerializedDictionary<int, skillInfo> statistics = new SerializedDictionary<int, skillInfo>();
    public ItemBaseSO.TypeWeapon weaponForUseSkill;
    public virtual void UseSkill(CharacterBase characterMakeSkill, CharacterBase characterToMakeSkill, int skillIndex) { Debug.LogError("UseSkill non implemented"); }
    public virtual void DiscountMpAfterUseSkill(CharacterBase characterMakeSkill) { Debug.LogError("DiscountMpAfterUseSkill non implemented"); }
    public virtual void LevelUpSkill(CharacterBase character, int skillIndex) { Debug.LogError("LevelUpSkill non implemented"); }
    public void AddSkill(CharacterBase character, int characterIndex)
    {

    }
    public bool ValidateCanUseSkill(CharacterBase character, int characterIndex, int skillIndex)
    {
        return 
            character.charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Sp].currentValue - 
            character.charactersData[characterIndex].characterData.skills[skillIndex].skillsBaseSO.statistics[skillIndex].statistics[CharacterData.TypeStatistic.Sp].baseValue >= 0;
    }
    public enum TypeSkill
    {
        Attack,
        Heal,
        Buff,
        Debuff
    }
    [Serializable]
    public class skillInfo
    {
        public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> statistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>();
        public float cd;
    }
}