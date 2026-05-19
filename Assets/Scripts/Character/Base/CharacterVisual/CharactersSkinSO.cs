using AYellowpaper.SerializedCollections;
using UnityEngine;
[CreateAssetMenu(fileName = "CharacterSkins", menuName = "ScriptableObjects/Character/CharacterSkinSO", order = 1)]
public class CharactersSkinSO : ScriptableObject
{
    public SerializedDictionary<string, Texture2D> textures = new SerializedDictionary<string, Texture2D>();
}
