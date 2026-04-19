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
    [Serializable]
    public class StatusEffect
    {
        public StatusEffectBaseSO statusEffectBaseSO = new StatusEffectBaseSO();
        public float currentCD;
        public float maxCD;
    }
}
