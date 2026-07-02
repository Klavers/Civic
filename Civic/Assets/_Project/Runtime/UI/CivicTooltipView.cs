using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Vector2 screenOffset = new Vector2(18f, -18f);

        private object activeOwner;

        public bool HasRequiredReferences => panel != null && bodyLabel != null && DoesNotBlockRaycasts;
        public bool IsVisible => panel != null && panel.gameObject.activeSelf;
        public bool DoesNotBlockRaycasts
        {
            get
            {
                if (panel == null) return false;
                var canvasGroup = panel.GetComponent<CanvasGroup>();
                return canvasGroup != null && !canvasGroup.blocksRaycasts && !canvasGroup.interactable &&
                    panel.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget);
            }
        }

        public void Show(string text, Vector2 screenPosition)
        {
            Show(null, text, screenPosition);
        }

        public void Show(object owner, string text, Vector2 screenPosition)
        {
            if (string.IsNullOrWhiteSpace(text) || !HasRequiredReferences)
            {
                Hide(owner);
                return;
            }

            activeOwner = owner;
            bodyLabel.text = text;
            panel.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            Move(screenPosition);
        }

        public void Move(Vector2 screenPosition)
        {
            if (!HasRequiredReferences || !panel.gameObject.activeSelf)
            {
                return;
            }

            var canvas = panel.GetComponentInParent<Canvas>();
            var scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            var size = panel.rect.size * scaleFactor;
            var target = screenPosition + screenOffset;
            target.x = Mathf.Clamp(target.x, 0f, Mathf.Max(0f, Screen.width - size.x));
            target.y = Mathf.Clamp(target.y, size.y, Screen.height);
            panel.position = target;
        }

        public void Hide()
        {
            activeOwner = null;
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }

        public void Hide(object owner)
        {
            if (!ReferenceEquals(activeOwner, owner))
            {
                return;
            }

            Hide();
        }
    }
}
