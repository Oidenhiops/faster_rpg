using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[Serializable]
public class CharacterData
{
    public int level;
    public string name;
    public SerializedDictionary<TypeStatistic, Statistic> statistics = new SerializedDictionary<TypeStatistic, Statistic>();
    public SerializedDictionary<TypeCharacterItem, CharacterItem> items = new SerializedDictionary<TypeCharacterItem, CharacterItem>
    {
        {TypeCharacterItem.Helmet, null},
        {TypeCharacterItem.Front, null},
        {TypeCharacterItem.Pants, null},
        {TypeCharacterItem.Boots, null},
        {TypeCharacterItem.Gloves, null},
        {TypeCharacterItem.Pendant, null},
        {TypeCharacterItem.Ring, null},
        {TypeCharacterItem.Weapon, null},
        {TypeCharacterItem.Utility, null},
        {TypeCharacterItem.Object1, null},
        {TypeCharacterItem.Object2, null},
        {TypeCharacterItem.Object3, null},
    };
    public SerializedDictionary<int, CharacterItem> bag = new SerializedDictionary<int, CharacterItem>();
    public SerializedDictionary<ItemBaseSO.TypeWeapon, SerializedDictionary<string, CharacterSkillInfo>> skills = new SerializedDictionary<ItemBaseSO.TypeWeapon, SerializedDictionary<string, CharacterSkillInfo>>();
    public int characterId;
    public int characterSkinId;
    public void InitializeStatistics()
    {
        foreach (KeyValuePair<TypeStatistic, Statistic> statistic in statistics)
        {
            statistic.Value.RefreshValue();
            statistic.Value.SetMaxValue();
        }
    }
    public void LevelUp()
    {
        level++;

        foreach (KeyValuePair<TypeStatistic, Statistic> statistic in statistics)
        {
            if (statistic.Key != TypeStatistic.Exp && statistic.Key != TypeStatistic.Crtv && statistic.Key != TypeStatistic.Crtd)
            {
                statistic.Value.baseValue = Mathf.CeilToInt(statistic.Value.baseValue * (1.25f * statistic.Value.aptitudeValue / 100));
                statistic.Value.RefreshValue();
                if (statistic.Key != TypeStatistic.Hp && statistic.Key != TypeStatistic.Sp)
                {
                    statistic.Value.SetMaxValue();
                }
            }
        }
    }
    public bool GetCurrentWeapon(out CharacterItem weapon)
    {
        if (items.TryGetValue(TypeCharacterItem.Weapon, out CharacterItem currentWeapon) && currentWeapon != null)
        {
            weapon = currentWeapon;
            return true;
        }
        weapon = null;
        return false;
    }
    [Serializable]
    public class Statistic
    {
        public int baseValue = 0;
        public int aptitudeValue = 0;
        public int itemValue = 0;
        public SerializedDictionary<StatusEffectBaseSO, int> buffValue = new SerializedDictionary<StatusEffectBaseSO, int>();
        public int currentValue = 0;
        public int maxValue = 0;
        public void RefreshValue()
        {
            int baseWhitItem = baseValue + itemValue;
            int totalBuffValue = 0;
            foreach (KeyValuePair<StatusEffectBaseSO, int> buff in buffValue) totalBuffValue += buff.Value;
            int baseWhitBuff = baseValue * totalBuffValue / 100;
            int finalValue = Mathf.CeilToInt(baseWhitItem + baseWhitBuff);
            int whitAptitude = Mathf.CeilToInt(finalValue * (aptitudeValue / 100f));
            maxValue = Mathf.Clamp(whitAptitude, 1, 99999);
            if (currentValue > maxValue) currentValue = maxValue;
        }
        public void SetMaxValue()
        {
            currentValue = maxValue;
        }
    }
    [Serializable]
    public class CharacterItem
    {
        public int itemId;
        public ItemBaseSO itemBaseSO;
        public SerializedDictionary<TypeStatistic, Statistic> itemStatistics = new SerializedDictionary<TypeStatistic, Statistic>();
    }
    [Serializable]
    public class CharacterMasteryInfo
    {
        public MasteryRange masteryRange;
        public int masteryLevel;
        public int currentExp;
        public int maxExp;
    }
    [Serializable]
    public class CharacterSkillInfo
    {
        public string skillId;
        public SkillsBaseSO skillsBaseSO;
        public int level;
        public SerializedDictionary<TypeStatistic, Statistic> statistics = new SerializedDictionary<TypeStatistic, Statistic>();
    }
    public enum MasteryRange
    {
        N = 0,
        F = 1,
        E = 2,
        D = 3,
        C = 4,
        B = 5,
        A = 6,
        S = 7
    }
    public enum TypeCharacterItem
    {
        None = 0,
        Helmet = 1,
        Front = 2,
        Pants = 3,
        Boots = 4,
        Gloves = 5,
        Pendant = 6,
        Ring = 7,
        Weapon = 8,
        Utility = 9,
        Object1 = 10,
        Object2 = 11,
        Object3 = 12,
    }
    public enum TypeStatistic
    {
        None = 0,
        Hp = 1,
        Sp = 2,
        Atk = 3,
        Hit = 4,
        Int = 5,
        Def = 6,
        Res = 7,
        Spd = 8,
        Exp = 9,
        Crtv = 10,
        Crtd = 11,
        BagSpace = 12
    }
}