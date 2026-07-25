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
    public CharacterPlayerCamera characterPlayerCamera;
    public override void OnEnableHandle()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        inputActions.Player.ChangeCharacter.performed += OnHandleChangeCharacter;
        inputActions.Player.ToggleInventory.performed += OnHandleToggleInventory;
        inputActions.Player.ChangeFastItem.performed += OnHandleChangeFastItem;
        inputActions.Player.UseFastItem.performed += OnHandleUseFastItem;
        inputActions.Player.UseSkill.performed += OnHandleUseSkill;
        inputActions.Player.MoveCamera.performed += OnHandleMoveCamera;
        inputActions.Player.MoveCamera.canceled += OnHandleMoveCamera;
        inputActions.Player.SetFreeCamera.performed += OnHandleSetFreeCamera;
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
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters data: {ex.Message}");
        }
        try
        {
            await InitializeItems();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters items: {ex.Message}");
        }
        try
        {
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
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters portraits: {ex.Message}");
        }
        try
        {
            await characterPlayerHud.InitializeInventory();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters inventory: {ex.Message}");
        }
        try
        {
            await InitializeModels();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters models: {ex.Message}");
        }
        try
        {
            characterMovement.HandleInitialize();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters movement: {ex.Message}");
        }
        try
        {
            dissolvePlayer.ObtainCharacterModels();
            dissolvePlayer.NeedAppear();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters dissolve: {ex.Message}");
        }
        isInitialize = true;
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
        if (!isChangingCharacter && charactersData.Length - 1 >= context.ReadValue<float>() && characterIndex != (int)context.ReadValue<float>() && !isInCanalization)
        {
            isChangingCharacter = true;
            dissolvePlayer.NeedAppear();
            characterPlayerHud.characterUI.characterPortraits[characterIndex].characterBg.color = Color.white;
            characterIndex = (int)context.ReadValue<float>();
            characterPlayerHud.characterUI.characterPortraits[characterIndex].characterBg.color = Color.yellow;
            _ = InitializeModels();
            _ = ChangeCharacterPortraits();
            _ = characterPlayerHud.RefreshCharacterInventory();
            characterPlayerHud.RefreshStatusEffects();
        }
    }
    async Awaitable ChangeCharacterPortraits()
    {
        await characterPlayerHud.ChangeCharacterPortrait();
        isChangingCharacter = false;
    }
    public void OnHandleMoveCamera(InputAction.CallbackContext context)
    {
        if (isInventoryOpen) return;
        characterPlayerCamera.OnHandleMoveCamera(context);
    }
    public void OnHandleSetFreeCamera(InputAction.CallbackContext context)
    {
        characterPlayerCamera.OnHandleSetFreeCamera(context);
    }
    void OnHandleToggleInventory(InputAction.CallbackContext context)
    {
        isInventoryOpen = !isInventoryOpen;
        _ = characterPlayerHud.ToggleCharacterInventory();
    }
    public void OnHandleUseFastItem(InputAction.CallbackContext context)
    {
        if (!isInventoryOpen && !isInCanalization) UseFastItem();
    }
    public override void UseFastItem()
    {
        if (isInventoryOpen || isInCanalization) return;
        if (charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO)
        {
            if (!charactersData[characterIndex].fastItems[currentFastItemIndex].itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability) || 
                charactersData[characterIndex].fastItems[currentFastItemIndex].itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability) &&
                charactersData[characterIndex].fastItems[currentFastItemIndex].itemStatistics[CharacterData.TypeStatistic.Durability].currentValue > 0)
            {
                charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO.UseItem(new ItemBaseSO.UseItemInfo(
                    character: this,
                    characterItem: charactersData[characterIndex].fastItems[currentFastItemIndex],
                    isFastItem: true
                ));
            }
        }
    }
    public void OnHandleUseSkill(InputAction.CallbackContext context)
    {
        if (!isInventoryOpen && !isInCanalization) _ = UseSkill(Mathf.RoundToInt(context.ReadValue<float>()));
    }
    public override async Awaitable UseSkill(int skillIndex)
    {

        if (charactersData[characterIndex].skills[skillIndex].skillsBaseSO != null &&
            !(skillsCd.ContainsKey(characterIndex) && skillsCd[characterIndex].ContainsKey(skillIndex)) &&
            charactersData[characterIndex].skills[skillIndex].skillsBaseSO.ValidateCanUseSkill(new SkillsBaseSO.CharacterMakeSkillData(this, characterIndex), charactersData[characterIndex].skills[skillIndex].level))
        {
            await charactersData[characterIndex].skills[skillIndex].skillsBaseSO.UseSkill(new SkillsBaseSO.CharacterMakeSkillData(this, characterIndex), charactersData[characterIndex].skills[skillIndex].level);
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
        if (isInventoryOpen || isInCanalization) return;
        if (charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO) _ = charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO.DesEquipItem(this, charactersData[characterIndex].fastItems[currentFastItemIndex], false, true);
        currentFastItemIndex += (int)context.ReadValue<float>();
        if (currentFastItemIndex < 0) currentFastItemIndex = characterPlayerHud.characterUI.fastItems.Count - 1;
        else if (currentFastItemIndex >= characterPlayerHud.characterUI.fastItems.Count) currentFastItemIndex = 0;
        if (charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO) _ = charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO.EquipItem(this, charactersData[characterIndex].fastItems[currentFastItemIndex], true, true);
        characterPlayerHud.SelectFastItem();
        UpdateFastItemModel();
        characterPlayerHud.RefreshCharacterStatistics();
    }
    public void ChangeObjectPosition()
    {
        int lastSelectedSlotIndex = characterPlayerHud.lastSelectedSlot.slotIndex;
        int draggedSlotIndex = characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex;

        if (characterPlayerHud.lastSelectedSlot == characterPlayerHud.inventoryDraggedSlot.itemDraged) return;

        if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == ItemsDBSO.TypeModel.Bag && characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == ItemsDBSO.TypeModel.Bag)
        {
            ChangeBagToBag(lastSelectedSlotIndex, draggedSlotIndex);
        }
        else if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == ItemsDBSO.TypeModel.FastItems && characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == ItemsDBSO.TypeModel.FastItems)
        {
            ChangeFasItemToFastItem(lastSelectedSlotIndex, draggedSlotIndex);
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == ItemsDBSO.TypeModel.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot != ItemsDBSO.TypeModel.Bag)
        {
            if (characterPlayerHud.lastSelectedSlot.typeInventorySlot != ItemsDBSO.TypeModel.FastItems && IsEquipableItem(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.typeObject))
            {
                _ = ChangeEquipmentAndBag(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject, draggedSlotIndex);
            }
            else if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == ItemsDBSO.TypeModel.FastItems || characterPlayerHud.lastSelectedSlot.typeInventorySlot == ItemsDBSO.TypeModel.Bag)
            {
                ChangeBagAndFastItem(draggedSlotIndex, lastSelectedSlotIndex, false);
            }
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot != ItemsDBSO.TypeModel.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot == ItemsDBSO.TypeModel.Bag)
        {
            if (characterPlayerHud.lastSelectedSlot.typeInventorySlot != ItemsDBSO.TypeModel.FastItems && IsEquipableItem(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.typeObject) && characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot != ItemsDBSO.TypeModel.FastItems)
            {
                _ = ChangeEquipmentAndBag(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject, lastSelectedSlotIndex);
            }
            else if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == ItemsDBSO.TypeModel.FastItems || characterPlayerHud.lastSelectedSlot.typeInventorySlot == ItemsDBSO.TypeModel.Bag)
            {
                ChangeBagAndFastItem(lastSelectedSlotIndex, draggedSlotIndex, true);
            }
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == ItemsDBSO.TypeModel.FastItems && 
            IsEquipableItem(characterPlayerHud.lastSelectedSlot.typeInventorySlot))
        {
            if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject == characterPlayerHud.lastSelectedSlot.typeInventorySlot || 
                !characterPlayerHud.lastSelectedSlot.characterItem.itemBaseSO)
            {
                FastItemToEquipment(characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex, characterPlayerHud.lastSelectedSlot.typeInventorySlot);
            }
        }
        else if (
            characterPlayerHud.lastSelectedSlot.typeInventorySlot == ItemsDBSO.TypeModel.FastItems && 
            IsEquipableItem(characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot))
        {
            if (characterPlayerHud.lastSelectedSlot.characterItem.typeObject == characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject ||
                !characterPlayerHud.lastSelectedSlot.characterItem.itemBaseSO)
            {
                FastItemToEquipment(characterPlayerHud.lastSelectedSlot.slotIndex, characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot);
            }
        }
        _ = characterPlayerHud.ResetInventoryTarget();
    }
    void ChangeBagToBag(int bagSlotIndex, int draggedBagSlotIndex)
    {
        if (charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO != null &&
            charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO == charactersData[characterIndex].bag[draggedBagSlotIndex].itemBaseSO &&
            charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
        {
            FindAmountToAppend(charactersData[characterIndex].bag[draggedBagSlotIndex], charactersData[characterIndex].bag[bagSlotIndex], out int amountToAppend);
            charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue += amountToAppend;
            charactersData[characterIndex].bag[draggedBagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue -= amountToAppend;
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
            if (charactersData[characterIndex].bag[draggedBagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
            {
                charactersData[characterIndex].bag[draggedBagSlotIndex] = new CharacterData.CharacterItem(ItemsDBSO.TypeModel.Bag);
            }
            characterPlayerHud.GetBagSlotByIndex(draggedBagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[draggedBagSlotIndex]);
            characterPlayerHud.RefreshCharacterStatistics();
        }
        else
        {
            CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
            CharacterData.CharacterItem draggedItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(draggedBagSlotIndex));

            charactersData[characterIndex].bag[draggedBagSlotIndex] = bagItemTemp;
            charactersData[characterIndex].bag[bagSlotIndex] = draggedItemTemp;
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
            characterPlayerHud.GetBagSlotByIndex(draggedBagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[draggedBagSlotIndex]);
            characterPlayerHud.RefreshCharacterStatistics();
        }
    }
    void ChangeFasItemToFastItem(int fastItemSlotIndex, int draggedFastItemSlotIndex)
    {
        if (charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO != null &&
            charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO == charactersData[characterIndex].fastItems[draggedFastItemSlotIndex].itemBaseSO &&
            charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
        {
            FindAmountToAppend(charactersData[characterIndex].fastItems[draggedFastItemSlotIndex], charactersData[characterIndex].fastItems[fastItemSlotIndex], out int amountToAppend);
            charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue += amountToAppend;
            charactersData[characterIndex].fastItems[draggedFastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue -= amountToAppend;
            characterPlayerHud.GetFastItemSlotByIndex(fastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[fastItemSlotIndex]);
            if (charactersData[characterIndex].fastItems[draggedFastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
            {
                charactersData[characterIndex].fastItems[draggedFastItemSlotIndex] = new CharacterData.CharacterItem(ItemsDBSO.TypeModel.FastItems);
            }
            characterPlayerHud.GetFastItemSlotByIndex(draggedFastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[draggedFastItemSlotIndex]);
        }
        else
        {
            CharacterData.CharacterItem fastItemItemTemp = new CharacterData.CharacterItem(GetFastItemItemByIndex(fastItemSlotIndex));
            CharacterData.CharacterItem draggedItemTemp = new CharacterData.CharacterItem(GetFastItemItemByIndex(draggedFastItemSlotIndex));

            charactersData[characterIndex].fastItems[draggedFastItemSlotIndex] = fastItemItemTemp;
            charactersData[characterIndex].fastItems[fastItemSlotIndex] = draggedItemTemp;
            characterPlayerHud.GetFastItemSlotByIndex(fastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[fastItemSlotIndex]);
            characterPlayerHud.GetFastItemSlotByIndex(draggedFastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[draggedFastItemSlotIndex]);
        }
        characterPlayerHud.RefreshFastItems();
        if (fastItemSlotIndex == currentFastItemIndex)
        {
            _ = charactersData[characterIndex].fastItems[draggedFastItemSlotIndex].itemBaseSO.DesEquipItem(this, charactersData[characterIndex].fastItems[draggedFastItemSlotIndex], false, true);
            _ = charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO.EquipItem(this, charactersData[characterIndex].fastItems[fastItemSlotIndex], false, true);
            UpdateFastItemModel();
        }
        else if (draggedFastItemSlotIndex == currentFastItemIndex)
        {
            _ = charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO.DesEquipItem(this, charactersData[characterIndex].fastItems[fastItemSlotIndex], false, true);
            _ = charactersData[characterIndex].fastItems[draggedFastItemSlotIndex].itemBaseSO.EquipItem(this, charactersData[characterIndex].fastItems[draggedFastItemSlotIndex], false, true);
            UpdateFastItemModel();
        }
        characterPlayerHud.RefreshCharacterStatistics();
    }
    async Awaitable ChangeEquipmentAndBag(ItemsDBSO.TypeModel equipmentIndex, int bagSlotIndex)
    {
        if (charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO?.typeObject == equipmentIndex ||
            charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO == null)
        {
            CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
            CharacterData.CharacterItem equipmentItemTemp = new CharacterData.CharacterItem(GetEquipmentItemByIndex(equipmentIndex));

            charactersData[characterIndex].equipments[equipmentIndex] = bagItemTemp;
            charactersData[characterIndex].bag[bagSlotIndex] = equipmentItemTemp;

            if (equipmentItemTemp.itemBaseSO) await equipmentItemTemp.itemBaseSO.DesEquipItem(this, equipmentItemTemp, true, false);
            if (bagItemTemp.itemBaseSO) await bagItemTemp.itemBaseSO.EquipItem(this, bagItemTemp, true, false);
            characterPlayerHud.RefreshCharacterStatistics();
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
            characterPlayerHud.GetEquipmentSlotByIndex(equipmentIndex).InitializeSlot(charactersData[characterIndex].equipments[equipmentIndex]);
        }
    }
    private void ChangeBagAndFastItem(int bagSlotIndex, int fastItemSlotIndex, bool isFromBagToFastItem)
    {
        if (!isFromBagToFastItem)
        {
            if (charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO != null &&
                charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO == charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO &&
                charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
            {
                FindAmountToAppend(charactersData[characterIndex].bag[bagSlotIndex], charactersData[characterIndex].fastItems[fastItemSlotIndex], out int amountToAppend);
                charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue += amountToAppend;
                charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue -= amountToAppend;
                characterPlayerHud.GetFastItemSlotByIndex(fastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[fastItemSlotIndex]);
                if (charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
                {
                    charactersData[characterIndex].bag[bagSlotIndex] = new CharacterData.CharacterItem(ItemsDBSO.TypeModel.Bag);
                }
                characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
            }
            else
            {
                DiferentBagAndFastItem(bagSlotIndex, fastItemSlotIndex);
            }
        }
        else
        {
            if (charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO != null &&
                charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO == charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO &&
                charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
            {
                FindAmountToAppend(charactersData[characterIndex].fastItems[fastItemSlotIndex], charactersData[characterIndex].bag[bagSlotIndex], out int amountToAppend);
                charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue += amountToAppend;
                charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue -= amountToAppend;
                characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
                if (charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
                {
                    charactersData[characterIndex].fastItems[fastItemSlotIndex] = new CharacterData.CharacterItem(ItemsDBSO.TypeModel.FastItems);
                }
                characterPlayerHud.GetFastItemSlotByIndex(fastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[fastItemSlotIndex]);
            }
            else
            {
                DiferentBagAndFastItem(bagSlotIndex, fastItemSlotIndex);
            }
        }
        characterPlayerHud.RefreshFastItems();
        if (fastItemSlotIndex == currentFastItemIndex) UpdateFastItemModel();
    }
    void DiferentBagAndFastItem(int bagSlotIndex, int fastItemSlotIndex)
    {
        CharacterData.CharacterItem bagSlotTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
        CharacterData.CharacterItem fastItemItemTemp = new CharacterData.CharacterItem(GetFastItemItemByIndex(fastItemSlotIndex));

        charactersData[characterIndex].bag[bagSlotIndex] = fastItemItemTemp;
        charactersData[characterIndex].fastItems[fastItemSlotIndex] = bagSlotTemp;
        characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
        characterPlayerHud.GetFastItemSlotByIndex(fastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[fastItemSlotIndex]);
        characterPlayerHud.RefreshFastItems();
        if (fastItemSlotIndex == currentFastItemIndex)
        {
            _ = charactersData[characterIndex].fastItems[bagSlotIndex].itemBaseSO.DesEquipItem(this, charactersData[characterIndex].fastItems[bagSlotIndex], false, true);
            _ = charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO.EquipItem(this, charactersData[characterIndex].fastItems[fastItemSlotIndex], false, true);
            UpdateFastItemModel();
        }
        else if (bagSlotIndex == currentFastItemIndex)
        {
            _ = charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO.DesEquipItem(this, charactersData[characterIndex].fastItems[fastItemSlotIndex], false, true);
            _ = charactersData[characterIndex].fastItems[bagSlotIndex].itemBaseSO.EquipItem(this, charactersData[characterIndex].fastItems[bagSlotIndex], false, true);
            UpdateFastItemModel();
        }
        characterPlayerHud.RefreshCharacterStatistics();
    }
    void FastItemToEquipment(int fastItemSlotIndex, ItemsDBSO.TypeModel equipmentIndex)
    {
        CharacterData.CharacterItem fastItemTemp = new CharacterData.CharacterItem(GetFastItemItemByIndex(fastItemSlotIndex));
        CharacterData.CharacterItem equipmentItemTemp = new CharacterData.CharacterItem(GetEquipmentItemByIndex(equipmentIndex));

        if (fastItemTemp.typeObject == ItemsDBSO.TypeModel.None) fastItemTemp.typeObject = equipmentIndex;
        if (equipmentItemTemp.typeObject == ItemsDBSO.TypeModel.None) equipmentItemTemp.typeObject = ItemsDBSO.TypeModel.FastItems;

        charactersData[characterIndex].equipments[equipmentIndex] = fastItemTemp;
        charactersData[characterIndex].fastItems[fastItemSlotIndex] = equipmentItemTemp;

        if (equipmentItemTemp.itemBaseSO) _ = equipmentItemTemp.itemBaseSO.DesEquipItem(this, equipmentItemTemp, true, false);
        if (fastItemTemp.itemBaseSO) _ = fastItemTemp.itemBaseSO.EquipItem(this, fastItemTemp, true, false);
        characterPlayerHud.RefreshCharacterStatistics();
        characterPlayerHud.GetFastItemSlotByIndex(fastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[fastItemSlotIndex]);
        characterPlayerHud.GetEquipmentSlotByIndex(equipmentIndex).InitializeSlot(charactersData[characterIndex].equipments[equipmentIndex]);
        characterPlayerHud.RefreshFastItems();
        if (fastItemSlotIndex == currentFastItemIndex) UpdateFastItemModel();
        else if (IsEquipableItem(equipmentIndex)) RefreshCharacterItemModel(charactersData[characterIndex].equipments[equipmentIndex], true, ItemsDBSO.TypeModel.FastItems);
        else if (IsEquipableItem(equipmentItemTemp.typeObject)) RefreshCharacterItemModel(charactersData[characterIndex].equipments[equipmentItemTemp.typeObject], false, ItemsDBSO.TypeModel.FastItems);
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
    public bool FindEmptyFastItemSlot(out int bagIndex)
    {
        foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in charactersData[characterIndex].fastItems)
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
    public bool FindSimilarFastItemSlot(int bagIndex, out int fastItemIndex)
    {
        foreach (KeyValuePair<int, CharacterData.CharacterItem> fastItemSlot in charactersData[characterIndex].fastItems)
        {
            if (fastItemSlot.Value.itemBaseSO != null && fastItemSlot.Value.itemBaseSO.id == charactersData[characterIndex].bag[bagIndex].itemBaseSO.id && fastItemSlot.Value.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < fastItemSlot.Value.itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
            {
                fastItemIndex = fastItemSlot.Key;
                return true;
            }
        }
        fastItemIndex = 0;
        return false;
    }
    public void FindAmountToAppend(CharacterData.CharacterItem fromAppend, CharacterData.CharacterItem toAppend, out int amountToAppend)
    {
        if (fromAppend.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue + toAppend.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= toAppend.itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
        {
            amountToAppend = (int)fromAppend.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue;
        }
        else
        {
            amountToAppend = (int)(toAppend.itemStatistics[CharacterData.TypeStatistic.Amount].maxValue - toAppend.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue);
        }
    }
    public bool IsEquipableItem(ItemsDBSO.TypeModel typeModel)
    {
        return typeModel == ItemsDBSO.TypeModel.Helmet ||
               typeModel == ItemsDBSO.TypeModel.Front ||
               typeModel == ItemsDBSO.TypeModel.Pants ||
               typeModel == ItemsDBSO.TypeModel.Boots ||
               typeModel == ItemsDBSO.TypeModel.Gloves ||
               typeModel == ItemsDBSO.TypeModel.Pendant ||
               typeModel == ItemsDBSO.TypeModel.Ring ||
               typeModel == ItemsDBSO.TypeModel.Weapon;
    }
    public CharacterData.CharacterItem GetBagItemByIndex(int index)
    {
        if (charactersData[characterIndex].bag.TryGetValue(index, out CharacterData.CharacterItem bagItem))
        {
            return bagItem;
        }
        return new CharacterData.CharacterItem(ItemsDBSO.TypeModel.Bag);
    }
    public CharacterData.CharacterItem GetFastItemItemByIndex(int index)
    {
        if (charactersData[characterIndex].fastItems.TryGetValue(index, out CharacterData.CharacterItem fastItemItem))
        {
            return fastItemItem;
        }
        return new CharacterData.CharacterItem(ItemsDBSO.TypeModel.FastItems);
    }
    public CharacterData.CharacterItem GetEquipmentItemByIndex(ItemsDBSO.TypeModel index)
    {
        if (charactersData[characterIndex].equipments.TryGetValue(index, out CharacterData.CharacterItem equipmentItem))
        {
            return equipmentItem;
        }
        return new CharacterData.CharacterItem(index);
    }
    public void FastEquipItem(int slotIndex)
    {
        if (characterPlayerHud.isDraggingItem) return;
        if (charactersData[characterIndex].bag[slotIndex].itemBaseSO?.typeObject != ItemsDBSO.TypeModel.FastItems)
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
            if (FindSimilarFastItemSlot(slotIndex, out int similarFastItemIndex))
            {
                FindAmountToAppend(charactersData[characterIndex].bag[slotIndex], charactersData[characterIndex].fastItems[similarFastItemIndex], out int amountToAppend);
                charactersData[characterIndex].bag[slotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue -= amountToAppend;
                charactersData[characterIndex].fastItems[similarFastItemIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue += amountToAppend;
                characterPlayerHud.GetFastItemSlotByIndex(similarFastItemIndex).InitializeSlot(charactersData[characterIndex].fastItems[similarFastItemIndex]);
                if (charactersData[characterIndex].bag[slotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
                {
                    charactersData[characterIndex].bag[slotIndex] = new CharacterData.CharacterItem(ItemsDBSO.TypeModel.Bag);
                }
                characterPlayerHud.GetBagSlotByIndex(slotIndex).InitializeSlot(charactersData[characterIndex].bag[slotIndex]);
            }
            else if (FindEmptyFastItemSlot(out int fastItemIndex))
            {
                ChangeBagAndFastItem(slotIndex, fastItemIndex, true);
            }
            characterPlayerHud.RefreshFastItems();
        }
        _ = characterPlayerHud.ResetInventoryTarget();
    }
    public async Task DropItem()
    {
        int draggedSlotIndex = characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex;
        if (characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == ItemsDBSO.TypeModel.Bag)
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetBagItemByIndex(draggedSlotIndex), true);
            LaunchDropItem(itemDropped);
            charactersData[characterIndex].bag[draggedSlotIndex] = new CharacterData.CharacterItem(ItemsDBSO.TypeModel.Bag);
            characterPlayerHud.GetBagSlotByIndex(draggedSlotIndex).InitializeSlot(new CharacterData.CharacterItem(ItemsDBSO.TypeModel.Bag));
        }
        else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == ItemsDBSO.TypeModel.FastItems)
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetFastItemItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex), true);
            LaunchDropItem(itemDropped);
            _ = charactersData[characterIndex].fastItems[characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex].itemBaseSO.DesEquipItem(this, charactersData[characterIndex].fastItems[characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex], true, true);
            charactersData[characterIndex].fastItems[characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex] = new CharacterData.CharacterItem(ItemsDBSO.TypeModel.FastItems);
            characterPlayerHud.GetFastItemSlotByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex).InitializeSlot(new CharacterData.CharacterItem(ItemsDBSO.TypeModel.FastItems));
            characterPlayerHud.RefreshFastItems();
            if (characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex == currentFastItemIndex) UpdateFastItemModel();
        }
        else
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetEquipmentItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject), true);
            LaunchDropItem(itemDropped);
            if (charactersData[characterIndex].equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject].itemBaseSO) await charactersData[characterIndex].equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject].itemBaseSO.DesEquipItem(this, GetEquipmentItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject), false, false);
            charactersData[characterIndex].equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject] = new CharacterData.CharacterItem(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject);
            characterPlayerHud.GetEquipmentSlotByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject).InitializeSlot(new CharacterData.CharacterItem(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject));
            characterPlayerHud.RefreshCharacterStatistics();
        }
    }
    void LaunchDropItem(ItemDropped itemDropped)
    {
        itemDropped.rb.linearVelocity = Vector3.zero;
        itemDropped.rb.AddForce(characterModel.modelTransform.forward.normalized * dropLaunchForce + Vector3.up * dropUpForce, ForceMode.Impulse);
    }
}
