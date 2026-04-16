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
    public InventorySlot _lastSelectedSlot;
    public InventorySlot lastSelectedSlot
    {
        get => _lastSelectedSlot;
        set
        {
            _lastSelectedSlot = value;
            if (_lastSelectedSlot?.characterItem.itemBaseSO != null)
            {
                SetDescripitionData();
            }
            else
            {
                ResetDescription();
            }
        }
    }
    public InventoryDraggedSlot inventoryDraggedSlot;
    public Transform hudTransform;
    public bool isDraggingItem;
    public Dictionary<AnchorPreset, (Vector2 min, Vector2 max)> anchorPresets = new Dictionary<AnchorPreset, (Vector2, Vector2)>()
        {
            { AnchorPreset.TopLeft, (new Vector2(0, 1), new Vector2(0, 1)) },
            { AnchorPreset.TopCenter, (new Vector2(0.5f, 1), new Vector2(0.5f, 1)) },
            { AnchorPreset.TopRight, (new Vector2(1, 1), new Vector2(1, 1)) },

            { AnchorPreset.MiddleLeft, (new Vector2(0, 0.5f), new Vector2(0, 0.5f)) },
            { AnchorPreset.MiddleCenter, (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)) },
            { AnchorPreset.MiddleRight, (new Vector2(1, 0.5f), new Vector2(1, 0.5f)) },

            { AnchorPreset.BottomLeft, (new Vector2(0, 0), new Vector2(0, 0)) },
            { AnchorPreset.BottomCenter, (new Vector2(0.5f, 0), new Vector2(0.5f, 0)) },
            { AnchorPreset.BottomRight, (new Vector2(1, 0), new Vector2(1, 0)) },

            { AnchorPreset.StretchHorizontalTop, (new Vector2(0, 1), new Vector2(1, 1)) },
            { AnchorPreset.StretchHorizontalMiddle, (new Vector2(0, 0.5f), new Vector2(1, 0.5f)) },
            { AnchorPreset.StretchHorizontalBottom, (new Vector2(0, 0), new Vector2(1, 0)) },

            { AnchorPreset.StretchVerticalLeft, (new Vector2(0, 0), new Vector2(0, 1)) },
            { AnchorPreset.StretchVerticalCenter, (new Vector2(0.5f, 0), new Vector2(0.5f, 1)) },
            { AnchorPreset.StretchVerticalRight, (new Vector2(1, 0), new Vector2(1, 1)) },

            { AnchorPreset.StretchFull, (new Vector2(0, 0), new Vector2(1, 1)) },
        };
    public async Awaitable InitializeInventory()
    {
        foreach (var item in characterUI.equipments)
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
    public async Awaitable ToggleCharacterInventory()
    {
        characterInventoryAnim.SetBool("isOpen", characterPlayer.isInventoryOpen);
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
            foreach (KeyValuePair<ItemBaseSO.TypeObject, InventorySlot> item in characterUI.equipments)
            {
                characterUI.equipments[item.Key].InitializeSlot(characterPlayer.charactersData[characterPlayer.characterIndex].characterData.equipments[item.Key]);
            }
            foreach (KeyValuePair<int, InventorySlot> consumable in characterUI.consumables)
            {
                characterUI.consumables[consumable.Key].slotIndex = consumable.Key;
                characterUI.consumables[consumable.Key].InitializeSlot(characterPlayer.charactersData[characterPlayer.characterIndex].characterData.consumables[consumable.Key]);
            }
            foreach (KeyValuePair<CharacterData.TypeStatistic, TMP_Text> statistic in characterUI.statistics)
            {
                if (characterPlayer.charactersData[characterPlayer.characterIndex].characterData.statistics.TryGetValue(statistic.Key, out CharacterData.Statistic stat))
                {
                    if (statistic.Key == CharacterData.TypeStatistic.Hp || statistic.Key == CharacterData.TypeStatistic.Sp)
                    {
                        statistic.Value.text = $"{stat.currentValue}/{stat.maxValue}";
                    }
                    else
                    {
                        int otherStatsValue = stat.currentValue;
                        statistic.Value.text = stat.baseValue.ToString() + (otherStatsValue - stat.baseValue != 0 ? $" (+{otherStatsValue - stat.baseValue})" : "");
                    }
                }
            }
            UpdateFastItems();
            await Awaitable.NextFrameAsync();
            if (inventoryDraggedSlot) Destroy(inventoryDraggedSlot.gameObject);
            characterUI.panelToResetSelect.gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    public async Awaitable ResetInventoryTarget()
    {
        ResetDescription();
        characterUI.panelToResetSelect.gameObject.SetActive(true);
        await Awaitable.NextFrameAsync();
        characterUI.panelToResetSelect.gameObject.SetActive(false);
    }
    public void UpdateFastItems()
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
    public InventorySlot GetBagSlotByIndex(int index)
    {
        if (characterUI.characterBag.inventorySlots.TryGetValue(index, out InventorySlot bagSlot))
        {
            return bagSlot;
        }
        return null;
    }
    public InventorySlot GetConsumableSlotByIndex(int index)
    {
        if (characterUI.consumables.TryGetValue(index, out InventorySlot consumableSlot))
        {
            return consumableSlot;
        }
        return null;
    }
    public InventorySlot GetEquipmentSlotByIndex(ItemBaseSO.TypeObject index)
    {
        if (characterUI.equipments.TryGetValue(index, out InventorySlot equipmentSlot))
        {
            return equipmentSlot;
        }
        return null;
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
    public void SetDescripitionData()
    {
        characterUI.itemDescription.descriptionTextTransform.SetParent(lastSelectedSlot.transform);
        characterUI.itemDescription.descriptionTextTransform.localPosition = Vector2.zero;
        characterUI.itemDescription.itemIcon.sprite = lastSelectedSlot.characterItem.itemBaseSO.icon;
        characterUI.itemDescription.itemName.text = GameData.Instance.GetDialog(lastSelectedSlot.characterItem.itemBaseSO.idText, GameData.TypeLOCS.Items).dialog;
        string description = GameData.Instance.GetDialog(lastSelectedSlot.characterItem.itemBaseSO.idText, GameData.TypeLOCS.Items).description;
        if (Regex.IsMatch(description, @"\{\d+\}"))
        {
            List<CharacterData.Statistic> itemStats = lastSelectedSlot.characterItem.itemBaseSO.itemStatistics.Values.ToList();
            for (int i = 0; i < itemStats.Count; i++)
            {
                description = description.Replace($"{{{i}}}", itemStats[i].baseValue.ToString());
            }
        }
        characterUI.itemDescription.itemDescription.text = description;
        characterUI.itemDescription.descriptionCanvasGroup.alpha = 1;
        SetAnchorPreset(characterUI.itemDescription.descriptionTextBannerTransform, AnchorPreset.TopRight);
        SetAnchorPreset(characterUI.itemDescription.descriptionTextTransform, AnchorPreset.TopLeft);
    }
    public void ResetDescription()
    {
        characterUI.itemDescription.descriptionCanvasGroup.alpha = 0;
        characterUI.itemDescription.descriptionTextTransform.SetParent(characterUI.itemDescription.panelToResetSelect);
        characterUI.itemDescription.descriptionTextTransform.localPosition = Vector2.zero;
    }
    void SetAnchorPreset(RectTransform rect, AnchorPreset preset)
    {
        var data = anchorPresets[preset];

        rect.anchorMin = data.min;
        rect.anchorMax = data.max;

        Vector2 pivot = new Vector2(
            GetPivotValue(data.min.x, data.max.x),
            GetPivotValue(data.min.y, data.max.y)
        );

        rect.pivot = pivot;

        rect.anchoredPosition = Vector2.zero;
    }
    float GetPivotValue(float min, float max)
    {
        if (min != max) return 0.5f;
        return min;
    }
    internal void ShowItemsToPickUp()
    {
        foreach (Transform child in characterUI.interactables.container)
        {
            Destroy(child.gameObject);
        }
        if (characterPlayer.interactables.Count > 0)
        {
            characterUI.interactables.interactablesPanel.SetActive(true);
            foreach (KeyValuePair<InteractableBase, GameObject> interactable in characterPlayer.interactables)
            {
                InteractableBanner interactablePrefab = Instantiate(characterPlayer.interactableBannerPrefab, characterUI.interactables.container).GetComponent<InteractableBanner>();
                interactablePrefab.interactable = interactable.Key;
                interactablePrefab.character = characterPlayer;
                interactablePrefab.InitializeBanner(interactable.Key);
            }
        }
        else
        {
            characterUI.interactables.interactablesPanel.SetActive(false);
        }
    }

    [Serializable]
    public class CharacterUI
    {
        public CharacterPortrait[] characterPortraits;
        public CharacterBag characterBag;
        public SerializedDictionary<ItemBaseSO.TypeObject, InventorySlot> equipments = new SerializedDictionary<ItemBaseSO.TypeObject, InventorySlot>();
        public SerializedDictionary<int, InventorySlot> consumables = new SerializedDictionary<int, InventorySlot>();
        public SerializedDictionary<int, FastItem> fastItems = new SerializedDictionary<int, FastItem>();
        public SerializedDictionary<CharacterData.TypeStatistic, TMP_Text> statistics = new SerializedDictionary<CharacterData.TypeStatistic, TMP_Text>();
        public InteractableUI interactables;
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
    [Serializable]
    public class InteractableUI
    {
        public GameObject interactablesPanel;
        public Transform container;
    }
    public enum AnchorPreset
    {
        TopLeft,
        TopCenter,
        TopRight,

        MiddleLeft,
        MiddleCenter,
        MiddleRight,

        BottomLeft,
        BottomCenter,
        BottomRight,

        StretchHorizontalTop,
        StretchHorizontalMiddle,
        StretchHorizontalBottom,

        StretchVerticalLeft,
        StretchVerticalCenter,
        StretchVerticalRight,

        StretchFull
    }
}