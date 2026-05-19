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
    public bool isEightDirections;
    public SerializedDictionary<string, AnimationsInfo> animations = new SerializedDictionary<string, AnimationsInfo>();
    public string animationName;
    public Texture2D characterSpriteSheetBase;
    public List<SpritesInfo> spritesD8;
    public List<SpritesInfo> spritesD4;
    // #if UNITY_EDITOR
    [NaughtyAttributes.Button]
    public void GenerateCharacterAnimation()
    {
        Sprite[] allSprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(characterSpriteSheetBase)).OfType<Sprite>().ToArray();
        if (allSprites == null || allSprites.Length == 0 || animationName == null || animationName == "")
        {
            Debug.LogError("CharacterAnimationsSO: no se encontraron sprites en el spritesheet.");
            return;
        }
        allSprites = allSprites.OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).ToArray();

        int spriteW = Mathf.RoundToInt(allSprites[0].rect.width);
        int indexSpriteForEvaluate = 0;

        List<List<Sprite>> rows = new List<List<Sprite>>();

        while (true)
        {
            if (indexSpriteForEvaluate > allSprites.Length - 1)
            {
                break;
            }

            List<Sprite> row = new List<Sprite>();
            for (int i = 0; i < characterSpriteSheetBase.width / spriteW; i++)
            {
                if (i + indexSpriteForEvaluate > allSprites.Length - 1 || allSprites[i + indexSpriteForEvaluate].rect.y != allSprites[indexSpriteForEvaluate].rect.y)
                {
                    break;
                }
                row.Add(allSprites[i + indexSpriteForEvaluate]);
            }

            if (row.Count == 0)
            {
                break;
            }

            rows.Add(row);
            indexSpriteForEvaluate += row.Count;
        }
        int expectedRows = isEightDirections ? 8 : 4;
        if (rows.Count < expectedRows)
        {
            Debug.LogError($"CharacterAnimationsSO: se esperaban al menos {expectedRows} filas (una por dirección) pero se encontraron {rows.Count}.");
            return;
        }

        AnimationsInfo newAnimInfo = new AnimationsInfo
        {
            name = animationName,
            animationsEffects = new SerializedDictionary<CharacterAnimator.TypeAnimationsEffects, CharacterAnimator.AnimationEffectInfo>()
        };
        int frameCount = rows[0].Count;

        for (int frame = 0; frame < frameCount; frame++)
        {
            SpritesInfo spritesInfo = new SpritesInfo
            {
                upLeft    = new SpriteData(),
                up        = new SpriteData(),
                upRight   = new SpriteData(),
                right     = new SpriteData(),
                downRight = new SpriteData(),
                down      = new SpriteData(),
                downLeft  = new SpriteData(),
                left      = new SpriteData()
            };

            if (isEightDirections)
            {
                spritesInfo.upLeft.characterSprite    = GetFrameSprite(rows[0], frame);
                spritesInfo.up.characterSprite        = GetFrameSprite(rows[1], frame);
                spritesInfo.upRight.characterSprite   = GetFrameSprite(rows[2], frame);
                spritesInfo.right.characterSprite     = GetFrameSprite(rows[3], frame);
                spritesInfo.downRight.characterSprite = GetFrameSprite(rows[4], frame);
                spritesInfo.down.characterSprite      = GetFrameSprite(rows[5], frame);
                spritesInfo.downLeft.characterSprite  = GetFrameSprite(rows[6], frame);
                spritesInfo.left.characterSprite      = GetFrameSprite(rows[7], frame);
            }
            else
            {
                spritesInfo.up.characterSprite    = GetFrameSprite(rows[0], frame);
                spritesInfo.right.characterSprite = GetFrameSprite(rows[1], frame);
                spritesInfo.down.characterSprite  = GetFrameSprite(rows[2], frame);
                spritesInfo.left.characterSprite  = GetFrameSprite(rows[3], frame);
            }
        }
        animations.Add(animationName, newAnimInfo);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    private static Sprite GetFrameSprite(List<Sprite> row, int frame)
    {
        return (frame >= 0 && frame < row.Count) ? row[frame] : null;
    }
// #endif

    [Serializable]
    public class AnimationsInfo
    {
        public string name;
        public string linkAnimation;
        public int amountSprites;
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
        public SpriteData upLeft;
        public SpriteData up;
        public SpriteData upRight;
        public SpriteData right;
        public SpriteData downRight;
        public SpriteData down;
        public SpriteData downLeft;
        public SpriteData left;
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
