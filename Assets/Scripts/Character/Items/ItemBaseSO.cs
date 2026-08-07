using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;
[CreateAssetMenu(fileName = "BaseItem", menuName = "ScriptableObjects/Items/BaseItem", order = 1)]
public class ItemBaseSO : ScriptableObject
{
    public int id;
    public string idText;
    public ItemModelInfo modelInfo;
    public Sprite icon;
    public ItemsDBSO.TypeModel typeObject;
    public StatusEffectBaseSO canalizationEffect;
    public float costPerUse;
    public int animationValue;
    public bool useEnergy;
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> itemStatistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>();
    public virtual async Awaitable EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel, bool isFastItem) { Debug.LogError("EquipItem not implemented"); }
    public virtual void EquipModelItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel, bool isFastItem)
    {
        character.characterData.models[typeObject].itemId = id;
        character.characterData.models[typeObject].colors = new List<Color>(modelInfo.colors);
        foreach (ItemsDBSO.TypeModel occludedModel in modelInfo.occludedModels)
        {
            character.characterData.models[occludedModel].occlude = true;
        }
        if (refreshModel)
        {
            character.RefreshCharacterItemModel(characterItem, true, !isFastItem ? ItemsDBSO.TypeModel.None : ItemsDBSO.TypeModel.FastItems);
        }
    }
    public virtual async Awaitable DesEquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel,  bool isFastItem) { Debug.LogError("DesEquipItem not implemented"); }
    public void DesEquipModelItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel, bool isFastItem)
    {
        character.characterData.models[typeObject].itemId = 0;
        character.characterData.models[typeObject].colors = new List<Color>();
        foreach (ItemsDBSO.TypeModel occludedModel in modelInfo.occludedModels)
        {
            character.characterData.models[occludedModel].occlude = false;
        }
        if (refreshModel)
        {
            character.RefreshCharacterItemModel(characterItem, false, !isFastItem ? ItemsDBSO.TypeModel.None : ItemsDBSO.TypeModel.FastItems);
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
        characterData.models[typeObject].itemId = id;
        characterData.models[typeObject].colors = new List<Color>(modelInfo.colors);
        foreach (ItemsDBSO.TypeModel occludedModel in modelInfo.occludedModels)
        {
            characterData.models[occludedModel].occlude = true;
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
        characterData.models[typeObject].itemId = 0;
        foreach (ItemsDBSO.TypeModel occludedModel in modelInfo.occludedModels)
        {
            characterData.models[occludedModel].occlude = false;
        }
    }
    public virtual async Awaitable UseItem(UseItemInfo useItemInfo) { Debug.LogError("UseItem not implemented"); }
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
    public string GetHandLayer(bool isFastItem)
    {
        return !isFastItem ? "RightHand" : "LeftHand";
    }
    [Serializable]
    public class ItemModelInfo
    {
        public List<Color> colors;
        public bool useTexture;
        public List<Sprite> textures;
        public List<Mesh> originalMesh;
        public List<ItemsDBSO.TypeModel> occludedModels = new List<ItemsDBSO.TypeModel>();
    }
    [Serializable]
    public class UseItemInfo
    {
        public CharacterBase character;
        public CharacterData.CharacterItem characterItem;
        public bool isFastItem;
        public RaycastHit hit;
        public UseItemInfo(CharacterBase character, CharacterData.CharacterItem characterItem, bool isFastItem = false, RaycastHit hit = new RaycastHit())
        {
            this.character = character;
            this.characterItem = characterItem;
            this.isFastItem = isFastItem;
            this.hit = hit;
        }
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
        Monster = 7,
        Pickaxe = 8,
        Drill = 9,
        Shovel = 10,
        Hammer = 12,
        Hoe = 11,
        FishingRod = 13
    }
}
