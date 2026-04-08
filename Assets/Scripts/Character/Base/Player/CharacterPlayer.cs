using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayer : CharacterBase
{
    public InputSystem_Actions inputActions;
    public CharacterPlayerHud characterPlayerHud;
    public bool isChangingCharacter;
    public override void OnEnableHandle()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        inputActions.Player.ChangeCharacter.performed += OnHandleChangeCharacter;
        inputActions.Player.ToggleInventory.performed += OnHandleToggleInventory;
    }
    public async override Awaitable InitializeCharacter()
    {
        try
        {
            List<CharactersData> charactersDataList = new List<CharactersData>();
            foreach (var characterData in GameData.Instance.gameDataInfo.gameDataSlots[GameData.Instance.systemDataInfo.currentGameDataIndex].selectedCharacters)
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
            characterIndex = (int)context.ReadValue<float>();
            _ = InitializeAnimations();
            _ = ChangeCharacterAction();
            _ = characterPlayerHud.RefreshCharacterInventory();
        }
    }
    void OnHandleToggleInventory(InputAction.CallbackContext context)
    {
        _ = characterPlayerHud.ToggleCharacterInventory();
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
}
