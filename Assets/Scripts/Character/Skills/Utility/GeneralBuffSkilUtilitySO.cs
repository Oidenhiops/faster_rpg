using Unity.VisualScripting;
using UnityEngine;
[CreateAssetMenu(fileName = "GeneralBuffSkillUtility", menuName = "ScriptableObjects/Skills/GeneralBuffSkillUtility", order = 1)]
public class GeneralBuffSkilUtilitySO : SkillsBaseSO
{
    public StatusEffectBaseSO statusEffectBaseSO;
    public override async Awaitable UseSkill(CharacterMakeSkillData characterMakeSkillData, CharacterBase characterToMakeSkill, int skillIndex)
    {
        float elapsedTime = 0f;
        bool cancelSkill = false;
        if (canalizationEffect != null)
        {
            characterMakeSkillData.characterMakeSkill.characterAnimator.Play(canalizationEffect.canalizationAnimationName, -1, 0f);
            characterMakeSkillData.characterMakeSkill.isInCanalization = true;
            characterMakeSkillData.characterMakeSkill.AddStatusEffect(canalizationEffect);
            while (elapsedTime < canalizationEffect.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue)
            {
                elapsedTime += Time.deltaTime;
                if (characterMakeSkillData.characterMakeSkill.cancelCanalization)
                {
                    characterMakeSkillData.characterMakeSkill.statusEffects[characterMakeSkillData.characterMakeSkillIndex][canalizationEffect].cd = 0;
                    characterMakeSkillData.characterMakeSkill.AddStatusEffectToRemove(characterMakeSkillData.characterMakeSkillIndex, canalizationEffect);
                    cancelSkill = true;
                    break;
                }
                await Awaitable.NextFrameAsync();
            }
            characterMakeSkillData.characterMakeSkill.isInCanalization = false;
        }
        if (!cancelSkill)
        {
            statusEffectBaseSO.ApplyEffect(characterMakeSkillData.characterMakeSkill);
            GameObject effectPrefab = Instantiate(skillVFXPrefab, characterMakeSkillData.characterMakeSkill.transform.position, Quaternion.identity, characterMakeSkillData.characterMakeSkill.transform);
            Destroy(effectPrefab, skillVFXDuration);
        }
    }
}
