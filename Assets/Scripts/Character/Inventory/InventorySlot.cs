using System.Collections;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image itemImage;
    public TypeInventorySlot typeInventorySlot;
    public RectTransform descriptionTextTransform;
    public RectTransform descriptionTextBannerTransform;
    public bool showingText;
    public int test;
    public AnchorPreset currentPreset;
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
        if (item.itemBaseSO?.icon) itemImage.sprite = item.itemBaseSO.icon;
        else itemImage.enabled = false;
    }
    #region Adjusting description text position
    void FixedUpdate()
    {
        AdjustDescriptionContent();
    }
    public void AdjustDescriptionContent()
    {
        if (!showingText) return;

        if (test == 0)
        {
            if (currentPreset != AnchorPreset.MiddleLeft)
            {
                currentPreset = AnchorPreset.MiddleLeft;

                SetAnchorPreset(descriptionTextTransform, AnchorPreset.MiddleLeft);
                SetAnchorPreset(descriptionTextBannerTransform, AnchorPreset.MiddleRight);
            }
        }
        else if (test == 1)
        {
            if (currentPreset != AnchorPreset.MiddleRight)
            {
                currentPreset = AnchorPreset.MiddleRight;

                SetAnchorPreset(descriptionTextTransform, AnchorPreset.MiddleRight);
                SetAnchorPreset(descriptionTextBannerTransform, AnchorPreset.MiddleLeft);
            }
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
        StartCoroutine(WaitForShowText());
    }
    public IEnumerator WaitForShowText()
    {
        yield return new WaitForSeconds(0.5f);
        showingText = true;
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        throw new System.NotImplementedException();
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
