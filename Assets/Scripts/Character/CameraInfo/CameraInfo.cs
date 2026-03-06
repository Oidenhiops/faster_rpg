using UnityEngine;

public class CameraInfo : MonoBehaviour
{
    public static CameraInfo Instance { get; private set; }
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    public void CamDirection(Vector3 direction, out Vector3 directionFromCamera)
    {
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();
        Vector3 camRelativeDir = (direction.x * right + direction.z * forward).normalized;
        directionFromCamera = new Vector3(camRelativeDir.x, 0, camRelativeDir.z);
    }
}
