using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayerMovement : CharacterMovementBase
{
    public InputSystem_Actions inputActions;
    public Rigidbody rb;
    public override void HandleInitialize()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Player.Enable();
        inputActions.Player.Jump.performed += OnHandleJump;
        inputActions.Player.Dash.performed += OnHandleDash;
    }
    public override void HandleMovement()
    {
        characterBase.directionMovement = inputActions.Player.Move.ReadValue<Vector2>().normalized;
        CameraInfo.Instance.CamDirection(new Vector3(characterBase.directionMovement.x, 0, characterBase.directionMovement.y), out Vector3 directionFromCamera);
        directionFromCamera.y = rb.linearVelocity.y;
        if (!characterBase.isInCanalization)
        {
            if (characterBase.directionMovement != Vector2.zero)
            {
                if (!characterBase.isDashing && characterBase.isGrounded) characterBase.characterAnimator.SetBool("isWalking", true);
            }
            else if (!characterBase.isDashing && characterBase.isGrounded)
            {
                characterBase.characterAnimator.SetBool("isWalking", false);
            }
        }
        if (characterBase.isDashing)
        {
            if (characterBase.isInCanalization) characterBase.cancelCanalization = true;
            if (characterBase.directionMovement != Vector2.zero)
            {
                directionFromCamera.x *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4;
                directionFromCamera.z *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4;
            }
            else
            {
                if (characterBase.directionMovement != Vector2.zero)
                {
                    Vector3 launchDirection = new Vector3(characterBase.directionMovement.x, 0, characterBase.directionMovement.y);
                    CameraInfo.Instance.CamDirection(new Vector3(launchDirection.x, 0, launchDirection.z), out Vector3 directionFromCameraByAnimation);
                    directionFromCamera = directionFromCameraByAnimation * (characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4);
                }
                else
                {
                    Vector3 launchDirection = characterBase.characterModel.modelTransform.forward;
                    launchDirection.y = 0;
                    directionFromCamera = launchDirection * (characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4);
                }
            }
        }
        else if (!characterBase.isInCanalization)
        {
            directionFromCamera.x *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue;
            directionFromCamera.z *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue;
        }
        else
        {
            directionFromCamera.x = 0;
            directionFromCamera.z = 0;
        }
        characterBase.characterDirection.ChangeDirection();
        rb.linearVelocity = directionFromCamera;
    }
    void OnHandleJump(InputAction.CallbackContext context)
    {
        if (characterBase.isGrounded && !characterBase.isDashing)
        {
            _ = MakeJump();
        }
    }
    void OnHandleDash(InputAction.CallbackContext context)
    {
        if (characterBase.isGrounded && !characterBase.isDashing)
        {
            _ = MakeDash();
        }
    }

    public async Awaitable MakeDash()
    {
        characterBase.dissolvePlayer.NeedAppear();
        characterBase.isDashing = true;
        // characterBase.characterAnimations.characterAnimator.SetBool("isDashing", true);
        await Awaitable.WaitForSecondsAsync(0.1f);
        characterBase.isDashing = false;
        // characterBase.characterAnimations.characterAnimator.SetBool("isDashing", false);
    }
    public async Awaitable MakeJump()
    {
        if (characterBase.isInCanalization) characterBase.cancelCanalization = true;
        rb.AddForce(Vector3.up * 5, ForceMode.Impulse);
        await Awaitable.WaitForSecondsAsync(0.1f);
    }
}