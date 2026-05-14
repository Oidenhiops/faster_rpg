using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    public CharacterBase characterBase;
    public CharacterAnimationsSO.AnimationsInfo currentAnimation = new CharacterAnimationsSO.AnimationsInfo();
    public int currentSpriteIndex;
    public float currentSpritePerTime = 0.1f;
    public string animationAfterEnd;
    public Sprite spriteSheetBase;
    public void SetInitialData()
    {
        StopAllCoroutines();
        currentSpriteIndex = 0;
        animationAfterEnd = "";
        characterBase.characterModel.characterMeshRenderer.transform.parent.transform.localScale = Vector3.one * GetScaleFactor(characterBase.charactersData[characterBase.characterIndex].characterAnimationsSO.animations["Idle"].sprites.ElementAt(0).upLeft.characterSprite.rect.height);
        characterBase.characterScale = characterBase.characterModel.characterMeshRenderer.transform.parent.transform.localScale;
        characterBase.characterModel.characterMeshRenderer.material.SetTexture("_BaseTexture", characterBase.charactersData[characterBase.characterIndex].characterSkin.atlas);
        if (characterBase.charactersData[characterBase.characterIndex].characterSkin.atlasHands)
        {
            characterBase.characterModel.characterMeshRendererHand.gameObject.SetActive(true);
            characterBase.characterModel.characterMeshRendererHand.material.SetTexture("_BaseTexture", characterBase.charactersData[characterBase.characterIndex].characterSkin.atlasHands);
        }
        else
        {
            characterBase.characterModel.characterMeshRendererHand.gameObject.SetActive(false);
        }
        currentAnimation = GetAnimation("Idle");
        StartCoroutine(AnimateSprite());
    }
    void LateUpdate()
    {
        if (CameraInfo.Instance && characterBase.isInitialize)
        {
            ChangeDirectionModel(ref characterBase.directionAnimation);
        }
    }
        void ChangeDirectionModel(ref Vector3Int direction)
    {
        if (direction.x > 0)
        {
            characterBase.characterScale.x = -Mathf.Abs(characterBase.characterScale.x);
        }
        else if (direction.x < 0)
        {
            characterBase.characterScale.x = Mathf.Abs(characterBase.characterScale.x);
        }
        characterBase.characterModel.characterMeshRenderer.transform.localScale = characterBase.characterScale;
    }
    float GetScaleFactor(float size)
    {
        float baseScale = 64f;
        return size / baseScale;
    }
    public void MakeAnimation(string animationName)
    {
        if (currentAnimation == GetAnimation(animationName)) return;

        StopAllCoroutines();
        currentAnimation = GetAnimation(animationName);
        currentSpriteIndex = 0;
        StartCoroutine(AnimateSprite());
    }
    public string GetAnimationAttack()
    {
        characterBase.charactersData[characterBase.characterIndex].characterData.GetCurrentWeapon(out CharacterData.CharacterItem weapon);
        if (weapon != null)
        {
            return weapon.itemBaseSO.animationName;
        }
        return "FistAttack";
    }
    private CharacterAnimationsSO.AnimationsInfo GetAnimation(string animationName)
    {
        return characterBase.charactersData[characterBase.characterIndex].characterAnimationsSO.animations[animationName];
    }
    IEnumerator AnimateSprite()
    {
        while (true)
        {
            SetTextureFromAtlas(
                GetCurrentSpriteData().characterSprite,
                characterBase.characterModel.characterMeshRenderer
            );
            if (characterBase.charactersData[characterBase.characterIndex].characterSkin.atlasHands)
            {
                SetTextureFromAtlas(
                    GetCurrentSpriteData().characterSprite,
                    characterBase.characterModel.characterMeshRendererHand
                );
                SetHandsPos();
            }
            yield return new WaitForSeconds(currentSpritePerTime);
            currentSpriteIndex++;
            if (currentSpriteIndex > currentAnimation.sprites.Count - 1)
            {
                if (currentAnimation.loop)
                {
                    currentSpriteIndex = 0;
                }
                else
                {
                    if (currentAnimation.linkAnimation != "")
                    {
                        MakeAnimation(currentAnimation.linkAnimation);
                    }
                    else
                    {
                        if (animationAfterEnd != "")
                        {
                            MakeAnimation(animationAfterEnd);
                            animationAfterEnd = "";
                        }
                        else
                        {
                            MakeAnimation("Idle");
                        }
                    }
                }
            }
        }
    }
    public CharacterAnimationsSO.SpriteData GetCurrentSpriteData()
    {
        if (characterBase.charactersData[characterBase.characterIndex].characterAnimationsSO.isEightDirections)
        {
            if (characterBase.directionAnimation == Vector3Int.forward)
            {
                return currentAnimation.sprites[currentSpriteIndex].up;
            }
            else if (characterBase.directionAnimation == Vector3Int.back)
            {
                return currentAnimation.sprites[currentSpriteIndex].down;
            }
            else if (characterBase.directionAnimation == Vector3Int.left)
            {
                return currentAnimation.sprites[currentSpriteIndex].left;
            }
            else if (characterBase.directionAnimation == Vector3Int.right)
            {
                if (currentAnimation.sprites[currentSpriteIndex].right.characterSprite != null)
                {
                    return currentAnimation.sprites[currentSpriteIndex].right;
                }
                else
                {
                    return currentAnimation.sprites[currentSpriteIndex].left;
                }
            }
            else if (characterBase.directionAnimation == new Vector3Int(-1, 0, 1))
            {
                return currentAnimation.sprites[currentSpriteIndex].upLeft;
            }
            else if (characterBase.directionAnimation == new Vector3Int(1, 0, 1))
            {
                if (currentAnimation.sprites[currentSpriteIndex].upRight.characterSprite != null)
                {
                    return currentAnimation.sprites[currentSpriteIndex].upRight;
                }
                else
                {
                    return currentAnimation.sprites[currentSpriteIndex].upLeft;
                }
            }
            else if (characterBase.directionAnimation == new Vector3Int(-1, 0, -1))
            {
                return currentAnimation.sprites[currentSpriteIndex].downLeft;
            }
            else if (characterBase.directionAnimation == new Vector3Int(1, 0, -1))
            {
                if (currentAnimation.sprites[currentSpriteIndex].downRight.characterSprite != null)
                {
                    return currentAnimation.sprites[currentSpriteIndex].downRight;
                }
                else
                {
                    return currentAnimation.sprites[currentSpriteIndex].downLeft;
                }
            }
            else
            {
                if (currentAnimation.sprites[currentSpriteIndex].midle.characterSprite != null)
                {
                    return currentAnimation.sprites[currentSpriteIndex].midle;
                }
                else
                {
                    return currentAnimation.sprites[currentSpriteIndex].down;
                }
            }
        }
        else
        {
            return null;
        }
    }
    void SetTextureFromAtlas(Sprite spriteFromAtlas, MeshRenderer meshRenderer)
    {
        Vector2[] uvs = characterBase.characterModel.originalMesh.uv;
        Texture2D texture = spriteFromAtlas.texture;
        meshRenderer.material.mainTexture = texture;
        Rect spriteRect = spriteFromAtlas.rect;
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i].x = Mathf.Lerp(spriteRect.x / texture.width, (spriteRect.x + spriteRect.width) / texture.width, uvs[i].x);
            uvs[i].y = Mathf.Lerp(spriteRect.y / texture.height, (spriteRect.y + spriteRect.height) / texture.height, uvs[i].y);
        }
        meshRenderer.GetComponent<MeshFilter>().mesh.uv = uvs;
    }
    void SetHandsPos()
    {
        // if (currentSpriteIndex < currentAnimation.sprites.Length && currentAnimation.sprites.Length > 0)
        // {
        //     switch (isUp)
        //     {
        //         case true:
        //             Vector3 spriteLeftUpPos = character.direction.x > 0 ?
        //                                         currentAnimation.spritesInfoUp[currentSpriteIndex].leftHandPosDR :
        //                                         currentAnimation.spritesInfoUp[currentSpriteIndex].leftHandPosDL;
        //             Vector3 spriteRightUpPos = character.direction.x > 0 ?
        //                                         currentAnimation.spritesInfoUp[currentSpriteIndex].rightHandPosDR :
        //                                         currentAnimation.spritesInfoUp[currentSpriteIndex].rightHandPosDL;
        //             character.characterModel.leftHand.transform.localPosition = spriteLeftUpPos;
        //             character.characterModel.leftHand.transform.localRotation = currentAnimation.spritesInfoUp[currentSpriteIndex].leftHandRotation;
        //             character.characterModel.rightHand.transform.localPosition = spriteRightUpPos;
        //             character.characterModel.rightHand.transform.localRotation = currentAnimation.spritesInfoUp[currentSpriteIndex].rightHandRotation;
        //             break;
        //         case false:
        //             Vector3 spriteLeftDownPos = character.direction.x > 0 ?
        //                                         currentAnimation.spritesInfoDown[currentSpriteIndex].leftHandPosDR :
        //                                         currentAnimation.spritesInfoDown[currentSpriteIndex].leftHandPosDL;
        //             Vector3 spriteRightDownPos = character.direction.x > 0 ?
        //                                         currentAnimation.spritesInfoDown[currentSpriteIndex].rightHandPosDR :
        //                                         currentAnimation.spritesInfoDown[currentSpriteIndex].rightHandPosDL;
        //             character.characterModel.leftHand.transform.localPosition = spriteLeftDownPos;
        //             character.characterModel.leftHand.transform.localRotation = currentAnimation.spritesInfoDown[currentSpriteIndex].leftHandRotation;
        //             character.characterModel.rightHand.transform.localPosition = spriteRightDownPos;
        //             character.characterModel.rightHand.transform.localRotation = currentAnimation.spritesInfoDown[currentSpriteIndex].rightHandRotation;
        //             break;
        //     }
        // }
    }
    public void MakeEffect(TypeAnimationsEffects typeEffect)
    {
        switch (typeEffect)
        {
            case TypeAnimationsEffects.Blink:
                _ = Blink();
                break;
            case TypeAnimationsEffects.Shake:
                _ = Shake();
                break;
        }
    }
    #region AnimationsEffects
    async Awaitable Shake()
    {
        try
        {
            // float tiempoTranscurrido = 0f;
            // Vector3 initialPos = character.characterModel.characterMeshRenderer.transform.localPosition;

            // while (tiempoTranscurrido < currentSpritePerTime * currentAnimation.sprites.Count)
            // {
            //     float desplazamientoX = Mathf.Sin(Time.time * currentAnimation.animationsEffects[TypeAnimationsEffects.Shake].frequency) * currentAnimation.animationsEffects[TypeAnimationsEffects.Shake].amplitude;
            //     character.characterModel.characterMeshRenderer.transform.localPosition = initialPos + new Vector3(desplazamientoX, 0, 0);
            //     tiempoTranscurrido += Time.deltaTime;
            //     await Awaitable.NextFrameAsync();
            // }
            // initialPos.x = 0f;
            // character.characterModel.characterMeshRenderer.transform.localPosition = initialPos;
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
            // float tiempoTranscurrido = 0f;
            // while (tiempoTranscurrido < currentSpritePerTime * currentAnimation.sprites.Count)
            // {
            //     if (character.characterModel.characterMeshRenderer.material.color == Color.white)
            //     {
            //         character.characterModel.characterMeshRenderer.material.SetColor("_Color", currentAnimation.animationsEffects[TypeAnimationsEffects.Blink].colorBlink);
            //     }
            //     else
            //     {
            //         character.characterModel.characterMeshRenderer.material.SetColor("_Color", Color.white);
            //     }
            //     tiempoTranscurrido += currentSpritePerTime;
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
    public enum TypeAnimationsEffects
    {
        None = 0,
        Shake = 1,
        Blink = 2,
        Dash = 3
    }
}
