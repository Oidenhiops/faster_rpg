using AYellowpaper.SerializedCollections;
using UnityEngine;

public class StatusEffectBaseSO : ScriptableObject
{
    public Sprite icon;
    public int maxStack;
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> statusEffectStatistics;
    public virtual void ApplyEffect(CharacterBase characterToMakeEffect) { Debug.LogError("ApplyEffect no implemented"); }
    public virtual void ReApplyEffect(CharacterBase characterToMakeEffect) { Debug.LogError("ReApplyEffect no implemented"); }
    public virtual void DiscountEffect(CharacterBase characterToMakeEffect) { Debug.LogError("DiscountEffect no implemented"); }
    public virtual void RemoveEffect(CharacterBase characterToMakeEffect) { Debug.LogError("RemoveEffect no implemented"); }
}
