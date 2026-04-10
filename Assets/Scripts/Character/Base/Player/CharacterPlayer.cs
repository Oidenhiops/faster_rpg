using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayer : CharacterBase
{
    public InputSystem_Actions inputActions;
    public CharacterPlayerHud characterPlayerHud;
    public bool isChangingCharacter;
    public override void OnEnableHandle()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        inputActions.Player.ChangeCharacter.performed += OnHandleChangeCharacter;
        inputActions.Player.ToggleInventory.performed += OnHandleToggleInventory;
        inputActions.Player.ChangeItem.performed += OnHandleChangeItem;
    }
    public async override Awaitable InitializeCharacter()
    {
        try
        {
            List<CharactersData> charactersDataList = new List<CharactersData>();
            foreach (var characterData in GameData.Instance.gameDataInfo.gameDataSlots[GameData.Instance.systemDataInfo.currentGameDataIndex].characters)
            {
                if (GameData.Instance.charactersSkinDBSO.data.ContainsKey(characterData.Value.characterId))
                {
                    if (GameData.Instance.charactersSkinDBSO.data[characterData.Value.characterId].TryGetValue(characterData.Value.characterSkinId, out CharacterSkinData skinData))
                    {
                        charactersDataList.Add(new CharactersData
                        {
                            characterSkin = skinData,
                            characterAnimationsSO = GameData.Instance.charactersDBSO.data[characterData.Value.characterId][characterData.Value.characterSkinId].initialDataSO.characterAnimationsSO,
                            characterData = characterData.Value
                        });
                    }
                }
            }
            charactersData = charactersDataList.ToArray();
            await InitializeStatistics();
            await InitializeItems();
            for (int i = 0; i < 4; i++)
            {
                if (i <= charactersData.Length - 1)
                {
                    characterPlayerHud.characterUI.characterPortraits[i].portraitObject.SetActive(true);
                    characterPlayerHud.characterUI.characterPortraits[i].characterSprite.sprite = charactersDataList[i].characterSkin.icon;
                }
                else
                {
                    characterPlayerHud.characterUI.characterPortraits[i].portraitObject.SetActive(false);
                }
            }
            characterPlayerHud.characterUI.characterPortraits[characterIndex].characterBg.color = Color.yellow;
            await characterPlayerHud.InitializeInventory();
            await InitializeAnimations();
            isInitialize = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error initializing character: {ex.Message}");
        }
    }
    void OnHandleChangeCharacter(InputAction.CallbackContext context)
    {
        if (!isChangingCharacter && charactersData.Length - 1 >= context.ReadValue<float>() && characterIndex != (int)context.ReadValue<float>())
        {
            isChangingCharacter = true;
            characterPlayerHud.characterUI.characterPortraits[characterIndex].characterBg.color = Color.white;
            characterIndex = (int)context.ReadValue<float>();
            characterPlayerHud.characterUI.characterPortraits[characterIndex].characterBg.color = Color.yellow;
            _ = InitializeAnimations();
            _ = ChangeCharacterAction();
            _ = characterPlayerHud.RefreshCharacterInventory();
        }
    }
    void OnHandleToggleInventory(InputAction.CallbackContext context)
    {
        _ = characterPlayerHud.ToggleCharacterInventory();
    }
    void OnHandleChangeItem(InputAction.CallbackContext context)
    {
        currentFastItemIndex += (int)context.ReadValue<float>();
        if (currentFastItemIndex < 0) currentFastItemIndex = characterPlayerHud.characterUI.fastItems.Count - 1;
        else if (currentFastItemIndex >= characterPlayerHud.characterUI.fastItems.Count) currentFastItemIndex = 0;
        characterPlayerHud.SelectFastItem();
    }
    async Awaitable ChangeCharacterAction()
    {
        await characterPlayerHud.ChangeCharacterPortrait();
        isChangingCharacter = false;
    }
    async Awaitable InitializeStatistics()
    {
        foreach (CharactersData character in charactersData)
        {
            character.characterData.InitializeStatistics();
        }
    }
    async Awaitable InitializeItems()
    {
        foreach (CharactersData character in charactersData)
        {
            character.characterData.InitializeItems();
        }
    }
    public void ChangeObjectPosition()
    {
        int lastSelectedSlotIndex = characterPlayerHud.lastSelectedSlot.slotIndex;
        int draggedSlotIndex = characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex;

        if (characterPlayerHud.lastSelectedSlot == characterPlayerHud.inventoryDraggedSlot.itemDraged) return;

        if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag && characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag)
            ChangeItemBagToBag(lastSelectedSlotIndex, draggedSlotIndex);
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot != InventorySlot.TypeInventorySlot.Bag)
        {
            if (characterPlayerHud.lastSelectedSlot.characterItem.itemBaseSO == null)
            {
                if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment)
                {
                    // ChangeSlotBagToEquipment(lastSelectedSlotIndex, draggedSlotIndex);
                }
                else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables)
                {
                    ChangeSlotBagToConsumable(draggedSlotIndex, lastSelectedSlotIndex);
                }
            }
            else if (characterPlayerHud.lastSelectedSlot.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment)
            {
                // ChangeSlotEquipmentToBag(lastSelectedSlotIndex, draggedSlotIndex);
            }
            else if (characterPlayerHud.lastSelectedSlot.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables)
            {
                ChangeSlotBagToConsumable(draggedSlotIndex, lastSelectedSlotIndex);
            }
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot != InventorySlot.TypeInventorySlot.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag)
        {
            if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment)
            {
                // ChangeSlotEquipmentToBag(lastSelectedSlotIndex, draggedSlotIndex);
            }
            else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables)
            {
                ChangeSlotBagToConsumable(lastSelectedSlotIndex, draggedSlotIndex);
            }
        }
    }
    void ChangeItemBagToBag(int bagSlot, int draggedBagSlot)
    {
        CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlot));
        CharacterData.CharacterItem draggedItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(draggedBagSlot));

        charactersData[characterIndex].characterData.bag[draggedBagSlot] = bagItemTemp;
        charactersData[characterIndex].characterData.bag[bagSlot] = draggedItemTemp;
        characterPlayerHud.GetBagSlotByIndex(bagSlot).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlot]);
        characterPlayerHud.GetBagSlotByIndex(draggedBagSlot).InitializeSlot(charactersData[characterIndex].characterData.bag[draggedBagSlot]);
    }
    private void ChangeSlotBagToConsumable(int bagSlot, int draggedBagSlot)
    {
        CharacterData.CharacterItem bagSlotTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlot));
        CharacterData.CharacterItem consumableItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(draggedBagSlot));

        charactersData[characterIndex].characterData.bag[bagSlot] = consumableItemTemp;
        charactersData[characterIndex].characterData.consumables[draggedBagSlot] = bagSlotTemp;
        characterPlayerHud.GetBagSlotByIndex(bagSlot).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlot]);
        characterPlayerHud.GetConsumableSlotByIndex(draggedBagSlot).InitializeSlot(charactersData[characterIndex].characterData.consumables[draggedBagSlot]);
    }
    public CharacterData.CharacterItem GetBagItemByIndex(int index)
    {
        if (charactersData[characterIndex].characterData.bag.TryGetValue(index, out CharacterData.CharacterItem bagItem))
        {
            return bagItem;
        }
        return new CharacterData.CharacterItem();
    }
    public CharacterData.CharacterItem GetConsumableItemByIndex(int index)
    {
        if (charactersData[characterIndex].characterData.consumables.TryGetValue(index, out CharacterData.CharacterItem consumableItem))
        {
            return consumableItem;
        }
        return new CharacterData.CharacterItem();
    }
    public void DropItem()
    {
        int draggedSlotIndex = characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex;
        if (characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag)
        {
            charactersData[characterIndex].characterData.bag[draggedSlotIndex] = new CharacterData.CharacterItem();
            characterPlayerHud.GetBagSlotByIndex(draggedSlotIndex).InitializeSlot(new CharacterData.CharacterItem());
        }
    }
}
