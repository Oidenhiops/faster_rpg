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
    public CharacterBase otherCharacterToMakeSkill;
    public CharacterPlayerCamera characterPlayerCamera;
    public bool hideWeaponsInHand;
    public override void OnEnableHandle()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        inputActions.Player.ChangeCharacter.performed += OnHandleChangeCharacter;
        inputActions.Player.ToggleInventory.performed += OnHandleToggleInventory;
        inputActions.Player.ChangeFastItem.performed += OnHandleChangeFastItem;
        inputActions.Player.UseFastItem.performed += OnHandleUseFastItem;
        inputActions.Player.UseSkill.performed += OnHandleUseSkill;
        inputActions.Player.MoveCamera.performed += MoveCamera;
        OnShowItemsToPickUp += OnHandleShowItemsToPickUp;
    }
    public async override Awaitable InitializeCharacter()
    {
        try
        {
            List<CharacterData> charactersDataList = new List<CharacterData>();
            foreach (var characterData in GameData.Instance.gameDataInfo.gameDataSlots[GameData.Instance.systemDataInfo.currentGameDataIndex].characters)
            {
                charactersDataList.Add(characterData.Value);
            }
            charactersData = charactersDataList.ToArray();
            await InitializeItems();
            for (int i = 0; i < 4; i++)
            {
                if (i <= charactersData.Length - 1)
                {
                    characterPlayerHud.characterUI.characterPortraits[i].portraitObject.SetActive(true);
                    // characterPlayerHud.characterUI.characterPortraits[i].characterSprite.sprite = charactersDataList[i].characterSkin.icon;
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
        foreach (CharacterData character in charactersData)
        {
            character.InitializeItems();
        }
    }
    void OnHandleChangeCharacter(InputAction.CallbackContext context)
    {
        if (!isChangingCharacter && charactersData.Length - 1 >= context.ReadValue<float>() && characterIndex != (int)context.ReadValue<float>())
        {
            isChangingCharacter = true;
            dissolve.NeedAppear();
            characterPlayerHud.characterUI.characterPortraits[characterIndex].characterBg.color = Color.white;
            characterIndex = (int)context.ReadValue<float>();
            characterPlayerHud.characterUI.characterPortraits[characterIndex].characterBg.color = Color.yellow;
            _ = InitializeAnimations();
            _ = ChangeCharacterAction();
            _ = characterPlayerHud.RefreshCharacterInventory();
            characterPlayerHud.RefreshStatusEffects();
        }
    }
    public void MoveCamera(InputAction.CallbackContext context)
    {
        if (isInventoryOpen) return;
        if (inputActions.Player.UnlockCamera.ReadValue<float>() == 1)
        {
            characterPlayerCamera.MoveCamera(context);
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
        if (!isInventoryOpen) UseItem();
    }
    public void OnHandleUseSkill(InputAction.CallbackContext context)
    {
        if (!isInventoryOpen) UseSkill(Mathf.RoundToInt(context.ReadValue<float>()));
    }
    public override void UseSkill(int skillIndex)
    {
        if (charactersData[characterIndex].skills[skillIndex].skillsBaseSO != null &&
            !(skillsCd.ContainsKey(characterIndex) && skillsCd[characterIndex].ContainsKey(skillIndex)) &&
            charactersData[characterIndex].skills[skillIndex].skillsBaseSO.ValidateCanUseSkill(this, characterIndex, charactersData[characterIndex].skills[skillIndex].level))
        {
            if (charactersData[characterIndex].skills[skillIndex].skillsBaseSO.UseSkill(this, otherCharacterToMakeSkill ? otherCharacterToMakeSkill : this, skillIndex))
            {
                if (skillsCd.ContainsKey(characterIndex))
                {
                    skillsCd[characterIndex].Add(0, new SkillCd 
                    { 
                        maxCd = charactersData[characterIndex].skills[skillIndex].skillsBaseSO.statistics[charactersData[characterIndex].skills[skillIndex].level][CharacterData.TypeStatistic.Cd].baseValue,
                        currentCd = charactersData[characterIndex].skills[skillIndex].skillsBaseSO.statistics[charactersData[characterIndex].skills[skillIndex].level][CharacterData.TypeStatistic.Cd].baseValue
                    });
                }
                else
                {
                    skillsCd.Add(characterIndex, new SerializedDictionary<int, SkillCd> { { 0, new SkillCd 
                    { 
                        maxCd = charactersData[characterIndex].skills[skillIndex].skillsBaseSO.statistics[charactersData[characterIndex].skills[skillIndex].level][CharacterData.TypeStatistic.Cd].baseValue,
                        currentCd = charactersData[characterIndex].skills[skillIndex].skillsBaseSO.statistics[charactersData[characterIndex].skills[skillIndex].level][CharacterData.TypeStatistic.Cd].baseValue 
                    }}});
                }
                if (handleUseSkillCoroutine == null)
                {
                    handleUseSkillCoroutine = StartCoroutine(HandleUseSkill());
                }
            }
        }
    }
    public override void UseItem()
    {
        if (isInventoryOpen) return;
        if (charactersData[characterIndex].consumables[currentFastItemIndex].itemBaseSO) 
                charactersData[characterIndex].consumables[currentFastItemIndex].itemBaseSO.UseItem(this, charactersData[characterIndex].consumables[currentFastItemIndex]);
    }
    public override void UseItem(int bagSlotIndex)
    {
        if (isInventoryOpen) return;
        if (charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO) charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO.UseItem(this, charactersData[characterIndex].bag[bagSlotIndex]);
    }
    public void OnHandleShowItemsToPickUp()
    {
        characterPlayerHud.ShowItemsToPickUp();
    }
    public override void OnHandlePickUpItem(ItemDropped itemDropped)
    {
        if (FindEmptyBagSlot(out int bagIndex))
        {
            charactersData[characterIndex].bag[bagIndex] = itemDropped.itemData;
            characterPlayerHud.GetBagSlotByIndex(bagIndex).InitializeSlot(charactersData[characterIndex].bag[bagIndex]);
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
        if (charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO != null &&
            charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO == charactersData[characterIndex].bag[draggedBagSlotIndex].itemBaseSO &&
            charactersData[characterIndex].bag[bagSlotIndex].amount < charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO.maxStack)
        {
            FindAmountToAppend(charactersData[characterIndex].bag[draggedBagSlotIndex], charactersData[characterIndex].bag[bagSlotIndex], out int amountToAppend);
            charactersData[characterIndex].bag[bagSlotIndex].amount += amountToAppend;
            charactersData[characterIndex].bag[draggedBagSlotIndex].amount -= amountToAppend;
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
            if (charactersData[characterIndex].bag[draggedBagSlotIndex].amount <= 0)
            {
                charactersData[characterIndex].bag[draggedBagSlotIndex] = new CharacterData.CharacterItem();
            }
            characterPlayerHud.GetBagSlotByIndex(draggedBagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[draggedBagSlotIndex]);
        }
        else
        {
            CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
            CharacterData.CharacterItem draggedItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(draggedBagSlotIndex));

            charactersData[characterIndex].bag[draggedBagSlotIndex] = bagItemTemp;
            charactersData[characterIndex].bag[bagSlotIndex] = draggedItemTemp;
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
            characterPlayerHud.GetBagSlotByIndex(draggedBagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[draggedBagSlotIndex]);
        }
    }
    void ChangeConsumableToConsumable(int consumableSlotIndex, int draggedConsumableSlotIndex)
    {
        if (charactersData[characterIndex].consumables[consumableSlotIndex].itemBaseSO != null &&
            charactersData[characterIndex].consumables[consumableSlotIndex].itemBaseSO == charactersData[characterIndex].consumables[draggedConsumableSlotIndex].itemBaseSO &&
            charactersData[characterIndex].consumables[consumableSlotIndex].amount < charactersData[characterIndex].consumables[consumableSlotIndex].itemBaseSO.maxStack)
        {
            FindAmountToAppend(charactersData[characterIndex].consumables[draggedConsumableSlotIndex], charactersData[characterIndex].consumables[consumableSlotIndex], out int amountToAppend);
            charactersData[characterIndex].consumables[consumableSlotIndex].amount += amountToAppend;
            charactersData[characterIndex].consumables[draggedConsumableSlotIndex].amount -= amountToAppend;
            characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].consumables[consumableSlotIndex]);
            if (charactersData[characterIndex].consumables[draggedConsumableSlotIndex].amount <= 0)
            {
                charactersData[characterIndex].consumables[draggedConsumableSlotIndex] = new CharacterData.CharacterItem();
            }
            characterPlayerHud.GetConsumableSlotByIndex(draggedConsumableSlotIndex).InitializeSlot(charactersData[characterIndex].consumables[draggedConsumableSlotIndex]);
        }
        else
        {
            CharacterData.CharacterItem consumableItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(consumableSlotIndex));
            CharacterData.CharacterItem draggedItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(draggedConsumableSlotIndex));

            charactersData[characterIndex].consumables[draggedConsumableSlotIndex] = consumableItemTemp;
            charactersData[characterIndex].consumables[consumableSlotIndex] = draggedItemTemp;
            characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].consumables[consumableSlotIndex]);
            characterPlayerHud.GetConsumableSlotByIndex(draggedConsumableSlotIndex).InitializeSlot(charactersData[characterIndex].consumables[draggedConsumableSlotIndex]);
        }
        characterPlayerHud.UpdateFastItems();
    }
    async Awaitable ChangeEquipmentAndBag(ItemBaseSO.TypeObject equipmentIndex, int bagSlotIndex)
    {
        if (charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO?.typeObject == equipmentIndex ||
            charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO == null)
        {
            CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
            CharacterData.CharacterItem equipmentItemTemp = new CharacterData.CharacterItem(GetEquipmentItemByIndex(equipmentIndex));

            charactersData[characterIndex].equipments[equipmentIndex] = bagItemTemp;
            charactersData[characterIndex].bag[bagSlotIndex] = equipmentItemTemp;

            if (equipmentItemTemp.itemBaseSO) await equipmentItemTemp.itemBaseSO.DesEquipItem(this, equipmentItemTemp);
            if (bagItemTemp.itemBaseSO) await bagItemTemp.itemBaseSO.EquipItem(this, bagItemTemp);
            characterPlayerHud.RefreshCharacterStatistics();
            if ((equipmentItemTemp.itemBaseSO && equipmentItemTemp.itemBaseSO.typeObject == ItemBaseSO.TypeObject.Utility) || 
                bagItemTemp.itemBaseSO && bagItemTemp.itemBaseSO.typeObject == ItemBaseSO.TypeObject.Utility)
            {
                characterPlayerHud.RefreshSkills();
            }
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
            characterPlayerHud.GetEquipmentSlotByIndex(equipmentIndex).InitializeSlot(charactersData[characterIndex].equipments[equipmentIndex]);
        }
    }
    private void ChangeBagAndConsumable(int bagSlotIndex, int consumableSlotIndex, bool isFromBagToConsumable)
    {
        if (!isFromBagToConsumable)
        {
            if (charactersData[characterIndex].consumables[consumableSlotIndex].itemBaseSO != null &&
                charactersData[characterIndex].consumables[consumableSlotIndex].itemBaseSO == charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO &&
                charactersData[characterIndex].consumables[consumableSlotIndex].amount < charactersData[characterIndex].consumables[consumableSlotIndex].itemBaseSO.maxStack)
            {
                FindAmountToAppend(charactersData[characterIndex].bag[bagSlotIndex], charactersData[characterIndex].consumables[consumableSlotIndex], out int amountToAppend);
                charactersData[characterIndex].consumables[consumableSlotIndex].amount += amountToAppend;
                charactersData[characterIndex].bag[bagSlotIndex].amount -= amountToAppend;
                characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].consumables[consumableSlotIndex]);
                if (charactersData[characterIndex].bag[bagSlotIndex].amount <= 0)
                {
                    charactersData[characterIndex].bag[bagSlotIndex] = new CharacterData.CharacterItem();
                }
                characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
            }
            else
            {
                DiferentBagAndConsumable(bagSlotIndex, consumableSlotIndex);
            }
        }
        else
        {
            if (charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO != null &&
                charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO == charactersData[characterIndex].consumables[consumableSlotIndex].itemBaseSO &&
                charactersData[characterIndex].bag[bagSlotIndex].amount < charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO.maxStack)
            {
                FindAmountToAppend(charactersData[characterIndex].consumables[consumableSlotIndex], charactersData[characterIndex].bag[bagSlotIndex], out int amountToAppend);
                charactersData[characterIndex].bag[bagSlotIndex].amount += amountToAppend;
                charactersData[characterIndex].consumables[consumableSlotIndex].amount -= amountToAppend;
                characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
                if (charactersData[characterIndex].consumables[consumableSlotIndex].amount <= 0)
                {
                    charactersData[characterIndex].consumables[consumableSlotIndex] = new CharacterData.CharacterItem();
                }
                characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].consumables[consumableSlotIndex]);
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

        charactersData[characterIndex].bag[bagSlotIndex] = consumableItemTemp;
        charactersData[characterIndex].consumables[consumableSlotIndex] = bagSlotTemp;
        characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
        characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].consumables[consumableSlotIndex]);
        characterPlayerHud.UpdateFastItems();
    }
    public bool FindEmptyBagSlot(out int bagIndex)
    {
        foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in charactersData[characterIndex].bag)
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
        foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in charactersData[characterIndex].consumables)
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
        foreach (KeyValuePair<int, CharacterData.CharacterItem> consumableSlot in charactersData[characterIndex].consumables)
        {
            if (consumableSlot.Value.itemBaseSO != null && consumableSlot.Value.itemBaseSO.id == charactersData[characterIndex].bag[bagIndex].itemBaseSO.id && consumableSlot.Value.amount < consumableSlot.Value.itemBaseSO.maxStack)
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
        if (charactersData[characterIndex].bag.TryGetValue(index, out CharacterData.CharacterItem bagItem))
        {
            return bagItem;
        }
        return new CharacterData.CharacterItem();
    }
    public CharacterData.CharacterItem GetConsumableItemByIndex(int index)
    {
        if (charactersData[characterIndex].consumables.TryGetValue(index, out CharacterData.CharacterItem consumableItem))
        {
            return consumableItem;
        }
        return new CharacterData.CharacterItem();
    }
    public CharacterData.CharacterItem GetEquipmentItemByIndex(ItemBaseSO.TypeObject index)
    {
        if (charactersData[characterIndex].equipments.TryGetValue(index, out CharacterData.CharacterItem equipmentItem))
        {
            return equipmentItem;
        }
        return new CharacterData.CharacterItem();
    }
    public void FastEquipItem(int slotIndex)
    {
        if (characterPlayerHud.isDraggingItem) return;
        if (charactersData[characterIndex].bag[slotIndex].itemBaseSO?.typeObject != ItemBaseSO.TypeObject.Consumable)
        {
            if (GetEquipmentItemByIndex(charactersData[characterIndex].bag[slotIndex].itemBaseSO.typeObject).itemBaseSO != null)
            {
                _ = ChangeEquipmentAndBag(charactersData[characterIndex].bag[slotIndex].itemBaseSO.typeObject, slotIndex);
            }
            else
            {
                _ = ChangeEquipmentAndBag(charactersData[characterIndex].bag[slotIndex].itemBaseSO.typeObject, slotIndex);
            }
        }
        else
        {
            if (FindSimilarConsumableSlot(slotIndex, out int similarConsumableIndex))
            {
                FindAmountToAppend(charactersData[characterIndex].bag[slotIndex], charactersData[characterIndex].consumables[similarConsumableIndex], out int amountToAppend);
                charactersData[characterIndex].bag[slotIndex].amount -= amountToAppend;
                charactersData[characterIndex].consumables[similarConsumableIndex].amount += amountToAppend;
                characterPlayerHud.GetConsumableSlotByIndex(similarConsumableIndex).InitializeSlot(charactersData[characterIndex].consumables[similarConsumableIndex]);
                if (charactersData[characterIndex].bag[slotIndex].amount <= 0)
                {
                    charactersData[characterIndex].bag[slotIndex] = new CharacterData.CharacterItem();
                }
                characterPlayerHud.GetBagSlotByIndex(slotIndex).InitializeSlot(charactersData[characterIndex].bag[slotIndex]);
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
            charactersData[characterIndex].bag[draggedSlotIndex] = new CharacterData.CharacterItem();
            characterPlayerHud.GetBagSlotByIndex(draggedSlotIndex).InitializeSlot(new CharacterData.CharacterItem());
        }
        else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Equipment)
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetEquipmentItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject), true);
            LaunchDropItem(itemDropped);
            if (charactersData[characterIndex].equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject].itemBaseSO) await charactersData[characterIndex].equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject].itemBaseSO.DesEquipItem(this, GetEquipmentItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject));
            charactersData[characterIndex].equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject] = new CharacterData.CharacterItem();
            characterPlayerHud.GetEquipmentSlotByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject).InitializeSlot(new CharacterData.CharacterItem());
            characterPlayerHud.RefreshCharacterStatistics();
        }
        else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.generalTypeObject == ItemBaseSO.GeneralTypeObject.Consumables)
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetConsumableItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex), true);
            LaunchDropItem(itemDropped);
            charactersData[characterIndex].consumables[characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex] = new CharacterData.CharacterItem();
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
