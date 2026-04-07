using UnityEngine;

[CreateAssetMenu(fileName = "EquipableItem", menuName = "ScriptableObjects/Items/EquipableItem", order = 1)]
public class EquipableItemSO : ItemBaseSO
{
    public override void EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem)
    {
        
    }

    public void UseEquipableItem(CharacterBase character, CharacterData.CharacterItem characterItem)
    {
        throw new System.NotImplementedException();
    }
}
