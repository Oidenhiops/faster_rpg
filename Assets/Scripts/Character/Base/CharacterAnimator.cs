using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    public CharacterBase characterBase;
    public CharacterAnimationsSO.AnimationsInfo currentAnimation = new CharacterAnimationsSO.AnimationsInfo();
    public int currentSpriteIndex;
    public float currentSpritePerTime = 0.1f;
    public string animationAfterEnd;
    public CharacterAnimationsSO characterAnimationsSO;
    public void 
    SetInitialData()
    {
        StopAllCoroutines();
        currentSpriteIndex = 0;
        animationAfterEnd = "";
        if (
            currentAnimation.name == "Idle" || 
            currentAnimation.name == "Walk" || 
            currentAnimation.name == "Jump" || 
            currentAnimation.name == "Dash"
            ) currentAnimation = GetAnimation(currentAnimation.name);
        else
        {
            currentAnimation = GetAnimation("Idle");
        }
        SetTextureFromAnimation();
        StartCoroutine(AnimateSprite());
    }
    public void MakeAnimation(string animationName)
    {
        if (currentAnimation == GetAnimation(animationName)) return;

        StopAllCoroutines();
        currentAnimation = GetAnimation(animationName);
        currentSpriteIndex = 0;
        SetTextureFromAnimation();
        StartCoroutine(AnimateSprite());
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
    private CharacterAnimationsSO.AnimationsInfo GetAnimation(string animationName)
    {
        return characterAnimationsSO.animations[animationName];
    }
    IEnumerator AnimateSprite()
    {
        while (true)
        {
            SetUvsFromAtlas(GetCurrentSpriteData().characterSprite);
            if (
                characterBase.charactersData[characterBase.characterIndex].skins.ContainsKey(CharacterData.TypeSkin.Hands) &&
                characterBase.charactersData[characterBase.characterIndex].skins[CharacterData.TypeSkin.Hands].originalSkin != null
                )
            {
                SetUvsFromAtlas(GetCurrentSpriteData().characterSprite);
                SetHandsPos();
            }
            yield return new WaitForSeconds(currentSpritePerTime);
            currentSpriteIndex++;
            if (currentSpriteIndex > currentAnimation.amountSprites - 1)
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
        if (characterAnimationsSO.isEightDirections)
        {
            if (characterBase.directionAnimation == Vector3Int.forward)
            {
                return characterAnimationsSO.spritesD8[currentSpriteIndex].up;
            }
            else if (characterBase.directionAnimation == Vector3Int.back)
            {
                return characterAnimationsSO.spritesD8[currentSpriteIndex].down;
            }
            else if (characterBase.directionAnimation == Vector3Int.left)
            {
                return characterAnimationsSO.spritesD8[currentSpriteIndex].left;
            }
            else if (characterBase.directionAnimation == Vector3Int.right)
            {
                return characterAnimationsSO.spritesD8[currentSpriteIndex].right;
            }
            else if (characterBase.directionAnimation == new Vector3Int(-1, 0, 1))
            {
                return characterAnimationsSO.spritesD8[currentSpriteIndex].upLeft;
            }
            else if (characterBase.directionAnimation == new Vector3Int(1, 0, 1))
            {
                return characterAnimationsSO.spritesD8[currentSpriteIndex].upRight;
            }
            else if (characterBase.directionAnimation == new Vector3Int(-1, 0, -1))
            {
                return characterAnimationsSO.spritesD8[currentSpriteIndex].downLeft;
            }
            else if (characterBase.directionAnimation == new Vector3Int(1, 0, -1))
            {
                return characterAnimationsSO.spritesD8[currentSpriteIndex].downRight;
            }
            else
            {
                return characterAnimationsSO.spritesD8[currentSpriteIndex].down;
            }
        }
        else
        {
            if (characterBase.directionAnimation == Vector3Int.forward)
            {
                return characterAnimationsSO.spritesD4[currentSpriteIndex].upLeft;
            }
            else if (characterBase.directionAnimation == Vector3Int.back)
            {
                return characterAnimationsSO.spritesD4[currentSpriteIndex].downLeft;
            }
            else if (characterBase.directionAnimation == Vector3Int.left)
            {
                return characterAnimationsSO.spritesD4[currentSpriteIndex].upLeft;
            }
            else if (characterBase.directionAnimation == Vector3Int.right)
            {
                return characterAnimationsSO.spritesD4[currentSpriteIndex].downRight;
            }
            else if (characterBase.directionAnimation == new Vector3Int(-1, 0, 1))
            {
                return characterAnimationsSO.spritesD4[currentSpriteIndex].upLeft;
            }
            else if (characterBase.directionAnimation == new Vector3Int(1, 0, 1))
            {
                return characterAnimationsSO.spritesD4[currentSpriteIndex].upRight;
            }
            else if (characterBase.directionAnimation == new Vector3Int(-1, 0, -1))
            {
                return characterAnimationsSO.spritesD4[currentSpriteIndex].downLeft;
            }
            else if (characterBase.directionAnimation == new Vector3Int(1, 0, -1))
            {
                return characterAnimationsSO.spritesD4[currentSpriteIndex].downRight;
            }
            else
            {
                return characterAnimationsSO.spritesD4[currentSpriteIndex].downLeft;
            }
        }
    }
    void SetUvsFromAtlas(Sprite spriteFromAtlas)
    {
        Vector2[] uvs = characterBase.characterModel.originalMesh.uv;
        Texture2D texture = spriteFromAtlas.texture;
        Rect spriteRect = spriteFromAtlas.rect;
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i].x = Mathf.Lerp(spriteRect.x / texture.width, (spriteRect.x + spriteRect.width) / texture.width, uvs[i].x);
            uvs[i].y = Mathf.Lerp(spriteRect.y / texture.height, (spriteRect.y + spriteRect.height) / texture.height, uvs[i].y);
        }
        foreach (KeyValuePair<CharacterData.TypeSkin, MeshRenderer> mesh in characterBase.characterModel.meshRenderers)
        {
            mesh.Value.GetComponent<MeshFilter>().mesh.uv = uvs;
        }
    }
    void SetTextureFromAnimation()
    {
        foreach (KeyValuePair<CharacterData.TypeSkin, MeshRenderer> mesh in characterBase.characterModel.meshRenderers)
        {
            if (characterBase.charactersData[characterBase.characterIndex].skins.TryGetValue(mesh.Key, out CharacterData.CharacterSkinInfo skinInfo))
            {
                if (skinInfo.originalSkin)
                {
                    mesh.Value.enabled = true;
                    mesh.Value.material.SetTexture("_BaseTexture", characterBase.charactersData[characterBase.characterIndex].skins[mesh.Key].originalSkin.textures[currentAnimation.name]);
                    mesh.Value.material.SetColor("_Color", characterBase.charactersData[characterBase.characterIndex].skins[mesh.Key].originalSpriteColor);
                }
                else
                {
                    mesh.Value.enabled = false;
                }
            }
            else
            {
                mesh.Value.enabled = false;
            }
        }
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
