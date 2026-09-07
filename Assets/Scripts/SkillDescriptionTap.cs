using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SkillDescriptionTap : MonoBehaviour, IPointerClickHandler
{
    public System.Action Open;
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Open?.Invoke();
        eventData.Use();
    }
}
