#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using System.Linq;
[CreateAssetMenu(fileName = "CharacterAnimations", menuName = "ScriptableObjects/Character/CharacterAnimationsSO", order = 1)]
public class CharacterAnimationsSO : ScriptableObject
{
    public bool isHumanoid;
    public bool isEightDirections;
    public SerializedDictionary<string, AnimationsInfo> animations = new SerializedDictionary<string, AnimationsInfo>();
    public string animationName;
    public Sprite characterSpriteSheetBase;
    #if UNITY_EDITOR
    [NaughtyAttributes.Button]
    public void GenerateAllCharacterAnimations()
    {
        Sprite[] allSprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(characterSpriteSheetBase)).OfType<Sprite>().ToArray();
        int spriteW = Mathf.RoundToInt(allSprites[0].rect.width);
        int indexSpriteForEvaluate = 0;
        animations.Clear();
        SerializedDictionary<string, AnimationsInfo> newAnimation = new SerializedDictionary<string, AnimationsInfo>();
        while (true)
        {
            List<Sprite> row = new List<Sprite>();
            for (int i = 0; i < characterSpriteSheetBase.rect.width / spriteW; i++)
            {
                if (i + indexSpriteForEvaluate > allSprites.Length - 1 || allSprites[i + indexSpriteForEvaluate].rect.y != allSprites[indexSpriteForEvaluate].rect.y)
                {
                    break;
                }
                row.Add(allSprites[i + indexSpriteForEvaluate]);
            }
        }
    }
#endif

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
}
