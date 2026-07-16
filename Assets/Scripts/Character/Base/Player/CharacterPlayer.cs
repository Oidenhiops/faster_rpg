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
        inputActions.Player.MoveCamera.performed += MoveCamera;
        inputActions.Player.SetCameraRadius.performed += SetCameraRadius;
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
    public void MoveCamera(InputAction.CallbackContext context)
    {
        if (isInventoryOpen) return;
        characterPlayerCamera.MoveCamera(context);
    }
    public void SetCameraRadius(InputAction.CallbackContext context)
    {
        if (isInventoryOpen) return;
        characterPlayerCamera.SetCameraRadius(context);
    }
    void OnHandleToggleInventory(InputAction.CallbackContext context)
    {
        isInventoryOpen = !isInventoryOpen;
        _ = characterPlayerHud.ToggleCharacterInventory();
    }
    public void OnHandleUseFastItem(InputAction.CallbackContext context)
    {
        if (!isInventoryOpen && !isInCanalization) UseItem();
    }
    public override void UseItem()
    {
        if (isInventoryOpen || isInCanalization) return;
        if (charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO)
        {
            if (!charactersData[characterIndex].fastItems[currentFastItemIndex].itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability) || 
                charactersData[characterIndex].fastItems[currentFastItemIndex].itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability) &&
                charactersData[characterIndex].fastItems[currentFastItemIndex].itemStatistics[CharacterData.TypeStatistic.Durability].currentValue > 0)
            {
                charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO.UseItem(this, charactersData[characterIndex].fastItems[currentFastItemIndex]);
            }
        }
    }
    public override void UseItem(int bagSlotIndex)
    {
        if (isInventoryOpen || isInCanalization) return;
        if (charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO) charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO.UseItem(this, charactersData[characterIndex].bag[bagSlotIndex]);
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
        currentFastItemIndex += (int)context.ReadValue<float>();
        if (currentFastItemIndex < 0) currentFastItemIndex = characterPlayerHud.characterUI.fastItems.Count - 1;
        else if (currentFastItemIndex >= characterPlayerHud.characterUI.fastItems.Count) currentFastItemIndex = 0;
        characterPlayerHud.SelectFastItem();
        UpdateFastItemModel();
    }
    void UpdateFastItemModel()
    {
        if (charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO)
        {
            RefreshCharacterItemModel(charactersData[characterIndex].fastItems[currentFastItemIndex], true);
            for (int i = 0; i < characterModel.meshesData[CharactersModelDBSO.TypeModel.FastItems].Count; i++)
            {
                dissolvePlayer.NeedAppearSpecificObj(characterModel.meshesData[CharactersModelDBSO.TypeModel.FastItems][i].meshRenderer);
            }
        }
        else
        {
            RefreshCharacterItemModel(new CharacterData.CharacterItem
            {
                typeObject = CharactersModelDBSO.TypeModel.FastItems,
            }, false);
        }
    }
    public void ChangeObjectPosition()
    {
        int lastSelectedSlotIndex = characterPlayerHud.lastSelectedSlot.slotIndex;
        int draggedSlotIndex = characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex;

        if (characterPlayerHud.lastSelectedSlot == characterPlayerHud.inventoryDraggedSlot.itemDraged) return;

        if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == CharactersModelDBSO.TypeModel.Bag && characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == CharactersModelDBSO.TypeModel.Bag)
        {
            ChangeBagToBag(lastSelectedSlotIndex, draggedSlotIndex);
        }
        else if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == CharactersModelDBSO.TypeModel.FastItems && characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == CharactersModelDBSO.TypeModel.FastItems)
        {
            ChangeFasItemToFastItem(lastSelectedSlotIndex, draggedSlotIndex);
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == CharactersModelDBSO.TypeModel.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot != CharactersModelDBSO.TypeModel.Bag)
        {
            if (characterPlayerHud.lastSelectedSlot.typeInventorySlot != CharactersModelDBSO.TypeModel.FastItems && IsEquipableItem(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.typeObject))
            {
                _ = ChangeEquipmentAndBag(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject, draggedSlotIndex);
            }
            else if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == CharactersModelDBSO.TypeModel.FastItems || characterPlayerHud.lastSelectedSlot.typeInventorySlot == CharactersModelDBSO.TypeModel.Bag)
            {
                ChangeBagAndConsumable(draggedSlotIndex, lastSelectedSlotIndex, false);
            }
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot != CharactersModelDBSO.TypeModel.Bag &&
            characterPlayerHud.lastSelectedSlot.typeInventorySlot == CharactersModelDBSO.TypeModel.Bag)
        {
            if (characterPlayerHud.lastSelectedSlot.typeInventorySlot != CharactersModelDBSO.TypeModel.FastItems && IsEquipableItem(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.typeObject))
            {
                _ = ChangeEquipmentAndBag(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject, lastSelectedSlotIndex);
            }
            else if (characterPlayerHud.lastSelectedSlot.typeInventorySlot == CharactersModelDBSO.TypeModel.FastItems || characterPlayerHud.lastSelectedSlot.typeInventorySlot == CharactersModelDBSO.TypeModel.Bag)
            {
                ChangeBagAndConsumable(lastSelectedSlotIndex, draggedSlotIndex, true);
            }
        }
        else if (
            characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == CharactersModelDBSO.TypeModel.FastItems && 
            IsEquipableItem(characterPlayerHud.lastSelectedSlot.typeInventorySlot))
        {
            if (characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject == characterPlayerHud.lastSelectedSlot.characterItem.typeObject)
            {
                FastItemToEquipment(characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex, characterPlayerHud.lastSelectedSlot.characterItem.typeObject);
            }
        }
        else if (
            characterPlayerHud.lastSelectedSlot.typeInventorySlot == CharactersModelDBSO.TypeModel.FastItems && 
            IsEquipableItem(characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot))
        {
            if (characterPlayerHud.lastSelectedSlot.characterItem.typeObject == characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject)
            {
                FastItemToEquipment(characterPlayerHud.lastSelectedSlot.slotIndex, characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject);
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
    void ChangeFasItemToFastItem(int fastItemSlotIndex, int draggedFastItemSlotIndex)
    {
        if (charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO != null &&
            charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO == charactersData[characterIndex].fastItems[draggedFastItemSlotIndex].itemBaseSO &&
            charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
        {
            FindAmountToAppend(charactersData[characterIndex].fastItems[draggedFastItemSlotIndex], charactersData[characterIndex].fastItems[fastItemSlotIndex], out int amountToAppend);
            charactersData[characterIndex].fastItems[fastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue += amountToAppend;
            charactersData[characterIndex].fastItems[draggedFastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue -= amountToAppend;
            characterPlayerHud.GetConsumableSlotByIndex(fastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[fastItemSlotIndex]);
            if (charactersData[characterIndex].fastItems[draggedFastItemSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
            {
                charactersData[characterIndex].fastItems[draggedFastItemSlotIndex] = new CharacterData.CharacterItem();
            }
            characterPlayerHud.GetConsumableSlotByIndex(draggedFastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[draggedFastItemSlotIndex]);
        }
        else
        {
            CharacterData.CharacterItem consumableItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(fastItemSlotIndex));
            CharacterData.CharacterItem draggedItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(draggedFastItemSlotIndex));

            charactersData[characterIndex].fastItems[draggedFastItemSlotIndex] = consumableItemTemp;
            charactersData[characterIndex].fastItems[fastItemSlotIndex] = draggedItemTemp;
            characterPlayerHud.GetConsumableSlotByIndex(fastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[fastItemSlotIndex]);
            characterPlayerHud.GetConsumableSlotByIndex(draggedFastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[draggedFastItemSlotIndex]);
        }
        characterPlayerHud.RefreshFastItems();
        if (fastItemSlotIndex == currentFastItemIndex || draggedFastItemSlotIndex == currentFastItemIndex) UpdateFastItemModel();
    }
    async Awaitable ChangeEquipmentAndBag(CharactersModelDBSO.TypeModel equipmentIndex, int bagSlotIndex)
    {
        if (charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO?.typeObject == equipmentIndex ||
            charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO == null)
        {
            CharacterData.CharacterItem bagItemTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
            CharacterData.CharacterItem equipmentItemTemp = new CharacterData.CharacterItem(GetEquipmentItemByIndex(equipmentIndex));

            charactersData[characterIndex].equipments[equipmentIndex] = bagItemTemp;
            charactersData[characterIndex].bag[bagSlotIndex] = equipmentItemTemp;

            if (equipmentItemTemp.itemBaseSO) await equipmentItemTemp.itemBaseSO.DesEquipItem(this, equipmentItemTemp, true);
            if (bagItemTemp.itemBaseSO) await bagItemTemp.itemBaseSO.EquipItem(this, bagItemTemp, true);
            characterPlayerHud.RefreshCharacterStatistics();
            characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
            characterPlayerHud.GetEquipmentSlotByIndex(equipmentIndex).InitializeSlot(charactersData[characterIndex].equipments[equipmentIndex]);
        }
    }
    private void ChangeBagAndConsumable(int bagSlotIndex, int consumableSlotIndex, bool isFromBagToFastItem)
    {
        if (!isFromBagToFastItem)
        {
            if (charactersData[characterIndex].fastItems[consumableSlotIndex].itemBaseSO != null &&
                charactersData[characterIndex].fastItems[consumableSlotIndex].itemBaseSO == charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO &&
                charactersData[characterIndex].fastItems[consumableSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < charactersData[characterIndex].fastItems[consumableSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
            {
                FindAmountToAppend(charactersData[characterIndex].bag[bagSlotIndex], charactersData[characterIndex].fastItems[consumableSlotIndex], out int amountToAppend);
                charactersData[characterIndex].fastItems[consumableSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue += amountToAppend;
                charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue -= amountToAppend;
                characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[consumableSlotIndex]);
                if (charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
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
                charactersData[characterIndex].bag[bagSlotIndex].itemBaseSO == charactersData[characterIndex].fastItems[consumableSlotIndex].itemBaseSO &&
                charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
            {
                FindAmountToAppend(charactersData[characterIndex].fastItems[consumableSlotIndex], charactersData[characterIndex].bag[bagSlotIndex], out int amountToAppend);
                charactersData[characterIndex].bag[bagSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue += amountToAppend;
                charactersData[characterIndex].fastItems[consumableSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue -= amountToAppend;
                characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
                if (charactersData[characterIndex].fastItems[consumableSlotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
                {
                    charactersData[characterIndex].fastItems[consumableSlotIndex] = new CharacterData.CharacterItem();
                }
                characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[consumableSlotIndex]);
            }
            else
            {
                DiferentBagAndConsumable(bagSlotIndex, consumableSlotIndex);
            }
        }
        characterPlayerHud.RefreshFastItems();
        if (consumableSlotIndex == currentFastItemIndex) UpdateFastItemModel();
    }
    void DiferentBagAndConsumable(int bagSlotIndex, int consumableSlotIndex)
    {
        CharacterData.CharacterItem bagSlotTemp = new CharacterData.CharacterItem(GetBagItemByIndex(bagSlotIndex));
        CharacterData.CharacterItem consumableItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(consumableSlotIndex));

        charactersData[characterIndex].bag[bagSlotIndex] = consumableItemTemp;
        charactersData[characterIndex].fastItems[consumableSlotIndex] = bagSlotTemp;
        characterPlayerHud.GetBagSlotByIndex(bagSlotIndex).InitializeSlot(charactersData[characterIndex].bag[bagSlotIndex]);
        characterPlayerHud.GetConsumableSlotByIndex(consumableSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[consumableSlotIndex]);
        characterPlayerHud.RefreshFastItems();
    }
    void FastItemToEquipment(int fastItemSlotIndex, CharactersModelDBSO.TypeModel equipmentIndex)
    {
        if (charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO?.typeObject == equipmentIndex ||
            charactersData[characterIndex].fastItems[fastItemSlotIndex].itemBaseSO == null)
        {
            CharacterData.CharacterItem fastItemTemp = new CharacterData.CharacterItem(GetConsumableItemByIndex(fastItemSlotIndex));
            CharacterData.CharacterItem equipmentItemTemp = new CharacterData.CharacterItem(GetEquipmentItemByIndex(equipmentIndex));

            charactersData[characterIndex].equipments[equipmentIndex] = fastItemTemp;
            charactersData[characterIndex].fastItems[fastItemSlotIndex] = equipmentItemTemp;

            if (equipmentItemTemp.itemBaseSO) _ = equipmentItemTemp.itemBaseSO.DesEquipItem(this, equipmentItemTemp, true);
            if (fastItemTemp.itemBaseSO) _ = fastItemTemp.itemBaseSO.EquipItem(this, fastItemTemp, true);
            characterPlayerHud.RefreshCharacterStatistics();
            characterPlayerHud.GetConsumableSlotByIndex(fastItemSlotIndex).InitializeSlot(charactersData[characterIndex].fastItems[fastItemSlotIndex]);
            characterPlayerHud.GetEquipmentSlotByIndex(equipmentIndex).InitializeSlot(charactersData[characterIndex].equipments[equipmentIndex]);
            characterPlayerHud.RefreshFastItems();
            if (fastItemSlotIndex == currentFastItemIndex) UpdateFastItemModel();
        }
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
    public bool FindSimilarConsumableSlot(int bagIndex, out int consumableIndex)
    {
        foreach (KeyValuePair<int, CharacterData.CharacterItem> consumableSlot in charactersData[characterIndex].fastItems)
        {
            if (consumableSlot.Value.itemBaseSO != null && consumableSlot.Value.itemBaseSO.id == charactersData[characterIndex].bag[bagIndex].itemBaseSO.id && consumableSlot.Value.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < consumableSlot.Value.itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
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
        if (fromAppend.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue + toAppend.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= toAppend.itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
        {
            amountToAppend = (int)fromAppend.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue;
        }
        else
        {
            amountToAppend = (int)(toAppend.itemStatistics[CharacterData.TypeStatistic.Amount].maxValue - toAppend.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue);
        }
    }
    public bool IsEquipableItem(CharactersModelDBSO.TypeModel typeModel)
    {
        return typeModel == CharactersModelDBSO.TypeModel.Helmet ||
               typeModel == CharactersModelDBSO.TypeModel.Front ||
               typeModel == CharactersModelDBSO.TypeModel.Pants ||
               typeModel == CharactersModelDBSO.TypeModel.Boots ||
               typeModel == CharactersModelDBSO.TypeModel.Gloves ||
               typeModel == CharactersModelDBSO.TypeModel.Pendant ||
               typeModel == CharactersModelDBSO.TypeModel.Ring ||
               typeModel == CharactersModelDBSO.TypeModel.Weapon;
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
        if (charactersData[characterIndex].fastItems.TryGetValue(index, out CharacterData.CharacterItem consumableItem))
        {
            return consumableItem;
        }
        return new CharacterData.CharacterItem();
    }
    public CharacterData.CharacterItem GetEquipmentItemByIndex(CharactersModelDBSO.TypeModel index)
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
        if (charactersData[characterIndex].bag[slotIndex].itemBaseSO?.typeObject != CharactersModelDBSO.TypeModel.FastItems)
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
                FindAmountToAppend(charactersData[characterIndex].bag[slotIndex], charactersData[characterIndex].fastItems[similarConsumableIndex], out int amountToAppend);
                charactersData[characterIndex].bag[slotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue -= amountToAppend;
                charactersData[characterIndex].fastItems[similarConsumableIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue += amountToAppend;
                characterPlayerHud.GetConsumableSlotByIndex(similarConsumableIndex).InitializeSlot(charactersData[characterIndex].fastItems[similarConsumableIndex]);
                if (charactersData[characterIndex].bag[slotIndex].itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
                {
                    charactersData[characterIndex].bag[slotIndex] = new CharacterData.CharacterItem();
                }
                characterPlayerHud.GetBagSlotByIndex(slotIndex).InitializeSlot(charactersData[characterIndex].bag[slotIndex]);
            }
            else if (FindEmptyConsumableSlot(out int consumableIndex))
            {
                ChangeBagAndConsumable(slotIndex, consumableIndex, true);
            }
            characterPlayerHud.RefreshFastItems();
        }
        _ = characterPlayerHud.ResetInventoryTarget();
    }
    public async Task DropItem()
    {
        int draggedSlotIndex = characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex;
        if (characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == CharactersModelDBSO.TypeModel.Bag)
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetBagItemByIndex(draggedSlotIndex), true);
            LaunchDropItem(itemDropped);
            charactersData[characterIndex].bag[draggedSlotIndex] = new CharacterData.CharacterItem();
            characterPlayerHud.GetBagSlotByIndex(draggedSlotIndex).InitializeSlot(new CharacterData.CharacterItem());
        }
        else if (characterPlayerHud.inventoryDraggedSlot.itemDraged.typeInventorySlot == CharactersModelDBSO.TypeModel.FastItems)
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetConsumableItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex), true);
            LaunchDropItem(itemDropped);
            charactersData[characterIndex].fastItems[characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex] = new CharacterData.CharacterItem();
            characterPlayerHud.GetConsumableSlotByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex).InitializeSlot(new CharacterData.CharacterItem());
            characterPlayerHud.RefreshFastItems();
            if (characterPlayerHud.inventoryDraggedSlot.itemDraged.slotIndex == currentFastItemIndex) UpdateFastItemModel();
        }
        else
        {
            ItemDropped itemDropped = Instantiate(itempDroppedPrefab, transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
            itemDropped.InitializeDropItem(GetEquipmentItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject), true);
            LaunchDropItem(itemDropped);
            if (charactersData[characterIndex].equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject].itemBaseSO) await charactersData[characterIndex].equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject].itemBaseSO.DesEquipItem(this, GetEquipmentItemByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject));
            charactersData[characterIndex].equipments[characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject] = new CharacterData.CharacterItem();
            characterPlayerHud.GetEquipmentSlotByIndex(characterPlayerHud.inventoryDraggedSlot.itemDraged.characterItem.itemBaseSO.typeObject).InitializeSlot(new CharacterData.CharacterItem());
            characterPlayerHud.RefreshCharacterStatistics();
        }
    }
    void LaunchDropItem(ItemDropped itemDropped)
    {
        itemDropped.rb.linearVelocity = Vector3.zero;
        itemDropped.rb.AddForce(characterModel.modelTransform.forward.normalized * dropLaunchForce + Vector3.up * dropUpForce, ForceMode.Impulse);
    }
}
