using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "CanalizationEffect", menuName = "ScriptableObjects/StatusEffect/CanalizationEffect", order = 1)]
public class CanalizationEffectSO : StatusEffectBaseSO
{
    public override void ApplyEffect(CharacterBase characterToMakeEffect)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in statusEffectStatistics)
        {
            if (characterToMakeEffect.characterData.statistics.ContainsKey(statistic.Key))
            {
                if (statistic.Value.isPercentage)
                {
                    characterToMakeEffect.characterData.statistics[statistic.Key].buffValuePorcent.Add(this, statistic.Value.baseValue);
                }
                else
                {
                    characterToMakeEffect.characterData.statistics[statistic.Key].buffValue += statistic.Value.baseValue;
                }
                characterToMakeEffect.characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
    }
    public override void ReApplyEffect(CharacterBase characterToMakeEffect) {  }
    public override void RemoveEffect(CharacterBase characterToMakeEffect)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in statusEffectStatistics)
        {
            if (characterToMakeEffect.characterData.statistics.ContainsKey(statistic.Key))
            {
                if (statistic.Value.isPercentage)
                {
                    characterToMakeEffect.characterData.statistics[statistic.Key].buffValuePorcent.Remove(this);
                }
                else
                {
                    characterToMakeEffect.characterData.statistics[statistic.Key].buffValue -= statistic.Value.baseValue;
                }
                characterToMakeEffect.characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
            }
        }
    }
}
