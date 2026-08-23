using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cards;

namespace UI
{
    /// <summary>
    /// Hover preview for deck rows: shows the actual card(s) by instantiating the Card
    /// prefab on a top-most overlay canvas, placed beside the hovered row. Purely visual —
    /// all graphics are raycast-disabled so the preview never blocks clicks or flickers
    /// under the cursor. Show takes an owner token so one row's exit can't dismiss a
    /// preview another row just opened.
    /// </summary>
    public static class CardPreview
    {
        private const int SortingOrder = 150;
        private const float RowGap = 12f;
        private const float ScreenMargin = 8f;

        private static GameObject canvasRoot;
        private static readonly List<Object> spawned = new List<Object>();
        private static Object currentOwner;

        /// <summary>
        /// Shows <paramref name="cards"/> as instantiations of <paramref name="cardPrefab"/>,
        /// placed beside <paramref name="anchorRect"/> (its right edge by default, left edge
        /// when that would run off-screen) and vertically centred on it, so the hovered
        /// element itself stays fully visible.
        /// </summary>
        public static void Show(CardView cardPrefab, IReadOnlyList<CardData> cards, RectTransform anchorRect,
            Object owner)
        {
            if (owner == null || anchorRect == null) return;
            Hide();
            if (cardPrefab == null)
            {
                Debug.LogWarning("[DeckPreview] Card preview skipped: no card prefab was provided " +
                                 "(injection chain broken?).");
                return;
            }
            if (cards == null || cards.Count == 0) return;

            EnsureCanvas();
            if (canvasRoot == null)
            {
                Debug.LogError("[DeckPreview] Card preview skipped: overlay canvas could not be created.");
                return;
            }

            var column = new GameObject("CardPreview", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            column.transform.SetParent(canvasRoot.transform, false);
            var columnRect = (RectTransform)column.transform;

            // Anchored to the canvas origin so anchoredPosition maps 1:1 to screen pixels.
            columnRect.anchorMin = Vector2.zero;
            columnRect.anchorMax = Vector2.zero;

            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 10f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(6, 6, 6, 6);

            var fitter = column.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            int shown = 0;
            foreach (CardData data in cards)
            {
                if (data == null) continue;
                CardView view = Object.Instantiate(cardPrefab, columnRect);
                view.Setup(new CardInstance(data));
                DisableRaycasts(view.gameObject);
                spawned.Add(view);
                shown++;
            }
            spawned.Add(column);

            if (shown == 0)
            {
                Debug.LogWarning("[DeckPreview] Show aborted: every card entry was null.");
                Hide();
                return;
            }

            // Resolve layout now so we know the real size for placement.
            LayoutRebuilder.ForceRebuildLayoutImmediate(columnRect);

            // Sit beside the source box, vertically centred, never covering it. Flip to
            // its left edge when the right side would run past the screen.
            Rect box = ScreenRectOf(anchorRect);
            float centreY = box.center.y;
            bool overflowsRight = box.xMax + RowGap + columnRect.rect.width > Screen.width - ScreenMargin;
            if (overflowsRight)
            {
                columnRect.pivot = new Vector2(1f, 0.5f);
                columnRect.anchoredPosition = new Vector2(box.xMin - RowGap, centreY);
            }
            else
            {
                columnRect.pivot = new Vector2(0f, 0.5f);
                columnRect.anchoredPosition = new Vector2(box.xMax + RowGap, centreY);
            }

            currentOwner = owner;
        }

        private static Rect ScreenRectOf(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, topRight.x), Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x), Mathf.Max(bottomLeft.y, topRight.y));
        }

        /// <summary>Hides the preview only if <paramref name="owner"/> is still the one showing it.</summary>
        public static void Hide(Object owner)
        {
            if (!ReferenceEquals(currentOwner, owner)) return;
            Hide();
        }

        public static void Hide()
        {
            currentOwner = null;
            foreach (Object obj in spawned)
            {
                if (obj is GameObject go) DestroyPreviewObject(go);
                else if (obj is Component c) DestroyPreviewObject(c.gameObject);
            }
            spawned.Clear();

            if (canvasRoot != null) canvasRoot.SetActive(false);
        }

        private static void DestroyPreviewObject(Object obj)
        {
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        private static void EnsureCanvas()
        {
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(true);
                return;
            }

            canvasRoot = new GameObject("CardPreviewCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
        }

        private static void DisableRaycasts(GameObject root)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
    }
}
