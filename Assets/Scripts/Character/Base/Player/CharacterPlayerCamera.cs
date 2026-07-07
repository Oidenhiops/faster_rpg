using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayerCamera : MonoBehaviour
{
    [SerializeField] CinemachineCamera vcam;
    [SerializeField] float speed = 0.01f;
    public bool flipYAxis = false;
    private CinemachineOrbitalFollow orbital;
    public void Awake()
    {
        orbital = vcam.GetComponent<CinemachineOrbitalFollow>();
    }
    public void MoveCamera(InputAction.CallbackContext context)
    {
        orbital.HorizontalAxis.Value += context.ReadValue<Vector2>().x * speed * Time.deltaTime;
        orbital.VerticalAxis.Value += context.ReadValue<Vector2>().y * (flipYAxis ? -1 : 1) * speed * Time.deltaTime;
        orbital.VerticalAxis.Value = Mathf.Clamp(orbital.VerticalAxis.Value, orbital.VerticalAxis.Range.x, orbital.VerticalAxis.Range.y);
    }
}