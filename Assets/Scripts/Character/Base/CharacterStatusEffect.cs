using System;
using AYellowpaper.SerializedCollections;
using UnityEngine;

public class CharacterStatusEffect : MonoBehaviour
{
    public CharacterBase characterBase;
    public SerializedDictionary<int, SerializedDictionary<StatusEffectBaseSO, StatusEffect>> statusEffects = new SerializedDictionary<int, SerializedDictionary<StatusEffectBaseSO, StatusEffect>>();
    void FixedUpdate()
    {
        if (statusEffects.Count > 0)
        {
            
        }
    }
    public void AddStatusEffect(StatusEffectBaseSO statusEffect)
    {
        if (statusEffects.ContainsKey(characterBase.characterIndex))
        {
            if (statusEffects[characterBase.characterIndex].ContainsKey(statusEffect))
            {
                statusEffects[characterBase.characterIndex][statusEffect].AppendStatusEffect();
            }
            else
            {
                statusEffects[characterBase.characterIndex].Add(statusEffect, new StatusEffect(statusEffect));
            }
        }
        else
        {
            statusEffects.Add(characterBase.characterIndex, new SerializedDictionary<StatusEffectBaseSO, StatusEffect>
            {
                {statusEffect, new StatusEffect(statusEffect)}
            });
        }
        characterBase.characterPlayerHud?.RefreshCharacterStatistics();
    }
    public void AddStatusEffect(int characterIndex, StatusEffectBaseSO statusEffect)
    {
        if (statusEffects.ContainsKey(characterIndex))
        {
            if (statusEffects[characterIndex].ContainsKey(statusEffect))
            {
                statusEffects[characterIndex][statusEffect].AppendStatusEffect();
            }
            else
            {
                statusEffects[characterIndex].Add(statusEffect, new StatusEffect(statusEffect));
            }
        }
        else
        {
            statusEffects.Add(characterIndex, new SerializedDictionary<StatusEffectBaseSO, StatusEffect>
            {
                {statusEffect, new StatusEffect(statusEffect)}
            });
        }
    }
    [Serializable]
    public class StatusEffect
    {
        public StatusEffectBaseSO statusEffectBaseSO = new StatusEffectBaseSO();
        public float cd;
        public int amount;
        public StatusEffect(StatusEffectBaseSO statusEffect)
        {
            statusEffectBaseSO = statusEffect;
            cd = statusEffect.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
            amount = 1;
        }
        public void AppendStatusEffect()
        {
            bool canAdd = amount < statusEffectBaseSO.maxStack;
            amount = canAdd ? amount + 1 : statusEffectBaseSO.maxStack;
            if (!canAdd)
            {
                cd = statusEffectBaseSO.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
            }
        }
    }
}
