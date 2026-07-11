using Unity.VisualScripting;
using UnityEngine;
[CreateAssetMenu(fileName = "GeneralBuffSkillUtility", menuName = "ScriptableObjects/Skills/GeneralBuffSkillUtility", order = 1)]
public class GeneralBuffSkilUtilitySO : SkillsBaseSO
{
    public StatusEffectBaseSO statusEffectBaseSO;
    public override async Awaitable UseSkill(CharacterBase character, CharacterData.CharacterItem characterItem)
    {
        float elapsedTime = 0f;
        bool cancelSkill = false;
        if (characterItem.itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability))
        {
            characterItem.itemStatistics[CharacterData.TypeStatistic.Durability].currentValue -= 1;
        }
        if (canalizationEffect != null)
        {
            character.characterAnimator.Play(canalizationEffect.canalizationAnimationName, -1, 0f);
            character.isInCanalization = true;
            character.AddStatusEffect(canalizationEffect);
            while (elapsedTime < canalizationEffect.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue)
            {
                elapsedTime += Time.deltaTime;
                if (character.cancelCanalization)
                {
                    character.AddStatusEffectToRemove(character.characterIndex, canalizationEffect);
                    cancelSkill = true;
                    break;
                }
                await Awaitable.NextFrameAsync();
            }
            character.isInCanalization = false;
        }
        character.characterPlayerHud.RefreshConsumables();
        if (!cancelSkill)
        {
            statusEffectBaseSO.ApplyEffect(character);
            GameObject effectPrefab = Instantiate(skillVFXPrefab, character.transform.position, Quaternion.identity, character.transform);
            Destroy(effectPrefab, skillVFXDuration);
        }
    }
}
