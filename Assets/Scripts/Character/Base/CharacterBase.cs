using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class CharacterBase : MonoBehaviour
{
    public bool isInitialize;
    public bool isCharacterPlayer;
    public bool autoInit;
    public CharacterModel characterModel;
    public CharactersData[] charactersData;
    public int characterIndex;
    public int currentFastItemIndex;
    public Vector3Int directionAnimation = new Vector3Int();
    [NonSerialized] public Vector3 characterScale;
    public GameObject floatingTextPrefab;
    public GameObject dieEffectPrefab;
    public GameObject itempDroppedPrefab;
    public CharacterPlayerHud characterPlayerHud;
    public CharacterMovementBase characterMovement;
    public CharacterAnimator characterAnimations;
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
    public void OnEnable()
    {
        if (isInitialize) characterAnimations.MakeAnimation("Idle");
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
        }
    }
    public virtual void OnEnableHandle() { }
    public async virtual Awaitable InitializeCharacter() { }
    protected async Awaitable InitializeAnimations()
    {
        try
        {
            characterAnimations.SetInitialData();
            await Awaitable.NextFrameAsync();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    public void MoveCharacter()
    {
        characterMovement.HandleMovement();
    }
    public virtual void OnHandlePickUpItem(ItemDropped itemDropped) { }
    public virtual void UseItem() { }
    public virtual void UseItem(int bagSlotIndex) { }
    public virtual void UseSkill(int skillIndex) { }
    protected bool SetGrounded()
    {
        return Physics.OverlapBox
        (
            transform.position,
            new Vector3(0.5f, 0.1f, 0.5f) / 2,
            Quaternion.identity,
            LayerMask.GetMask("Map")).Length > 0;
    }
    public virtual void TakeExp(CharacterData.Statistic statistic)
    {
        int amount = Mathf.CeilToInt(statistic.maxValue * 0.1f);
        charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue += amount;
        int level = 0;
        while (charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue >= charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue)
        {
            int spare = charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue > charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue ?
                Mathf.CeilToInt(charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue - charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue) : 0;
            charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].baseValue = Mathf.CeilToInt(charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue * 2.2f);
            charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].maxValue = charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].baseValue;
            charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Exp].currentValue = spare;
            charactersData[characterIndex].characterData.LevelUp();
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
    public void LookAt(Vector3Int startPos, Vector3Int finalPos)
    {
        if (startPos.x == finalPos.x)
        {
            directionAnimation.x = startPos.z < finalPos.z ? 1 : -1;
        }
        else
        {
            directionAnimation.x = startPos.x < finalPos.x ? -1 : 1;
        }
        if (startPos.z == finalPos.z)
        {
            directionAnimation.z = startPos.x < finalPos.x ? 1 : -1;
        }
        else
        {
            directionAnimation.z = startPos.z < finalPos.z ? 1 : -1;
        }
    }
    public async Awaitable TakeDamage(CharacterBase characterMakeDamage, int damage, string otherAnimation = "")
    {
        characterAnimations.MakeAnimation("TakeDamage");
        characterAnimations.animationAfterEnd = otherAnimation;
        FloatingText floatingText = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity).GetComponent<FloatingText>();
        _ = floatingText.SendText(damage.ToString(), Color.red, false);
        if (charactersData[characterIndex].characterData.statistics.TryGetValue(CharacterData.TypeStatistic.Hp, out CharacterData.Statistic characterTakedDamageStatistic))
        {
            characterTakedDamageStatistic.currentValue -= damage;
        }
        characterAnimations.MakeEffect(CharacterAnimator.TypeAnimationsEffects.Shake);
        characterAnimations.MakeEffect(CharacterAnimator.TypeAnimationsEffects.Blink);
        if (charactersData[characterIndex].characterData.statistics[CharacterData.TypeStatistic.Hp].currentValue <= 0) await Die(characterMakeDamage, otherAnimation);
        await Awaitable.NextFrameAsync();
    }
    public virtual async Awaitable Die(CharacterBase characterMakeDamage, string lastAnimation = "")
    {
        await Awaitable.WaitForSecondsAsync(0.3f);
        GameObject dieEffect = Instantiate(dieEffectPrefab, transform.position, Quaternion.identity);
        characterModel.characterMeshRenderer.gameObject.SetActive(false);
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
                                Destroy(characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status.Key].gameObject);
                                characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners.Remove(status.Key);
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
    public class CharactersData
    {
        public CharacterAnimationsSO characterAnimationsSO;
        public CharacterSkinData characterSkin;
        public CharacterData characterData;
    }
    [Serializable]
    public class CharacterModel
    {
        public MeshRenderer characterMeshRenderer;
        public MeshRenderer characterMeshRendererHand;
        public Transform leftHand;
        public Transform rightHand;
        public Mesh originalMesh;
    }
    [Serializable]
    public class CharacterSkinData
    {
        public Texture2D atlas;
        public Texture2D atlasHands;
        public Sprite icon;
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