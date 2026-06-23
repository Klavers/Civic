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

        public bool HasTooltipView => tooltipView != null;

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
                    tooltipView?.Hide();
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isPointerInside = true;
            if (!string.IsNullOrWhiteSpace(tooltipText))
            {
                tooltipView?.Show(tooltipText, eventData.position);
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (isPointerInside && !string.IsNullOrWhiteSpace(tooltipText))
            {
                tooltipView?.Move(eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerInside = false;
            tooltipView?.Hide();
        }
    }
}
