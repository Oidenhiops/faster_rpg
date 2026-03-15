using System;
using UnityEngine;

public class CharacterBase : MonoBehaviour
{
    public bool isInitialize;
    public bool isCharacterPlayer;
    public bool autoInit;
    public CharacterModel characterModel;
    public CharactersData[] charactersData;
    public int characterIndex;
    public Vector3Int directionAnimation = new Vector3Int();
    [NonSerialized] public Vector3 characterScale;
    public GameObject floatingTextPrefab;
    public GameObject dieEffectPrefab;
    public CharacterMovementBase characterMovement;
    public CharacterAnimator characterAnimations;
    public bool isGrounded => SetGrounded();
    public bool isDashing;
    // public CharacterStatusEffect characterStatusEffect;
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
    async Awaitable InitializeDataWhitInitialValues(InitialDataSO initialDataSO)
    {
        charactersData[characterIndex].characterData.name = GenerateFantasyName();
        charactersData[characterIndex].characterData.level = 1;
        charactersData[characterIndex].characterData.characterSkinId = initialDataSO.skinId;
        gameObject.name = charactersData[characterIndex].characterData.name;
        charactersData[characterIndex].characterData.statistics = initialDataSO.CloneStatistics();
        charactersData[characterIndex].characterData.skills = initialDataSO.CloneSkills();
        foreach (var statistic in charactersData[characterIndex].characterData.statistics)
        {
            statistic.Value.RefreshValue();
            statistic.Value.SetMaxValue();
        }
        charactersData[characterIndex].characterSkin = new CharacterSkinData
        {
            // atlas = initialDataSO.characterVisualSO.atlas,
            // atlasHands = initialDataSO.characterVisualSO.atlasHands
        };
        await Awaitable.NextFrameAsync();
    }
    public void MoveCharacter()
    {
        characterMovement.HandleMovement();
    }
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
}