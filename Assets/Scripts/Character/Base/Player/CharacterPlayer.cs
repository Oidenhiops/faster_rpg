using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayer : CharacterBase
{
    public InputSystem_Actions inputActions;
    public CharacterPlayerHud characterPlayerHud;
    public float dropLaunchForce = 4f;
    public float dropUpForce = 2f;
    public SerializedDictionary<ItemDropped, CharacterData.CharacterItem> droppedItems = new SerializedDictionary<ItemDropped, CharacterData.CharacterItem>();
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
            ChangeBagAndBag(lastSelectedSlotIndex, draggedSlotIndex);
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot != InventorySlot.TypeInventorySlot.Bag)
        {
            if (characterPlayerHud.lastSelectedSlot.characterItem.itemBaseSO == null)
            {
                if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment &&
                    characterPlayerHud.lastSelectedSlot.typeInventorySlot != InventorySlot.TypeInventorySlot.Consumables)
                {
                    ChangeEquipmentAndBag(ConvertTypeIntoTypeObject((int)characterPlayerHud.lastSelectedSlot.typeInventorySlot), draggedSlotIndex);
                }
                else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables &&
                         characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Consumables)
                {
                    ChangeBagAndConsumable(draggedSlotIndex, lastSelectedSlotIndex);
                }
            }
            else if (characterPlayerHud.lastSelectedSlot.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment)
            {
                ChangeEquipmentAndBag(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject, draggedSlotIndex);
            }
            else if (characterPlayerHud.lastSelectedSlot.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables)
            {
                ChangeBagAndConsumable(draggedSlotIndex, lastSelectedSlotIndex);
            }
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot != InventorySlot.TypeInventorySlot.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag)
        {
            if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment)
            {
                ChangeEquipmentAndBag(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject, lastSelectedSlotIndex);
            }
            else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables)
            {
                ChangeBagAndConsumable(lastSelectedSlotIndex, draggedSlotIndex);
            }
        }
    }
    void ChangeBagAndBag(int bagSlotIndex, int draggedBagSlotIndex)
    {
        CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
        CharacterData.CharacterItem draggedItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(draggedBagSlotIndex));

        charactersData[characterIndex].characterData.bag[draggedBagSlotIndex] = bagItemTemp;
        charactersData[characterIndex].characterData.bag[bagSlotIndex] = draggedItemTemp;
        characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlotIndex]);
        characterPlayerHud.GetBagSlotByIndex(draggedBagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[draggedBagSlotIndex]);
    }
    private void ChangeBagAndConsumable(int bagSlotIndex, int consumableSlotIndex)
    {
        CharacterData.CharacterItem bagSlotTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
        CharacterData.CharacterItem consumableItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(consumableSlotIndex));

        charactersData[characterIndex].characterData.bag[bagSlotIndex] = consumableItemTemp;
        charactersData[characterIndex].characterData.consumables[consumableSlotIndex] = bagSlotTemp;
        characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlotIndex]);
        characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.consumables[consumableSlotIndex]);
    }
    private void ChangeEquipmentAndBag(ItemBaseSO.TypeObject equipmentIndex, int bagSlotIndex)
    {
        if (charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO?.typeObject == equipmentIndex ||
            charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO == null)
        {
            CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
            CharacterData.CharacterItem equipmentItemTemp = new CharacterData.CharacterItem(GetEquipmentItemByIndex(equipmentIndex));

            charactersData[characterIndex].characterData.equipments[equipmentIndex] = bagItemTemp;
            charactersData[characterIndex].characterData.bag[bagSlotIndex] = equipmentItemTemp;
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlotIndex]);
            characterPlayerHud.GetEquipmentSlotByIndex(equipmentIndex).InitializeSlot(charactersData[characterIndex].characterData.equipments[equipmentIndex]);
        }
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
    public CharacterData.CharacterItem GetEquipmentItemByIndex(ItemBaseSO.TypeObject index)
    {
        if (charactersData[characterIndex].characterData.equipments.TryGetValue(index, out CharacterData.CharacterItem equipmentItem))
        {
            return equipmentItem;
        }
        return new CharacterData.CharacterItem();
    }
    public ItemBaseSO.TypeObject ConvertTypeIntoTypeObject(int typeObjectInt)
    {
        return typeObjectInt switch
        {
            1 => ItemBaseSO.TypeObject.Helmet,
            2 => ItemBaseSO.TypeObject.Front,
            3 => ItemBaseSO.TypeObject.Pants,
            4 => ItemBaseSO.TypeObject.Boots,
            5 => ItemBaseSO.TypeObject.Gloves,
            6 => ItemBaseSO.TypeObject.Pendant,
            7 => ItemBaseSO.TypeObject.Ring,
            8 => ItemBaseSO.TypeObject.Weapon,
            9 => ItemBaseSO.TypeObject.Utility,
            _ => ItemBaseSO.TypeObject.None,
        };
    }
    public void DropItem()
    {
        int draggedSlotIndex = characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex;
        if (characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag)
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetBagItemByIndex(draggedSlotIndex), true);
            LaunchDropItem(itemDropped);
            charactersData[characterIndex].characterData.bag[draggedSlotIndex] = new CharacterData.CharacterItem();
            characterPlayerHud.GetBagSlotByIndex(draggedSlotIndex).InitializeSlot(new CharacterData.CharacterItem());
        }
        else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment)
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetEquipmentItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject), true);
            LaunchDropItem(itemDropped);
            charactersData[characterIndex].characterData.equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject] = new CharacterData.CharacterItem();
            characterPlayerHud.GetEquipmentSlotByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject).InitializeSlot(new CharacterData.CharacterItem());
        }
        else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables)
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetConsumableItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex), true);
            LaunchDropItem(itemDropped);
            charactersData[characterIndex].characterData.consumables[characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex] = new CharacterData.CharacterItem();
            characterPlayerHud.GetConsumableSlotByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex).InitializeSlot(new CharacterData.CharacterItem());
        }
    }
    void LaunchDropItem(ItemDropped itemDropped)
    {
        CameraInfo.Instance.CamDirection(new Vector3(directionAnimation.x, 0, directionAnimation.z), out Vector3 directionFromCamera);
        if (directionFromCamera == Vector3.zero)
            return;

        directionFromCamera.Normalize();
        itemDropped.rb.linearVelocity = Vector3.zero;
        itemDropped.rb.AddForce(directionFromCamera * dropLaunchForce + Vector3.up * dropUpForce, ForceMode.Impulse);
    }
}
