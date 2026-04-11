using UnityEngine;

public class InteractableBase : MonoBehaviour
{
    public virtual void Interact(CharacterBase character) { Debug.LogError("Interact method not implemented in " + gameObject.name); }
    public virtual Sprite GetInteractIcon() { Debug.LogError("GetInteractIcon method not implemented in " + gameObject.name);  return null; }
    public virtual string GetInteractText() { Debug.LogError("GetInteractText method not implemented in " + gameObject.name);  return ""; }
}
