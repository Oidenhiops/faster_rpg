using System;
using System.Collections.Generic;
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
            foreach(KeyValuePair<int, SerializedDictionary<StatusEffectBaseSO, StatusEffect>> statusEffect in statusEffects)
            {
                foreach(KeyValuePair<StatusEffectBaseSO, StatusEffect> status in statusEffect.Value)
                {
                    status.Value.cd -= Time.deltaTime;
                    if (status.Value.cd <= 0)
                    {
                        status.Value.amount--;
                        if (status.Value.amount > 0)
                        {
                            status.Value.cd = status.Value.statusEffectBaseSO.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue;
                            status.Value.statusEffectBaseSO.ReApplyEffect(characterBase);
                            if (characterBase.characterPlayerHud.characterUI.statusEffectUI.statusEffectsBanners.ContainsKey(status.Key)) 
                                    characterBase.characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status.Key].SetBannerData(status.Value);
                        }
                        else
                        {
                            status.Value.statusEffectBaseSO.RemoveEffect(characterBase);
                            if (characterBase.characterPlayerHud.characterUI.statusEffectUI.statusEffectsBanners.ContainsKey(status.Key))
                            {
                                Destroy(characterBase.characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status.Key].gameObject);
                                characterBase.characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners.Remove(status.Key);
                                statusEffect.Value.Remove(status.Key);
                                if (statusEffect.Value.Count <= 0)
                                {
                                    statusEffects.Remove(statusEffect.Key);
                                }
                            }
                            break;
                        }
                    }
                    else
                    {
                        if (statusEffects.ContainsKey(characterBase.characterIndex)) 
                                characterBase.characterPlayerHud?.characterUI.statusEffectUI.statusEffectsBanners[status.Key].RefreshCD(status.Value);
                    }
                }
            }
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
        characterBase.characterPlayerHud?.AddStatusEffect(statusEffects[characterBase.characterIndex][statusEffect]);
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
        characterBase.characterPlayerHud?.AddStatusEffect(statusEffects[characterBase.characterIndex][statusEffect]);
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
