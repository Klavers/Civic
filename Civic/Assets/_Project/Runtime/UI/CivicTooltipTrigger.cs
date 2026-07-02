using UnityEngine;
using UnityEngine.EventSystems;

namespace Civic.UI
{
    public sealed class CivicTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private CivicTooltipView tooltipView;
        [TextArea]
        [SerializeField] private string tooltipText;

        private bool isPointerInside;
        private Vector2 lastPointerPosition;

        public bool HasTooltipView => tooltipView != null;
        public string TooltipText => tooltipText;

        public void AssignTooltipView(CivicTooltipView view)
        {
            tooltipView = view;
        }

        public void SetTooltipText(string text)
        {
            tooltipText = text ?? string.Empty;
            if (isPointerInside)
            {
                if (string.IsNullOrWhiteSpace(tooltipText))
                {
                    tooltipView?.Hide(this);
                }
                else
                {
                    tooltipView?.Show(this, tooltipText, lastPointerPosition);
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerInside = true;
            lastPointerPosition = eventData.position;
            if (!string.IsNullOrWhiteSpace(tooltipText))
            {
                tooltipView?.Show(this, tooltipText, eventData.position);
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
            if (isPointerInside && !string.IsNullOrWhiteSpace(tooltipText))
            {
                tooltipView?.Move(eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            tooltipView?.Hide(this);
        }

        private void OnDisable()
        {
            isPointerInside = false;
            tooltipView?.Hide(this);
        }
    }
}
