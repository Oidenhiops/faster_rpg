using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterTest : MonoBehaviour
{
    public CharacterBase characterPlayer;
    [NaughtyAttributes.Button]
    public void HpBarPlus()
    {
        characterPlayer.characterData.statistics[CharacterData.TypeStatistic.Hp].currentValue += 10;
        characterPlayer.characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Hp);
    }
    [NaughtyAttributes.Button]
    public void HpBarDiscount()
    {
        characterPlayer.characterData.statistics[CharacterData.TypeStatistic.Hp].currentValue -= 10;
        characterPlayer.characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Hp);
    }
    [NaughtyAttributes.Button]
    public void SpBarPlus()
    {
        characterPlayer.characterData.statistics[CharacterData.TypeStatistic.Sp].currentValue += 10;
        characterPlayer.characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Sp);
    }
    [NaughtyAttributes.Button]
    public void SpBarDiscount()
    {
        characterPlayer.characterData.statistics[CharacterData.TypeStatistic.Sp].currentValue -= 10;
        characterPlayer.characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Sp);
    }
    [NaughtyAttributes.Button]
    public void StrBarPlus()
    {
        characterPlayer.characterData.statistics[CharacterData.TypeStatistic.Str].currentValue += 10;
        characterPlayer.characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Str);
    }
    [NaughtyAttributes.Button]
    public void StrBarDiscount()
    {
        characterPlayer.characterData.statistics[CharacterData.TypeStatistic.Str].currentValue -= 10;
        characterPlayer.characterPlayerHud.ChangeBar(CharacterData.TypeStatistic.Str);
    }
}
