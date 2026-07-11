using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class ItemBaseSO : ScriptableObject
{
    public int id;
    public string idText;
    public ItemModelInfo modelInfo;
    public Sprite icon;
    public GeneralTypeObject generalTypeObject;
    public TypeObject typeObject;
    public TypeWeapon typeWeapon;
    public string animationName;
    public int maxStack;
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> itemStatistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>();
    public virtual async Awaitable EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel = false) { Debug.LogError("EquipItem not implemented"); }
    public virtual void EquipModelItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel = false)
    {
        if (modelInfo.meshId != 0)
        {
            character.charactersData[character.characterIndex].models[modelInfo.typeModel].meshId = modelInfo.meshId;
            character.charactersData[character.characterIndex].models[modelInfo.typeModel].colors = new List<Color>(modelInfo.colors);
            foreach (CharactersModelDBSO.TypeModel occludedModel in modelInfo.occludedModels)
            {
                character.charactersData[character.characterIndex].models[occludedModel].occlude = true;
            }
            if (refreshModel)
            {
                character.RefreshCharacterItemModel(characterItem, true);
            }
        }
    }
    public virtual async Awaitable DesEquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel = false) { Debug.LogError("DesEquipItem not implemented"); }
    public void DesEquipModelItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel = false)
    {
        if (modelInfo.meshId != 0)
        {
            character.charactersData[character.characterIndex].models[modelInfo.typeModel].meshId = 0;
            character.charactersData[character.characterIndex].models[modelInfo.typeModel].colors = new List<Color>();
            foreach (CharactersModelDBSO.TypeModel occludedModel in modelInfo.occludedModels)
            {
                character.charactersData[character.characterIndex].models[occludedModel].occlude = false;
            }
            if (refreshModel)
            {
                character.RefreshCharacterItemModel(characterItem, false);
            }
        }
    }
    public void EquipItem(CharacterData characterData, CharacterData.CharacterItem characterItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in itemStatistics)
        {
            if (characterData.statistics.ContainsKey(statistic.Key))
            {
                characterData.statistics[statistic.Key].itemValue += statistic.Value.baseValue;
                characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        EquipModelItem(characterData, characterItem);
    }
    public void EquipModelItem(CharacterData characterData, CharacterData.CharacterItem characterItem, bool refreshModel = false)
    {
        if (modelInfo.meshId != 0)
        {
            characterData.models[modelInfo.typeModel].meshId = modelInfo.meshId;
            characterData.models[modelInfo.typeModel].colors = new List<Color>(modelInfo.colors);
            foreach (CharactersModelDBSO.TypeModel occludedModel in modelInfo.occludedModels)
            {
                characterData.models[occludedModel].occlude = true;
            }
        }
    }
    public void DesEquipItem(CharacterData characterData, CharacterData.CharacterItem characterItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in itemStatistics)
        {
            if (characterData.statistics.ContainsKey(statistic.Key))
            {
                characterData.statistics[statistic.Key].itemValue -= statistic.Value.baseValue;
                characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        DesEquipModelItem(characterData, characterItem);
    }
    public virtual void DesEquipModelItem(CharacterData characterData, CharacterData.CharacterItem characterItem, bool refreshModel = false)
    {
        if (modelInfo.meshId != 0)
        {
            characterData.models[modelInfo.typeModel].meshId = 0;
            characterData.models[modelInfo.typeModel].colors = new List<Color>();
            foreach (CharactersModelDBSO.TypeModel occludedModel in modelInfo.occludedModels)
            {
                characterData.models[occludedModel].occlude = false;
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
        Consumable = 9,
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
    [Serializable]
    public class ItemModelInfo: CharacterData.CharacterSkinInfo
    {
        public CharactersModelDBSO.TypeModel typeModel;
        public List<CharactersModelDBSO.TypeModel> occludedModels = new List<CharactersModelDBSO.TypeModel>();
    }
}
