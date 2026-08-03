using System;
using System.Collections;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class CharacterBase : MonoBehaviour
{
    public bool isInitialize;
    public bool autoInit;
    public CharacterModel characterModel;
    public CharacterData characterData;
    public int currentFastItemIndex;
    public Vector2 directionMovement = new Vector2();
    public CharacterPlayerHud characterPlayerHud;
    public CharacterMovementBase characterMovement;
    public CharacterAnimator characterAnimations;
    public CharacterDirection characterDirection;
    public DissolvePlayer dissolvePlayer;
    public Animator characterAnimator;
    public SerializedDictionary<StatusEffectBaseSO, StatusEffect> statusEffects = new SerializedDictionary<StatusEffectBaseSO, StatusEffect>();
    public List<StatusEffectBaseSO> statusToRemove = new List<StatusEffectBaseSO>();
    public SerializedDictionary<int, SkillCd> skillsCd = new SerializedDictionary<int, SkillCd>();
    public SerializedDictionary<int, ActibableItemsInfo> activableItems = new SerializedDictionary<int, ActibableItemsInfo>();
    public List<int> skillsToRemove = new List<int>();
    protected Coroutine handleStatusEffectCoroutine;
    protected Coroutine handleUseSkillCoroutine;
    public bool isGrounded => SetGrounded();
    public bool isDashing;
    public bool isRunning;
    public bool isInCanalization;
    public bool _cancelCanalization;
    public bool cancelCanalization
    {
        get => _cancelCanalization;
        set
        {
            _cancelCanalization = value;
            if (value)
            {
                StartCoroutine(ResetCancelCanalization());
            }
        }
    }
    public bool isUsingFastItem;
    public bool _cancelUseFastItem;
    public bool cancelUseFastItem
    {
        get => _cancelUseFastItem;
        set
        {
            _cancelUseFastItem = value;
            if (value)
            {
                StartCoroutine(ResetCancelUseFastItem());
            }
        }
    }
    public void OnEnable()
    {
        OnEnableHandle();
    }
    public void Awake()
    {
        if (autoInit) _ = InitializeCharacter();
    }
    void Update()
    {
        if (isInitialize)
        {
            MoveCharacter();
            AnimateCharacter();
        }
    }
    public virtual void OnEnableHandle() { }
    public async virtual Awaitable InitializeCharacter() { }
    protected async Awaitable InitializeModels()
    {
        foreach (KeyValuePair<ItemsDBSO.TypeModel, List<CharacterModelData>> model in characterModel.meshesData)
        {
            if (characterData.models.TryGetValue(model.Key, out CharacterData.CharacterSkinInfo skinInfo))
            {
                if (skinInfo.itemId != 0)
                {
                    for (int i = 0; i < model.Value.Count; i++)
                    {
                        model.Value[i].meshFilter.mesh = skinInfo.itemBaseSO.modelInfo.originalMesh[i];
                        Material[] materials = model.Value[i].meshRenderer.materials;
                        if (!skinInfo.itemBaseSO.modelInfo.useTexture)
                        {
                            for (int j = 0; j < skinInfo.colors.Count; j++)
                            {
                                materials[j].SetFloat("_UseTexture", 0f);
                                materials[j].SetColor("_Color", skinInfo.colors[j]);
                            }
                        }
                        else
                        {
                            for (int j = 0; j < skinInfo.itemBaseSO.modelInfo.textures.Count; j++)
                            {
                                materials[j].SetFloat("_UseTexture", 1f);
                                materials[j].SetTexture("_MainTex", skinInfo.itemBaseSO.modelInfo.textures[j].texture);
                                SetTextureFromAtlas(
                                    skinInfo.itemBaseSO.modelInfo.textures[j],
                                    characterModel.meshesData[model.Key][i].meshRenderer,
                                    skinInfo.itemBaseSO.modelInfo.originalMesh[j]
                                );
                            }
                        }
                        model.Value[i].meshRenderer.materials = materials;
                        model.Value[i].meshFilter.gameObject.SetActive(!skinInfo.occlude);
                    }
                }
                else
                {
                    for (int i = 0; i < model.Value.Count; i++)
                    {
                        model.Value[i].meshFilter.gameObject.SetActive(false);
                    }
                }
            }
        }
        if (characterData.fastItems[currentFastItemIndex].itemBaseSO)
        {
            RefreshCharacterItemModel(characterData.fastItems[currentFastItemIndex], true, ItemsDBSO.TypeModel.FastItems);
        }
        else
        {
            RefreshCharacterItemModel(new CharacterData.CharacterItem(ItemsDBSO.TypeModel.FastItems), false);
        }
    }
    public void RefreshCharacterItemModel(CharacterData.CharacterItem characterItem, bool isEquip, ItemsDBSO.TypeModel typeObject = ItemsDBSO.TypeModel.None)
    {
        ItemsDBSO.TypeModel typeModel = typeObject == ItemsDBSO.TypeModel.None ? characterItem.typeObject : typeObject;
        if (characterItem.itemBaseSO != null)
        {
            for (int i = 0; i < characterModel.meshesData[typeModel].Count; i++)
            {
                characterModel.meshesData[typeModel][i].meshFilter.gameObject.SetActive(isEquip);
            }
            if (isEquip)
            {
                for (int i = 0; i < characterModel.meshesData[typeModel].Count; i++)
                {
                    characterModel.meshesData[typeModel][i].meshFilter.mesh = characterItem.itemBaseSO.modelInfo.originalMesh[i];
                    Material[] materials = characterModel.meshesData[typeModel][i].meshRenderer.materials;
                    if (!characterItem.itemBaseSO.modelInfo.useTexture)
                    {
                        for (int j = 0; j < characterItem.itemBaseSO.modelInfo.colors.Count; j++)
                        {
                            materials[j].SetFloat("_UseTexture", 0f);
                            materials[j].SetColor("_Color", characterItem.itemBaseSO.modelInfo.colors[j]);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < characterItem.itemBaseSO.modelInfo.textures.Count; j++)
                        {
                            materials[j].SetFloat("_UseTexture", 1f);
                            materials[j].SetTexture("_MainTex", characterItem.itemBaseSO.modelInfo.textures[j].texture);
                            SetTextureFromAtlas(
                                characterItem.itemBaseSO.modelInfo.textures[j],
                                characterModel.meshesData[typeModel][i].meshRenderer,
                                characterItem.itemBaseSO.modelInfo.originalMesh[j]
                            );
                        }
                    }
                    characterModel.meshesData[typeModel][i].meshRenderer.materials = materials;
                    characterModel.meshesData[typeModel][i].meshFilter.gameObject.SetActive(true);
                }
                if (typeModel != ItemsDBSO.TypeModel.FastItems)
                {
                    for (int i = 0; i < characterItem.itemBaseSO.modelInfo.occludedModels.Count; i++)
                    {
                        characterData.models[characterItem.itemBaseSO.modelInfo.occludedModels[i]].occlude = true;
                        foreach (CharacterModelData modelData in characterModel.meshesData[characterItem.itemBaseSO.modelInfo.occludedModels[i]])
                        {
                            modelData.meshFilter.gameObject.SetActive(false);
                        }
                    }
                }
            }
            else if (typeModel != ItemsDBSO.TypeModel.FastItems)
            {
                for (int i = 0; i < characterItem.itemBaseSO.modelInfo.occludedModels.Count; i++)
                {
                    characterData.models[characterItem.itemBaseSO.modelInfo.occludedModels[i]].occlude = false;
                    foreach (CharacterModelData modelData in characterModel.meshesData[characterItem.itemBaseSO.modelInfo.occludedModels[i]])
                    {
                        modelData.meshFilter.gameObject.SetActive(true);
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < characterModel.meshesData[typeModel].Count; i++)
            {
                characterModel.meshesData[typeModel][i].meshFilter.gameObject.SetActive(false);
            }
        }
    }
    public void UpdateFastItemModel()
    {
        if (characterData.fastItems[currentFastItemIndex].itemBaseSO)
        {
            RefreshCharacterItemModel(characterData.fastItems[currentFastItemIndex], true, ItemsDBSO.TypeModel.FastItems);
            for (int i = 0; i < characterModel.meshesData[ItemsDBSO.TypeModel.FastItems].Count; i++)
            {
                dissolvePlayer.NeedAppearSpecificObj(characterModel.meshesData[ItemsDBSO.TypeModel.FastItems][i].meshRenderer);
            }
        }
        else
        {
            RefreshCharacterItemModel(new CharacterData.CharacterItem(ItemsDBSO.TypeModel.FastItems), false);
        }
    }
    void SetTextureFromAtlas(Sprite spriteFromAtlas, MeshRenderer meshRenderer, Mesh originalMesh)
    {
        Vector2[] uvs = originalMesh.uv;
        Texture2D texture = spriteFromAtlas.texture;
        meshRenderer.material.mainTexture = texture;
        Rect spriteRect = spriteFromAtlas.rect;
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i].x = Mathf.Lerp(spriteRect.x / texture.width, (spriteRect.x + spriteRect.width) / texture.width, uvs[i].x);
            uvs[i].y = Mathf.Lerp(spriteRect.y / texture.height, (spriteRect.y + spriteRect.height) / texture.height, uvs[i].y);
        }
        meshRenderer.GetComponent<MeshFilter>().mesh.uv = uvs;
    }
    public void MoveCharacter()
    {
        characterMovement.HandleMovement();
    }
    public void AnimateCharacter()
    {
        characterAnimations.HandleAnimation();
    }
    public virtual void OnHandlePickUpItem(ItemDropped itemDropped) { }
    public virtual void UseFastItem() { }
    public virtual async Awaitable UseSkill(int skillIndex) { }
    protected bool SetGrounded()
    {        
        return Physics.OverlapBox(transform.position, new Vector3(0.5f, 0.1f, 0.5f) / 2, Quaternion.identity, LayerMask.GetMask("Map")).Length > 0;
    }
    public virtual void TakeExp(CharacterData.Statistic statistic)
    {
        int amount = Mathf.CeilToInt(statistic.maxValue * 0.1f);
        characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue += amount;
        int level = 0;
        while (characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue >= characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue)
        {
            int spare = characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue > characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue ?
                Mathf.CeilToInt(characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue - characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue) : 0;
            characterData.statistics[CharacterData.TypeStatistic.Exp].baseValue = Mathf.CeilToInt(characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue * 2.2f);
            characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue = characterData.statistics[CharacterData.TypeStatistic.Exp].baseValue;
            characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue = spare;
            characterData.LevelUp();
            level++;
        }
    }
    public string GenerateFantasyName()
    {
        string[] syllablesStart = { "Ka", "Lo", "Mi", "Ra", "Th", "El", "Ar", "Va", "Zy", "Xe", "Lu", "Na" };
        string[] syllablesMiddle = { "ra", "en", "or", "il", "um", "ar", "is", "al", "on", "ir" };
        string[] syllablesEnd = { "th", "dor", "ion", "mir", "rak", "len", "var", "oth", "us", "iel" };

        int pattern = UnityEngine.Random.Range(0, 3);
        string name = "";

        switch (pattern)
        {
            case 0:
                name = string.Concat(
                    syllablesStart[UnityEngine.Random.Range(0, syllablesStart.Length)],
                    syllablesEnd[UnityEngine.Random.Range(0, syllablesEnd.Length)]
                );
                break;
            case 1:
                name = string.Concat(
                    syllablesStart[UnityEngine.Random.Range(0, syllablesStart.Length)],
                    syllablesMiddle[UnityEngine.Random.Range(0, syllablesMiddle.Length)],
                    syllablesEnd[UnityEngine.Random.Range(0, syllablesEnd.Length)]
                );
                break;
            case 2:
                name = string.Concat(
                    syllablesStart[UnityEngine.Random.Range(0, syllablesStart.Length)],
                    syllablesMiddle[UnityEngine.Random.Range(0, syllablesMiddle.Length)],
                    syllablesMiddle[UnityEngine.Random.Range(0, syllablesMiddle.Length)],
                    syllablesEnd[UnityEngine.Random.Range(0, syllablesEnd.Length)]
                );
                break;
        }

        return name;
    }
    public async Awaitable TakeDamage(CharacterBase characterMakeDamage, int damage)
    {
        FloatingText floatingText = Instantiate(GameData.Instance.utils.prefabs["FloatingText"], transform.position, Quaternion.identity).GetComponent<FloatingText>();
        _ = floatingText.SendText(damage.ToString(), Color.red, false);
        if (characterData.statistics.TryGetValue(CharacterData.TypeStatistic.Hp, out CharacterData.Statistic characterTakedDamageStatistic))
        {
            characterTakedDamageStatistic.currentValue -= damage;
        }
        characterAnimations.MakeEffect(AnimationEffectsSO.TypeAnimationsEffects.Shake);
        characterAnimations.MakeEffect(AnimationEffectsSO.TypeAnimationsEffects.Blink);
        if (characterData.statistics[CharacterData.TypeStatistic.Hp].currentValue <= 0) await Die(characterMakeDamage);
        await Awaitable.NextFrameAsync();
    }
    public virtual async Awaitable Die(CharacterBase characterMakeDamage)
    {
        await Awaitable.WaitForSecondsAsync(0.3f);
        GameObject dieEffect = Instantiate(GameData.Instance.utils.prefabs["DieEffect"], transform.position, Quaternion.identity);
        await Awaitable.WaitForSecondsAsync(1);
        Destroy(dieEffect);
        // _ = GameManager.Instance.LoadScene(GameManager.TypeScene.HomeScene);
        await Awaitable.NextFrameAsync();
    }
    public IEnumerator HandleStatusEffect()
    {
        while (statusEffects.Count > 0)
        {
            foreach (KeyValuePair<StatusEffectBaseSO, StatusEffect> status in statusEffects)
            {
                status.Value.cd -= Time.deltaTime;
                if (status.Value.cd <= 0)
                {
                    status.Value.amount--;
                    if (status.Value.amount > 0)
                    {
                        status.Value.cd = status.Value.statusEffectBaseSO.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
                        status.Value.statusEffectBaseSO.DiscountEffect(this);
                        if (characterPlayerHud.characterUI.statusEffectUI.statusEffectsBanners.ContainsKey(status.Key))
                            characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status.Key].SetBannerData(status.Value);
                    }
                    else
                    {
                        if (characterPlayerHud.characterUI.statusEffectUI.statusEffectsBanners.ContainsKey(status.Key))
                        {
                            statusToRemove.Add(status.Key);
                        }
                        break;
                    }
                }
                else
                {
                    characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status.Key].RefreshCD(status.Value);
                }
            }
            if (statusToRemove.Count > 0)
            {
                RemoveStatus();
            }
            yield return null;
        }
        handleStatusEffectCoroutine = null;
    }
    public void RemoveStatus()
    {
        foreach (StatusEffectBaseSO status in statusToRemove)
        {
            status.RemoveEffect(this);
            statusEffects.Remove(status);
            Destroy(characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status].gameObject);
            characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners.Remove(status);
        }
        statusToRemove.Clear();
    }
    public IEnumerator HandleUseSkill()
    {
        while (skillsCd.Count > 0)
        {
            foreach (KeyValuePair<int, SkillCd> skill in skillsCd)
            {
                skill.Value.currentCd -= Time.deltaTime;
                characterPlayerHud?.characterUI.skills[skill.Key].RefreshCD(skill.Value);
                if (skill.Value.currentCd <= 0)
                {

                    skillsToRemove.Add(skill.Key);
                }
            }
            if (skillsToRemove.Count > 0)
            {
                RemoveSkillCd();
            }
            yield return null;
        }
        handleUseSkillCoroutine = null;
    }
    public void RemoveSkillCd()
    {
        foreach (int skill in skillsToRemove)
        {
            skillsCd.Remove(skill);
        }
        skillsToRemove.Clear();
    }
    public void AddStatusEffect(StatusEffectBaseSO statusEffect)
    {
        if (statusEffects.ContainsKey(statusEffect))
        {
            statusEffects[statusEffect].AppendStatusEffect();
            statusEffect.ReApplyEffect(this);
        }
        else
        {
            statusEffects.Add(statusEffect, new StatusEffect(statusEffect));
            statusEffect.ApplyEffect(this);
        }
        characterPlayerHud?.AddStatusEffect(statusEffects[statusEffect]);

        if (handleStatusEffectCoroutine == null)
        {
            handleStatusEffectCoroutine = StartCoroutine(HandleStatusEffect());
        }
    }
    IEnumerator ResetCancelCanalization()
    {
        yield return null;
        cancelCanalization = false;
    }
    IEnumerator ResetCancelUseFastItem()
    {
        yield return null;
        cancelUseFastItem = false;
    }
    [Serializable]
    public class CharacterModel
    {
        public SerializedDictionary<ItemsDBSO.TypeModel, List<CharacterModelData>> meshesData = new SerializedDictionary<ItemsDBSO.TypeModel, List<CharacterModelData>>();
        public Transform modelTransform;
    }
    [Serializable]
    public class CharacterModelData
    {
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
    }
    [Serializable]
    public class StatusEffect
    {
        public StatusEffectBaseSO statusEffectBaseSO = new StatusEffectBaseSO();
        public float cd;
        public int amount;
        public StatusEffect(StatusEffectBaseSO statusEffect)
        {
            statusEffectBaseSO = statusEffect;
            cd = statusEffect.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
            amount = 1;
        }
        public void AppendStatusEffect()
        {
            bool canAdd = amount < statusEffectBaseSO.maxStack;
            amount = canAdd ? amount + 1 : statusEffectBaseSO.maxStack;
            if (!canAdd)
            {
                cd = statusEffectBaseSO.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
            }
        }
    }
    [Serializable]
    public class ActibableItemsInfo
    {
        public ActivableItemSO activableItemSO;
        public GameObject activableItemPrefab;
        public ActibableItemsInfo(ActivableItemSO activableItemSO, GameObject activableItemPrefab, Coroutine handleCoroutine = null)
        {
            this.activableItemSO = activableItemSO;
            this.activableItemPrefab = activableItemPrefab;
            this.handleCoroutine = handleCoroutine;
        }
        public Coroutine handleCoroutine;
    }
    [Serializable]
    public class SkillCd
    {
        public float maxCd;
        public float currentCd;
    }
}