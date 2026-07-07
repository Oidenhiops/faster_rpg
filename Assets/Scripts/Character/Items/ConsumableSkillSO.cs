using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "ConsumableSkillItem", menuName = "ScriptableObjects/Items/ConsumableSkillItem", order = 1)]
public class ConsumableSkillSO : ItemBaseSO
{
    public SkillsBaseSO skillsBaseSO;
        public override void UseItem(CharacterBase character, CharacterData.CharacterItem characterItem)
    {
        if (skillsBaseSO)
        {
            _ = skillsBaseSO.UseSkill(character, characterItem);
        }
    }
}
