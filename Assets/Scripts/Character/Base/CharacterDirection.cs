using UnityEngine;

public class CharacterDirection : MonoBehaviour
{
    public CharacterBase characterBase;
    [SerializeField] Transform cameraTransform;
    [SerializeField] float rotationSpeed = 10f;
    [SerializeField] Vector3 direction;

    public void ChangeDirection()
    {
        if (characterBase.directionMovement != Vector2.zero)
        {
            CameraInfo.Instance.CamDirection(
                new Vector3(characterBase.directionMovement.x, 0f, characterBase.directionMovement.y),
                out direction
            );
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                characterBase.characterModel.modelTransform.rotation = Quaternion.Slerp(
                    characterBase.characterModel.modelTransform.rotation,
                    targetRot,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }
}