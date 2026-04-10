using System.Collections.Generic;
using System.Xml.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CharacterPlayerHud characterPlayerHud;
    public Image itemImage;
    public TMP_Text itemAmount;
    public RectTransform rectTransform;
    public TypeInventorySlot typeInventorySlot;
    public CharacterData.CharacterItem characterItem;
    public bool showingText;
    public int slotIndex;
    public bool isUsingSlot;
    public bool isDragging;
    public int test;
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
    public void InitializeSlot(CharacterData.CharacterItem item)
    {
        if (item.itemBaseSO?.icon)
        {
            itemImage.sprite = item.itemBaseSO.icon;
            itemImage.enabled = true;
            isUsingSlot = true;
            itemAmount.enabled = true;
            itemAmount.text = item.amount > 1 ? item.amount.ToString() : "";
            characterItem = item;
        }
        else
        {
            itemImage.sprite = null;
            itemImage.enabled = false;
            isUsingSlot = false;
            itemAmount.enabled = false;
            itemAmount.text = "";
            characterItem = new CharacterData.CharacterItem();
        }
    }
    #region Adjusting description text position
    void FixedUpdate()
    {
        if (isUsingSlot)
        {
            AdjustDescriptionContent();
        }
    }
    public void AdjustDescriptionContent()
    {
        if (!showingText) return;

        if (test == 0)
        {
            SetAnchorPreset(characterPlayerHud.characterUI.itemDescription.descriptionTextBannerTransform, AnchorPreset.TopRight);
            SetAnchorPreset(characterPlayerHud.characterUI.itemDescription.descriptionTextTransform, AnchorPreset.TopLeft);
        }
        else if (test == 1)
        {
            SetAnchorPreset(characterPlayerHud.characterUI.itemDescription.descriptionTextTransform, AnchorPreset.TopRight);
            SetAnchorPreset(characterPlayerHud.characterUI.itemDescription.descriptionTextBannerTransform, AnchorPreset.TopLeft);
        }
    }
    public void SetAnchorPreset(RectTransform rect, AnchorPreset preset)
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
    #endregion
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUsingSlot && !characterPlayerHud.isDraggingItem)
        {
            _ = EnableSlot();
        }
        characterPlayerHud.lastSelectedSlot = this;
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isUsingSlot && !characterPlayerHud.isDraggingItem)
        {
            DisableSlot();
        }
        characterPlayerHud.lastSelectedSlot = null;
    }
    async Awaitable EnableSlot()
    {
        characterPlayerHud.SetDescripitionData(characterItem.itemBaseSO);
        characterPlayerHud.characterUI.itemDescription.descriptionTextTransform.SetParent(transform);
        characterPlayerHud.characterUI.itemDescription.descriptionTextTransform.localPosition = Vector2.zero;
        showingText = true;
        await Awaitable.NextFrameAsync();
        characterPlayerHud.characterUI.itemDescription.descriptionCanvasGroup.alpha = 1;
    }
    void DisableSlot()
    {
        showingText = false;
        characterPlayerHud.characterUI.itemDescription.descriptionCanvasGroup.alpha = 0;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isUsingSlot)
        {
            showingText = false;
            isDragging = true;
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
        if (isUsingSlot && characterPlayerHud.inventoryDraggedSlot != null) characterPlayerHud.inventoryDraggedSlot.transform.position = eventData.position;        
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isUsingSlot) return;
        characterPlayerHud.characterUI.itemDescription.descriptionCanvasGroup.alpha = 1;
        characterPlayerHud.isDraggingItem = false;
        isDragging = false;
        if (characterPlayerHud.lastSelectedSlot != null)
        {
            characterPlayerHud.ChangeSlotPosition();
        }
        else
        {
            characterPlayerHud.DropItem();
        }
        characterPlayerHud.lastSelectedSlot = null;
        Destroy(characterPlayerHud.inventoryDraggedSlot.gameObject);
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
        Item1 = 10,
        Item2 = 11,
        Item3 = 12,
        Utility = 13,
        Bag = 14
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
