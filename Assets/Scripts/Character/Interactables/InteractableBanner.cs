using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InteractableBanner : MonoBehaviour
{
    public Image interactableIcon;
    public TMP_Text interactableTest;
    public InteractableBase interactable;
    public CharacterBase character;
    public void InitializeBanner(InteractableBase interactable)
    {
        this.interactable = interactable;
        interactableIcon.sprite = interactable.GetInteractIcon();
        interactableTest.text = interactable.GetInteractText();
    }
    public void OnClickBanner()
    {
        interactable.Interact(character);
    }
}
