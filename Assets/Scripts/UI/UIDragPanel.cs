using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Put on a window's header: dragging it moves the whole window (target), clamped to the screen.</summary>
public class UIDragPanel : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public RectTransform target;
    Vector2 grabOffset;
    Canvas canvas;

    void Awake() => canvas = GetComponentInParent<Canvas>();

    public void OnBeginDrag(PointerEventData e)
    {
        target.SetAsLastSibling();
        grabOffset = (Vector2)target.position - e.position;
    }

    public void OnDrag(PointerEventData e)
    {
        Vector2 pos = e.position + grabOffset;
        float s = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 half = target.rect.size * 0.5f * s;
        pos.x = Mathf.Clamp(pos.x, half.x, Screen.width - half.x);
        pos.y = Mathf.Clamp(pos.y, half.y, Screen.height - half.y);
        target.position = pos;
    }
}
