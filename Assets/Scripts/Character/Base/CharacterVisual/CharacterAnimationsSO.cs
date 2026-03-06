#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;
[CreateAssetMenu(fileName = "CharacterAnimations", menuName = "ScriptableObjects/Character/CharacterAnimationsSO", order = 1)]
public class CharacterAnimationsSO : ScriptableObject
{
    public bool isHumanoid;
    public bool isEightDirections;
    public SerializedDictionary<string, AnimationsInfo> animations = new SerializedDictionary<string, AnimationsInfo>();
    private string[] defaultNames = { "Idle", "Walk", "TakeDamage", "Defend", "Lifted", "Lift", "Throw", "FistAttack", "SwordAttack", "SpearAttack", "BowAttack", "GunAttack", "AxeAttack", "StaffAttack" };
    public GenerateAllAnimations generateAllAnimations;
//     #if UNITY_EDITOR
//     [NaughtyAttributes.Button]
//     public void GenerateAllCharacterAnimations()
//     {
//         if (generateAllAnimations.atlas == null || generateAllAnimations.baseSprite == null) return;

//         isHumanoid = generateAllAnimations.isHumanoid;
//         isEightDirections = generateAllAnimations.isEightDirections;
//         Sprite[] allSprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(generateAllAnimations.atlas)).OfType<Sprite>().ToArray();
//         int spriteW = Mathf.RoundToInt(generateAllAnimations.baseSprite.rect.width);
//         int indexSpriteForEvaluate = 0;
//         int nameIndex = 0;
//         string animationName;
//         int middleIndex;
//         animations.Clear();
//         while (true)
//         {
//             animationName = isHumanoid ? defaultNames.Length > nameIndex ? defaultNames[nameIndex] : nameIndex.ToString() : 5 > nameIndex ? defaultNames[nameIndex] : nameIndex == 5 ? "FistAttack" : nameIndex.ToString();
//             List<Sprite> row = new List<Sprite>();
//             for (int i = 0; i < generateAllAnimations.atlas.width / spriteW; i++)
//             {
//                 if (i + indexSpriteForEvaluate > allSprites.Length - 1 || allSprites[i + indexSpriteForEvaluate].rect.y != allSprites[indexSpriteForEvaluate].rect.y)
//                 {
//                     break;
//                 }
//                 row.Add(allSprites[i + indexSpriteForEvaluate]);
//             }
//             middleIndex = row.Count / 2;
//             AnimationsInfo animationInfo = new AnimationsInfo
//             {
//                 name = animationName,
//                 sprites = new SpritesInfo[middleIndex],
//             };
//             for (int i = 0; i < row.Count; i++)
//             {
//                 if (i < middleIndex)
//                 {
//                     animationInfo.spritesInfoDown[i] = new SpritesInfo();
//                     animationInfo.spritesInfoDown[i].characterSprite = row[i];
//                 }
//                 else
//                 {
//                     animationInfo.spritesInfoUp[i - middleIndex] = new SpritesInfo();
//                     animationInfo.spritesInfoUp[i - middleIndex].characterSprite = row[i];
//                 }
//             }
//             animations.Add(animationName, animationInfo);

//             switch (animationName)
//             {
//                 case "Defend":
//                 case "TakeDamage":
//                     int amountSprites = 0;
//                     List<SpritesInfo> spritesUp = new List<SpritesInfo>();
//                     List<SpritesInfo> spritesDown = new List<SpritesInfo>();
//                     for (int i = 0; i < 6; i++)
//                     {
//                         foreach (var spriteUp in animations[animationName].spritesInfoDown)
//                         {
//                             spritesDown.Add(new SpritesInfo
//                             {
//                                 characterSprite = spriteUp.characterSprite
//                             });
//                             amountSprites++;
//                         }
//                         foreach (var spriteUp in animations[animationName].spritesInfoUp)
//                         {
//                             spritesUp.Add(new SpritesInfo
//                             {
//                                 characterSprite = spriteUp.characterSprite
//                             });
//                         }
//                         if (i == 0 && amountSprites == 4 || amountSprites == 6)
//                         {
//                             break;
//                         }
//                     }
//                     animations[animationName].sprites = sprites.ToArray();
//                     break;
//                 case "Idle":
//                 case "Walk":
//                 case "Lifted":
//                 case "Lift":
//                     animations[animationName].loop = true;
//                     break;
//             }
//             if (animationName == "Defend")
//             {
//                 animations.Add("GeneralSkillEffect", animations["Defend"]);
//                 animations["GeneralSkillEffect"].name = "GeneralSkillEffect";
//             }
//             nameIndex++;
//             indexSpriteForEvaluate += row.Count;
//             if (nameIndex >= generateAllAnimations.atlas.height / spriteW)
//             {
//                 break;
//             }
//         }
//         animations["TakeDamage"].animationsEffects = new SerializedDictionary<CharacterAnimator.TypeAnimationsEffects, CharacterAnimator.AnimationEffectInfo>
//         {
//             {
//                 CharacterAnimator.TypeAnimationsEffects.Shake,
//                 new CharacterAnimator.AnimationEffectInfo
//                 {
//                     amplitude = 0.1f,
//                     frequency = 100
//                 }
//             },
//             {
//                 CharacterAnimator.TypeAnimationsEffects.Blink,
//                 new CharacterAnimator.AnimationEffectInfo
//                 {
//                     colorBlink = Color.HSVToRGB(0, 100, 58)
//                 }
//             }
//         };
//         atlas = generateAllAnimations.atlas;
//         atlasHands = generateAllAnimations.atlasHands;
//         icon = generateAllAnimations.icon;

//         if (isHumanoid)
//         {
//             animations["FistAttack"].frameToInstance = 2;
//             animations["SwordAttack"].frameToInstance = 3;
//         }
//     }
// #endif

    [Serializable]
    public class AnimationsInfo
    {
        public string name;
        public string linkAnimation;
        public List<SpritesInfo> sprites;
        public SerializedDictionary<CharacterAnimator.TypeAnimationsEffects, CharacterAnimator.AnimationEffectInfo> animationsEffects;
        public bool loop = false;
        public bool needInstance = false;
        public int frameToInstance = 0;
        public GameObject instanceObj;
        public GameObject instance;
    }
    [Serializable]
    public class SpritesInfo
    {
        public SpriteData midle;
        public SpriteData up;
        public SpriteData down;
        public SpriteData left;
        public SpriteData right;
        public SpriteData upLeft;
        public SpriteData upRight;
        public SpriteData downLeft;
        public SpriteData downRight;
    }
    [Serializable]
    public class SpriteData
    {
        public Sprite characterSprite;
        public Vector3 leftHandPos;
        public Quaternion leftHandRotation;
        public Vector3 rightHandPos;
        public Quaternion rightHandRotation;        
    }
    [Serializable]
    public class GenerateAllAnimations
    {
        public Sprite baseSprite;
        public Texture2D atlas;
        public Texture2D atlasHands;
        public Sprite icon;
        public bool isHumanoid;
        public bool isEightDirections;
    }
}
