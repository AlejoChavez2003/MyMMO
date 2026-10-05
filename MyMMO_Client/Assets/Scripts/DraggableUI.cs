using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableUI : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField] private RectTransform target;

    private RectTransform parentRect;
    private Vector2 pointerOffset;

    public void SetTarget(RectTransform targetRect)
    {
        target = targetRect;
        parentRect = target != null ? target.parent as RectTransform : null;
    }

    private void Awake()
    {
        if (target == null) target = transform.parent as RectTransform;
        parentRect = target != null ? target.parent as RectTransform : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (target == null || parentRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out Vector2 pointerPosition))
        {
            pointerOffset = target.anchoredPosition - pointerPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null || parentRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out Vector2 pointerPosition))
        {
            target.anchoredPosition = pointerPosition + pointerOffset;
        }
    }
}
