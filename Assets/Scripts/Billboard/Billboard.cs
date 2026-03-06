using UnityEngine;

public class Billboard : MonoBehaviour
{
    [Header("Settings")]
    public Vector3 offsetRotation;
    [Range(1f, 20f)]
    public float smoothSpeed = 10f;

    private void LateUpdate()
    {
        if (!Camera.main) return;

        Quaternion targetRotation =
            Camera.main.transform.rotation *
            Quaternion.Euler(offsetRotation);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            smoothSpeed * Time.deltaTime
        );
    }
}
