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
        foreach (var equipment in characterUI.equipments)
        {
            equipment.Value.characterPlayerHud = this;
        }
        foreach (var fastItem in characterUI.fastItemsInventory)
        {
            fastItem.Value.characterPlayerHud = this;
        }
        foreach (var ammo in characterUI.ammoInventory)
        {
            ammo.Value.characterPlayerHud = this;
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
            RefreshAmmo();
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
            if (characterPlayer.characterData.statistics.TryGetValue(statistic.Key, out CharacterData.Statistic stat))
            {
                if (statistic.Key == CharacterData.TypeStatistic.Hp || statistic.Key == CharacterData.TypeStatistic.Sp || statistic.Key == CharacterData.TypeStatistic.Str)
                {
                    statistic.Value.text = $"{stat.currentValue}/{stat.maxValue}";
                }
                else
                {
                    bool hasStat = stat.currentValue - stat.baseValue != 0;
                    bool sumStat = stat.currentValue - stat.baseValue > 0;
                    statistic.Value.SetTextFx((stat.baseValue.ToString() + (hasStat ? sumStat ? $" (+{stat.currentValue - stat.baseValue})" : $" ({stat.currentValue - stat.baseValue})" : "")).ToString());
                    statistic.Value.ApplyColor(
                        new[] {hasStat ? sumStat ? $" (+{stat.currentValue - stat.baseValue})" : $" ({stat.currentValue - stat.baseValue})" : ""},
                        new[] {hasStat ? sumStat ? Color.green : Color.red : Color.white}
                    );
                }
            }
        }
    }
    public void RefreshSkills()
    {
        foreach (KeyValuePair<int, SkillUi> skill in characterUI.skills)
        {
            if (characterPlayer.characterData.skills[skill.Key].skillsBaseSO)
            {
                skill.Value.skillImage.sprite = characterPlayer.characterData.skills[skill.Key].skillsBaseSO.icon;
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
        foreach (KeyValuePair<StatusEffectBaseSO, CharacterBase.StatusEffect> statusEffect in characterPlayer.statusEffects)
        {
            StatusEffectBanner statusEffectBanner = Instantiate(GameData.Instance.utils.prefabs["StatusEffect"], characterUI.statusEffectUI.statusEffectContainer.transform).GetComponent<StatusEffectBanner>();
            statusEffectBanner.SetBannerData(statusEffect.Value);
            characterUI.statusEffectUI.statusEffectsBanners.Add(statusEffect.Key, statusEffectBanner);
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
            StatusEffectBanner statusEffectBanner = Instantiate(GameData.Instance.utils.prefabs["StatusEffect"], characterUI.statusEffectUI.statusEffectContainer.transform).GetComponent<StatusEffectBanner>();
            statusEffectBanner.SetBannerData(statusEffect);
            characterUI.statusEffectUI.statusEffectsBanners.Add(statusEffect.statusEffectBaseSO, statusEffectBanner);
        }
    }
    public void RefreshFastItems()
    {
        foreach (KeyValuePair<int, InventorySlot> fastItem in characterUI.fastItemsInventory)
        {
            characterUI.fastItemsInventory[fastItem.Key].slotIndex = fastItem.Key;
            characterUI.fastItemsInventory[fastItem.Key].InitializeSlot(characterPlayer.characterData.fastItems[fastItem.Key]);
        }

        foreach (KeyValuePair<int, InventorySlot> fastItem in characterUI.fastItems)
        {
            fastItem.Value.InitializeSlot(characterPlayer.characterData.fastItems[fastItem.Key]);
        }
    }
    public void RefreshEquipments()
    {
        foreach (KeyValuePair<ItemsDBSO.TypeModel, InventorySlot> item in characterUI.equipments)
        {
            characterUI.equipments[item.Key].InitializeSlot(characterPlayer.characterData.equipments[item.Key]);
        }
    }
    public void RefreshAmmo()
    {
        foreach (KeyValuePair<int, InventorySlot> item in characterUI.ammoInventory)
        {
            characterUI.ammoInventory[item.Key].InitializeSlot(characterPlayer.characterData.ammo[item.Key]);
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
        foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in characterPlayer.characterData.bag)
        {
            InventorySlot bagSlotPrefab = Instantiate(GameData.Instance.utils.prefabs["BagSlot"], characterUI.characterBag.bagContainer).GetComponent<InventorySlot>();
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
    public InventorySlot GetAmmoSlotByIndex(int index)
    {
        if (characterUI.ammoInventory.TryGetValue(index, out InventorySlot ammoSlot))
        {
            return ammoSlot;
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
        foreach (KeyValuePair<int, InventorySlot> fastItem in characterUI.fastItems)
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
    public void ChangeBar(CharacterData.TypeStatistic typeBar)
    {
        
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
                InteractableBanner interactablePrefab = Instantiate(GameData.Instance.utils.prefabs["InteractableBanner"], characterUI.interactables.container).GetComponent<InteractableBanner>();
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
        public CharacterBag characterBag;
        public SerializedDictionary<ItemsDBSO.TypeModel, InventorySlot> equipments = new SerializedDictionary<ItemsDBSO.TypeModel, InventorySlot>();
        public SerializedDictionary<int, InventorySlot> fastItemsInventory = new SerializedDictionary<int, InventorySlot>();
        public SerializedDictionary<int, InventorySlot> fastItems = new SerializedDictionary<int, InventorySlot>();
        public SerializedDictionary<int, InventorySlot> ammoInventory = new SerializedDictionary<int, InventorySlot>();
        public SerializedDictionary<CharacterData.TypeStatistic, TMP_Text> statistics = new SerializedDictionary<CharacterData.TypeStatistic, TMP_Text>();
        public SerializedDictionary<int, SkillUi> skills = new SerializedDictionary<int, SkillUi>();
        public SerializedDictionary<CharacterData.TypeStatistic, BarsInfo> bars = new SerializedDictionary<CharacterData.TypeStatistic, BarsInfo>();
        public StatusEffectUI statusEffectUI;
        public InteractableUI interactables;
        public ItemDescription itemDescription;
        public Transform panelToResetSelect;
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
    [Serializable]
    public class BarsInfo
    {
        public Transform gameObjectBar;
        public Image delayBar;
        public Image plainBar;
        public Image flashBar;
        public TMP_Text textBar;
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