using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryDraggedSlot : MonoBehaviour
{
    public Image itemImage;
    public GameObject itemAmountBg;
    public TMP_Text itemAmount;
    public Image itemDurability;
    public InventorySlot itemDraged;
    public RectTransform rectTransform;

    public void InitializeDraggedSlot(CharacterData.CharacterItem item)
    {
        itemImage.sprite = item.itemBaseSO.icon;
        itemImage.enabled = true;
        itemAmount.enabled = true;
        itemAmountBg.SetActive(item.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue > 1);
        itemAmount.text = item.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue.ToString();
        if (item.itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability))
        {
            float durabilityPorcent = item.itemStatistics[CharacterData.TypeStatistic.Durability].currentValue / item.itemStatistics[CharacterData.TypeStatistic.Durability].maxValue;
            itemDurability.enabled = true;
            itemDurability.fillAmount = durabilityPorcent > 0 ? durabilityPorcent : 1;
            if (durabilityPorcent >= 0.7f) itemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(item.itemBaseSO.useEnergy ? "EnergyGood" : "DurabilityGood", out Color durabilityColor) ? durabilityColor : Color.white;
            else if (durabilityPorcent < 0.7f && durabilityPorcent >= 0.3f) itemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(item.itemBaseSO.useEnergy ? "EnergyMedium" : "DurabilityMedium", out Color durabilityColor) ? durabilityColor : Color.white;
            else if (durabilityPorcent < 0.3f && durabilityPorcent > 0f) itemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(item.itemBaseSO.useEnergy ? "EnergyBad" : "DurabilityBad", out Color durabilityColor) ? durabilityColor : Color.white;
            else itemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(item.itemBaseSO.useEnergy ? "OutEnergy" : "OutDurability", out Color durabilityColor) ? durabilityColor : Color.white;
        }
        else
        {
            itemDurability.enabled = false;
        }
    }
}
