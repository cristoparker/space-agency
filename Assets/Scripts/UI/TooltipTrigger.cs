using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Put on any UI element to show a tooltip with the given text while the pointer is over it.</summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [TextArea] public string text;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipUI.Instance != null && !string.IsNullOrEmpty(text))
            TooltipUI.Instance.Show(text, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData) => TooltipUI.Instance?.Hide();

    public void OnPointerDown(PointerEventData eventData) => TooltipUI.Instance?.Hide();      // a click means the player knows what it is: get out of the way

    void OnDisable() => TooltipUI.Instance?.Hide();
}
