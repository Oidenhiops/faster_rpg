using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CharacterPlayerHud characterPlayerHud;
    public CanvasGroup canvasGroup;
    public Image itemSelect;
    public Image hasItem;
    public Image itemImage;
    public GameObject itemAmountBg;
    public TMP_Text itemAmount;
    public Image itemDurability;
    public ItemsDBSO.TypeModel typeInventorySlot;
    public CharacterData.CharacterItem characterItem;
    public int slotIndex;
    public void InitializeSlot(CharacterData.CharacterItem item)
    {
        if (item.itemBaseSO?.icon)
        {
            hasItem.enabled = true;            
            itemImage.sprite = item.itemBaseSO.icon;
            itemImage.enabled = true;
            itemAmount.enabled = true;
            itemAmountBg.SetActive(item.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue > 1);
            itemAmount.text = item.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue.ToString();
            canvasGroup.alpha = 1;
            if (item.itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability))
            {
                float durabilityPorcent = item.itemStatistics[CharacterData.TypeStatistic.Durability].currentValue / item.itemStatistics[CharacterData.TypeStatistic.Durability].maxValue;
                itemDurability.enabled = true;
                itemDurability.fillAmount = durabilityPorcent;
                if (durabilityPorcent >= 0.7f) itemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(item.itemBaseSO.useEnergy ? "EnergyGood" : "DurabilityGood", out Color durabilityColor) ? durabilityColor : Color.white;
                else if (durabilityPorcent < 0.7f && durabilityPorcent >= 0.3f) itemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(item.itemBaseSO.useEnergy ? "EnergyMedium" : "DurabilityMedium", out Color durabilityColor) ? durabilityColor : Color.white;
                else if (durabilityPorcent < 0.3f && durabilityPorcent > 0f) itemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(item.itemBaseSO.useEnergy ? "EnergyBad" : "DurabilityBad", out Color durabilityColor) ? durabilityColor : Color.white;
                else itemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(item.itemBaseSO.useEnergy ? "OutEnergy" : "OutDurability", out Color durabilityColor) ? durabilityColor : Color.white;
            }
            else
            {
                itemDurability.enabled = false;
            }
            characterItem = item;
        }
        else
        {
            itemAmountBg.SetActive(false);
            canvasGroup.alpha = 0.5f;
            hasItem.enabled = false;
            itemImage.sprite = null;
            itemImage.enabled = false;
            itemAmount.enabled = false;
            itemAmount.text = "";
            itemDurability.enabled = false;
            characterItem = new CharacterData.CharacterItem(typeInventorySlot);
        }
    }
    public void SelectFastItem(bool isSelect)
    {
        itemSelect.enabled = isSelect;
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        characterPlayerHud.lastSelectedSlot = this;
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        characterPlayerHud.lastSelectedSlot = null;
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (characterItem.itemBaseSO != null)
        {
            characterPlayerHud.inventoryDraggedSlot = Instantiate(GameData.Instance.utils.prefabs["DraggedSlot"], eventData.position, Quaternion.identity, characterPlayerHud.hudTransform).GetComponent<InventoryDraggedSlot>();
            characterPlayerHud.inventoryDraggedSlot.InitializeDraggedSlot(characterItem);
            characterPlayerHud.inventoryDraggedSlot.itemDraged = this;
            characterPlayerHud.inventoryDraggedSlot.rectTransform.sizeDelta = Vector2.one * 100;
            characterPlayerHud.characterUI.itemDescription.descriptionCanvasGroup.alpha = 0.5f;
            characterPlayerHud.isDraggingItem = true;
            characterPlayerHud.ResetDescription();
        }
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (characterItem.itemBaseSO != null && characterPlayerHud.inventoryDraggedSlot != null) characterPlayerHud.inventoryDraggedSlot.transform.position = eventData.position;        
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (characterItem.itemBaseSO == null) return;
        characterPlayerHud.characterUI.itemDescription.descriptionCanvasGroup.alpha = 1;
        characterPlayerHud.isDraggingItem = false;
        if (characterPlayerHud.inventoryDraggedSlot == null) return;

        InventorySlot draggedSlot = characterPlayerHud.inventoryDraggedSlot.itemDraged;
        InventorySlot targetSlot = characterPlayerHud.lastSelectedSlot;
        if (targetSlot != null && draggedSlot != null)
        {
            characterPlayerHud.characterPlayer.ChangeObjectPosition(
                GetItemInfo(draggedSlot),
                GetItemInfo(targetSlot)
            );
        }
        else
        {
            _ = characterPlayerHud.characterPlayer.DropItem(this);
        }
        characterPlayerHud.lastSelectedSlot = null;
        Destroy(characterPlayerHud.inventoryDraggedSlot.gameObject);
    }
    ItemInfo GetItemInfo(InventorySlot slot)
    {
        return new ItemInfo
        {
            typeItem = slot.typeInventorySlot,
            index = slot.slotIndex,
            itemData = characterPlayerHud.characterPlayer.GetItem(slot.typeInventorySlot, slot.slotIndex),
            inventorySlot = slot
        };
    }
    public void FastEquipItem()
    {
        if (characterItem.itemBaseSO != null)
        {
            characterPlayerHud.characterPlayer.FastEquipItem(slotIndex, this);
        }
    }
    public class ItemInfo
    {
        public ItemsDBSO.TypeModel typeItem;
        public int index;
        public CharacterData.CharacterItem itemData;
        public InventorySlot inventorySlot;
    }
}