using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayerCamera : MonoBehaviour
{
    [SerializeField] CinemachineCamera vcam;
    [SerializeField] float speed = 0.01f;
    public InputSystem_Actions inputActions;
    private CinemachineOrbitalFollow orbital;
    public void Awake()
    {
        orbital = vcam.GetComponent<CinemachineOrbitalFollow>();
        inputActions = new InputSystem_Actions();
        inputActions.Player.Enable();
        inputActions.Player.MoveCamera.performed += MoveCamera;
    }
    public void MoveCamera(InputAction.CallbackContext context)
    {
        if (inputActions.Player.UnlockCamera.ReadValue<float>() == 1)
        {
            orbital.HorizontalAxis.Value += context.ReadValue<Vector2>().x * speed * Time.deltaTime;
        }
    }
}