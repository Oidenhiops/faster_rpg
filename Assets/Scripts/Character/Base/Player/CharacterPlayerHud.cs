using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AYellowpaper.SerializedCollections;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;

public class CharacterPlayerHud : MonoBehaviour
{
    public CharacterPlayer characterPlayer;
    public CharacterUI characterUI;
    public Animator characterInventoryAnim;
    public InventorySlot lastSelectedSlot;
    public InventoryDraggedSlot inventoryDraggedSlot;
    public Transform hudTransform;
    public bool isDraggingItem;
    public async Awaitable InitializeInventory()
    {
        foreach (var item in characterUI.items)
        {
            item.Value.characterPlayerHud = this;
        }
        foreach (var consumable in characterUI.consumables)
        {
            consumable.Value.characterPlayerHud = this;
        }
        SelectFastItem();
        await RefreshCharacterInventory();
    }
    public async Awaitable ChangeCharacterPortrait()
    {
        try
        {
            foreach (var portrait in characterUI.characterPortraits)
            {
                portrait.characterCounterText.text = "1";
                portrait.characterCounter.fillAmount = 1f;
            }
            float elapsedTime = 0f;
            float duration = 1f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float fillAmount = Mathf.Lerp(1, 0, elapsedTime / duration);
                foreach (var character in characterUI.characterPortraits)
                {
                    character.characterCounter.fillAmount = fillAmount;
                    character.characterCounterText.text = fillAmount.ToString("F1");
                }
                await Awaitable.NextFrameAsync();
            }
            foreach (var character in characterUI.characterPortraits)
            {
                character.characterCounterText.text = "";
            }
            await Awaitable.NextFrameAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error changing character portrait: {ex.Message}");
        }
    }
    public async Awaitable RefreshCharacterInventory()
    {
        try
        {
            characterUI.panelToResetSelect.gameObject.SetActive(true);
            ResetDescription();
            foreach (Transform child in characterUI.characterBag.bagContainer)
            {
                Destroy(child.gameObject);
            }
            int index = 0;
            characterUI.characterBag.inventorySlots.Clear();
            foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in characterPlayer.charactersData[characterPlayer.characterIndex].characterData.bag)
            {
                InventorySlot bagSlotPrefab = Instantiate(Resources.Load<GameObject>("Prefabs/BagSlot/BagSlot"), characterUI.characterBag.bagContainer).GetComponent<InventorySlot>();
                bagSlotPrefab.characterPlayerHud = this;
                bagSlotPrefab.slotIndex = bagSlot.Key;
                bagSlotPrefab.InitializeSlot(bagSlot.Value);
                characterUI.characterBag.inventorySlots.Add(index, bagSlotPrefab);
                index++;
            }
            foreach (KeyValuePair<ItemBaseSO.TypeObject, InventorySlot> item in characterUI.items)
            {
                characterUI.items[item.Key].InitializeSlot(characterPlayer.charactersData[characterPlayer.characterIndex].characterData.items[item.Key]);
            }
            foreach (KeyValuePair<int, InventorySlot> consumable in characterUI.consumables)
            {
                characterUI.consumables[consumable.Key].InitializeSlot(characterPlayer.charactersData[characterPlayer.characterIndex].characterData.consumables[consumable.Key]);
            }
            UpdateFastItems();
            await Awaitable.NextFrameAsync();
            characterUI.panelToResetSelect.gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    public void ChangeSlotPosition()
    {
        int lastSelectedSlotIndex = lastSelectedSlot.slotIndex;
        int draggedSlotIndex = inventoryDraggedSlot.itemDraged.slotIndex;

        if (lastSelectedSlot == inventoryDraggedSlot.itemDraged) return;

        if (lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag && inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag)
            ChangeSlotBagToBag(lastSelectedSlotIndex, draggedSlotIndex);            
    }

    private void ChangeSlotItemToItem(int lastSelectedSlotIndex, int draggedSlotIndex)
    {
        throw new NotImplementedException();
    }
    public void DropItem()
    {
        int draggedSlotIndex = inventoryDraggedSlot.itemDraged.slotIndex;
        if (inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag)
        {
            characterPlayer.charactersData[characterPlayer.characterIndex].characterData.bag[draggedSlotIndex] = new CharacterData.CharacterItem();
            GetBagSlotByIndex(draggedSlotIndex).InitializeSlot(new CharacterData.CharacterItem());
        }
    }
    InventorySlot GetBagSlotByIndex(int index)
    {
        if (characterUI.characterBag.inventorySlots.TryGetValue(index, out InventorySlot bagSlot))
        {
            return bagSlot;
        }
        return null;
    }
    void ChangeSlotBagToBag(int BagSlot, int DraggedBagSlot)
    {
        CharacterData.CharacterItem bagSlotTemp = new CharacterData.CharacterItem(GetBagSlotByIndex(BagSlot).characterItem);
        CharacterData.CharacterItem draggedBagSlotTemp = new CharacterData.CharacterItem(GetBagSlotByIndex(DraggedBagSlot).characterItem);

        characterPlayer.charactersData[characterPlayer.characterIndex].characterData.bag[BagSlot] = draggedBagSlotTemp;
        characterPlayer.charactersData[characterPlayer.characterIndex].characterData.bag[DraggedBagSlot] = bagSlotTemp;
        GetBagSlotByIndex(BagSlot).InitializeSlot(characterPlayer.charactersData[characterPlayer.characterIndex].characterData.bag[BagSlot]);
        GetBagSlotByIndex(DraggedBagSlot).InitializeSlot(characterPlayer.charactersData[characterPlayer.characterIndex].characterData.bag[DraggedBagSlot]);
    }
    void UpdateFastItems()
    {
        foreach (KeyValuePair<int, FastItem> fastItem in characterUI.fastItems)
        {
            if (characterPlayer.charactersData[characterPlayer.characterIndex].characterData.consumables[fastItem.Key].itemBaseSO != null)
            {
                characterUI.fastItems[fastItem.Key].fastItemCanvasGroup.alpha = 1;
                characterUI.fastItems[fastItem.Key].fastItemIcon.enabled = true;
                characterUI.fastItems[fastItem.Key].fastItemIcon.sprite = characterPlayer.charactersData[characterPlayer.characterIndex].characterData.consumables[fastItem.Key].itemBaseSO.icon;
                characterUI.fastItems[fastItem.Key].fastItemAmount.enabled = true;
                characterUI.fastItems[fastItem.Key].fastItemAmount.text = characterPlayer.charactersData[characterPlayer.characterIndex].characterData.consumables[fastItem.Key].amount > 1 ? characterPlayer.charactersData[characterPlayer.characterIndex].characterData.consumables[fastItem.Key].amount.ToString() : "";
            }
            else
            {
                characterUI.fastItems[fastItem.Key].fastItemIcon.enabled = false;
                characterUI.fastItems[fastItem.Key].fastItemCanvasGroup.alpha = 0.5f;
                characterUI.fastItems[fastItem.Key].fastItemAmount.enabled = false;
                characterUI.fastItems[fastItem.Key].fastItemAmount.text = "";
            }
        }
    }
    public void SelectFastItem()
    {
        foreach (KeyValuePair<int, FastItem> fastItem in characterUI.fastItems)
        {
            if (fastItem.Key == characterPlayer.currentFastItemIndex)
            {
                characterUI.fastItems[fastItem.Key].fastItemBg.color = Color.yellow;
            }
            else
            {
                characterUI.fastItems[fastItem.Key].fastItemBg.color = Color.white;
            }
        }
    }
    public void ResetDescription()
    {
        characterUI.itemDescription.descriptionCanvasGroup.alpha = 0;
        characterUI.itemDescription.descriptionTextTransform.SetParent(characterUI.itemDescription.panelToResetSelect);
        characterUI.itemDescription.descriptionTextTransform.localPosition = Vector2.zero;
    }
    public async Awaitable ToggleCharacterInventory()
    {
        characterInventoryAnim.SetBool("isOpen", !characterInventoryAnim.GetBool("isOpen"));
        if (!characterInventoryAnim.GetBool("isOpen"))
        {
            UpdateFastItems();
        }
    }
    public void SetDescripitionData(ItemBaseSO itemBaseSO)
    {
        characterUI.itemDescription.itemIcon.sprite = itemBaseSO.icon;
        characterUI.itemDescription.itemName.text = GameData.Instance.GetDialog(itemBaseSO.idText, GameData.TypeLOCS.Items).dialog;
        string description = GameData.Instance.GetDialog(itemBaseSO.idText, GameData.TypeLOCS.Items).description;
        if (Regex.IsMatch(description, @"\{\d+\}"))
        {
            List<CharacterData.Statistic> itemStats = itemBaseSO.itemStatistics.Values.ToList();
            for (int i = 0; i < itemStats.Count; i++)
            {
                description = description.Replace($"{{{i}}}", itemStats[i].maxValue.ToString());
            }
        }
        characterUI.itemDescription.itemDescription.text = description;
    }
    [Serializable]
    public class CharacterUI
    {
        public CharacterPortrait[] characterPortraits;
        public CharacterBag characterBag;
        public SerializedDictionary<ItemBaseSO.TypeObject, InventorySlot> items = new SerializedDictionary<ItemBaseSO.TypeObject, InventorySlot>();
        public SerializedDictionary<int, InventorySlot> consumables = new SerializedDictionary<int, InventorySlot>();
        public SerializedDictionary<int, FastItem> fastItems = new SerializedDictionary<int, FastItem>();
        public ItemDescription itemDescription;
        public Transform panelToResetSelect;
    }
    [Serializable]
    public class FastItem
    {
        public CanvasGroup fastItemCanvasGroup;
        public Image fastItemBg;
        public Image fastItemIcon;
        public TMP_Text fastItemAmount;
    }
    [Serializable]
    public class ItemDescription
    {
        public CanvasGroup descriptionCanvasGroup;
        public RectTransform descriptionTextTransform;
        public RectTransform descriptionTextBannerTransform;
        public Image itemIcon;
        public TMP_Text itemName;
        public TMP_Text itemDescription;
        public RectTransform panelToResetSelect;
    }
    [Serializable]
    public class CharacterPortrait
    {
        public GameObject portraitObject;
        public Image characterBg;
        public Image characterSprite;
        public Image characterCounter;
        public TMP_Text characterCounterText;
    }
    [Serializable]
    public class CharacterBag
    {
        public Transform bagContainer;
        public SerializedDictionary<int, InventorySlot> inventorySlots;
    }
}
