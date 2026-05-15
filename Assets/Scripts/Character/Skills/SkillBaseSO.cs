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
    public GameObject skillVFXPrefab;
    public float skillVFXDuration = 1f;
    public SerializedDictionary<int, SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>> statistics = new SerializedDictionary<int, SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>>();
    public ItemBaseSO.TypeWeapon weaponForUseSkill;
    public virtual bool UseSkill(CharacterBase characterMakeSkill, CharacterBase characterToMakeSkill, int skillIndex) { Debug.LogError("UseSkill non implemented"); return false; }
    public virtual void LevelUpSkill(CharacterBase character, int skillIndex) { Debug.LogError("LevelUpSkill non implemented"); }
    public bool ValidateCanUseSkill(CharacterBase character, int characterIndex, int skillLevel)
    {
        if (statistics.Count == 0 || !statistics[skillLevel].ContainsKey(CharacterData.TypeStatistic.Sp)) return true;
        else if (
            character.charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Sp].currentValue - 
            statistics[skillLevel][CharacterData.TypeStatistic.Sp].baseValue >= 0
            ) return true;
        return false;
    }
    public enum TypeSkill
    {
        Attack,
        Heal,
        Buff,
        Debuff
    }
}