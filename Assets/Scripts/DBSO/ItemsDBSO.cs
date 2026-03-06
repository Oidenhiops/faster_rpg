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
            data[ItemBaseSO.TypeObject.None].Add(itemsToAdd[i].id, itemsToAdd[i]);
            data[itemsToAdd[i].typeObject].Add(itemsToAdd[i].id, itemsToAdd[i]);
        }
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
}
