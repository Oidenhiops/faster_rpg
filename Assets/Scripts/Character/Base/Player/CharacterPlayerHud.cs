using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterPlayerHud : MonoBehaviour
{
    public CharacterPlayer characterPlayer;
    public CharacterUI characterUI;
    public Animator characterInventoryAnim;
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
        await RefreshCharacterInventory();
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
                characterUI.consumables[consumable.Key].InitializeSlot(characterPlayer.charactersData[characterPlayer.characterIndex].characterData.consumable[consumable.Key]);
            }
            await Awaitable.NextFrameAsync();
            characterUI.panelToResetSelect.gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    void ResetDescription()
    {
        characterUI.itemDescription.descriptionTextTransform.gameObject.SetActive(false);
        characterUI.itemDescription.descriptionTextTransform.SetParent(characterUI.itemDescription.descriptionContainer);
        characterUI.itemDescription.descriptionTextTransform.localPosition = Vector2.zero;
    }
    public async Awaitable ToggleCharacterInventory()
    {
        characterInventoryAnim.SetBool("isOpen", !characterInventoryAnim.GetBool("isOpen"));
    }
    [Serializable]
    public class CharacterUI
    {
        public CharacterPortrait[] characterPortraits;
        public CharacterBag characterBag;
        public SerializedDictionary<ItemBaseSO.TypeObject, InventorySlot> items = new SerializedDictionary<ItemBaseSO.TypeObject, InventorySlot>();
        public SerializedDictionary<int, InventorySlot> consumables = new SerializedDictionary<int, InventorySlot>();
        public ItemDescription itemDescription;
        public Transform panelToResetSelect;
    }
    [Serializable]
    public class ItemDescription
    {
        public RectTransform descriptionTextTransform;
        public RectTransform descriptionTextBannerTransform;
        public RectTransform descriptionContainer;
    }
    [Serializable]
    public class CharacterPortrait
    {
        public GameObject portraitObject;
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
