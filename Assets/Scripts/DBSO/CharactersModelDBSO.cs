using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "CharactersModelDB", menuName = "ScriptableObjects/DB/CharactersModelDB", order = 1)]
public class CharactersModelDBSO : ScriptableObject
{
    public SerializedDictionary<TypeModel, SerializedDictionary<int, List<Mesh>>> data = new SerializedDictionary<TypeModel, SerializedDictionary<int, List<Mesh>>>();
    public SerializedDictionary<TypeModel, CharacterData.CharacterSkinInfo> GenerateRandomModel()
    {
        SerializedDictionary<TypeModel, CharacterData.CharacterSkinInfo> model = new SerializedDictionary<TypeModel, CharacterData.CharacterSkinInfo>();
        int hairIndex = Random.Range(1, data[TypeModel.Hair].Count + 1);
        model.Add(TypeModel.Hair, new CharacterData.CharacterSkinInfo
        {
            meshId = hairIndex,
            colors = new List<Color> { RandomColor(), RandomColor(), RandomColor(), RandomColor(), RandomColor() },
        });
        int headIndex = Random.Range(1, data[TypeModel.Head].Count + 1);
        Color skinColor = RandomColor();
        model.Add(TypeModel.Head, new CharacterData.CharacterSkinInfo
        {
            meshId = headIndex,
            colors = new List<Color> { skinColor },
        });
        int eyesIndex = Random.Range(1, data[TypeModel.Eyes].Count + 1);
        model.Add(TypeModel.Eyes, new CharacterData.CharacterSkinInfo
        {
            meshId = eyesIndex,
            colors = new List<Color> { RandomColor(), RandomColor(), RandomColor() },
        });
        int eyebrowsIndex = Random.Range(1, data[TypeModel.Eyebrows].Count + 1);
        model.Add(TypeModel.Eyebrows, new CharacterData.CharacterSkinInfo
        {
            meshId = eyebrowsIndex,
            colors = new List<Color> { RandomColor() },
        });
        int earsIndex = Random.Range(1, data[TypeModel.Ears].Count + 1);
        model.Add(TypeModel.Ears, new CharacterData.CharacterSkinInfo
        {
            meshId = earsIndex,
            colors = new List<Color> { skinColor },
        });
        int bodyIndex = Random.Range(1, data[TypeModel.Body].Count + 1);
        model.Add(TypeModel.Body, new CharacterData.CharacterSkinInfo
        {
            meshId = bodyIndex,
            colors = new List<Color> { skinColor },
        });
        int handsIndex = Random.Range(1, data[TypeModel.Hands].Count + 1);
        model.Add(TypeModel.Hands, new CharacterData.CharacterSkinInfo
        {
            meshId = handsIndex,
            colors = new List<Color> { skinColor },
        });
        int feetIndex = Random.Range(1, data[TypeModel.Feets].Count + 1);
        model.Add(TypeModel.Feets, new CharacterData.CharacterSkinInfo
        {
            meshId = feetIndex,
            colors = new List<Color> { skinColor },
        });
        model.Add(TypeModel.Helmet, new CharacterData.CharacterSkinInfo
        {
            meshId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
        });
        model.Add(TypeModel.Front, new CharacterData.CharacterSkinInfo
        {
            meshId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
        });
        model.Add(TypeModel.Pants, new CharacterData.CharacterSkinInfo
        {
            meshId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
        });
        model.Add(TypeModel.Boots, new CharacterData.CharacterSkinInfo
        {
            meshId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
        });
        model.Add(TypeModel.Gloves, new CharacterData.CharacterSkinInfo
        {
            meshId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
        });
        model.Add(TypeModel.Pendant, new CharacterData.CharacterSkinInfo
        {
            meshId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
        });
        model.Add(TypeModel.Ring, new CharacterData.CharacterSkinInfo
        {
            meshId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
        });
        model.Add(TypeModel.Weapon, new CharacterData.CharacterSkinInfo
        {
            meshId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
        });
        model.Add(TypeModel.Consumable, new CharacterData.CharacterSkinInfo
        {
            meshId = 0,
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white },
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
        Helmet = 9,
        Front = 10,
        Pants = 11,
        Boots = 12,
        Gloves = 13,
        Pendant = 14,
        Ring = 15,
        Weapon = 16,
        Consumable = 17,
    }
}
