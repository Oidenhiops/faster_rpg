using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemsDataDB", menuName = "ScriptableObjects/DB/ItemsDataDB", order = 1)]
public class ItemsDBSO : ScriptableObject
{
    public SerializedDictionary<ItemBaseSO.TypeObject, SerializedDictionary<int, ItemBaseSO>> data = new SerializedDictionary<ItemBaseSO.TypeObject, SerializedDictionary<int, ItemBaseSO>>();
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
    public CharacterData.CharacterItem GenerateItem(ItemBaseSO.TypeObject typeObject, int id)
    {
        if (data.ContainsKey(typeObject) && data[typeObject].ContainsKey(id))
        {
            return new CharacterData.CharacterItem
            {
                itemId = data[typeObject][id].id,
                typeObject = typeObject,
                itemBaseSO = data[typeObject][id],
                itemStatistics = data[typeObject][id].itemStatistics
            };
        }
        Debug.LogError($"Item with TypeObject: {typeObject} and ID: {id} not found in ItemsDBSO.");
        return null;
    }
}
