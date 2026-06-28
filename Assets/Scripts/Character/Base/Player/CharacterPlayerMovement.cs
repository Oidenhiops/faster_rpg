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
        inputActions.Player.Run.performed += OnHandleRun;
        inputActions.Player.Run.canceled += OnHandleRun;
    }
    public override void HandleMovement()
    {
        characterBase.directionMovement = inputActions.Player.Move.ReadValue<Vector2>().normalized;
        CameraInfo.Instance.CamDirection(new Vector3(characterBase.directionMovement.x, 0, characterBase.directionMovement.y), out Vector3 directionFromCamera);
        directionFromCamera.y = rb.linearVelocity.y;
        if (characterBase.isDashing)
        {
            if (characterBase.isInCanalization) characterBase.cancelCanalization = true;
            if (characterBase.directionMovement != Vector2.zero)
            {
                directionFromCamera.x *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4 * (characterBase.isRunning ? 1.5f : 1);
                directionFromCamera.z *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4 * (characterBase.isRunning ? 1.5f : 1);
            }
            else
            {
                Vector3 launchDirection = characterBase.characterModel.modelTransform.forward;
                launchDirection.y = 0;
                directionFromCamera = launchDirection * (characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * 4);
            }
        }
        else if (!characterBase.isInCanalization)
        {
            directionFromCamera.x *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * (characterBase.isRunning ? 1.5f : 1);
            directionFromCamera.z *= characterBase.charactersData[characterBase.characterIndex].statistics[CharacterData.TypeStatistic.Spd].currentValue * (characterBase.isRunning ? 1.5f : 1);
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
    void OnHandleRun(InputAction.CallbackContext context)
    {
        if (context.performed && characterBase.isGrounded && !characterBase.isDashing)
        {
            characterBase.isRunning = true;
        }
        else if (context.canceled)
        {
            characterBase.isRunning = false;
        }
    }

    public async Awaitable MakeDash()
    {
        characterBase.dissolvePlayer.NeedAppear();
        characterBase.isDashing = true;
        await Awaitable.WaitForSecondsAsync(0.1f);
        characterBase.isDashing = false;
    }
    public async Awaitable MakeJump()
    {
        if (characterBase.isInCanalization) characterBase.cancelCanalization = true;
        rb.AddForce(Vector3.up * 5, ForceMode.Impulse);
        await Awaitable.WaitForSecondsAsync(0.1f);
    }
}