using UnityEngine;
using UnityEngine.EventSystems;

namespace Civic.UI
{
    public sealed class CivicTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
    {
        [SerializeField] private CivicTooltipView tooltipView;
        [TextArea]
        [SerializeField] private string tooltipText;

        private bool isPointerInside;
        private Vector2 lastPointerPosition;

        public bool HasTooltipView => tooltipView != null;
        public string TooltipText => tooltipText;

        public void AssignTooltipView(CivicTooltipView view) => tooltipView = view;

        public void SetTooltipText(string text)
        {
            tooltipText = text ?? string.Empty;
            if (!isPointerInside) return;
            if (string.IsNullOrWhiteSpace(tooltipText)) tooltipView?.Hide(this);
            else tooltipView?.RequestShow(this, tooltipText, transform as RectTransform, lastPointerPosition);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerInside = true;
            lastPointerPosition = eventData.position;
            if (!string.IsNullOrWhiteSpace(tooltipText)) tooltipView?.RequestShow(this, tooltipText, transform as RectTransform, eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
            if (isPointerInside) tooltipView?.UpdatePointer(this, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            tooltipView?.RequestHide(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Middle || string.IsNullOrWhiteSpace(tooltipText)) return;
            tooltipView?.TogglePinned(this, tooltipText, transform as RectTransform, eventData.position);
        }

        private void OnDisable()
        {
            isPointerInside = false;
            tooltipView?.Hide(this);
        }
    }
}
