using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterPlayerCamera : MonoBehaviour
{
    [Header("Pivote de cámara")]
    [Tooltip("Transform que rota con el mouse. Debe ser el Tracking Target de la cámara (Third Person Follow). Mantenlo FUERA de la jerarquía del jugador para evitar acoplamiento.")]
    [SerializeField] Transform pivot;
    [SerializeField] float speed = 0.1f;
    public bool flipYAxis = false;
    [Header("Límites de inclinación (pitch)")]
    [SerializeField] float minPitch = -40f;
    [SerializeField] float maxPitch = 70f;
    [Header("Jugador")]
    [Tooltip("Raíz del personaje que se orienta según la cámara.")]
    [SerializeField] Transform player;
    [SerializeField] public bool rotatePlayerWithCamera = true;
    [SerializeField] float playerRotationSpeed = 12f;
    float yaw;
    float pitch;
    public bool isRotatingCamera = false;
    public float awaitForFreeRotate;
    public Coroutine awaitForFreeRotateCoroutine;
    public void Awake()
    {
        if (pivot != null)
        {
            Vector3 e = pivot.eulerAngles;
            yaw = e.y;
            pitch = e.x;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    private void LateUpdate()
    {
        HandlePlayerRotation();
    }

    private void HandlePlayerRotation()
    {
        if (!rotatePlayerWithCamera || player == null || pivot == null || !isRotatingCamera)
            return;

        Quaternion target = Quaternion.Euler(0f, pivot.eulerAngles.y, 0f);
        player.rotation = Quaternion.Slerp(
            player.rotation, target, playerRotationSpeed * Time.deltaTime);
    }
    public void OnHandleSetFreeCamera(InputAction.CallbackContext context)
    {
        rotatePlayerWithCamera = !rotatePlayerWithCamera;
    }
    public void OnHandleMoveCamera(InputAction.CallbackContext context)
    {
        if (context.performed || context.started)
        {
            if (awaitForFreeRotateCoroutine != null) StopCoroutine(awaitForFreeRotateCoroutine);
            isRotatingCamera = true;
            if (pivot == null)
                return;

            Vector2 look = context.ReadValue<Vector2>();
            yaw += look.x * speed;
            pitch += look.y * (flipYAxis ? 1 : -1) * speed;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            pivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        else
        {
            ResetAwaitForFreeRotate();
        }
    }
    public void ResetAwaitForFreeRotate()
    {
        if (awaitForFreeRotateCoroutine == null) awaitForFreeRotateCoroutine = StartCoroutine(AwaitForFreeRotate());
        else
        {
            StopCoroutine(awaitForFreeRotateCoroutine);
            awaitForFreeRotateCoroutine = StartCoroutine(AwaitForFreeRotate());
        }
    }
    public IEnumerator AwaitForFreeRotate()
    {
        yield return new WaitForSeconds(awaitForFreeRotate);
        isRotatingCamera = false;
    }
}
