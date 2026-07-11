using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryDraggedSlot : MonoBehaviour
{
    public Image itemImage;
    public TMP_Text itemAmount;
    public TMP_Text itemDurability;
    public InventorySlot itemDraged;
    public RectTransform rectTransform;

    public void InitializeDraggedSlot(CharacterData.CharacterItem item)
    {
        itemImage.sprite = item.itemBaseSO.icon;
        itemImage.enabled = true;
        itemAmount.enabled = true;
        itemAmount.text = item.amount > 1 ? item.amount.ToString() : "";
        itemDurability.enabled = true;
        if (item.itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability))
        {
            itemDurability.enabled = true;
            itemDurability.text = item.itemStatistics[CharacterData.TypeStatistic.Durability].currentValue.ToString("F0");
            itemDurability.color =
                GameData.Instance.utils.systemColors.TryGetValue(
                    item.itemStatistics[CharacterData.TypeStatistic.Durability].currentValue > 0 ?
                    item.itemBaseSO.useEnergy ? "Energy" : "Durability" : "Broken", out Color durabilityColor) ? durabilityColor : Color.white;
        }
        else
        {
            itemDurability.enabled = false;
            itemDurability.text = "";
        }
    }
}
