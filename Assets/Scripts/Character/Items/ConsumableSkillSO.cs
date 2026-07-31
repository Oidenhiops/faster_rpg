using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "ConsumableSkillItem", menuName = "ScriptableObjects/Items/ConsumableSkillItem", order = 1)]
public class ConsumableSkillSO : ItemBaseSO
{
    public SkillsBaseSO skillsBaseSO;
    public override async Awaitable UseItem(UseItemInfo useItemInfo)
    {
        if (skillsBaseSO)
        {
            _ = skillsBaseSO.UseSkill(useItemInfo.character, useItemInfo.characterItem);
        }
    }
}
