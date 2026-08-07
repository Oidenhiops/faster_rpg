using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "ConsumableItem", menuName = "ScriptableObjects/Items/ConsumableItem", order = 1)]
public class ConsumableItemSO : ItemBaseSO
{
    public GameObject useEffectPrefab;
    public override async Awaitable UseItem(UseItemInfo useItemInfo)
    {
        useItemInfo.character.characterAnimator.SetFloat(useItemInfo.characterItem.itemBaseSO.GetHandLayer(useItemInfo.isFastItem), useItemInfo.characterItem.itemBaseSO.animationValue);
        useItemInfo.character.isUsingFastItem = true;
        float elapsedTime = 0f;
        bool cancelAction = false;
        useItemInfo.character.AddStatusEffect(canalizationEffect);
        while (elapsedTime < canalizationEffect.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue)
        {
            elapsedTime += Time.deltaTime;
            if (useItemInfo.character.cancelUseFastItem)
            {
                useItemInfo.character.statusToRemove.Add(canalizationEffect);
                cancelAction = true;
                break;
            }
            await Awaitable.NextFrameAsync();
        }
        useItemInfo.character.isUsingFastItem = false;
        useItemInfo.character.characterAnimator.SetFloat(useItemInfo.characterItem.itemBaseSO.GetHandLayer(useItemInfo.isFastItem), 0);
        if (!cancelAction)
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
            useItemInfo.character.characterPlayerHud?.RefreshFastItems();
        }
    }
}
