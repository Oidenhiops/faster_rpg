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
    public CharacterData[] charactersData;
    public int characterIndex;
    public int currentFastItemIndex;
    public Vector2 directionMovement = new Vector2();
    public GameObject floatingTextPrefab;
    public GameObject dieEffectPrefab;
    public GameObject itempDroppedPrefab;
    public CharacterPlayerHud characterPlayerHud;
    public CharacterMovementBase characterMovement;
    public CharacterAnimator characterAnimations;
    public CharacterDirection characterDirection;
    public DissolvePlayer dissolvePlayer;
    public Animator characterAnimator;
    public SerializedDictionary<int, SerializedDictionary<StatusEffectBaseSO, StatusEffect>> statusEffects = new SerializedDictionary<int, SerializedDictionary<StatusEffectBaseSO, StatusEffect>>();
    public SerializedDictionary<int, List<StatusEffectBaseSO>> statusToRemove = new SerializedDictionary<int, List<StatusEffectBaseSO>>();
    List<int> statusEffectsCharacterKeysToRemove = new();
    public SerializedDictionary<int, SerializedDictionary<int, SkillCd>> skillsCd = new SerializedDictionary<int, SerializedDictionary<int, SkillCd>>();
    public SerializedDictionary<int, List<int>> skillsToRemove = new SerializedDictionary<int, List<int>>();
    List<int> skillsCharacterKeysToRemove = new();
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
        foreach (KeyValuePair<CharactersModelDBSO.TypeModel, List<CharacterModelData>> model in characterModel.meshesData)
        {
            if (charactersData[characterIndex].models.TryGetValue(model.Key, out CharacterData.CharacterSkinInfo skinInfo))
            {
                if (skinInfo.meshId != 0)
                {
                    for (int i = 0; i < model.Value.Count; i++)
                    {
                        model.Value[i].meshFilter.mesh = GameData.Instance.charactersModelDBSO.data[model.Key][skinInfo.meshId][i];
                        Material[] materials = model.Value[i].meshRenderer.materials;
                        if (!skinInfo.useTexture)
                        {
                            for (int j = 0; j < skinInfo.colors.Count; j++)
                            {
                                materials[j].SetColor("_Color", skinInfo.colors[j]);
                            }
                        }
                        else
                        {
                            for (int j = 0; j < skinInfo.textures.Count; j++)
                            {
                                materials[j].SetTexture("_MainTex", skinInfo.textures[j].texture);
                                SetTextureFromAtlas(
                                    skinInfo.textures[j],
                                    characterModel.meshesData[model.Key][i].meshRenderer,
                                    skinInfo.originalMesh[j]
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
        if (charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO)
        {
            RefreshCharacterItemModel(charactersData[characterIndex].fastItems[currentFastItemIndex], true, CharactersModelDBSO.TypeModel.FastItems);
        }
        else
        {
            RefreshCharacterItemModel(new CharacterData.CharacterItem
            {
                typeObject = CharactersModelDBSO.TypeModel.FastItems,
            }, false);
        }
    }
    public void RefreshCharacterItemModel(CharacterData.CharacterItem characterItem, bool isEquip, CharactersModelDBSO.TypeModel typeObject = CharactersModelDBSO.TypeModel.None)
    {
        CharactersModelDBSO.TypeModel typeModel = typeObject == CharactersModelDBSO.TypeModel.None ? characterItem.typeObject : typeObject;
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
                    characterModel.meshesData[typeModel][i].meshFilter.mesh =
                        GameData.Instance.charactersModelDBSO.data[characterItem.itemBaseSO.typeObject][characterItem.itemBaseSO.modelInfo.meshId][i];
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
                if (typeModel != CharactersModelDBSO.TypeModel.FastItems)
                {
                    for (int i = 0; i < characterItem.itemBaseSO.modelInfo.occludedModels.Count; i++)
                    {
                        charactersData[characterIndex].models[characterItem.itemBaseSO.modelInfo.occludedModels[i]].occlude = true;
                        foreach (CharacterModelData modelData in characterModel.meshesData[characterItem.itemBaseSO.modelInfo.occludedModels[i]])
                        {
                            modelData.meshFilter.gameObject.SetActive(false);
                        }
                    }
                }
            }
            else if (typeModel != CharactersModelDBSO.TypeModel.FastItems)
            {
                for (int i = 0; i < characterItem.itemBaseSO.modelInfo.occludedModels.Count; i++)
                {
                    charactersData[characterIndex].models[characterItem.itemBaseSO.modelInfo.occludedModels[i]].occlude = false;
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
        if (charactersData[characterIndex].fastItems[currentFastItemIndex].itemBaseSO)
        {
            RefreshCharacterItemModel(charactersData[characterIndex].fastItems[currentFastItemIndex], true, CharactersModelDBSO.TypeModel.FastItems);
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
    public virtual void UseItem() { }
    public virtual void UseItem(int bagSlotIndex) { }
    public virtual async Awaitable UseSkill(int skillIndex) { }
    protected bool SetGrounded()
    {        
        return Physics.OverlapBox(transform.position, new Vector3(0.5f, 0.1f, 0.5f) / 2, Quaternion.identity, LayerMask.GetMask("Map")).Length > 0;
    }
    public virtual void TakeExp(CharacterData.Statistic statistic)
    {
        int amount = Mathf.CeilToInt(statistic.maxValue * 0.1f);
        charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].currentValue += amount;
        int level = 0;
        while (charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].currentValue >= charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].maxValue)
        {
            int spare = charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].currentValue > charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].maxValue ?
                Mathf.CeilToInt(charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].currentValue - charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].maxValue) : 0;
            charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].baseValue = Mathf.CeilToInt(charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].maxValue * 2.2f);
            charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].maxValue = charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].baseValue;
            charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Exp].currentValue = spare;
            charactersData[characterIndex].LevelUp();
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
        FloatingText floatingText = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity).GetComponent<FloatingText>();
        _ = floatingText.SendText(damage.ToString(), Color.red, false);
        if (charactersData[characterIndex].statistics.TryGetValue(CharacterData.TypeStatistic.Hp, out CharacterData.Statistic characterTakedDamageStatistic))
        {
            characterTakedDamageStatistic.currentValue -= damage;
        }
        characterAnimations.MakeEffect(AnimationEffectsSO.TypeAnimationsEffects.Shake);
        characterAnimations.MakeEffect(AnimationEffectsSO.TypeAnimationsEffects.Blink);
        if (charactersData[characterIndex].statistics[CharacterData.TypeStatistic.Hp].currentValue <= 0) await Die(characterMakeDamage);
        await Awaitable.NextFrameAsync();
    }
    public virtual async Awaitable Die(CharacterBase characterMakeDamage)
    {
        await Awaitable.WaitForSecondsAsync(0.3f);
        GameObject dieEffect = Instantiate(dieEffectPrefab, transform.position, Quaternion.identity);
        await Awaitable.WaitForSecondsAsync(1);
        Destroy(dieEffect);
        // _ = GameManager.Instance.LoadScene(GameManager.TypeScene.HomeScene);
        await Awaitable.NextFrameAsync();
    }
    public IEnumerator HandleStatusEffect()
    {
        while (statusEffects.Count > 0)
        {
            foreach (KeyValuePair<int, SerializedDictionary<StatusEffectBaseSO, StatusEffect>> statusEffect in statusEffects)
            {
                foreach (KeyValuePair<StatusEffectBaseSO, StatusEffect> status in statusEffect.Value)
                {
                    status.Value.cd -= Time.deltaTime;
                    if (status.Value.cd <= 0)
                    {
                        status.Value.amount--;
                        if (status.Value.amount > 0)
                        {
                            status.Value.cd = status.Value.statusEffectBaseSO.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
                            status.Value.statusEffectBaseSO.ReApplyEffect(this);
                            if (characterPlayerHud.characterUI.statusEffectUI.statusEffectsBanners.ContainsKey(status.Key))
                                characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status.Key].SetBannerData(status.Value);
                        }
                        else
                        {
                            status.Value.statusEffectBaseSO.RemoveEffect(this);
                            if (characterPlayerHud.characterUI.statusEffectUI.statusEffectsBanners.ContainsKey(status.Key))
                            {
                                AddStatusEffectToRemove(statusEffect.Key, status.Key);
                                if (statusEffect.Value.Count - statusToRemove[statusEffect.Key].Count <= 0)
                                {
                                    statusEffectsCharacterKeysToRemove.Add(statusEffect.Key);
                                }
                            }
                            break;
                        }
                    }
                    else
                    {
                        if (statusEffects.ContainsKey(characterIndex))
                            characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status.Key].RefreshCD(status.Value);
                    }
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
    public void AddStatusEffectToRemove(int character, StatusEffectBaseSO status)
    {
        if (statusToRemove.ContainsKey(character))
        {
            statusToRemove[character].Add(status);
        }
        else
        {
            statusToRemove.Add(character, new List<StatusEffectBaseSO> { status });
        }
    }
    public void RemoveStatus()
    {
        foreach (KeyValuePair<int, List<StatusEffectBaseSO>> character in statusToRemove)
        {
            foreach (StatusEffectBaseSO status in character.Value)
            {
                statusEffects[character.Key].Remove(status);
                Destroy(characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status].gameObject);
                characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners.Remove(status);
            }
        }
        foreach (int character in statusEffectsCharacterKeysToRemove)
        {
            statusEffects.Remove(character);
        }
        statusToRemove.Clear();
        statusEffectsCharacterKeysToRemove.Clear();
    }
    public IEnumerator HandleUseSkill()
    {
        while (skillsCd.Count > 0)
        {
            foreach (KeyValuePair<int, SerializedDictionary<int, SkillCd>> character in skillsCd)
            {
                foreach (KeyValuePair<int, SkillCd> skill in character.Value)
                {
                    skill.Value.currentCd -= Time.deltaTime;
                    if (character.Key == characterIndex)
                    {
                        characterPlayerHud?.characterUI.skills[skill.Key].RefreshCD(skill.Value);
                    }
                    if (skill.Value.currentCd <= 0)
                    {
                        AddSkillToRemove(character.Key, skill.Key);
                        if (character.Value.Count - 1 <= 0)
                        {
                            skillsCharacterKeysToRemove.Add(character.Key);
                        }
                    }
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
    public void AddSkillToRemove(int character, int skillId)
    {
        if (skillsToRemove.ContainsKey(character))
        {
            skillsToRemove[character].Add(skillId);
        }
        else
        {
            skillsToRemove.Add(character, new List<int> { skillId });
        }
    }
    public void RemoveSkillCd()
    {
        foreach (KeyValuePair<int, List<int>> character in skillsToRemove)
        {
            foreach (int skill in character.Value)
            {
                skillsCd[character.Key].Remove(skill);
            }
        }
        foreach (int character in skillsCharacterKeysToRemove)
        {
            skillsCd.Remove(character);
        }
        skillsToRemove.Clear();
        skillsCharacterKeysToRemove.Clear();
    }
    public void AddStatusEffect(StatusEffectBaseSO statusEffect)
    {
        if (statusEffects.ContainsKey(characterIndex))
        {
            if (statusEffects[characterIndex].ContainsKey(statusEffect))
            {
                statusEffects[characterIndex][statusEffect].AppendStatusEffect();
            }
            else
            {
                statusEffects[characterIndex].Add(statusEffect, new StatusEffect(statusEffect));
            }
        }
        else
        {
            statusEffects.Add(characterIndex, new SerializedDictionary<StatusEffectBaseSO, StatusEffect>
            {
                {statusEffect, new StatusEffect(statusEffect)}
            });
        }
        characterPlayerHud?.AddStatusEffect(statusEffects[characterIndex][statusEffect]);

        if (handleStatusEffectCoroutine == null)
        {
            handleStatusEffectCoroutine = StartCoroutine(HandleStatusEffect());
        }
    }
    public void AddStatusEffect(int characterIndex, StatusEffectBaseSO statusEffect)
    {
        if (statusEffects.ContainsKey(characterIndex))
        {
            if (statusEffects[characterIndex].ContainsKey(statusEffect))
            {
                statusEffects[characterIndex][statusEffect].AppendStatusEffect();
            }
            else
            {
                statusEffects[characterIndex].Add(statusEffect, new StatusEffect(statusEffect));
            }
        }
        else
        {
            statusEffects.Add(characterIndex, new SerializedDictionary<StatusEffectBaseSO, StatusEffect>
            {
                {statusEffect, new StatusEffect(statusEffect)}
            });
        }
        characterPlayerHud?.AddStatusEffect(statusEffects[characterIndex][statusEffect]);

        if (handleStatusEffectCoroutine == null)
        {
            handleStatusEffectCoroutine = StartCoroutine(HandleStatusEffect());
        }
    }
    [Serializable]
    public class CharacterModel
    {
        public SerializedDictionary<CharactersModelDBSO.TypeModel, List<CharacterModelData>> meshesData = new SerializedDictionary<CharactersModelDBSO.TypeModel, List<CharacterModelData>>();
        public Transform modelTransform;
        public Transform leftHand;
        public Transform rightHand;
    }
    IEnumerator ResetCancelCanalization()
    {
        yield return null;
        cancelCanalization = false;
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
    public class SkillCd
    {
        public float maxCd;
        public float currentCd;
    }
}