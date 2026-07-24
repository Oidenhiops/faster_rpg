using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AYellowpaper.SerializedCollections;
using TMPro;
using UnityEngine;
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
        foreach (var fastItem in characterUI.fastItemsInventory)
        {
            fastItem.Value.characterPlayerHud = this;
        }
        SelectFastItem();
        await RefreshCharacterInventory();
    }
    public async Awaitable ToggleCharacterInventory()
    {
        characterInventoryAnim.SetBool("isOpen", characterPlayer.isInventoryOpen);
        if (!characterPlayer.isInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
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
            RefreshBag();
            RefreshEquipments();
            RefreshCharacterStatistics();
            RefreshFastItems();
            RefreshSkills();
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
    public void RefreshCharacterStatistics()
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, TMP_Text> statistic in characterUI.statistics)
        {
            if (characterPlayer.charactersData[characterPlayer.characterIndex].statistics.TryGetValue(statistic.Key, out CharacterData.Statistic stat))
            {
                if (statistic.Key == CharacterData.TypeStatistic.Hp || statistic.Key == CharacterData.TypeStatistic.Sp)
                {
                    statistic.Value.text = $"{stat.currentValue}/{stat.maxValue}";
                }
                else
                {
                    float otherStatsValue = stat.currentValue;
                    statistic.Value.text = stat.baseValue.ToString() + (otherStatsValue - stat.baseValue != 0 ? $" (+{otherStatsValue - stat.baseValue})" : "");
                }
            }
        }
    }
    public void RefreshSkills()
    {
        foreach (KeyValuePair<int, SkillUi> skill in characterUI.skills)
        {
            if (characterPlayer.charactersData[characterPlayer.characterIndex].skills[skill.Key].skillsBaseSO)
            {
                skill.Value.skillImage.sprite = characterPlayer.charactersData[characterPlayer.characterIndex].skills[skill.Key].skillsBaseSO.icon;
                skill.Value.lockImage.gameObject.SetActive(false);
                skill.Value.skillImage.gameObject.SetActive(true);
                skill.Value.RefreshCD(new CharacterBase.SkillCd { currentCd = 0, maxCd = 0 });
            }
            else
            {
                skill.Value.skillImage.gameObject.SetActive(false);
                skill.Value.lockImage.gameObject.SetActive(true);
                skill.Value.RefreshCD(new CharacterBase.SkillCd { currentCd = 0, maxCd = 0 });
            }
        }
    }
    public void RefreshStatusEffects()
    {
        foreach(Transform statusEffect in characterUI.statusEffectUI.statusEffectContainer.transform)
        {
            Destroy(statusEffect.gameObject);
        }
        characterUI.statusEffectUI.statusEffectsBanners.Clear();
        if (characterPlayer.statusEffects.ContainsKey(characterPlayer.characterIndex))
        {
            foreach(KeyValuePair<StatusEffectBaseSO, CharacterBase.StatusEffect> statusEffect in characterPlayer.statusEffects[characterPlayer.characterIndex])
            {
                StatusEffectBanner statusEffectBanner = Instantiate(characterUI.statusEffectUI.statusEffectPrefab, characterUI.statusEffectUI.statusEffectContainer.transform).GetComponent<StatusEffectBanner>();
                statusEffectBanner.SetBannerData(statusEffect.Value);
                characterUI.statusEffectUI.statusEffectsBanners.Add(statusEffect.Key, statusEffectBanner);
            }
        }
    }
    public void AddStatusEffect(CharacterBase.StatusEffect statusEffect)
    {
        if (characterUI.statusEffectUI.statusEffectsBanners.ContainsKey(statusEffect.statusEffectBaseSO))
        {
            characterUI.statusEffectUI.statusEffectsBanners[statusEffect.statusEffectBaseSO].RefreshData(statusEffect);
        }
        else
        {
            StatusEffectBanner statusEffectBanner = Instantiate(characterUI.statusEffectUI.statusEffectPrefab, characterUI.statusEffectUI.statusEffectContainer.transform).GetComponent<StatusEffectBanner>();
            statusEffectBanner.SetBannerData(statusEffect);
            characterUI.statusEffectUI.statusEffectsBanners.Add(statusEffect.statusEffectBaseSO, statusEffectBanner);
        }
    }
    public void RefreshFastItems()
    {
        foreach (KeyValuePair<int, InventorySlot> fastItem in characterUI.fastItemsInventory)
        {
            characterUI.fastItemsInventory[fastItem.Key].slotIndex = fastItem.Key;
            characterUI.fastItemsInventory[fastItem.Key].InitializeSlot(characterPlayer.charactersData[characterPlayer.characterIndex].fastItems[fastItem.Key]);
        }

        foreach (KeyValuePair<int, FastItem> fastItem in characterUI.fastItems)
        {
            bool hasDurability = characterPlayer.charactersData[characterPlayer.characterIndex].fastItems[fastItem.Key].itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability);
            bool hasAmount = characterPlayer.charactersData[characterPlayer.characterIndex].fastItems[fastItem.Key].itemStatistics.ContainsKey(CharacterData.TypeStatistic.Amount);
            fastItem.Value.UpdateData(
                hasAmount ? characterPlayer.charactersData[characterPlayer.characterIndex].fastItems[fastItem.Key].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue : 0,
                hasAmount ? characterPlayer.charactersData[characterPlayer.characterIndex].fastItems[fastItem.Key].itemBaseSO.icon : null,
                hasDurability,
                hasDurability ? characterPlayer.charactersData[characterPlayer.characterIndex].fastItems[fastItem.Key].itemStatistics[CharacterData.TypeStatistic.Durability] : null
            );
        }
    }
    public void RefreshEquipments()
    {
        foreach (KeyValuePair<ItemsDBSO.TypeModel, InventorySlot> item in characterUI.equipments)
        {
            characterUI.equipments[item.Key].InitializeSlot(characterPlayer.charactersData[characterPlayer.characterIndex].equipments[item.Key]);
        }
    }
    public void RefreshBag()
    {
        foreach (Transform child in characterUI.characterBag.bagContainer)
        {
            Destroy(child.gameObject);
        }
        int index = 0;
        characterUI.characterBag.inventorySlots.Clear();
        foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in characterPlayer.charactersData[characterPlayer.characterIndex].bag)
        {
            InventorySlot bagSlotPrefab = Instantiate(Resources.Load<GameObject>("Prefabs/BagSlot/BagSlot"), characterUI.characterBag.bagContainer).GetComponent<InventorySlot>();
            bagSlotPrefab.characterPlayerHud = this;
            bagSlotPrefab.slotIndex = bagSlot.Key;
            bagSlotPrefab.InitializeSlot(bagSlot.Value);
            characterUI.characterBag.inventorySlots.Add(index, bagSlotPrefab);
            index++;
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
    public InventorySlot GetFastItemSlotByIndex(int index)
    {
        if (characterUI.fastItemsInventory.TryGetValue(index, out InventorySlot fastItemSlot))
        {
            return fastItemSlot;
        }
        return null;
    }
    public InventorySlot GetEquipmentSlotByIndex(ItemsDBSO.TypeModel index)
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
            characterUI.fastItems[fastItem.Key].SelectFastItem(fastItem.Key == characterPlayer.currentFastItemIndex);
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
        public SerializedDictionary<ItemsDBSO.TypeModel, InventorySlot> equipments = new SerializedDictionary<ItemsDBSO.TypeModel, InventorySlot>();
        public SerializedDictionary<int, InventorySlot> fastItemsInventory = new SerializedDictionary<int, InventorySlot>();
        public SerializedDictionary<int, FastItem> fastItems = new SerializedDictionary<int, FastItem>();
        public SerializedDictionary<CharacterData.TypeStatistic, TMP_Text> statistics = new SerializedDictionary<CharacterData.TypeStatistic, TMP_Text>();
        public SerializedDictionary<int, SkillUi> skills = new SerializedDictionary<int, SkillUi>();
        public StatusEffectUI statusEffectUI;
        public InteractableUI interactables;
        public ItemDescription itemDescription;
        public Transform panelToResetSelect;
    }
    [Serializable]
    public class FastItem
    {
        public CanvasGroup fastItemCanvasGroup;
        public Image fastItemSelect;
        public Image fastItemBg;
        public Image fastItemIcon;
        public GameObject fastItemAmountBg;
        public TMP_Text fastItemAmount;
        public Image fastItemDurability;
        public void UpdateData(float amount, Sprite sprite = null, bool hasDurability = false, CharacterData.Statistic durability = null, bool useEnergy = false)
        {
            fastItemAmountBg.SetActive(amount > 1);
            if (amount != 0)
            {
                fastItemCanvasGroup.alpha = 1;
                fastItemIcon.enabled = true;
                if (sprite) fastItemIcon.sprite = sprite;
                fastItemAmount.enabled = true;
                fastItemAmount.text = amount.ToString();
                if (hasDurability)
                {
                    float durabilityPorcent =durability.currentValue / durability.maxValue;
                    fastItemDurability.enabled = true;
                    fastItemDurability.fillAmount = durabilityPorcent > 0 ? durabilityPorcent : 1;
                    if (durabilityPorcent >= 0.7f) fastItemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(useEnergy ? "EnergyGood" : "DurabilityGood", out Color durabilityColor) ? durabilityColor : Color.white;
                    else if (durabilityPorcent < 0.7f && durabilityPorcent >= 0.3f) fastItemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(useEnergy ? "EnergyMedium" : "DurabilityMedium", out Color durabilityColor) ? durabilityColor : Color.white;
                    else if (durabilityPorcent < 0.3f && durabilityPorcent > 0f) fastItemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(useEnergy ? "EnergyBad" : "DurabilityBad", out Color durabilityColor) ? durabilityColor : Color.white;
                    else fastItemDurability.color = GameData.Instance.utils.systemColors.TryGetValue(useEnergy ? "OutEnergy" : "OutDurability", out Color durabilityColor) ? durabilityColor : Color.white;
                }
                else
                {
                    fastItemDurability.enabled = false;
                }
            }
            else
            {
                fastItemIcon.enabled = false;
                fastItemCanvasGroup.alpha = 0.5f;
                fastItemAmount.enabled = false;
                fastItemAmount.text = "";
                fastItemDurability.enabled = false;
            }
        }
        public void SelectFastItem(bool isSelect)
        {
            fastItemSelect.enabled = isSelect;
        }
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
    [Serializable]
    public class SkillUi
    {
        public Image skillImage;
        public Image lockImage;
        public Image skillImageCd;
        public TMP_Text skillCdText;
        public void RefreshCD(CharacterBase.SkillCd skillCd)
        {
            if (skillCd.currentCd > 0)
            {
                skillImageCd.fillAmount = skillCd.currentCd / skillCd.maxCd;
                skillCdText.text = skillCd.currentCd.ToString("F1");
            }
            else
            {
                skillImageCd.fillAmount = 0;
                skillCdText.text = "";
            }
        }
    }
    [Serializable]
    public class StatusEffectUI
    {
        public Transform statusEffectContainer;
        public GameObject statusEffectPrefab;
        public SerializedDictionary<StatusEffectBaseSO, StatusEffectBanner> statusEffectsBanners = new SerializedDictionary<StatusEffectBaseSO, StatusEffectBanner>();
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