using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "ConsumableItem", menuName = "ScriptableObjects/Items/ConsumableItem", order = 1)]
public class ConsumableItemSO : ItemBaseSO
{
    public GameObject useEffectPrefab;
    public override void UseItem(UseItemInfo useItemInfo)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in itemStatistics)
        {
            if (useItemInfo.character.characterData.statistics.ContainsKey(statistic.Key))
            {
                useItemInfo.character.characterData.statistics[statistic.Key].currentValue += statistic.Value.baseValue;
                useItemInfo.character.characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        GameObject effect = Instantiate(useEffectPrefab, useItemInfo.character.transform.position + Vector3.up * 0.5f, Quaternion.identity);
        effect.transform.SetParent(useItemInfo.character.transform);
        Destroy(effect, 2f);
        useItemInfo.characterItem.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue--;
        if (useItemInfo.characterItem.itemStatistics[CharacterData.TypeStatistic.Amount].currentValue <= 0)
        {
            useItemInfo.characterItem.ResetItem();
            useItemInfo.character.UpdateFastItemModel();
        }
        _ = useItemInfo.character.characterPlayerHud?.RefreshCharacterInventory();
    }
}
