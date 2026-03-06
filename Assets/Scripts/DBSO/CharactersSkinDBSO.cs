using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "CharactersSkinDB", menuName = "ScriptableObjects/DB/CharactersSkinDB", order = 1)]
public class CharactersSkinDBSO : ScriptableObject
{
    public SerializedDictionary<int, SerializedDictionary<int, CharacterBase.CharacterSkinData>> data = new SerializedDictionary<int, SerializedDictionary<int, CharacterBase.CharacterSkinData>>();
}
