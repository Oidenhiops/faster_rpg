using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayerMovement : CharacterMovementBase
{
    public InputSystem_Actions inputActions;
    public Rigidbody rb;
    public Vector2 inputsDirection;
    public override void HandleAwake()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Player.Enable();
        inputActions.Player.Jump.performed += OnHandleJump;
        inputActions.Player.Dash.performed += OnHandleDash;
    }
    public override void HandleMovement()
    {
        inputsDirection = inputActions.Player.Move.ReadValue<Vector2>().normalized;
        CameraInfo.Instance.CamDirection(new Vector3(inputsDirection.x, 0, inputsDirection.y), out Vector3 directionFromCamera);
        directionFromCamera.y = rb.linearVelocity.y;
        if (!characterBase.isInCanalization)
        {
            if (inputsDirection != Vector2.zero)
            {
                if (characterBase.characterAnimations.characterAnimationsSO.isEightDirections)
                {
                    characterBase.directionAnimation.x = Mathf.RoundToInt(inputsDirection.x);
                    characterBase.directionAnimation.z = Mathf.RoundToInt(inputsDirection.y);
                }
                else
                {
                    if (inputsDirection.x > 0)
                    {
                        characterBase.directionAnimation.x = 1;
                    }
                    else if (inputsDirection.x < 0)
                    {
                        characterBase.directionAnimation.x = -1;
                    }
                    if (inputsDirection.y > 0)
                    {
                        characterBase.directionAnimation.z = 1;
                    }
                    else if (inputsDirection.y < 0)
                    {
                        characterBase.directionAnimation.z = -1;
                    }
                }
                if (!characterBase.isDashing && !characterBase.isJumping) characterBase.characterAnimations.MakeAnimation("Walk");
            }
            else if (!characterBase.isDashing && !characterBase.isJumping)
            {
                characterBase.characterAnimations.MakeAnimation("Idle");
            }
        }
        if (characterBase.isDashing)
        {
            if (characterBase.isInCanalization) characterBase.cancelCanalization = true;
            if (inputsDirection != Vector2.zero)
            {
                directionFromCamera.x *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4;
                directionFromCamera.z *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4;
            }
            else
            {
                CameraInfo.Instance.CamDirection(new Vector3(characterBase.directionAnimation.x, 0, characterBase.directionAnimation.z), out Vector3 directionFromCameraByAnimation);
                directionFromCamera = directionFromCameraByAnimation * (characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4);
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
        characterBase.dissolve.NeedAppear();
        characterBase.isDashing = true;
        characterBase.characterAnimations.MakeAnimation("Dash");
        await Awaitable.WaitForSecondsAsync(0.1f);
        characterBase.isDashing = false;
        characterBase.characterAnimations.MakeAnimation("Idle");
    }
    public async Awaitable MakeJump()
    {
        if (characterBase.isInCanalization) characterBase.cancelCanalization = true;
        characterBase.isJumping = true;
        characterBase.characterAnimations.MakeAnimation("Jump");
        rb.AddForce(Vector3.up * 5, ForceMode.Impulse);
        await Awaitable.WaitForSecondsAsync(0.1f);
        while (characterBase.characterAnimations.name == "Jump")
        {
            await Awaitable.NextFrameAsync();
        }
        while (!characterBase.isGrounded )
        {
            await Awaitable.NextFrameAsync();
        }
        characterBase.isJumping = false;
        characterBase.characterAnimations.MakeAnimation("Idle");
    }
}