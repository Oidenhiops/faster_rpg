using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    public Image itemImage;
    public TypeInventorySlot typeInventorySlot;
    public void InitializeSlot(CharacterData.CharacterItem item)
    {
        if (item.itemBaseSO?.icon) itemImage.sprite = item.itemBaseSO.icon;
        else itemImage.enabled = false;
    }
    public enum TypeInventorySlot
    {
        None = 0,
        Pendant = 1,
        Gloves = 2,
        Ring = 3,
        Weapon = 4,
        Helmet = 5,
        Front = 6,
        Pants = 7,
        Boots = 9,
        Object1 = 10,
        Object2 = 11,
        Object3 = 12,
        Utility = 13,
        Bag = 14
    }
}
