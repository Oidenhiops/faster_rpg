using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    public CharacterBase characterBase;
    public AnimationEffectsSO animationEffectsSO;
    float animSpeed;
    public void HandleAnimation()
    {
        if (characterBase.isInCanalization) return;
        animSpeed = Mathf.MoveTowards(characterBase.characterAnimator.GetFloat("speed"), GetAnimationSpeed(), Time.deltaTime * 10f);
        characterBase.characterAnimator.SetFloat("speed", animSpeed);
        characterBase.characterAnimator.SetBool("isGrounded", characterBase.isGrounded);
    }
    float GetAnimationSpeed()
    {
        if (characterBase.directionMovement == Vector2.zero) return 0f;
        else if (characterBase.isRunning) return 1;
        else return 0.5f;
    }
    public string GetAnimationAttack()
    {
        characterBase.characterData.GetCurrentWeapon(out CharacterData.CharacterItem weapon);
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
    int blinkToken;

    void ApplyBlinkColor(Color color)
    {
        foreach (KeyValuePair<ItemsDBSO.TypeModel, List<CharacterBase.CharacterModelData>> model in characterBase.characterModel.meshesData)
        {
            if (model.Value == null) continue;
            foreach (CharacterBase.CharacterModelData modelData in model.Value)
            {
                Material[] mats = modelData.meshRenderer.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i].HasProperty("_Color"))
                    {
                        mats[i].SetColor("_Color", color);
                    }
                }
            }
        }
    }

    void RestoreOriginalColors()
    {
        foreach (KeyValuePair<ItemsDBSO.TypeModel, List<CharacterBase.CharacterModelData>> model in characterBase.characterModel.meshesData)
        {
            if (model.Value == null) continue;
            foreach (CharacterBase.CharacterModelData modelData in model.Value)
            {
                Material[] mats = modelData.meshRenderer.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i].HasProperty("_Color"))
                    {
                        if (characterBase.characterData.models.TryGetValue(model.Key, out CharacterData.CharacterSkinInfo skinInfo) && i < skinInfo.colors.Count)
                        {
                            mats[i].SetColor("_Color", skinInfo.colors[i]);
                        }
                        else
                        {
                            mats[i].SetColor("_Color", Color.white);
                        }
                    }
                }
            }
        }
    }

    async Awaitable Blink()
    {
        int token = ++blinkToken;
        RestoreOriginalColors();

        AnimationEffectsSO.AnimationEffect info = animationEffectsSO.animationsEffects[AnimationEffectsSO.TypeAnimationsEffects.Blink];
        try
        {
            float step = info.frequency > 0f ? info.frequency : 0.1f;
            int blinkCount = info.amplitude > 0f ? Mathf.RoundToInt(info.amplitude) : 3;

            for (int b = 0; b < blinkCount; b++)
            {
                if (token != blinkToken) return;

                ApplyBlinkColor(info.color);
                await Awaitable.WaitForSecondsAsync(step);

                if (token != blinkToken) return;

                RestoreOriginalColors();
                await Awaitable.WaitForSecondsAsync(step);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            if (token == blinkToken) RestoreOriginalColors();
        }
    }
    #endregion
}
