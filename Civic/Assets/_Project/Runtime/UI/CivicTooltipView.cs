using System;
using System.Collections.Generic;
using System.Linq;
using Civic.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Civic.UI
{
    public sealed class CivicTooltipView : MonoBehaviour
    {
        public const int MaximumDepth = 12;

        [SerializeField] private Canvas tooltipCanvas;
        [SerializeField] private CivicTooltipCard[] cards;
        [SerializeField] private float showDelaySeconds = 0.25f;
        [SerializeField] private float movementGraceSeconds = 0.25f;
        [SerializeField] private float pinDelaySeconds = 1.5f;
        [SerializeField] private float stationaryPixelThreshold = 4f;
        [SerializeField] private Vector2 screenOffset = new Vector2(14f, -8f);
        [SerializeField] private Color neutralOutlineColor = new Color(0.30f, 0.38f, 0.48f, 0.55f);
        [SerializeField] private Color pendingPinOutlineColor = new Color(0.90f, 0.68f, 0.24f, 0.95f);
        [SerializeField] private Color pinnedOutlineColor = new Color(0.35f, 0.76f, 1f, 1f);

        private readonly List<TooltipNode> stack = new List<TooltipNode>();
        private object activeOwner;
        private string activeText;
        private RectTransform activeSource;
        private bool activeOwnerPointerInside;
        private object pendingOwner;
        private string pendingText;
        private RectTransform pendingSource;
        private Vector2 pendingPointer;
        private float pendingSince;
        private float hideAt = -1f;
        private int pendingLinkDepth = -1;
        private string pendingLinkId;
        private float pendingLinkSince;
        private int pendingCloseDepth = -1;
        private float pendingCloseAt = -1f;
        private int hoveredCardDepth = -1;
        private float pinCandidateSince = -1f;
        private bool isPinned;
        private bool suppressAutoPinUntilExit;

        public bool HasRequiredReferences => tooltipCanvas != null && cards != null && cards.Length == MaximumDepth && cards.All(item => item != null && item.HasRequiredReferences);
        public bool IsVisible => cards != null && cards.Any(item => item != null && item.gameObject.activeSelf);
        public bool IsPinned => isPinned;
        public bool DoesNotBlockRaycasts => cards != null && cards.All(item => item != null && item.Background != null && !item.Background.raycastTarget);
        public float ShowDelaySeconds => showDelaySeconds;
        public float MovementGraceSeconds => movementGraceSeconds;
        public float PinDelaySeconds => pinDelaySeconds;
        public IReadOnlyList<CivicTooltipCard> Cards => cards ?? Array.Empty<CivicTooltipCard>();

        private void Awake()
        {
            InitializeCards();
        }

        private void OnEnable()
        {
            InitializeCards();
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (pendingOwner != null && now - pendingSince >= showDelaySeconds)
            {
                ShowImmediate(pendingOwner, pendingText, pendingSource, pendingPointer);
                ClearPendingRoot();
            }
            if (pendingLinkDepth >= 0 && !string.IsNullOrEmpty(pendingLinkId) && now - pendingLinkSince >= showDelaySeconds)
            {
                OpenLinkedTooltip(pendingLinkDepth, pendingLinkId);
                ClearPendingLink();
            }
            if (!isPinned && pendingCloseDepth >= 0 && pendingCloseAt >= 0f && now >= pendingCloseAt)
            {
                CloseAfter(pendingCloseDepth);
                ClearPendingClose();
            }
            if (hideAt >= 0f && now >= hideAt) Hide();
            UpdatePinState(now);
            HandlePinnedOutsideClick();
        }

        public void Show(string text, Vector2 screenPosition) => Show(null, text, screenPosition);

        public void Show(object owner, string text, Vector2 screenPosition)
        {
            ShowImmediate(owner, text, null, screenPosition);
        }

        public void RequestShow(object owner, string text, RectTransform source, Vector2 screenPosition)
        {
            if (string.IsNullOrWhiteSpace(text) || !HasRequiredReferences)
            {
                Hide(owner);
                return;
            }
            if (isPinned && !ReferenceEquals(activeOwner, owner)) return;
            if (ReferenceEquals(activeOwner, owner))
            {
                activeOwnerPointerInside = true;
                hideAt = -1f;
                if (!string.Equals(activeText, text, StringComparison.Ordinal))
                {
                    activeText = text;
                    activeSource = source;
                    stack.Clear();
                    var node = TooltipNode.Create(text, false, string.Empty);
                    stack.Add(node);
                    RenderCard(0, node, source, screenPosition);
                    CloseAfter(0);
                }
                BeginPinCandidate();
                return;
            }
            if (ReferenceEquals(pendingOwner, owner) && string.Equals(pendingText, text, StringComparison.Ordinal)) return;
            hideAt = -1f;
            pendingOwner = owner;
            pendingText = text;
            pendingSource = source;
            pendingPointer = screenPosition;
            pendingSince = Time.unscaledTime;
        }

        public void UpdatePointer(object owner, Vector2 screenPosition)
        {
            if (ReferenceEquals(owner, activeOwner)) activeOwnerPointerInside = true;
            if (ReferenceEquals(owner, pendingOwner) && (screenPosition - pendingPointer).sqrMagnitude > stationaryPixelThreshold * stationaryPixelThreshold)
            {
                pendingSince = Time.unscaledTime;
            }
            if (ReferenceEquals(owner, pendingOwner)) pendingPointer = screenPosition;
        }

        public void RequestHide(object owner)
        {
            if (ReferenceEquals(owner, pendingOwner)) ClearPendingRoot();
            if (!ReferenceEquals(owner, activeOwner)) return;
            activeOwnerPointerInside = false;
            if (isPinned) return;
            hideAt = Time.unscaledTime + movementGraceSeconds;
        }

        public void Move(Vector2 screenPosition)
        {
            pendingPointer = screenPosition;
        }

        public void Hide()
        {
            activeOwner = null;
            activeText = null;
            activeSource = null;
            activeOwnerPointerInside = false;
            hideAt = -1f;
            hoveredCardDepth = -1;
            pinCandidateSince = -1f;
            isPinned = false;
            suppressAutoPinUntilExit = false;
            ClearPendingRoot();
            ClearPendingLink();
            ClearPendingClose();
            stack.Clear();
            if (cards == null) return;
            foreach (var card in cards)
            {
                if (card == null) continue;
                card.SetPinVisual(neutralOutlineColor, false);
                card.gameObject.SetActive(false);
            }
        }

        public void Hide(object owner)
        {
            if (ReferenceEquals(owner, pendingOwner)) ClearPendingRoot();
            if (owner != null && !ReferenceEquals(activeOwner, owner)) return;
            Hide();
        }

        internal void NotifyCardEntered(int depth)
        {
            hoveredCardDepth = depth;
            hideAt = -1f;
            ClearPendingClose();
            BeginPinCandidate();
        }

        internal void NotifyCardExited(int depth)
        {
            if (hoveredCardDepth == depth) hoveredCardDepth = -1;
            ClearPendingLink();
            if (isPinned) return;
            if (depth > 0) ScheduleCloseAfter(depth - 1);
            hideAt = Time.unscaledTime + movementGraceSeconds;
        }

        internal void NotifyLinkHover(int depth, string linkId)
        {
            hideAt = -1f;
            if (string.IsNullOrEmpty(linkId))
            {
                ClearPendingLink();
                if (!isPinned) ScheduleCloseAfter(depth);
                return;
            }
            ClearPendingClose();
            BeginPinCandidate();
            if (stack.Count > depth + 1 && string.Equals(stack[depth + 1].SourceLinkId, linkId, StringComparison.Ordinal)) return;
            if (pendingLinkDepth == depth && pendingLinkId == linkId) return;
            pendingLinkDepth = depth;
            pendingLinkId = linkId;
            pendingLinkSince = Time.unscaledTime;
        }

        public void TogglePinned(object owner, string text, RectTransform source, Vector2 screenPosition)
        {
            if (isPinned && ReferenceEquals(activeOwner, owner))
            {
                Unpin();
                return;
            }

            if (!ReferenceEquals(activeOwner, owner) || !IsVisible) ShowImmediate(owner, text, source, screenPosition);
            Pin();
        }

        internal void TogglePinnedFromCard(int depth)
        {
            if (depth < 0 || depth >= stack.Count) return;
            if (isPinned) Unpin();
            else Pin();
        }

        public bool TryDismissPinned()
        {
            if (!isPinned) return false;
            Hide();
            return true;
        }

        private void InitializeCards()
        {
            if (cards == null) return;
            for (var index = 0; index < cards.Length; index++)
            {
                if (cards[index] == null) continue;
                cards[index].Bind(this, index);
                cards[index].SetPinVisual(neutralOutlineColor, false);
                cards[index].gameObject.SetActive(false);
            }
        }

        private void ShowImmediate(object owner, string text, RectTransform source, Vector2 screenPosition)
        {
            if (string.IsNullOrWhiteSpace(text) || !HasRequiredReferences)
            {
                Hide(owner);
                return;
            }
            Hide();
            activeOwner = owner;
            activeText = text;
            activeSource = source;
            activeOwnerPointerInside = owner != null;
            var node = TooltipNode.Create(text, false, string.Empty);
            stack.Add(node);
            RenderCard(0, node, source, screenPosition);
            BeginPinCandidate();
        }

        private void OpenLinkedTooltip(int parentDepth, string linkId)
        {
            if (parentDepth < 0 || parentDepth >= stack.Count) return;
            if (parentDepth + 1 >= MaximumDepth)
            {
                var parent = cards[parentDepth];
                parent.FooterLabel.gameObject.SetActive(true);
                parent.FooterLabel.text = CivicLocalizationService.Catalog.Resolve("tooltip.depth_limit", null, true);
                return;
            }

            string text;
            var expanded = false;
            if (linkId.StartsWith("continue:", StringComparison.Ordinal))
            {
                if (!int.TryParse(linkId.Substring("continue:".Length), out var page) || page < 0 || page >= stack[parentDepth].Pages.Count) return;
                text = string.Join("\n", stack[parentDepth].Pages.Skip(page));
                expanded = true;
            }
            else
            {
                text = CivicLocalizationService.Catalog.ConceptDescription(linkId, true);
            }

            CloseAfter(parentDepth);
            var node = TooltipNode.Create(text, expanded, linkId);
            stack.Add(node);
            var parentCard = cards[parentDepth];
            RenderCard(parentDepth + 1, node, parentCard.Panel, parentCard.Panel.position);
        }

        private void RenderCard(int depth, TooltipNode node, RectTransform source, Vector2 fallbackPosition)
        {
            var card = cards[depth];
            var page = node.Pages[0];
            var footer = node.Pages.Count > 1
                ? $"<link=\"continue:1\"><u>{CivicLocalizationService.Catalog.Resolve("tooltip.continue", null, false)}</u></link>"
                : string.Empty;
            card.SetContent(page, footer);
            card.gameObject.SetActive(true);
            card.transform.SetAsLastSibling();
            card.SetPinVisual(isPinned ? pinnedOutlineColor : neutralOutlineColor, isPinned);
            Resize(card, node.Expanded);
            Position(card, depth, source, fallbackPosition);
        }

        private void Resize(CivicTooltipCard card, bool expanded)
        {
            var safeWidth = Screen.safeArea.width / Mathf.Max(0.01f, tooltipCanvas.scaleFactor);
            var safeHeight = Screen.safeArea.height / Mathf.Max(0.01f, tooltipCanvas.scaleFactor);
            var maxWidth = Mathf.Min(expanded ? 760f : 560f, safeWidth * (expanded ? 0.70f : 0.42f));
            var minWidth = Mathf.Min(360f, maxWidth);
            var unconstrained = card.BodyLabel.GetPreferredValues(card.BodyLabel.text);
            var width = Mathf.Clamp(unconstrained.x + 28f, minWidth, maxWidth);
            var body = card.BodyLabel.GetPreferredValues(card.BodyLabel.text, Mathf.Max(40f, width - 28f), 0f);
            var footerHeight = card.FooterLabel.gameObject.activeSelf ? 30f : 0f;
            var height = Mathf.Min(body.y + 24f + footerHeight, safeHeight * 0.82f);
            card.Panel.sizeDelta = new Vector2(width, Mathf.Max(58f, height));
            card.BodyLabel.rectTransform.offsetMin = new Vector2(14f, 12f + footerHeight);
            card.BodyLabel.rectTransform.offsetMax = new Vector2(-14f, -12f);
            card.FooterLabel.rectTransform.offsetMin = new Vector2(14f, 8f);
            card.FooterLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card.Panel);
        }

        private void Position(CivicTooltipCard card, int depth, RectTransform source, Vector2 fallbackPosition)
        {
            var scale = Mathf.Max(0.01f, tooltipCanvas.scaleFactor);
            var safe = Screen.safeArea;
            var size = card.Panel.rect.size * scale;
            Vector2 target;
            if (depth > 0 && source != null)
            {
                var sourceSize = source.rect.size * scale;
                var right = new Vector2(source.position.x + sourceSize.x + 12f, source.position.y);
                var left = new Vector2(source.position.x - size.x - 12f, source.position.y);
                target = right.x + size.x <= safe.xMax ? right : left.x >= safe.xMin ? left : right;
            }
            else if (source != null)
            {
                var corners = new Vector3[4];
                source.GetWorldCorners(corners);
                target = new Vector2(corners[2].x + screenOffset.x, corners[2].y + screenOffset.y);
            }
            else
            {
                target = fallbackPosition + screenOffset;
            }
            target.x = Mathf.Clamp(target.x, safe.xMin, Mathf.Max(safe.xMin, safe.xMax - size.x));
            target.y = Mathf.Clamp(target.y, safe.yMin + size.y, safe.yMax);
            card.Panel.position = target;
        }

        private void CloseAfter(int depth)
        {
            while (stack.Count > depth + 1) stack.RemoveAt(stack.Count - 1);
            if (cards == null) return;
            for (var index = depth + 1; index < cards.Length; index++) if (cards[index] != null) cards[index].gameObject.SetActive(false);
        }

        private void ScheduleCloseAfter(int depth)
        {
            if (depth < 0 || isPinned) return;
            pendingCloseDepth = depth;
            pendingCloseAt = Time.unscaledTime + movementGraceSeconds;
        }

        private void ClearPendingClose()
        {
            pendingCloseDepth = -1;
            pendingCloseAt = -1f;
        }

        private void BeginPinCandidate()
        {
            if (isPinned || suppressAutoPinUntilExit || !IsVisible || (!activeOwnerPointerInside && hoveredCardDepth < 0)) return;
            if (pinCandidateSince < 0f) pinCandidateSince = Time.unscaledTime;
        }

        private void UpdatePinState(float now)
        {
            if (!IsVisible) return;
            if (isPinned)
            {
                ApplyPinVisual(1f, true);
                return;
            }

            var pointerEngaged = activeOwnerPointerInside || hoveredCardDepth >= 0;
            if (suppressAutoPinUntilExit)
            {
                if (!pointerEngaged)
                {
                    suppressAutoPinUntilExit = false;
                    pinCandidateSince = -1f;
                }
                ApplyPinVisual(0f, false);
                return;
            }

            if (!pointerEngaged) return;
            BeginPinCandidate();
            var progress = pinCandidateSince < 0f ? 0f : Mathf.Clamp01((now - pinCandidateSince) / Mathf.Max(0.01f, pinDelaySeconds));
            ApplyPinVisual(progress, false);
            if (progress >= 1f) Pin();
        }

        private void Pin()
        {
            if (!IsVisible) return;
            isPinned = true;
            suppressAutoPinUntilExit = false;
            hideAt = -1f;
            ClearPendingClose();
            ClearPendingLink();
            ApplyPinVisual(1f, true);
        }

        private void Unpin()
        {
            if (!isPinned) return;
            isPinned = false;
            suppressAutoPinUntilExit = true;
            pinCandidateSince = -1f;
            ApplyPinVisual(0f, false);
            if (!activeOwnerPointerInside && hoveredCardDepth < 0) hideAt = Time.unscaledTime + movementGraceSeconds;
        }

        private void ApplyPinVisual(float progress, bool pinned)
        {
            if (cards == null) return;
            var color = pinned ? pinnedOutlineColor : Color.Lerp(neutralOutlineColor, pendingPinOutlineColor, Mathf.Clamp01(progress));
            foreach (var card in cards)
            {
                if (card != null && card.gameObject.activeSelf) card.SetPinVisual(color, pinned);
            }
        }

        private void HandlePinnedOutsideClick()
        {
            var mouse = Mouse.current;
            if (!isPinned || mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var position = mouse.position.ReadValue();
            if (activeSource != null && RectTransformUtility.RectangleContainsScreenPoint(activeSource, position, null)) return;
            if (cards != null && cards.Any(card => card != null && card.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(card.Panel, position, tooltipCanvas.worldCamera))) return;
            Hide();
        }

        private void ClearPendingRoot()
        {
            pendingOwner = null;
            pendingText = null;
            pendingSource = null;
        }

        private void ClearPendingLink()
        {
            pendingLinkDepth = -1;
            pendingLinkId = null;
        }

        private sealed class TooltipNode
        {
            private TooltipNode(IReadOnlyList<string> pages, bool expanded, string sourceLinkId)
            {
                Pages = pages;
                Expanded = expanded;
                SourceLinkId = sourceLinkId ?? string.Empty;
            }
            public IReadOnlyList<string> Pages { get; }
            public bool Expanded { get; }
            public string SourceLinkId { get; }

            public static TooltipNode Create(string text, bool expanded, string sourceLinkId)
            {
                var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                var pages = new List<string>();
                var current = new List<string>();
                var characters = 0;
                foreach (var line in lines)
                {
                    if (current.Count >= 10 || characters + line.Length > 900)
                    {
                        pages.Add(string.Join("\n", current));
                        current.Clear();
                        characters = 0;
                    }
                    current.Add(line);
                    characters += line.Length + 1;
                }
                if (current.Count > 0) pages.Add(string.Join("\n", current));
                if (pages.Count == 0) pages.Add(string.Empty);
                return new TooltipNode(pages, expanded, sourceLinkId);
            }
        }
    }
}
