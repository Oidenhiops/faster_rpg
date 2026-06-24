using UnityEngine;

public class CharacterDirection : MonoBehaviour
{
    public CharacterBase characterBase;
    [SerializeField] Vector3 direction;
    public void ChangeDirection()
    {
        if (characterBase.directionMovement != Vector2.zero)
        {
            direction = new Vector3(characterBase.directionMovement.x, 0, characterBase.directionMovement.y);
            characterBase.characterModel.modelTransform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
