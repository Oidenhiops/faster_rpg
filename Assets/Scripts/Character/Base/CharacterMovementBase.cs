using UnityEngine;

public class CharacterMovementBase : MonoBehaviour
{
    public CharacterBase characterBase;
    public virtual void HandleInitialize(){}
    public virtual void HandleMovement(){}
}
