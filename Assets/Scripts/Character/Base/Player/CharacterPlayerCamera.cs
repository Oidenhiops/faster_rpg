using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayerCamera : MonoBehaviour
{
    [SerializeField] CinemachineCamera vcam;
    [SerializeField] float speed = 0.01f;
    private CinemachineOrbitalFollow orbital;
    public void Awake()
    {
        orbital = vcam.GetComponent<CinemachineOrbitalFollow>();
    }
    public void MoveCamera(InputAction.CallbackContext context)
    {
        orbital.HorizontalAxis.Value += context.ReadValue<Vector2>().x * speed * Time.deltaTime;
    }
}