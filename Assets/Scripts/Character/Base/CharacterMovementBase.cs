using UnityEngine;

public class CharacterMovementBase : MonoBehaviour
{
    public CharacterBase characterBase;
    public void Awake()
    {
        HandleAwake();
    }
    public virtual void HandleAwake(){}
    public virtual void HandleMovement(){}
}
