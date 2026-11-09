using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Put on a window's header: dragging it moves the whole window (target), clamped inside its parent canvas.</summary>
public class UIDragPanel : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public RectTransform target;
    Vector3 grabOffset;

    RectTransform Parent => (RectTransform)target.parent;

    public void OnBeginDrag(PointerEventData e)
    {
        target.SetAsLastSibling();
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(Parent, e.position, e.pressEventCamera, out var world))
            grabOffset = target.position - world;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(Parent, e.position, e.pressEventCamera, out var world)) return;
        target.position = world + grabOffset;

        // Keep the window fully inside its parent (window is centre-anchored).
        Vector2 half = (Parent.rect.size - target.rect.size) * 0.5f;
        Vector2 p = target.anchoredPosition;
        p.x = Mathf.Clamp(p.x, -Mathf.Max(0f, half.x), Mathf.Max(0f, half.x));
        p.y = Mathf.Clamp(p.y, -Mathf.Max(0f, half.y), Mathf.Max(0f, half.y));
        target.anchoredPosition = p;
    }
}
