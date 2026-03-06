using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillsDB", menuName = "ScriptableObjects/DB/SkillsDB", order = 1)]
public class SkillsDBSO : ScriptableObject
{
    public SerializedDictionary<string, SkillsBaseSO> data;
}