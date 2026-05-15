using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "CharactersSkinDB", menuName = "ScriptableObjects/DB/CharactersSkinDB", order = 1)]
public class CharactersSkinDBSO : ScriptableObject
{
    public SerializedDictionary<CharacterData.TypeSkin, SerializedDictionary<int, CharactersSkinSO>> data = new SerializedDictionary<CharacterData.TypeSkin, SerializedDictionary<int, CharactersSkinSO>>();
    public SerializedDictionary<CharacterData.TypeSkin, CharacterData.CharacterSkinInfo> GenerateRandomSkin()
    {
        SerializedDictionary<CharacterData.TypeSkin, CharacterData.CharacterSkinInfo> skins = new SerializedDictionary<CharacterData.TypeSkin, CharacterData.CharacterSkinInfo>();
        int skinIndex = Random.Range(1, data[CharacterData.TypeSkin.Skin].Count);
        Color skinColor = RandomColor();
        skins.Add(CharacterData.TypeSkin.Skin, new CharacterData.CharacterSkinInfo
        {
            originalSprite = data[CharacterData.TypeSkin.Skin][skinIndex],
            originalSpriteColor = skinColor,
            otherSkin = null,
            otherSkinColor = Color.white
        });
        skins.Add(CharacterData.TypeSkin.Hands, new CharacterData.CharacterSkinInfo
        {
            originalSprite = data[CharacterData.TypeSkin.Hands][skinIndex],
            originalSpriteColor = skinColor,
            otherSkin = null,
            otherSkinColor = Color.white
        });
        int hairIndex = Random.Range(1, data[CharacterData.TypeSkin.Hair].Count);
        skins.Add(CharacterData.TypeSkin.Hair, new CharacterData.CharacterSkinInfo
        {
            originalSprite = data[CharacterData.TypeSkin.Hair][hairIndex],
            originalSpriteColor = RandomColor(),
            otherSkin = null,
            otherSkinColor = Color.white
        });
        int eyesIndex = Random.Range(1, data[CharacterData.TypeSkin.Eyes].Count);
        skins.Add(CharacterData.TypeSkin.Eyes, new CharacterData.CharacterSkinInfo
        {
            originalSprite = data[CharacterData.TypeSkin.Eyes][eyesIndex],
            originalSpriteColor = RandomColor(),
            otherSkin = null,
            otherSkinColor = Color.white
        });
        // int mouthIndex = Random.Range(1, data[CharacterData.TypeSkin.Mouth].Count);
        // skins.Add(CharacterData.TypeSkin.Mouth, new CharacterData.CharacterSkinInfo
        // {
        //     originalSprite = data[CharacterData.TypeSkin.Mouth][mouthIndex],
        //     originalSpriteColor = Color.white,
        //     otherSprite = null,
        //     otherSpriteColor = Color.white
        // });
        return skins;
    }
    Color RandomColor()
    {
        return Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f, 1f, 1f);
    }
}
