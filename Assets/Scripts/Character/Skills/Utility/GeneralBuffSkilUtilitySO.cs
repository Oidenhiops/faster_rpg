using UnityEngine;
[CreateAssetMenu(fileName = "GeneralBuffSkillUtility", menuName = "ScriptableObjects/Skills/GeneralBuffSkillUtility", order = 1)]
public class GeneralBuffSkilUtilitySO : SkillsBaseSO
{
    public StatusEffectBaseSO statusEffectBaseSO;
    public override bool UseSkill(CharacterBase characterMakeSkill, CharacterBase characterToMakeSkill, int skillIndex)
    {
        statusEffectBaseSO.ApplyEffect(characterMakeSkill);
        return true;
    }
}
