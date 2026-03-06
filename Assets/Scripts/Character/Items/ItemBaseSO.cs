using AYellowpaper.SerializedCollections;
using UnityEngine;

public class ItemBaseSO : ScriptableObject
{
    public int id;
    public string idText;
    public Sprite icon;
    public TypeObject typeObject;
    public TypeWeapon typeWeapon;
    public Vector3Int[] positionsToAttack;
    public int attackRadius = 5;
    public string animationName;
    public SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic> itemStatistics = new SerializedDictionary<CharacterData.TypeStatistic, CharacterData.Statistic>();
    public virtual void EquipItem(CharacterBase character, CharacterData.CharacterItem characterItem) { Debug.LogError("EquipItem not implemented"); }
    public virtual void DesEquipItem(CharacterBase character, CharacterData.CharacterItem characterItem) { Debug.LogError("DesEquipItem not implemented"); }
    public enum TypeObject
    {
        None = 0,
        Weapon = 1,
        Item = 2,
        Consumable = 3
    }
    public enum TypeWeapon
    {
        None = 0,
        Fist = 1,
        Sword = 2,
        Spear = 3,
        Bow = 4,
        Axe = 5,
        Staff = 6,
        Monster = 7
    }
}
