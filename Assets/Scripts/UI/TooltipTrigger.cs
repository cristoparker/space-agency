using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Put on any UI element to show a tooltip with the given text while the pointer is over it.</summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string text;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipUI.Instance != null && !string.IsNullOrEmpty(text))
            TooltipUI.Instance.Show(text, (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData) => TooltipUI.Instance?.Hide();

    void OnDisable() => TooltipUI.Instance?.Hide();
}
