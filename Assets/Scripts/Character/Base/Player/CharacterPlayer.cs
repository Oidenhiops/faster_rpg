using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayer : CharacterBase
{
    public InputSystem_Actions inputActions;
    private float dropLaunchForce = 4f;
    private float dropUpForce = 2f;
    public SerializedDictionary<InteractableBase, GameObject> interactables = new SerializedDictionary<InteractableBase, GameObject>();
    public Action OnShowItemsToPickUp;
    public bool isChangingCharacter;
    public bool isInventoryOpen;
    public CharacterPlayerCamera characterPlayerCamera;
    public Coroutine recoverStrCoroutine;
    [Tooltip("Segundos que tarda la barra de Str en pasar de 0 a maxValue.")]
    public float strFullRecoverTime = 2f;
    public VoxelOutlineIndicator outline;
    RaycastHit _currentHit;
    Vector3Int lastDamageBlock;
    int lastDamageMicro = -1;
    bool hadDamageTarget;
    public RaycastHit currentHit
    {
        get => _currentHit;
        set
        {
            bool hadTarget = hadDamageTarget;
            Vector3Int oldBlock = lastDamageBlock;
            int oldMicro = lastDamageMicro;

            _currentHit = value;
            isSeeingBlock = value.collider != null &&
                            value.collider.GetComponentInParent<VoxelWorld>() != null &&
                            LayerMask.LayerToName(value.collider.gameObject.layer) == "Map";

            VoxelWorld world = VoxelWorld.Instance;
            bool hasTarget = false;
            Vector3Int newBlock = default;
            int newMicro = -1;
            if (isSeeingBlock && world != null)
            {
                Vector3 inside = value.point - value.normal * 0.01f;
                switch (currentMiningType)
                {
                    case ToolItemSO.MiningType.Perfect:
                        hasTarget = world.TryLocateMicro(inside, out newBlock, out newMicro, out _);
                        break;
                    case ToolItemSO.MiningType.Sphere:
                        break; // daño por área: sin reset al mover la mira (expira por tiempo)
                    default: // Block / Free
                        hasTarget = true;
                        newBlock = world.WorldToBlock(inside);
                        break;
                }
            }

            if (hadTarget && world != null && (!hasTarget || newBlock != oldBlock || newMicro != oldMicro))
            {
                if (oldMicro >= 0) world.ResetVoxelDamage(oldBlock, oldMicro);
                else world.ResetBlockDamage(oldBlock);
            }

            hadDamageTarget = hasTarget;
            lastDamageBlock = newBlock;
            lastDamageMicro = newMicro;

            if (outline != null && world != null)
                UpdateOutline(world, isSeeingBlock, value, currentMiningType, GetItemStatistic(CharacterData.TypeStatistic.ItemRadius)?.currentValue ?? 0f);
        }
    }
    public bool isSeeingBlock;
    public ToolItemSO.MiningType currentMiningType;
    readonly List<VoxelWorld.MiningQuad> pickaxePreviewBuf = new List<VoxelWorld.MiningQuad>(64);
    readonly List<VoxelWorld.MiningQuad> spherePreviewBuf = new List<VoxelWorld.MiningQuad>(128);
    public override void OnEnableHandle()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        inputActions.Player.ToggleInventory.performed += OnHandleToggleInventory;
        inputActions.Player.ChangeFastItem.performed += OnHandleChangeFastItem;
        inputActions.Player.UseFastItem.performed += OnHandleUseFastItem;
        inputActions.Player.UseFastItem.canceled += OnHandleUseFastItem;
        inputActions.Player.UseSkill.performed += OnHandleUseSkill;
        inputActions.Player.MoveCamera.performed += OnHandleMoveCamera;
        inputActions.Player.MoveCamera.canceled += OnHandleMoveCamera;
        inputActions.Player.SetFreeCamera.performed += OnHandleSetFreeCamera;
        inputActions.Player.Attack.performed += OnHandleAttack;
        inputActions.Player.Attack.canceled += OnHandleAttack;
        OnShowItemsToPickUp += OnHandleShowItemsToPickUp;
    }
    public async override Awaitable InitializeCharacter()
    {
        try
        {
            characterData = GameData.Instance.gameDataInfo.gameDataSlots[GameData.Instance.systemDataInfo.currentGameDataIndex].character;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing character data: {ex.Message}");
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
            await characterPlayerHud.InitializeBars();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters bars: {ex.Message}");
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
        try
        {
            await Awaitable.NextFrameAsync();
            await InitializeModels();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters models: {ex.Message}");
        }
        try
        {
            await InitialiceActions();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing characters actions: {ex.Message}");
        }
        isInitialize = true;
    }
    async Awaitable InitializeItems()
    {
        characterData.InitializeItems(this);
    }
    async Awaitable InitialiceActions()
    {
        characterData.statistics[CharacterData.TypeStatistic.Str].OnCurrentValueChanged = new Action(RecoverStr);
    }
    void RecoverStr()
    {
        if (recoverStrCoroutine != null) StopCoroutine(recoverStrCoroutine);
        recoverStrCoroutine = StartCoroutine(HandleRecoverStr());
    }
    public IEnumerator HandleRecoverStr()
    {
        yield return new WaitForSeconds(2f);
        CharacterData.Statistic str = characterData.statistics[CharacterData.TypeStatistic.Str];

        if (strFullRecoverTime <= 0f)
        {
            str.currentValue = str.maxValue;
            characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Str);
            yield break;
        }

        float accumulated = 0f;

        while (str.currentValue < str.maxValue)
        {
            yield return null;

            accumulated += Time.deltaTime * (str.maxValue / strFullRecoverTime);

            if (accumulated < 1f) continue;

            int points = Mathf.FloorToInt(accumulated);
            accumulated -= points;
            str.currentValue += points;
            characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Str, false);
        }
    }
    public override void SetHitPoint()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
        Physics.Raycast(ray, out RaycastHit hit, GetItemStatistic(CharacterData.TypeStatistic.ItemRange)?.currentValue ?? 0f, ~0);
        currentHit = hit; // el setter recalcula isSeeingBlock/currentMiningType, actualiza el outline y resetea el daño si corresponde
    }
    public CharacterData.Statistic GetItemStatistic(CharacterData.TypeStatistic statistic)
    {
        CharacterData.Statistic itemStatistic = characterData.statistics[statistic];
        if (characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemBaseSO)
        {
            if (characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemStatistics.ContainsKey(statistic))
            {
                itemStatistic.baseValue = characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemStatistics[statistic].baseValue;
                itemStatistic.itemValue = characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemStatistics[statistic].itemValue;
                itemStatistic.buffValue = characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemStatistics[statistic].buffValue;
                itemStatistic.maxValue = characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemStatistics[statistic].maxValue;
                itemStatistic.currentValue = characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemStatistics[statistic]._currentValue;
                return itemStatistic;
            }
            return itemStatistic;
        }
        return itemStatistic;
    }
    public CharacterData.Statistic GetItemPower(ItemBaseSO.TypeWeapon typeItem)
    {
        switch (typeItem)
        {
            case ItemBaseSO.TypeWeapon.Pickaxe:
                return GetItemStatistic(CharacterData.TypeStatistic.PicaxePower);
            case ItemBaseSO.TypeWeapon.Axe:
                return GetItemStatistic(CharacterData.TypeStatistic.AxePower);
            case ItemBaseSO.TypeWeapon.Drill:
                return GetItemStatistic(CharacterData.TypeStatistic.DrillPower);
            case ItemBaseSO.TypeWeapon.Shovel:
                return GetItemStatistic(CharacterData.TypeStatistic.ShovelPower);
            case ItemBaseSO.TypeWeapon.Hammer:
                return GetItemStatistic(CharacterData.TypeStatistic.HammerPower);
            case ItemBaseSO.TypeWeapon.Hoe:
                return GetItemStatistic(CharacterData.TypeStatistic.HoePower);
            case ItemBaseSO.TypeWeapon.FishingRod:
                return GetItemStatistic(CharacterData.TypeStatistic.FishingRodPower);
            default:
                return new CharacterData.Statistic();
        }
    }
    void UpdateOutline(VoxelWorld world, bool hasHit, RaycastHit hit, ToolItemSO.MiningType toolMode, float radius)
    {
        if (outline == null) return;
        if (!hasHit) { outline.Hide(); return; }

        switch (toolMode)
        {
            case ToolItemSO.MiningType.Sphere:
                {
                    // contorno voxelizado: las caras externas de los voxels exactos que la esfera
                    // tallaría (mismo criterio que DigSphere). radius (stat ItemRadius) viene en
                    // voxels de diámetro: misma conversión que Mine. El tinte usa el daño guardado
                    // del bloque bajo la mira (representativo del área).
                    Vector3Int block = world.WorldToBlock(hit.point - hit.normal * 0.01f);
                    world.PreviewSphereContour(hit.point, VoxelWorld.SphereRadiusMeters(radius), spherePreviewBuf);
                    if (spherePreviewBuf.Count > 0)
                        outline.ShowContour(spherePreviewBuf, world.GetBlockDamageRatio01(block, this));
                    else
                        outline.Hide();
                    break;
                }

            case ToolItemSO.MiningType.Perfect:
                {
                    Vector3 worldPos = hit.point - hit.normal * 0.01f;
                    if (world.PreviewPerfect(worldPos, this, out VoxelWorld.MiningCell cell))
                        // grietas según el daño guardado de ESE micro-voxel
                        outline.ShowVoxel(cell.min, cell.size, world.GetVoxelDamageRatio01(worldPos, this));
                    else
                        outline.Hide(); // indestructible o poder insuficiente: sin highlight
                    break;
                }

            default: // Pickaxe
                {
                    Vector3Int block = world.WorldToBlock(hit.point - hit.normal * 0.01f);
                    world.PreviewPickaxeContour(block, pickaxePreviewBuf);
                    if (pickaxePreviewBuf.Count > 0)
                        // grietas según el daño guardado del bloque (lo escribe Mine → DamageBlock)
                        outline.ShowContour(pickaxePreviewBuf, world.GetBlockDamageRatio01(block, this));
                    else
                        outline.Hide();
                    break;
                }
        }
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
    public void OnHandleAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (isInitialize && !isInventoryOpen && !isInCanalization && characterAnimator.GetFloat("RightHand") == 0)
            {
                isUsingItem = true;
                StartCoroutine(HanldeAttack());
            }
        }
        else if (context.canceled)
        {
            isUsingItem = false;
        }
    }
    public async Awaitable HanldeAttack()
    {
        while (isUsingItem)
        {
            if (characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemBaseSO)
            {
                if (characterData.statistics[CharacterData.TypeStatistic.Str].currentValue - characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemBaseSO.costPerUse >= 0)
                {
                    await characterData.equipments[ItemsDBSO.TypeModel.Weapon].itemBaseSO.UseItem(new ItemBaseSO.UseItemInfo(this, characterData.equipments[ItemsDBSO.TypeModel.Weapon], false));
                    if (!isSeeingBlock)
                    {
                        isUsingItem = false;
                    }
                    else
                    {
                        print("Repetir ataque");
                    }
                }
            }
            else if (characterData.statistics[CharacterData.TypeStatistic.Str].currentValue - 1 >= 0)
            {
                _ = AwaitForHandAttack();
            }

            await Awaitable.NextFrameAsync();
        }
    }
    async Awaitable AwaitForHandAttack()
    {
        characterData.statistics[CharacterData.TypeStatistic.Str].currentValue--;
        characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Str);
        characterAnimator.SetFloat("RightHand", 1);
        await Awaitable.NextFrameAsync();
        while (true)
        {
            if (!characterAnimator.GetCurrentAnimatorStateInfo(2).IsName("RightHand"))
            {
                Debug.Log("RightHand finish animation");
                break;
            }
        }
        characterAnimator.SetFloat("RightHand", 0);
    }
    void OnHandleToggleInventory(InputAction.CallbackContext context)
    {
        if (isInCanalization || isUsingFastItem) return;
        isInventoryOpen = !isInventoryOpen;
        _ = characterPlayerHud.ToggleCharacterInventory();
    }
    public void OnHandleUseFastItem(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (CanMakeAction()) UseFastItem();
        }
        else if (context.canceled)
        {
            cancelUseFastItem = true;
        }
    }
    public override void UseFastItem()
    {
        if (characterData.fastItems[currentFastItemIndex].itemBaseSO)
        {
            if (!characterData.fastItems[currentFastItemIndex].itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability) || 
                characterData.fastItems[currentFastItemIndex].itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability) &&
                characterData.fastItems[currentFastItemIndex].itemStatistics[CharacterData.TypeStatistic.Durability].currentValue > 0 &&
                (characterData.fastItems[currentFastItemIndex].itemBaseSO.costPerUse == 0 || 
                characterData.statistics[CharacterData.TypeStatistic.Str].currentValue - characterData.fastItems[currentFastItemIndex].itemBaseSO.costPerUse >= 0 && characterAnimator.GetFloat("LeftHand") == 0))
            {
                _ = characterData.fastItems[currentFastItemIndex].itemBaseSO.UseItem(new ItemBaseSO.UseItemInfo(
                    character: this,
                    characterItem: characterData.fastItems[currentFastItemIndex],
                    isFastItem: true
                ));
            }
        }
    }
    public void OnHandleUseSkill(InputAction.CallbackContext context)
    {
        if (CanMakeAction()) _ = UseSkill(Mathf.RoundToInt(context.ReadValue<float>()));
    }
    public override async Awaitable UseSkill(int skillIndex)
    {

        if (characterData.skills[skillIndex].skillsBaseSO != null && !skillsCd.ContainsKey(skillIndex) &&
            characterData.skills[skillIndex].skillsBaseSO.ValidateCanUseSkill(new SkillsBaseSO.CharacterMakeSkillData(this), characterData.skills[skillIndex].level))
        {
            skillsCd.Add(skillIndex, new SkillCd
            {
                maxCd = characterData.skills[skillIndex].skillsBaseSO.statistics[characterData.skills[skillIndex].level][CharacterData.TypeStatistic.Cd].baseValue,
                currentCd = characterData.skills[skillIndex].skillsBaseSO.statistics[characterData.skills[skillIndex].level][CharacterData.TypeStatistic.Cd].baseValue
            });
            await characterData.skills[skillIndex].skillsBaseSO.UseSkill(new SkillsBaseSO.CharacterMakeSkillData(this), characterData.skills[skillIndex].level);
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
            characterData.bag[bagIndex] = itemDropped.itemData;
            characterPlayerHud.GetBagSlotByIndex(bagIndex).InitializeSlot(characterData.bag[bagIndex]);
            Destroy(itemDropped.gameObject);
            interactables.Remove(itemDropped);
            OnShowItemsToPickUp?.Invoke();
        }
    }
    void OnHandleChangeFastItem(InputAction.CallbackContext context)
    {
        if (!CanMakeAction()) return;
        DesEquipSlotItem(GetFastItemItemByIndex(currentFastItemIndex), ItemsDBSO.TypeModel.FastItems, true);
        currentFastItemIndex += (int)context.ReadValue<float>();
        if (currentFastItemIndex < 0) currentFastItemIndex = characterPlayerHud.characterUI.fastItems.Count - 1;
        else if (currentFastItemIndex >= characterPlayerHud.characterUI.fastItems.Count) currentFastItemIndex = 0;
        EquipSlotItem(GetFastItemItemByIndex(currentFastItemIndex), ItemsDBSO.TypeModel.FastItems, true);
        characterPlayerHud.SelectFastItem();
        UpdateFastItemModel();
        characterPlayerHud.RefreshCharacterStatistics();
    }
    public void ChangeObjectPosition(InventorySlot.ItemInfo itemSource, InventorySlot.ItemInfo itemTarget)
    {
        if (itemSource == null || itemTarget == null) return;
        if (IsSameSlot(itemSource, itemTarget)) return;

        itemSource.itemData = GetOrCreateItem(itemSource.typeItem, itemSource.index);
        itemTarget.itemData = GetOrCreateItem(itemTarget.typeItem, itemTarget.index);

        if (itemSource.itemData.itemBaseSO == null) return;

        if (!CanSlotHoldItem(itemTarget.typeItem, itemSource.itemData)) return;
        if (!CanSlotHoldItem(itemSource.typeItem, itemTarget.itemData)) return;

        if (!TryStackItems(itemSource, itemTarget)) SwapItems(itemSource, itemTarget);

        RefreshSlotsAfterChange(itemSource, itemTarget);
        _ = characterPlayerHud.ResetInventoryTarget();
    }
    bool TryStackItems(InventorySlot.ItemInfo itemSource, InventorySlot.ItemInfo itemTarget)
    {
        CharacterData.CharacterItem sourceItem = itemSource.itemData;
        CharacterData.CharacterItem targetItem = itemTarget.itemData;

        if (!IsStackableSlot(itemSource.typeItem) || !IsStackableSlot(itemTarget.typeItem)) return false;
        if (targetItem.itemBaseSO == null || targetItem.itemBaseSO != sourceItem.itemBaseSO) return false;
        if (!sourceItem.itemStatistics.ContainsKey(CharacterData.TypeStatistic.Amount)) return false;
        if (!targetItem.itemStatistics.ContainsKey(CharacterData.TypeStatistic.Amount)) return false;

        CharacterData.Statistic sourceAmount = sourceItem.itemStatistics[CharacterData.TypeStatistic.Amount];
        CharacterData.Statistic targetAmount = targetItem.itemStatistics[CharacterData.TypeStatistic.Amount];
        if (targetAmount.currentValue >= targetAmount.maxValue) return false;

        FindAmountToAppend(sourceItem, targetItem, out int amountToAppend);
        if (amountToAppend <= 0) return false;

        targetAmount.currentValue += amountToAppend;
        sourceAmount.currentValue -= amountToAppend;

        if (sourceAmount.currentValue <= 0)
        {
            if (IsActiveSlot(itemSource)) DesEquipSlotItem(sourceItem, itemSource.typeItem);
            SetItem(itemSource.typeItem, itemSource.index, new CharacterData.CharacterItem(itemSource.typeItem));
            itemSource.itemData = GetOrCreateItem(itemSource.typeItem, itemSource.index);
        }
        return true;
    }
    void SwapItems(InventorySlot.ItemInfo itemSource, InventorySlot.ItemInfo itemTarget)
    {
        CharacterData.CharacterItem sourceItem = new CharacterData.CharacterItem(itemSource.itemData);
        CharacterData.CharacterItem targetItem = new CharacterData.CharacterItem(itemTarget.itemData);

        bool isSourceActive = IsActiveSlot(itemSource);
        bool isTargetActive = IsActiveSlot(itemTarget);

        if (isSourceActive) DesEquipSlotItem(sourceItem, itemSource.typeItem);
        if (isTargetActive) DesEquipSlotItem(targetItem, itemTarget.typeItem);

        NormalizeTypeObject(sourceItem, itemTarget.typeItem);
        NormalizeTypeObject(targetItem, itemSource.typeItem);
        SetItem(itemTarget.typeItem, itemTarget.index, sourceItem);
        SetItem(itemSource.typeItem, itemSource.index, targetItem);
        itemTarget.itemData = sourceItem;
        itemSource.itemData = targetItem;

        if (isTargetActive) EquipSlotItem(sourceItem, itemTarget.typeItem);
        if (isSourceActive) EquipSlotItem(targetItem, itemSource.typeItem);
    }
    void RefreshSlotsAfterChange(InventorySlot.ItemInfo itemSource, InventorySlot.ItemInfo itemTarget)
    {
        itemSource.inventorySlot.InitializeSlot(GetOrCreateItem(itemSource.typeItem, itemSource.index));
        itemTarget.inventorySlot.InitializeSlot(GetOrCreateItem(itemTarget.typeItem, itemTarget.index));

        if (itemSource.typeItem == ItemsDBSO.TypeModel.FastItems || itemTarget.typeItem == ItemsDBSO.TypeModel.FastItems)
        {
            characterPlayerHud.RefreshFastItems();
            characterPlayerHud.SelectFastItem();
        }
        if (IsCurrentFastItemSlot(itemSource) || IsCurrentFastItemSlot(itemTarget)) UpdateFastItemModel();

        characterPlayerHud.RefreshCharacterStatistics();
    }
    void EquipSlotItem(CharacterData.CharacterItem item, ItemsDBSO.TypeModel slotType, bool cancelRefreshModel = false)
    {
        if (!AppliesEquipEffects(item)) return;
        _ = item.itemBaseSO.EquipItem(this, item, !cancelRefreshModel, slotType == ItemsDBSO.TypeModel.FastItems);
    }
    void DesEquipSlotItem(CharacterData.CharacterItem item, ItemsDBSO.TypeModel slotType, bool cancelRefreshModel = false)
    {
        if (!AppliesEquipEffects(item)) return;
        _ = item.itemBaseSO.DesEquipItem(this, item, !cancelRefreshModel, slotType == ItemsDBSO.TypeModel.FastItems);
    }
    bool AppliesEquipEffects(CharacterData.CharacterItem item)
    {
        return item != null && (item.itemBaseSO is EquipableItemSO || item.itemBaseSO is ActivableItemSO);
    }
    bool IsActiveSlot(InventorySlot.ItemInfo itemInfo)
    {
        if (IsEquipableItem(itemInfo.typeItem)) return true;
        return IsCurrentFastItemSlot(itemInfo);
    }
    bool IsCurrentFastItemSlot(InventorySlot.ItemInfo itemInfo)
    {
        return itemInfo.typeItem == ItemsDBSO.TypeModel.FastItems && itemInfo.index == currentFastItemIndex;
    }
    bool IsSameSlot(InventorySlot.ItemInfo itemA, InventorySlot.ItemInfo itemB)
    {
        if (itemA.typeItem != itemB.typeItem) return false;
        if (IsEquipableItem(itemA.typeItem)) return true;
        return itemA.index == itemB.index;
    }
    bool IsStackableSlot(ItemsDBSO.TypeModel slotType)
    {
        return slotType == ItemsDBSO.TypeModel.Bag ||
               slotType == ItemsDBSO.TypeModel.FastItems ||
               slotType == ItemsDBSO.TypeModel.Ammo;
    }
    bool CanSlotHoldItem(ItemsDBSO.TypeModel slotType, CharacterData.CharacterItem item)
    {
        if (item?.itemBaseSO == null) return true;
        if (IsEquipableItem(slotType)) return item.itemBaseSO.typeObject == slotType;
        if (slotType == ItemsDBSO.TypeModel.Ammo) return item.itemBaseSO.typeObject == ItemsDBSO.TypeModel.Ammo;
        return slotType == ItemsDBSO.TypeModel.Bag || slotType == ItemsDBSO.TypeModel.FastItems;
    }
    void NormalizeTypeObject(CharacterData.CharacterItem item, ItemsDBSO.TypeModel slotType)
    {
        item.typeObject = item.itemBaseSO == null ? slotType : item.itemBaseSO.typeObject;
    }
    public CharacterData.CharacterItem GetItem(ItemsDBSO.TypeModel typeItem, int index)
    {
        if (IsEquipableItem(typeItem)) return GetEquipmentItemByIndex(typeItem);
        if (typeItem == ItemsDBSO.TypeModel.FastItems) return GetFastItemItemByIndex(index);
        if (typeItem == ItemsDBSO.TypeModel.Bag) return GetBagItemByIndex(index);
        if (typeItem == ItemsDBSO.TypeModel.Ammo) return GetAmmoItemByIndex(index);
        return null;
    }
    public CharacterData.CharacterItem GetOrCreateItem(ItemsDBSO.TypeModel typeItem, int index)
    {
        CharacterData.CharacterItem item = GetItem(typeItem, index) ?? new CharacterData.CharacterItem(typeItem);
        SetItem(typeItem, index, item);
        return item;
    }
    public void SetItem(ItemsDBSO.TypeModel typeItem, int index, CharacterData.CharacterItem item)
    {
        if (IsEquipableItem(typeItem)) characterData.equipments[typeItem] = item;
        else if (typeItem == ItemsDBSO.TypeModel.FastItems) characterData.fastItems[index] = item;
        else if (typeItem == ItemsDBSO.TypeModel.Bag) characterData.bag[index] = item;
        else if (typeItem == ItemsDBSO.TypeModel.Ammo) characterData.ammo[index] = item;
    }
    public bool FindEmptyBagSlot(out int bagIndex)
    {
        foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in characterData.bag)
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
        foreach (KeyValuePair<int, CharacterData.CharacterItem> bagSlot in characterData.fastItems)
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
        foreach (KeyValuePair<int, CharacterData.CharacterItem> fastItemSlot in characterData.fastItems)
        {
            if (fastItemSlot.Value.itemBaseSO != null && fastItemSlot.Value.itemBaseSO.id == characterData.bag[bagIndex].itemBaseSO.id && fastItemSlot.Value.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue < fastItemSlot.Value.itemStatistics[CharacterData.TypeStatistic.Amount].maxValue)
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
    public bool CanMakeAction()
    {
        return !isInventoryOpen && !isInCanalization && !isUsingFastItem;
    }
    public CharacterData.CharacterItem GetBagItemByIndex(int index)
    {
        if (characterData.bag.TryGetValue(index, out CharacterData.CharacterItem bagItem) && bagItem != null)
        {
            return bagItem;
        }
        return new CharacterData.CharacterItem(ItemsDBSO.TypeModel.Bag);
    }
    public CharacterData.CharacterItem GetFastItemItemByIndex(int index)
    {
        if (characterData.fastItems.TryGetValue(index, out CharacterData.CharacterItem fastItemItem) && fastItemItem != null)
        {
            return fastItemItem;
        }
        return new CharacterData.CharacterItem(ItemsDBSO.TypeModel.FastItems);
    }
    public CharacterData.CharacterItem GetEquipmentItemByIndex(ItemsDBSO.TypeModel index)
    {
        if (characterData.equipments.TryGetValue(index, out CharacterData.CharacterItem equipmentItem) && equipmentItem != null)
        {
            return equipmentItem;
        }
        return new CharacterData.CharacterItem(index);
    }
    public CharacterData.CharacterItem GetAmmoItemByIndex(int index)
    {
        if (characterData.ammo.TryGetValue(index, out CharacterData.CharacterItem ammoItem) && ammoItem != null)
        {
            return ammoItem;
        }
        return new CharacterData.CharacterItem(ItemsDBSO.TypeModel.Ammo);
    }
    public void FastEquipItem(int slotIndex, InventorySlot bagSlot = null)
    {
        if (characterPlayerHud.isDraggingItem) return;

        CharacterData.CharacterItem bagItem = GetOrCreateItem(ItemsDBSO.TypeModel.Bag, slotIndex);
        if (bagItem.itemBaseSO == null) return;

        InventorySlot.ItemInfo source = new InventorySlot.ItemInfo
        {
            typeItem = ItemsDBSO.TypeModel.Bag,
            index = slotIndex,
            itemData = bagItem,
            inventorySlot = bagSlot
        };

        if (IsEquipableItem(bagItem.itemBaseSO.typeObject))
        {
            ChangeObjectPosition(source, new InventorySlot.ItemInfo { 
                typeItem = bagItem.itemBaseSO.typeObject,
                index = 0,
                itemData = characterData.equipments[bagItem.typeObject],
                inventorySlot = characterPlayerHud.characterUI.equipments[bagItem.itemBaseSO.typeObject] });
            return;
        }
        if (bagItem.itemBaseSO.typeObject != ItemsDBSO.TypeModel.FastItems) return;

        if (FindSimilarFastItemSlot(slotIndex, out int similarFastItemIndex))
        {
            ChangeObjectPosition(source, new InventorySlot.ItemInfo { 
                typeItem = ItemsDBSO.TypeModel.FastItems,
                index = similarFastItemIndex,
                itemData =  characterData.fastItems[similarFastItemIndex],
                inventorySlot = characterPlayerHud.characterUI.fastItems[similarFastItemIndex]});
        }
        else if (FindEmptyFastItemSlot(out int fastItemIndex))
        {
            ChangeObjectPosition(source, new InventorySlot.ItemInfo { 
                typeItem = ItemsDBSO.TypeModel.FastItems,
                index = fastItemIndex,
                itemData = new CharacterData.CharacterItem(ItemsDBSO.TypeModel.FastItems),
                inventorySlot = characterPlayerHud.characterUI.fastItems[fastItemIndex] });
        }
    }
    public Task DropItem(InventorySlot draggedSlot)
    {
        if (draggedSlot == null) return Task.CompletedTask;

        InventorySlot.ItemInfo itemInfo = new InventorySlot.ItemInfo
        {
            typeItem = draggedSlot.typeInventorySlot,
            index = draggedSlot.slotIndex,
            inventorySlot = draggedSlot
        };
        itemInfo.itemData = GetOrCreateItem(itemInfo.typeItem, itemInfo.index);
        if (itemInfo.itemData.itemBaseSO == null) return Task.CompletedTask;

        ItemDropped itemDropped = Instantiate(GameData.Instance.utils.prefabs["ItempDropped"], transform.position + Vector3.up / 2, Quaternion.identity).GetComponent<ItemDropped>();
        itemDropped.InitializeDropItem(itemInfo.itemData, true);
        LaunchDropItem(itemDropped);

        if (IsActiveSlot(itemInfo)) DesEquipSlotItem(itemInfo.itemData, itemInfo.typeItem);

        SetItem(itemInfo.typeItem, itemInfo.index, new CharacterData.CharacterItem(itemInfo.typeItem));
        itemInfo.inventorySlot.InitializeSlot(GetOrCreateItem(itemInfo.typeItem, itemInfo.index));

        if (itemInfo.typeItem == ItemsDBSO.TypeModel.FastItems)
        {
            characterPlayerHud.RefreshFastItems();
            characterPlayerHud.SelectFastItem();
        }
        if (IsCurrentFastItemSlot(itemInfo)) UpdateFastItemModel();
        characterPlayerHud.RefreshCharacterStatistics();
        return Task.CompletedTask;
    }
    void LaunchDropItem(ItemDropped itemDropped)
    {
        itemDropped.rb.linearVelocity = Vector3.zero;
        itemDropped.rb.AddForce(characterModel.modelTransform.forward.normalized * dropLaunchForce + Vector3.up * dropUpForce, ForceMode.Impulse);
    }
}
