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
    public SerializedDictionary<CharactersModelDBSO.TypeModel, CharacterItem> equipments = new SerializedDictionary<CharactersModelDBSO.TypeModel, CharacterItem>
    {
        {CharactersModelDBSO.TypeModel.Helmet, null},
        {CharactersModelDBSO.TypeModel.Front, null},
        {CharactersModelDBSO.TypeModel.Pants, null},
        {CharactersModelDBSO.TypeModel.Boots, null},
        {CharactersModelDBSO.TypeModel.Gloves, null},
        {CharactersModelDBSO.TypeModel.Pendant, null},
        {CharactersModelDBSO.TypeModel.Ring, null},
        {CharactersModelDBSO.TypeModel.Weapon, null},
    };
    public SerializedDictionary<int, CharacterItem> fastItems = new SerializedDictionary<int, CharacterItem>();
    public SerializedDictionary<int, CharacterItem> bag = new SerializedDictionary<int, CharacterItem>();
    public SerializedDictionary<int, CharacterSkillInfo> skills = new SerializedDictionary<int, CharacterSkillInfo>
    {
        {0, new CharacterSkillInfo()},
        {1, new CharacterSkillInfo()},
        {2, new CharacterSkillInfo()},
        {3, new CharacterSkillInfo()},
        {4, new CharacterSkillInfo()},
    };
    public SerializedDictionary<CharactersModelDBSO.TypeModel, CharacterSkinInfo> models = new SerializedDictionary<CharactersModelDBSO.TypeModel, CharacterSkinInfo>
    {
        {CharactersModelDBSO.TypeModel.Hair, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Head, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Eyes, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Eyebrows, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Ears, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Body, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Hands, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Feets, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Helmet, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Front, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Pants, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Boots, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Gloves, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Pendant, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Ring, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.Weapon, new CharacterSkinInfo()},
        {CharactersModelDBSO.TypeModel.FastItems, new CharacterSkinInfo()},
    };
    public void InitializeStatistics()
    {
        foreach (KeyValuePair<TypeStatistic, Statistic> statistic in statistics)
        {
            statistic.Value.RefreshValue((int)statistic.Key);
            statistic.Value.SetMaxValue();
        }
    }
    public void InitializeItems()
    {
        foreach (KeyValuePair<CharactersModelDBSO.TypeModel, CharacterItem> item in equipments)
        {
            if (item.Value.itemId != 0)
            {
                item.Value.itemBaseSO = GameData.Instance.itemsDBSO.data[item.Value.typeObject][item.Value.itemId];
            }
        }
    }
    public void LevelUp()
    {
        level++;

        foreach (KeyValuePair<TypeStatistic, Statistic> statistic in statistics)
        {
            if (statistic.Key != TypeStatistic.Exp && statistic.Key != TypeStatistic.Crtv && statistic.Key != TypeStatistic.Crtd && statistic.Key != TypeStatistic.Durability)
            {
                statistic.Value.baseValue = Mathf.CeilToInt(statistic.Value.baseValue * (1.25f * statistic.Value.aptitudeValue / 100));
                statistic.Value.RefreshValue((int)statistic.Key);
            }
        }
    }
    public bool GetCurrentWeapon(out CharacterItem weapon)
    {
        if (equipments.TryGetValue(CharactersModelDBSO.TypeModel.Weapon, out CharacterItem currentWeapon) && currentWeapon != null)
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
        public float baseValue = 0;
        public float aptitudeValue = 0;
        public float itemValue = 0;
        public SerializedDictionary<StatusEffectBaseSO, float> buffValue = new SerializedDictionary<StatusEffectBaseSO, float>();
        public float currentValue = 0;
        public float maxValue = 0;
        public void RefreshValue(int typeStatistic = 0)
        {
            float baseWhitItem = baseValue + itemValue;
            float totalBuffValue = 0;
            foreach (KeyValuePair<StatusEffectBaseSO, float> buff in buffValue) totalBuffValue += buff.Value;
            float baseWhitBuff = Mathf.CeilToInt(baseValue * totalBuffValue / 100);
            float finalValue = Mathf.CeilToInt(baseWhitItem + baseWhitBuff);
            float whitAptitude = Mathf.CeilToInt(finalValue * (aptitudeValue / 100f));
            maxValue = Mathf.Clamp(whitAptitude, 1, 99999);
            if (currentValue > maxValue) currentValue = maxValue;
            else if (typeStatistic != 0 && typeStatistic != 1 && typeStatistic != 2) currentValue = maxValue;
        }
        public void SetMaxValue()
        {
            currentValue = maxValue;
        }
        public bool IsValidStatCharacter(TypeStatistic typeStatistic)
        {
            return typeStatistic == TypeStatistic.Hp ||
                   typeStatistic == TypeStatistic.Sp ||
                   typeStatistic == TypeStatistic.Atk ||
                   typeStatistic == TypeStatistic.Hit ||
                   typeStatistic == TypeStatistic.Int ||
                   typeStatistic == TypeStatistic.Def ||
                   typeStatistic == TypeStatistic.Res ||
                   typeStatistic == TypeStatistic.Spd ||
                   typeStatistic == TypeStatistic.Crtv;
        }
    }
    [Serializable]
    public class CharacterItem
    {
        public int itemId;
        public CharactersModelDBSO.TypeModel typeObject;
        public ItemBaseSO itemBaseSO;
        public SerializedDictionary<TypeStatistic, Statistic> itemStatistics = new SerializedDictionary<TypeStatistic, Statistic>();
        public void ResetItem()
        {
            itemId = 0;
            typeObject = default;
            itemBaseSO = null;
            itemStatistics.Clear();
        }
        public CharacterItem()
        {
            itemId = 0;
            typeObject = default;
            itemBaseSO = null;
            itemStatistics.Clear();
        }
        public CharacterItem(CharacterItem characterItem)
        {
            itemId = characterItem.itemId;
            typeObject = characterItem.typeObject;
            itemBaseSO = characterItem.itemBaseSO;
            itemStatistics = characterItem.itemStatistics;
        }
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
        public int skillId;
        public SkillsBaseSO skillsBaseSO;
        public int level;
        public int cd;
    }
    [Serializable]
    public class CharacterSkinInfo
    {
        public int meshId;
        public List<Color> colors;
        public bool useTexture;
        public List<Sprite> textures;
        public List<Mesh> originalMesh;
        public bool occlude;
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
        BagSpace = 12,
        Cd = 13,
        JumpDistance = 14,
        DropDistance = 15,
        Durability = 16,
        Amount = 17,
    }
}