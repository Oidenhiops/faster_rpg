using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    public CharacterBase characterBase;
    public Animator characterAnimator;
    public AnimationEffectsSO animationEffectsSO;
    public void MakeAnimation(string animationName)
    {
        if (characterAnimator.GetCurrentAnimatorStateInfo(0).IsName(animationName)) return;
        characterAnimator.Play(animationName);
    }
    public string GetAnimationAttack()
    {
        characterBase.charactersData[characterBase.characterIndex].GetCurrentWeapon(out CharacterData.CharacterItem weapon);
        if (weapon != null)
        {
            return weapon.itemBaseSO.animationName;
        }
        return "FistAttack";
    }
    public void MakeEffect(AnimationEffectsSO.TypeAnimationsEffects typeEffect)
    {
        switch (typeEffect)
        {
            case AnimationEffectsSO.TypeAnimationsEffects.Blink:
                _ = Blink();
                break;
            case AnimationEffectsSO.TypeAnimationsEffects.Shake:
                _ = Shake();
                break;
        }
    }
    #region AnimationsEffects
    async Awaitable Shake()
    {
        try
        {
            float elapsedTime = 0f;
            Vector3 initialPos = characterBase.characterModel.modelTransform.localPosition;

            while (elapsedTime < animationEffectsSO.animationsEffects[AnimationEffectsSO.TypeAnimationsEffects.Shake].frequency)
            {
                float desplazamientoX = Mathf.Sin(Time.time * animationEffectsSO.animationsEffects[AnimationEffectsSO.TypeAnimationsEffects.Shake].frequency) * animationEffectsSO.animationsEffects[AnimationEffectsSO.TypeAnimationsEffects.Shake].amplitude;
                characterBase.characterModel.modelTransform.localPosition = initialPos + new Vector3(desplazamientoX, 0, 0);
                elapsedTime += Time.deltaTime;
                await Awaitable.NextFrameAsync();
            }
            initialPos.x = 0f;
            characterBase.characterModel.modelTransform.localPosition = initialPos;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    async Awaitable Blink()
    {
        try
        {
            // float elapsedTime = 0f;
            // while (elapsedTime < currentSpritePerTime * currentAnimation.sprites.Count)
            // {
            //     if (character.characterModel.characterMeshRenderer.material.color == Color.white)
            //     {
            //         character.characterModel.characterMeshRenderer.material.SetColor("_Color", currentAnimation.animationsEffects[TypeAnimationsEffects.Blink].colorBlink);
            //     }
            //     else
            //     {
            //         character.characterModel.characterMeshRenderer.material.SetColor("_Color", Color.white);
            //     }
            //     elapsedTime += currentSpritePerTime;
            //     await Task.Delay(TimeSpan.FromSeconds(currentSpritePerTime));
            // }
            // character.characterModel.characterMeshRenderer.material.SetColor("_Color", Color.white);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    #endregion
    [Serializable] public class AnimationEffectInfo
    {
        public float amplitude = 0;
        public float frequency = 0;
        public Color colorBlink = Color.white;
    }
}
