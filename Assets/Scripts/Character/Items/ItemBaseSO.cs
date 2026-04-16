using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class ItemBaseSO : ScriptableObject
{
    public int id;
    public string idText;
    public Sprite icon;
    public GeneralTypeObject generalTypeObject;
    public TypeObject typeObject;
    public TypeWeapon typeWeapon;
    public string animationName;
    public int maxStack;
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> itemStatistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>();
    public virtual void EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem) { Debug.LogError("EquipItem not implemented"); }
    public virtual void EquipItem(CharacterData characterData, CharacterData.CharacterItem characterItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in itemStatistics)
        {
            if (characterData.statistics.ContainsKey(statistic.Key))
            {
                characterData.statistics[statistic.Key].itemValue += statistic.Value.baseValue;
                characterData.statistics[statistic.Key].RefreshValue();
            }
        }
    }
    public virtual void DesEquipItem(CharacterBase character, CharacterData.CharacterItem characterItem) { Debug.LogError("DesEquipItem not implemented"); }
    public virtual void DesEquipItem(CharacterData characterData, CharacterData.CharacterItem characterItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in itemStatistics)
        {
            if (characterData.statistics.ContainsKey(statistic.Key))
            {
                characterData.statistics[statistic.Key].itemValue -= statistic.Value.baseValue;
                characterData.statistics[statistic.Key].RefreshValue();
            }
        }
    }
    public virtual void UseItem(CharacterBase character, CharacterData.CharacterItem characterItem) { Debug.LogError("UseItem not implemented"); }
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> CloneStatistics()
    {
        var clone = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>();

        foreach (var kvp in itemStatistics)
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
    public enum GeneralTypeObject
    {
        None = 0,
        Equipment = 1,
        Consumables = 2
    }
    public enum TypeObject
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
        Consumable = 10,
    }
    public enum TypeWeapon
    {
        None = 0,
        Fist = 1,
        Sword = 2,
        Spear = 3,
        Bow = 4,
        Axe = 5,
        Staff = 6,
        Monster = 7
    }
}
