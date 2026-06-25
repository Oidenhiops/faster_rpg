using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "CharactersModelDB", menuName = "ScriptableObjects/DB/CharactersModelDB", order = 1)]
public class CharactersModelDBSO : ScriptableObject
{
    public SerializedDictionary<TypeModel, SerializedDictionary<int, CharactersModelSO>> data = new SerializedDictionary<TypeModel, SerializedDictionary<int, CharactersModelSO>>();
    public SerializedDictionary<TypeModel, CharacterData.CharacterSkinInfo> GenerateRandomModel()
    {
        SerializedDictionary<TypeModel, CharacterData.CharacterSkinInfo> model = new SerializedDictionary<TypeModel, CharacterData.CharacterSkinInfo>();
        int hairIndex = Random.Range(1, data[TypeModel.Hair].Count + 1);
        Color hairColor = RandomColor();
        model.Add(TypeModel.Hair, new CharacterData.CharacterSkinInfo
        {
            originalSkinId = hairIndex,
            originalSkin = data[TypeModel.Hair][hairIndex],
            originalColor = new List<Color> { hairColor },
            otherSkin = null,
            otherSkinColor = new List<Color>()
        });
        int headIndex = Random.Range(1, data[TypeModel.Head].Count + 1);
        Color skinColor = RandomColor();
        model.Add(TypeModel.Head, new CharacterData.CharacterSkinInfo
        {
            originalSkinId = headIndex,
            originalSkin = data[TypeModel.Head][headIndex],
            originalColor = new List<Color> { skinColor },
            otherSkin = null,
            otherSkinColor = new List<Color>()
        });
        int eyesIndex = Random.Range(1, data[TypeModel.Eyes].Count - 1);
        model.Add(TypeModel.Eyes, new CharacterData.CharacterSkinInfo
        {
            originalSkinId = eyesIndex,
            originalSkin = data[TypeModel.Eyes][eyesIndex],
            originalColor = new List<Color> { RandomColor(), RandomColor(), RandomColor() },
            otherSkin = null,
            otherSkinColor = new List<Color>()
        });
        int eyebrowsIndex = Random.Range(1, data[TypeModel.Eyebrows].Count - 1);
        model.Add(TypeModel.Eyebrows, new CharacterData.CharacterSkinInfo
        {
            originalSkinId = eyebrowsIndex,
            originalSkin = data[TypeModel.Eyebrows][eyebrowsIndex],
            originalColor = new List<Color> { RandomColor() },
            otherSkin = null,
            otherSkinColor = new List<Color>()
        });
        int bodyIndex = Random.Range(1, data[TypeModel.Body].Count + 1);
        model.Add(TypeModel.Body, new CharacterData.CharacterSkinInfo
        {
            originalSkinId = bodyIndex,
            originalSkin = data[TypeModel.Body][bodyIndex],
            originalColor = new List<Color> { skinColor },
            otherSkin = null,
            otherSkinColor = new List<Color>()
        });
        int handsIndex = Random.Range(1, data[TypeModel.Hands].Count + 1);
        model.Add(TypeModel.Hands, new CharacterData.CharacterSkinInfo
        {
            originalSkinId = handsIndex,
            originalSkin = data[TypeModel.Hands][handsIndex],
            originalColor = new List<Color> { skinColor },
            otherSkin = null,
            otherSkinColor = new List<Color>()
        });
        int feetIndex = Random.Range(1, data[TypeModel.Feets].Count + 1);
        model.Add(TypeModel.Feets, new CharacterData.CharacterSkinInfo
        {
            originalSkinId = feetIndex,
            originalSkin = data[TypeModel.Feets][feetIndex],
            originalColor = new List<Color> { skinColor },
            otherSkin = null,
            otherSkinColor = new List<Color>()
        });
        return model;
    }
    Color RandomColor()
    {
        return Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f, 1f, 1f);
    }
    public enum TypeModel
    {
        None = 0,
        Hair = 1,
        Head = 2,
        Eyes = 3,
        Eyebrows = 4,
        Ears = 5,
        Body = 6,
        Hands = 7,
        Feets = 8,
    }
}
