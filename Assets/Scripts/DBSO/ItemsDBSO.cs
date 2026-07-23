using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemsDataDB", menuName = "ScriptableObjects/DB/ItemsDataDB", order = 1)]
public class ItemsDBSO : ScriptableObject
{
    public SerializedDictionary<TypeModel, SerializedDictionary<int, ItemBaseSO>> data = new SerializedDictionary<TypeModel, SerializedDictionary<int, ItemBaseSO>>();
    public ItemBaseSO[] itemsToAdd;
    [NaughtyAttributes.Button]
    public void AddNewItems()
    {
        for (int i = 0; i < itemsToAdd.Length; i++)
        {
            data[itemsToAdd[i].typeObject].Add(itemsToAdd[i].id, itemsToAdd[i]);
        }
        itemsToAdd = new ItemBaseSO[0];
    }
    [NaughtyAttributes.Button]
    public void SortItems()
    {
        foreach (var itemType in data.ToList())
        {
            data[itemType.Key] = new SerializedDictionary<int, ItemBaseSO>(
                itemType.Value.OrderBy(kvp => kvp.Key)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            );
        }
    }
    public CharacterData.CharacterItem GenerateItem(TypeModel typeObject, int id, int amountItems = 1)
    {
        if (data.ContainsKey(typeObject) && data[typeObject].ContainsKey(id))
        {
            SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> newStatistics = data[typeObject][id].CloneStatistics();
            newStatistics[CharacterData.TypeStatistic.Amount].currentValue = amountItems;
            CharacterData.CharacterItem newItem = new CharacterData.CharacterItem
            {
                itemId = data[typeObject][id].id,
                typeObject = typeObject,
                itemBaseSO = data[typeObject][id],
                itemStatistics = newStatistics,
            };
            if (newItem.itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability))
            {
                newItem.itemStatistics[CharacterData.TypeStatistic.Durability].currentValue = newItem.itemStatistics[CharacterData.TypeStatistic.Durability].baseValue;
                newItem.itemStatistics[CharacterData.TypeStatistic.Durability].maxValue = newItem.itemStatistics[CharacterData.TypeStatistic.Durability].baseValue;
            }
            return newItem;
        }
        Debug.LogError($"Item with TypeObject: {typeObject} and ID: {id} not found in ItemsDBSO.");
        return null;
    }
    public SerializedDictionary<TypeModel, CharacterData.CharacterSkinInfo> GenerateRandomModel()
    {
        SerializedDictionary<TypeModel, CharacterData.CharacterSkinInfo> model = new SerializedDictionary<TypeModel, CharacterData.CharacterSkinInfo>();
        int hairIndex = Random.Range(1, data[TypeModel.Hair].Count + 1);
        model.Add(TypeModel.Hair, new CharacterData.CharacterSkinInfo
        {
            itemId = hairIndex,
            colors = new List<Color> { RandomColor(), RandomColor(), RandomColor(), RandomColor(), RandomColor() },
            typeObject = TypeModel.Hair
        });
        int headIndex = Random.Range(1, data[TypeModel.Head].Count + 1);
        Color skinColor = RandomColor();
        model.Add(TypeModel.Head, new CharacterData.CharacterSkinInfo
        {
            itemId = headIndex,
            colors = new List<Color> { skinColor },
            typeObject = TypeModel.Head
        });
        int eyesIndex = Random.Range(1, data[TypeModel.Eyes].Count + 1);
        model.Add(TypeModel.Eyes, new CharacterData.CharacterSkinInfo
        {
            itemId = eyesIndex,
            colors = new List<Color> { RandomColor(), RandomColor(), RandomColor() },
            typeObject = TypeModel.Eyes
        });
        int eyebrowsIndex = Random.Range(1, data[TypeModel.Eyebrows].Count + 1);
        model.Add(TypeModel.Eyebrows, new CharacterData.CharacterSkinInfo
        {
            itemId = eyebrowsIndex,
            colors = new List<Color> { RandomColor() },
            typeObject = TypeModel.Eyebrows
        });
        int earsIndex = Random.Range(1, data[TypeModel.Ears].Count + 1);
        model.Add(TypeModel.Ears, new CharacterData.CharacterSkinInfo
        {
            itemId = earsIndex,
            colors = new List<Color> { skinColor },
            typeObject = TypeModel.Ears
        });
        int bodyIndex = Random.Range(1, data[TypeModel.Body].Count + 1);
        model.Add(TypeModel.Body, new CharacterData.CharacterSkinInfo
        {
            itemId = bodyIndex,
            colors = new List<Color> { skinColor },
            typeObject = TypeModel.Body
        });
        int handsIndex = Random.Range(1, data[TypeModel.Hands].Count + 1);
        model.Add(TypeModel.Hands, new CharacterData.CharacterSkinInfo
        {
            itemId = handsIndex,
            colors = new List<Color> { skinColor },
            typeObject = TypeModel.Hands
        });
        int feetIndex = Random.Range(1, data[TypeModel.Feets].Count + 1);
        model.Add(TypeModel.Feets, new CharacterData.CharacterSkinInfo
        {
            itemId = feetIndex,
            colors = new List<Color> { skinColor },
            typeObject = TypeModel.Feets
        });
        model.Add(TypeModel.Helmet, new CharacterData.CharacterSkinInfo
        {
            itemId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
            typeObject = TypeModel.Helmet
        });
        model.Add(TypeModel.Front, new CharacterData.CharacterSkinInfo
        {
            itemId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
            typeObject = TypeModel.Front
        });
        model.Add(TypeModel.Pants, new CharacterData.CharacterSkinInfo
        {
            itemId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
            typeObject = TypeModel.Pants
        });
        model.Add(TypeModel.Boots, new CharacterData.CharacterSkinInfo
        {
            itemId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
            typeObject = TypeModel.Boots
        });
        model.Add(TypeModel.Gloves, new CharacterData.CharacterSkinInfo
        {
            itemId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
            typeObject = TypeModel.Gloves
        });
        model.Add(TypeModel.Pendant, new CharacterData.CharacterSkinInfo
        {
            itemId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
            typeObject = TypeModel.Pendant
        });
        model.Add(TypeModel.Ring, new CharacterData.CharacterSkinInfo
        {
            itemId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
            typeObject = TypeModel.Ring
        });
        model.Add(TypeModel.Weapon, new CharacterData.CharacterSkinInfo
        {
            itemId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
            typeObject = TypeModel.Weapon
        });
        model.Add(TypeModel.FastItems, new CharacterData.CharacterSkinInfo
        {
            itemId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
            typeObject = TypeModel.FastItems
        });
        return model;
    }
    Color RandomColor()
    {
        return Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f, 1f, 1f);
    }
    public enum TypeModel
    {
        None = 0,
        Hair = 1,
        Head = 2,
        Eyes = 3,
        Eyebrows = 4,
        Ears = 5,
        Body = 6,
        Hands = 7,
        Feets = 8,
        Helmet = 9,
        Front = 10,
        Pants = 11,
        Boots = 12,
        Gloves = 13,
        Pendant = 14,
        Ring = 15,
        Weapon = 16,
        FastItems = 17,
        Bag = 18
    }
}
