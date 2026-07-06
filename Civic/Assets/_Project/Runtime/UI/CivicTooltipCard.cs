using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicTooltipCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Image background;
        [SerializeField] private Outline outline;
        [SerializeField] private TextMeshProUGUI bodyLabel;
        [SerializeField] private TextMeshProUGUI footerLabel;

        private CivicTooltipView owner;
        private int depth;

        public bool HasRequiredReferences => panel != null && background != null && outline != null && bodyLabel != null && footerLabel != null;
        public RectTransform Panel => panel;
        public Image Background => background;
        public Outline Outline => outline;
        public TextMeshProUGUI BodyLabel => bodyLabel;
        public TextMeshProUGUI FooterLabel => footerLabel;

        public void Bind(CivicTooltipView view, int cardDepth)
        {
            owner = view;
            depth = cardDepth;
        }

        public void SetContent(string body, string footer)
        {
            bodyLabel.text = body ?? string.Empty;
            footerLabel.text = footer ?? string.Empty;
            footerLabel.gameObject.SetActive(!string.IsNullOrEmpty(footer));
        }

        public void OnPointerEnter(PointerEventData eventData) => owner?.NotifyCardEntered(depth);
        public void OnPointerExit(PointerEventData eventData) => owner?.NotifyCardExited(depth);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle) owner?.TogglePinnedFromCard(depth);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (owner == null) return;
            var link = FindLink(bodyLabel, eventData);
            if (string.IsNullOrEmpty(link) && footerLabel.gameObject.activeSelf) link = FindLink(footerLabel, eventData);
            owner.NotifyLinkHover(depth, link);
        }

        public void SetPinVisual(Color color, bool pinned)
        {
            if (outline == null) return;
            outline.effectColor = color;
            outline.effectDistance = pinned ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        }

        private static string FindLink(TMP_Text label, PointerEventData eventData)
        {
            var linkIndex = TMP_TextUtilities.FindIntersectingLink(label, eventData.position, eventData.pressEventCamera);
            return linkIndex < 0 ? string.Empty : label.textInfo.linkInfo[linkIndex].GetLinkID();
        }
    }
}
