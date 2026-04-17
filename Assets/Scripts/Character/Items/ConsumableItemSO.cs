using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "ConsumableItem", menuName = "ScriptableObjects/Items/ConsumableItem", order = 1)]
public class ConsumableItemSO : ItemBaseSO
{
    public GameObject useEffectPrefab;
    public override void UseItem(CharacterBase character, CharacterData.CharacterItem characterItem)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in itemStatistics)
        {
            if (character.charactersData[character.characterIndex].characterData.statistics.ContainsKey(statistic.Key))
            {
                character.charactersData[character.characterIndex].characterData.statistics[statistic.Key].currentValue += statistic.Value.baseValue;
                character.charactersData[character.characterIndex].characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        GameObject effect = Instantiate(useEffectPrefab, character.transform.position + Vector3.up * 0.5f, Quaternion.identity);
        effect.transform.SetParent(character.transform);
        Destroy(effect, 2f);
        characterItem.amount--;
        if (characterItem.amount <= 0)
        {
            characterItem.ResetItem();
        }
        _ = character.characterPlayerHud?.RefreshCharacterInventory();
    }
}
