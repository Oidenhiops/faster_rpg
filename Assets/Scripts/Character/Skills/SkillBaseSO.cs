using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class SkillsBaseSO : ScriptableObject
{
    public int skillId;
    public int skillIdText;
    public Sprite icon;
    public string animationSkillName;
    public string generalAnimationSkillName;
    public GameObject skillVFXPrefab;
    public float skillVFXDuration = 1f;
    public CanalizationEffect canalizationEffect;
    public SerializedDictionary<int, SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>> statistics = new SerializedDictionary<int, SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>>();
    public ItemBaseSO.TypeWeapon weaponForUseSkill;
    public virtual async Awaitable UseSkill(CharacterMakeSkillData characterMakeSkillData, CharacterBase characterToMakeSkill, int skillIndex) { Debug.LogError("UseSkill non implemented");}
    public virtual void LevelUpSkill(CharacterBase character, int skillIndex) { Debug.LogError("LevelUpSkill non implemented"); }
    public bool ValidateCanUseSkill(CharacterMakeSkillData characterMakeSkillData, int skillLevel)
    {
        if (statistics.Count == 0 || !statistics[skillLevel].ContainsKey(CharacterData.TypeStatistic.Sp)) return true;
        else if (
            characterMakeSkillData.characterMakeSkill.charactersData[characterMakeSkillData.characterMakeSkillIndex].statistics[CharacterData.TypeStatistic.Sp].currentValue - 
            statistics[skillLevel][CharacterData.TypeStatistic.Sp].baseValue >= 0
            ) return true;
        return false;
    }
    public class CharacterMakeSkillData
    {
        public CharacterBase characterMakeSkill;
        public int characterMakeSkillIndex;
        public CharacterMakeSkillData(CharacterBase characterMakeSkill, int characterMakeSkillIndex)
        {
            this.characterMakeSkill = characterMakeSkill;
            this.characterMakeSkillIndex = characterMakeSkillIndex;
        }
    }
    public enum TypeSkill
    {
        Attack,
        Heal,
        Buff,
        Debuff
    }
}