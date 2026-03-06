using AYellowpaper.SerializedCollections;
using UnityEngine;

public class StatusEffectBaseSO : ScriptableObject
{
    public Sprite icon;
    public int maxStats;
    public bool isPermanent;
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> statusEffectStatistics;
    public virtual void ApplyEffect() { Debug.LogError("ApplyEffect no implemented"); }
    public virtual void ReApplyEffect() { Debug.LogError("ReApplyEffect no implemented"); }
    public virtual void DiscountEffect() { Debug.LogError("DiscountEffect no implemented"); }
    public virtual void ReloadEffect() { Debug.LogError("ReloadEffect no implemented"); }
    public virtual void RemoveEffect() { Debug.LogError("RemoveEffect no implemented"); }
}
