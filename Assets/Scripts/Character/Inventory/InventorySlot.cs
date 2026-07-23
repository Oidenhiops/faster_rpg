using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CharacterPlayerHud characterPlayerHud;
    public Image itemImage;
    public TMP_Text itemAmount;
    public TMP_Text itemDurability;
    public ItemsDBSO.TypeModel typeInventorySlot;
    public CharacterData.CharacterItem characterItem;
    public int slotIndex;
    public void InitializeSlot(CharacterData.CharacterItem item)
    {
        if (item.itemBaseSO?.icon)
        {
            itemImage.sprite = item.itemBaseSO.icon;
            itemImage.enabled = true;
            itemAmount.enabled = true;
            itemAmount.text = item.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue > 1 ? item.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue.ToString() : "";
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
            characterItem = item;
        }
        else
        {
            itemImage.sprite = null;
            itemImage.enabled = false;
            itemAmount.enabled = false;
            itemAmount.text = "";
            itemDurability.enabled = false;
            itemDurability.text = "";
            characterItem = new CharacterData.CharacterItem();
        }
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
            characterPlayerHud.inventoryDraggedSlot = Instantiate(Resources.Load<GameObject>("Prefabs/DraggedSlot/DraggedSlot"), eventData.position, Quaternion.identity, characterPlayerHud.hudTransform).GetComponent<InventoryDraggedSlot>();
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
        if (characterPlayerHud.lastSelectedSlot != null)
        {
            characterPlayerHud.characterPlayer.ChangeObjectPosition();
        }
        else
        {
            _ = characterPlayerHud.characterPlayer.DropItem();
        }
        characterPlayerHud.lastSelectedSlot = null;
        Destroy(characterPlayerHud.inventoryDraggedSlot.gameObject);
    }
    public void FastEquipItem()
    {
        if (characterItem.itemBaseSO != null)
        {
            characterPlayerHud.characterPlayer.FastEquipItem(slotIndex);
        }
    }
}