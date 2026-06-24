using System;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimationEffects", menuName = "ScriptableObjects/AnimationEffects", order = 1)]
public class AnimationEffectsSO : ScriptableObject
{
    public SerializedDictionary<TypeAnimationsEffects, AnimationEffect> animationsEffects = new SerializedDictionary<TypeAnimationsEffects, AnimationEffect>();
    [Serializable]
    public class AnimationEffect
    {
        public float frequency;
        public float amplitude;
    }
    public enum TypeAnimationsEffects
    {
        None = 0,
        Blink = 1,
        Shake = 2,
        Dash = 3
    }
}
