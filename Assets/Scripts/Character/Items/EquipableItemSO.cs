using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipableItem", menuName = "ScriptableObjects/Items/EquipableItem/EquipableItem", order = 1)]
public class EquipableItemSO : ItemBaseSO
{
    public override async Awaitable EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel,  bool isFastItem)
    {
        await EquipStats(character, characterItem, refreshModel, isFastItem, true);
        if (!isFastItem && this is ToolItemSO toolItem && character is CharacterPlayer characterPlayer)
        {
            characterPlayer.currentMiningType = toolItem.miningInfo.toolMode;
        }
    }
    public override async Awaitable DesEquipItem(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel, bool isFastItem)
    {
        await EquipStats(character, characterItem, refreshModel, isFastItem, false);
        if (!isFastItem && this is ToolItemSO && character is CharacterPlayer characterPlayer)
        {
            characterPlayer.currentMiningType = ToolItemSO.MiningType.Block;
        }
    }
    async Awaitable EquipStats(CharacterBase character, CharacterData.CharacterItem characterItem, bool refreshModel, bool isFastItem, bool isAppend)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in characterItem.itemStatistics)
        {
            if (CanEquipStatistic(character, statistic.Key))
            {
                if (isAppend) character.characterData.statistics[statistic.Key].itemValue += statistic.Value.baseValue;
                else character.characterData.statistics[statistic.Key].itemValue -= statistic.Value.baseValue;
                character.characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
        EquipModelItem(character, characterItem, refreshModel, isFastItem);
        await character.characterPlayerHud.InitializeBars();
    }
    public bool CanEquipStatistic(CharacterBase character,CharacterData.TypeStatistic statistic)
    {
        return character.characterData.statistics.ContainsKey(statistic) &&
            statistic != CharacterData.TypeStatistic.PicaxePower &&
            statistic != CharacterData.TypeStatistic.ItemRange;
    }
}
