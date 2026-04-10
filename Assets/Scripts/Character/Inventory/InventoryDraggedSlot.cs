using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryDraggedSlot : MonoBehaviour
{
    public Image itemImage;
    public TMP_Text itemAmount;
    public InventorySlot itemDraged;
    public RectTransform rectTransform;

    public void InitializeDraggedSlot(CharacterData.CharacterItem item)
    {
        itemImage.sprite = item.itemBaseSO.icon;
        itemImage.enabled = true;
        itemAmount.enabled = true;
        itemAmount.text = item.amount > 1 ? item.amount.ToString() : "";
    }
}
