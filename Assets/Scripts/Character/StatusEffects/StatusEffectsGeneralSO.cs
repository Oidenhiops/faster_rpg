using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
[CreateAssetMenu(fileName = "GeneralStatusEffect", menuName = "ScriptableObjects/StatusEffect/GeneralStatusEffect", order = 1)]
public class StatusEffectsGeneralSO : StatusEffectBaseSO
{
    public override void ApplyEffect(CharacterBase characterToMakeEffect)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in statusEffectStatistics)
        {
            if (statistic.Key != CharacterData.TypeStatistic.Cd)
            {
                if (!characterToMakeEffect.charactersData[characterToMakeEffect.characterIndex].characterData.statistics[statistic.Key].buffValue.ContainsKey(this))
                {
                    characterToMakeEffect.charactersData[characterToMakeEffect.characterIndex].characterData.statistics[statistic.Key].buffValue.Add(this, statistic.Value.baseValue);
                    characterToMakeEffect.charactersData[characterToMakeEffect.characterIndex].characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
                }
            }
        }
        characterToMakeEffect.AddStatusEffect(this);
        characterToMakeEffect.characterPlayerHud?.RefreshCharacterStatistics();
    }
    public override void ReApplyEffect(CharacterBase characterToMakeEffect)
    {
        
    }
    public override void RemoveEffect(CharacterBase characterToMakeEffect)
    {
        foreach (KeyValuePair<CharacterData.TypeStatistic, CharacterData.Statistic> statistic in statusEffectStatistics)
        {
            if (statistic.Key != CharacterData.TypeStatistic.Cd)
            {
                if (characterToMakeEffect.charactersData[characterToMakeEffect.characterIndex].characterData.statistics[statistic.Key].buffValue.ContainsKey(this))
                {
                    characterToMakeEffect.charactersData[characterToMakeEffect.characterIndex].characterData.statistics[statistic.Key].buffValue.Remove(this);
                    characterToMakeEffect.charactersData[characterToMakeEffect.characterIndex].characterData.statistics[statistic.Key].RefreshValue((int)statistic.Key);
                }
            }
        }
        characterToMakeEffect.characterPlayerHud?.RefreshCharacterStatistics();
    }
}
