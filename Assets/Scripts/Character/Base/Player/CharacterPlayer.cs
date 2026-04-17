using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayer : CharacterBase
{
    public InputSystem_Actions inputActions;
    public GameObject interactableBannerPrefab;
    private float dropLaunchForce = 4f;
    private float dropUpForce = 2f;
    public SerializedDictionary<InteractableBase, GameObject> interactables = new SerializedDictionary<InteractableBase, GameObject>();
    public Action OnShowItemsToPickUp;
    public bool isChangingCharacter;
    public bool isInventoryOpen;
    public override void OnEnableHandle()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        inputActions.Player.ChangeCharacter.performed += OnHandleChangeCharacter;
        inputActions.Player.ToggleInventory.performed += OnHandleToggleInventory;
        inputActions.Player.ChangeFastItem.performed += OnHandleChangeFastItem;
        inputActions.Player.UseFastItem.performed += OnHandleUseFastItem;
        OnShowItemsToPickUp += OnHandleShowItemsToPickUp;
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
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing character: {ex.Message}");
        }
    }
    async Awaitable InitializeItems()
    {
        foreach (CharactersData character in charactersData)
        {
            character.characterData.InitializeItems();
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
    async Awaitable ChangeCharacterAction()
    {
        await characterPlayerHud.ChangeCharacterPortrait();
        isChangingCharacter = false;
    }
    void OnHandleToggleInventory(InputAction.CallbackContext context)
    {
        isInventoryOpen = !isInventoryOpen;
        _ = characterPlayerHud.ToggleCharacterInventory();
    }
    public void OnHandleUseFastItem(InputAction.CallbackContext context)
    {
        UseItem();
    }
    public override void UseItem()
    {
        if (isInventoryOpen) return;
        if (charactersData[characterIndex].characterData.consumables[currentFastItemIndex].itemBaseSO) charactersData[characterIndex].characterData.consumables[currentFastItemIndex].itemBaseSO.UseItem(this, charactersData[characterIndex].characterData.consumables[currentFastItemIndex]);
    }
    public override void UseItem(int bagSlotIndex)
    {
        if (isInventoryOpen) return;
        if (charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO) charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO.UseItem(this, charactersData[characterIndex].characterData.bag[bagSlotIndex]);
    }
    public void OnHandleShowItemsToPickUp()
    {
        characterPlayerHud.ShowItemsToPickUp();
    }
    public override void OnHandlePickUpItem(ItemDropped itemDropped)
    {
        if (FindEmptyBagSlot(out int bagIndex))
        {
            charactersData[characterIndex].characterData.bag[bagIndex] = itemDropped.itemData;
            characterPlayerHud.GetBagSlotByIndex(bagIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagIndex]);
            Destroy(itemDropped.gameObject);
            interactables.Remove(itemDropped);
            OnShowItemsToPickUp?.Invoke();
        }
    }
    void OnHandleChangeFastItem(InputAction.CallbackContext context)
    {
        if (isInventoryOpen) return;
        currentFastItemIndex += (int)context.ReadValue<float>();
        if (currentFastItemIndex < 0) currentFastItemIndex = characterPlayerHud.characterUI.fastItems.Count - 1;
        else if (currentFastItemIndex >= characterPlayerHud.characterUI.fastItems.Count) currentFastItemIndex = 0;
        characterPlayerHud.SelectFastItem();
    }
    public void ChangeObjectPosition()
    {
        int lastSelectedSlotIndex = characterPlayerHud.lastSelectedSlot.slotIndex;
        int draggedSlotIndex = characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex;

        if (characterPlayerHud.lastSelectedSlot == characterPlayerHud.inventoryDraggedSlot.itemDraged) return;

        if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag && characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag)
        {
            ChangeBagToBag(lastSelectedSlotIndex, draggedSlotIndex);
        }
        if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Consumables && characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Consumables)
        {
            ChangeConsumableToConsumable(lastSelectedSlotIndex, draggedSlotIndex);
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot != InventorySlot.TypeInventorySlot.Bag)
        {
            if (characterPlayerHud.lastSelectedSlot.typeInventorySlot != InventorySlot.TypeInventorySlot.Consumables &&
                characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment)
            {
                _ = ChangeEquipmentAndBag(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject, draggedSlotIndex);
            }
            else if ((characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Consumables || characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag) &&
                     characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables)
            {
                ChangeBagAndConsumable(draggedSlotIndex, lastSelectedSlotIndex, false);
            }
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot != InventorySlot.TypeInventorySlot.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag)
        {
            if (characterPlayerHud.lastSelectedSlot.typeInventorySlot != InventorySlot.TypeInventorySlot.Consumables &&
                characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment)
            {
                _ = ChangeEquipmentAndBag(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject, lastSelectedSlotIndex);
            }
            else if ((characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Consumables || characterPlayerHud.lastSelectedSlot.typeInventorySlot == InventorySlot.TypeInventorySlot.Bag) &&
                     characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables)
            {
                ChangeBagAndConsumable(lastSelectedSlotIndex, draggedSlotIndex, true);
            }
        }
        _ = characterPlayerHud.ResetInventoryTarget();
    }
    void ChangeBagToBag(int bagSlotIndex, int draggedBagSlotIndex)
    {
        if (charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO != null &&
            charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO == charactersData[characterIndex].characterData.bag[draggedBagSlotIndex].itemBaseSO &&
            charactersData[characterIndex].characterData.bag[bagSlotIndex].amount < charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO.maxStack)
        {
            FindAmountToAppend(charactersData[characterIndex].characterData.bag[draggedBagSlotIndex], charactersData[characterIndex].characterData.bag[bagSlotIndex], out int amountToAppend);
            charactersData[characterIndex].characterData.bag[bagSlotIndex].amount += amountToAppend;
            charactersData[characterIndex].characterData.bag[draggedBagSlotIndex].amount -= amountToAppend;
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlotIndex]);
            if (charactersData[characterIndex].characterData.bag[draggedBagSlotIndex].amount <= 0)
            {
                charactersData[characterIndex].characterData.bag[draggedBagSlotIndex] = new CharacterData.CharacterItem();
            }
            characterPlayerHud.GetBagSlotByIndex(draggedBagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[draggedBagSlotIndex]);
        }
        else
        {
            CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
            CharacterData.CharacterItem draggedItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(draggedBagSlotIndex));

            charactersData[characterIndex].characterData.bag[draggedBagSlotIndex] = bagItemTemp;
            charactersData[characterIndex].characterData.bag[bagSlotIndex] = draggedItemTemp;
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlotIndex]);
            characterPlayerHud.GetBagSlotByIndex(draggedBagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[draggedBagSlotIndex]);
        }
    }
    void ChangeConsumableToConsumable(int consumableSlotIndex, int draggedConsumableSlotIndex)
    {
        if (charactersData[characterIndex].characterData.consumables[consumableSlotIndex].itemBaseSO != null &&
            charactersData[characterIndex].characterData.consumables[consumableSlotIndex].itemBaseSO == charactersData[characterIndex].characterData.consumables[draggedConsumableSlotIndex].itemBaseSO &&
            charactersData[characterIndex].characterData.consumables[consumableSlotIndex].amount < charactersData[characterIndex].characterData.consumables[consumableSlotIndex].itemBaseSO.maxStack)
        {
            FindAmountToAppend(charactersData[characterIndex].characterData.consumables[draggedConsumableSlotIndex], charactersData[characterIndex].characterData.consumables[consumableSlotIndex], out int amountToAppend);
            charactersData[characterIndex].characterData.consumables[consumableSlotIndex].amount += amountToAppend;
            charactersData[characterIndex].characterData.consumables[draggedConsumableSlotIndex].amount -= amountToAppend;
            characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.consumables[consumableSlotIndex]);
            if (charactersData[characterIndex].characterData.consumables[draggedConsumableSlotIndex].amount <= 0)
            {
                charactersData[characterIndex].characterData.consumables[draggedConsumableSlotIndex] = new CharacterData.CharacterItem();
            }
            characterPlayerHud.GetConsumableSlotByIndex(draggedConsumableSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.consumables[draggedConsumableSlotIndex]);
        }
        else
        {
            CharacterData.CharacterItem consumableItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(consumableSlotIndex));
            CharacterData.CharacterItem draggedItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(draggedConsumableSlotIndex));

            charactersData[characterIndex].characterData.consumables[draggedConsumableSlotIndex] = consumableItemTemp;
            charactersData[characterIndex].characterData.consumables[consumableSlotIndex] = draggedItemTemp;
            characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.consumables[consumableSlotIndex]);
            characterPlayerHud.GetConsumableSlotByIndex(draggedConsumableSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.consumables[draggedConsumableSlotIndex]);
        }
        characterPlayerHud.UpdateFastItems();
    }
    async Awaitable ChangeEquipmentAndBag(ItemBaseSO.TypeObject equipmentIndex, int bagSlotIndex)
    {
        if (charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO?.typeObject == equipmentIndex ||
            charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO == null)
        {
            CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
            CharacterData.CharacterItem equipmentItemTemp = new CharacterData.CharacterItem(GetEquipmentItemByIndex(equipmentIndex));

            charactersData[characterIndex].characterData.equipments[equipmentIndex] = bagItemTemp;
            charactersData[characterIndex].characterData.bag[bagSlotIndex] = equipmentItemTemp;

            if (equipmentItemTemp.itemBaseSO) await equipmentItemTemp.itemBaseSO.DesEquipItem(this, equipmentItemTemp);
            if (bagItemTemp.itemBaseSO) await bagItemTemp.itemBaseSO.EquipItem(this, bagItemTemp);
            characterPlayerHud.RefreshCharacterStatistics();
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlotIndex]);
            characterPlayerHud.GetEquipmentSlotByIndex(equipmentIndex).InitializeSlot(charactersData[characterIndex].characterData.equipments[equipmentIndex]);
        }
    }
    private void ChangeBagAndConsumable(int bagSlotIndex, int consumableSlotIndex, bool isFromBagToConsumable)
    {
        if (!isFromBagToConsumable)
        {
            if (charactersData[characterIndex].characterData.consumables[consumableSlotIndex].itemBaseSO != null &&
                charactersData[characterIndex].characterData.consumables[consumableSlotIndex].itemBaseSO == charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO &&
                charactersData[characterIndex].characterData.consumables[consumableSlotIndex].amount < charactersData[characterIndex].characterData.consumables[consumableSlotIndex].itemBaseSO.maxStack)
            {
                FindAmountToAppend(charactersData[characterIndex].characterData.bag[bagSlotIndex], charactersData[characterIndex].characterData.consumables[consumableSlotIndex], out int amountToAppend);
                charactersData[characterIndex].characterData.consumables[consumableSlotIndex].amount += amountToAppend;
                charactersData[characterIndex].characterData.bag[bagSlotIndex].amount -= amountToAppend;
                characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.consumables[consumableSlotIndex]);
                if (charactersData[characterIndex].characterData.bag[bagSlotIndex].amount <= 0)
                {
                    charactersData[characterIndex].characterData.bag[bagSlotIndex] = new CharacterData.CharacterItem();
                }
                characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlotIndex]);
            }
            else
            {
                DiferentBagAndConsumable(bagSlotIndex, consumableSlotIndex);
            }
        }
        else
        {
            if (charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO != null &&
                charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO == charactersData[characterIndex].characterData.consumables[consumableSlotIndex].itemBaseSO &&
                charactersData[characterIndex].characterData.bag[bagSlotIndex].amount < charactersData[characterIndex].characterData.bag[bagSlotIndex].itemBaseSO.maxStack)
            {
                FindAmountToAppend(charactersData[characterIndex].characterData.consumables[consumableSlotIndex], charactersData[characterIndex].characterData.bag[bagSlotIndex], out int amountToAppend);
                charactersData[characterIndex].characterData.bag[bagSlotIndex].amount += amountToAppend;
                charactersData[characterIndex].characterData.consumables[consumableSlotIndex].amount -= amountToAppend;
                characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlotIndex]);
                if (charactersData[characterIndex].characterData.consumables[consumableSlotIndex].amount <= 0)
                {
                    charactersData[characterIndex].characterData.consumables[consumableSlotIndex] = new CharacterData.CharacterItem();
                }
                characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.consumables[consumableSlotIndex]);
            }
            else
            {
                DiferentBagAndConsumable(bagSlotIndex, consumableSlotIndex);
            }
        }
        characterPlayerHud.UpdateFastItems();
    }
    void DiferentBagAndConsumable(int bagSlotIndex, int consumableSlotIndex)
    {
        CharacterData.CharacterItem bagSlotTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
        CharacterData.CharacterItem consumableItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(consumableSlotIndex));

        charactersData[characterIndex].characterData.bag[bagSlotIndex] = consumableItemTemp;
        charactersData[characterIndex].characterData.consumables[consumableSlotIndex] = bagSlotTemp;
        characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[bagSlotIndex]);
        characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].characterData.consumables[consumableSlotIndex]);
        characterPlayerHud.UpdateFastItems();
    }
    public bool FindEmptyBagSlot(out int bagIndex)
    {
        foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in charactersData[characterIndex].characterData.bag)
        {
            if (bagSlot.Value.itemBaseSO == null)
            {
                bagIndex = bagSlot.Key;
                return true;
            }
        }
        bagIndex = 0;
        return false;
    }
    public bool FindEmptyConsumableSlot(out int bagIndex)
    {
        foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in charactersData[characterIndex].characterData.consumables)
        {
            if (bagSlot.Value.itemBaseSO == null)
            {
                bagIndex = bagSlot.Key;
                return true;
            }
        }
        bagIndex = 0;
        return false;
    }
    public bool FindSimilarConsumableSlot(int bagIndex, out int consumableIndex)
    {
        foreach (KeyValuePair<int, CharacterData.CharacterItem> consumableSlot in charactersData[characterIndex].characterData.consumables)
        {
            if (consumableSlot.Value.itemBaseSO != null && consumableSlot.Value.itemBaseSO.id == charactersData[characterIndex].characterData.bag[bagIndex].itemBaseSO.id && consumableSlot.Value.amount < consumableSlot.Value.itemBaseSO.maxStack)
            {
                consumableIndex = consumableSlot.Key;
                return true;
            }
        }
        consumableIndex = 0;
        return false;
    }
    public void FindAmountToAppend(CharacterData.CharacterItem fromAppend, CharacterData.CharacterItem toAppend, out int amountToAppend)
    {
        if (fromAppend.amount + toAppend.amount <= toAppend.itemBaseSO.maxStack)
        {
            amountToAppend = fromAppend.amount;
        }
        else
        {
            amountToAppend = toAppend.itemBaseSO.maxStack - toAppend.amount;
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
    public void FastEquipItem(int slotIndex)
    {
        if (characterPlayerHud.isDraggingItem) return;
        if (charactersData[characterIndex].characterData.bag[slotIndex].itemBaseSO?.typeObject != ItemBaseSO.TypeObject.Consumable)
        {
            if (GetEquipmentItemByIndex(charactersData[characterIndex].characterData.bag[slotIndex].itemBaseSO.typeObject).itemBaseSO != null)
            {
                _ = ChangeEquipmentAndBag(charactersData[characterIndex].characterData.bag[slotIndex].itemBaseSO.typeObject, slotIndex);
            }
            else
            {
                _ = ChangeEquipmentAndBag(charactersData[characterIndex].characterData.bag[slotIndex].itemBaseSO.typeObject, slotIndex);
            }
        }
        else
        {
            if (FindSimilarConsumableSlot(slotIndex, out int similarConsumableIndex))
            {
                FindAmountToAppend(charactersData[characterIndex].characterData.bag[slotIndex], charactersData[characterIndex].characterData.consumables[similarConsumableIndex], out int amountToAppend);
                charactersData[characterIndex].characterData.bag[slotIndex].amount -= amountToAppend;
                charactersData[characterIndex].characterData.consumables[similarConsumableIndex].amount += amountToAppend;
                characterPlayerHud.GetConsumableSlotByIndex(similarConsumableIndex).InitializeSlot(charactersData[characterIndex].characterData.consumables[similarConsumableIndex]);
                if (charactersData[characterIndex].characterData.bag[slotIndex].amount <= 0)
                {
                    charactersData[characterIndex].characterData.bag[slotIndex] = new CharacterData.CharacterItem();
                }
                characterPlayerHud.GetBagSlotByIndex(slotIndex).InitializeSlot(charactersData[characterIndex].characterData.bag[slotIndex]);
            }
            else if (FindEmptyConsumableSlot(out int consumableIndex))
            {
                ChangeBagAndConsumable(slotIndex, consumableIndex, true);
            }
            characterPlayerHud.UpdateFastItems();
        }
        _ = characterPlayerHud.ResetInventoryTarget();
    }
    public async Task DropItem()
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
            if (charactersData[characterIndex].characterData.equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject].itemBaseSO) await charactersData[characterIndex].characterData.equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject].itemBaseSO.DesEquipItem(this, GetEquipmentItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject));
            charactersData[characterIndex].characterData.equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject] = new CharacterData.CharacterItem();
            characterPlayerHud.GetEquipmentSlotByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject).InitializeSlot(new CharacterData.CharacterItem());
            characterPlayerHud.RefreshCharacterStatistics();
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
