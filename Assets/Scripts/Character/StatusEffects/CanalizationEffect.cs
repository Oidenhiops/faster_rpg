using UnityEngine;
[CreateAssetMenu(fileName = "CanalizationEffect", menuName = "ScriptableObjects/StatusEffect/CanalizationEffect", order = 1)]
public class CanalizationEffect : StatusEffectBaseSO
{
    public string canalizationAnimationName;
    public override void ApplyEffect(CharacterBase characterToMakeEffect) {  }
    public override void ReApplyEffect(CharacterBase characterToMakeEffect) {  }
    public override void RemoveEffect(CharacterBase characterToMakeEffect) {  }
}
