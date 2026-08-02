using Unity.VisualScripting;
using UnityEngine;
[CreateAssetMenu(fileName = "GeneralBuffSkillUtility", menuName = "ScriptableObjects/Skills/GeneralBuffSkillUtility", order = 1)]
public class GeneralBuffSkilUtilitySO : SkillsBaseSO
{
    public StatusEffectBaseSO statusEffectBaseSO;
    public override async Awaitable UseSkill(CharacterBase character, CharacterData.CharacterItem characterItem)
    {
        character.characterAnimator.SetFloat(characterItem.itemBaseSO.animationValueName.ToString(), characterItem.itemBaseSO.animationValue);
        float elapsedTime = 0f;
        bool cancelAction = false;
        character.isInCanalization = true;
        character.AddStatusEffect(canalizationEffect);
        while (elapsedTime < canalizationEffect.statusEffectStatistics[CharacterData.TypeStatistic.Cd].baseValue)
        {
            elapsedTime += Time.deltaTime;
            if (character.cancelCanalization)
            {
                character.statusToRemove.Add(canalizationEffect);
                cancelAction = true;
                break;
            }
            await Awaitable.NextFrameAsync();
        }
        character.isInCanalization = false;
        character.characterAnimator.SetFloat(characterItem.itemBaseSO.animationValueName.ToString(), 0);
        if (!cancelAction)
        {
            if (characterItem.itemStatistics.ContainsKey(CharacterData.TypeStatistic.Durability))
            {
                characterItem.itemStatistics[CharacterData.TypeStatistic.Durability].currentValue--;
            }
            character.characterPlayerHud.RefreshFastItems();
            character.AddStatusEffect(statusEffectBaseSO);
            GameObject effectPrefab = Instantiate(skillVFXPrefab, character.transform.position, Quaternion.identity, character.transform);
            Destroy(effectPrefab, skillVFXDuration);
        }
    }
}
