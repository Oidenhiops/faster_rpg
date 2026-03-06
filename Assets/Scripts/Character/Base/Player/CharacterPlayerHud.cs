using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterPlayerHud : MonoBehaviour
{
    public CharacterPlayer characterPlayer;
    public CharacterUI characterUI;
    public async Awaitable ChangeCharacterPortrait()
    {
        try
        {
            foreach (var portrait in characterUI.characterPortraits)
            {
                portrait.characterCounterText.text = "1";
                portrait.characterCounter.fillAmount = 1f;
            }
            float elapsedTime = 0f;
            float duration = 1f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float fillAmount = Mathf.Lerp(1, 0, elapsedTime / duration);
                foreach (var character in characterUI.characterPortraits)
                {
                    character.characterCounter.fillAmount = fillAmount;
                    character.characterCounterText.text = fillAmount.ToString("F1");
                }
                await Awaitable.NextFrameAsync();
            }
            foreach (var character in characterUI.characterPortraits)
            {
                character.characterCounterText.text = "";
            }
            await Awaitable.NextFrameAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error changing character portrait: {ex.Message}");
        }
    }
    [Serializable]
    public class CharacterUI
    {
        public CharacterPortrait[] characterPortraits;
    }
    [Serializable]
    public class CharacterPortrait
    {
        public GameObject portraitObject;
        public Image characterSprite;
        public Image characterCounter;
        public TMP_Text characterCounterText;
    }
}
