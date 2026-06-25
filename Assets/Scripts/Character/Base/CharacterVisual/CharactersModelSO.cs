using AYellowpaper.SerializedCollections;
using UnityEngine;
[CreateAssetMenu(fileName = "CharacterModels", menuName = "ScriptableObjects/Character/CharacterModelSO", order = 1)]
public class CharactersModelSO : ScriptableObject
{
    public SerializedDictionary<string, Texture2D> textures = new SerializedDictionary<string, Texture2D>();
}
