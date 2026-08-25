using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Equipment;
using Cards;

namespace UI
{
    public class Tooltip : MonoBehaviour
    {
        private const float ScreenMargin = 8f;

        [Header("Prefab")]
        [SerializeField] private GameObject uiPrefab;

        [Header("Canvas")]
        [SerializeField] private int sortingOrder = 200;

        [Header("Panel")]
        [SerializeField] private Vector2 panelSize = new Vector2(500, 400);
        [SerializeField] private Color panelColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private float panelSpacing = 6f;
        [SerializeField] private int paddingLeft = 10;
        [SerializeField] private int paddingRight = 10;
        [SerializeField] private int paddingTop = 10;
        [SerializeField] private int paddingBottom = 10;

        [Header("Header")]
        [SerializeField] private int headerFontSize = 30;
        [SerializeField] private Color headerColor = Color.white;

        [Header("Description")]
        [SerializeField] private int descriptionFontSize = 24;
        [SerializeField] private Color descriptionColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        [Header("Cards Section")]
        [SerializeField] private int cardsHeaderFontSize = 27;
        [SerializeField] private Color cardsHeaderColor = new Color(0.6f, 0.8f, 1f, 1f);
        [SerializeField] private string cardsHeaderText = "Adds these cards:";
        [SerializeField] private int cardsFontSize = 22;
        [SerializeField] private Color cardsColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        [Header("Placement")]
        [SerializeField] private float textWidth = 480f;
        [SerializeField] private Vector2 screenOffset = new Vector2(15, -10);

        private GameObject canvasRoot;
        private RectTransform panel;
        private TMP_Text headerText;
        private TMP_Text descriptionText;
        private TMP_Text cardsHeader;
        private TMP_Text cardsText;

        private static Tooltip instance;
        private static GameObject staticPrefab;

        private void Awake()
        {
            if (staticPrefab == null && uiPrefab != null) staticPrefab = uiPrefab;
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            CreateUI();
            canvasRoot.SetActive(false);
        }

        private void CreateUI()
        {
            canvasRoot = new GameObject("TooltipCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);

            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var overlay = new GameObject("DismissOverlay", typeof(Image), typeof(DismissHandler));
            var overlayRt = overlay.GetComponent<RectTransform>();
            overlayRt.SetParent(canvasRoot.transform, false);
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            var overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = Color.clear;
            overlayImage.raycastTarget = true;

            panel = new GameObject("TooltipPanel", typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            panel.SetParent(canvasRoot.transform, false);
            panel.pivot = new Vector2(0, 1);
            panel.anchorMin = new Vector2(0, 0);
            panel.anchorMax = new Vector2(0, 0);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelSize.x);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelSize.y);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = panelColor;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = panelSpacing;
            layout.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);

            var headerObj = new GameObject("Header", typeof(TextMeshProUGUI));
            headerObj.transform.SetParent(panel, false);
            headerText = headerObj.GetComponent<TextMeshProUGUI>();
            headerText.fontSize = headerFontSize;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = headerColor;
            headerText.textWrappingMode = TextWrappingModes.Normal;
            headerText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);

            var descObj = new GameObject("Description", typeof(TextMeshProUGUI));
            descObj.transform.SetParent(panel, false);
            descriptionText = descObj.GetComponent<TextMeshProUGUI>();
            descriptionText.fontSize = descriptionFontSize;
            descriptionText.color = descriptionColor;
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);

            var chObj = new GameObject("CardsHeader", typeof(TextMeshProUGUI));
            chObj.transform.SetParent(panel, false);
            cardsHeader = chObj.GetComponent<TextMeshProUGUI>();
            cardsHeader.fontSize = cardsHeaderFontSize;
            cardsHeader.fontStyle = FontStyles.Bold;
            cardsHeader.color = cardsHeaderColor;
            cardsHeader.text = cardsHeaderText;
            cardsHeader.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);

            var cardsObj = new GameObject("CardsList", typeof(TextMeshProUGUI));
            cardsObj.transform.SetParent(panel, false);
            cardsText = cardsObj.GetComponent<TextMeshProUGUI>();
            cardsText.fontSize = cardsFontSize;
            cardsText.color = cardsColor;
            cardsText.textWrappingMode = TextWrappingModes.Normal;
            cardsText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private class DismissHandler : MonoBehaviour, IPointerClickHandler
        {
            public void OnPointerClick(PointerEventData eventData)
            {
                Hide();
            }
        }

        private static Tooltip GetOrCreate()
        {
            if (instance != null && instance.canvasRoot != null) return instance;
            var existing = FindAnyObjectByType<Tooltip>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }
            if (staticPrefab != null)
            {
                instance = Object.Instantiate(staticPrefab).GetComponent<Tooltip>();
            }
            else
            {
                var obj = new GameObject("Tooltip", typeof(Tooltip));
                instance = obj.GetComponent<Tooltip>();
            }
            return instance;
        }

        /// <summary>
        /// Places the panel near the requested screen point but clamps it fully on-screen
        /// (pivot is the panel's top-left), so tooltips for rows near a screen edge never
        /// run off the display.
        /// </summary>
        private void PlacePanel(Vector2 screenPosition)
        {
            Vector2 pos = screenPosition + screenOffset;
            float width = panel.rect.width;
            float height = panel.rect.height;

            float minX = ScreenMargin;
            float maxX = Screen.width - ScreenMargin - width;
            float minY = ScreenMargin + height; // pivot sits at the panel's TOP edge
            float maxY = Screen.height - ScreenMargin;

            pos.x = Mathf.Clamp(pos.x, minX, Mathf.Max(minX, maxX));
            pos.y = Mathf.Clamp(pos.y, minY, Mathf.Max(minY, maxY));
            panel.anchoredPosition = pos;
        }

        public static void Show(EquipmentData data, Vector2 screenPosition)
        {
            var tip = GetOrCreate();
            if (tip == null || tip.canvasRoot == null) return;

            tip.headerText.text = data.EquipmentName;
            tip.descriptionText.text = data.Description;

            if (data.Cards != null && data.Cards.Count > 0)
            {
                tip.cardsHeader.gameObject.SetActive(true);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < data.Cards.Count; i++)
                {
                    var card = data.Cards[i];
                    sb.Append($"<b>{card.CardName}</b> - Cost: {card.Cost}\n{card.Description}");
                    if (i < data.Cards.Count - 1) sb.Append("\n\n");
                }
                tip.cardsText.text = sb.ToString();
            }
            else
            {
                tip.cardsHeader.gameObject.SetActive(false);
                tip.cardsText.text = "";
            }

            tip.PlacePanel(screenPosition);
            tip.canvasRoot.SetActive(true);
        }

        /// <summary>
        /// Shows a card the same way cards appear inside equipment tooltips: bold name with
        /// its inline cost, description beneath. Used by right-clicking deck list entries.
        /// </summary>
        public static void ShowCard(CardData card, Vector2 screenPosition)
        {
            if (card == null) return;
            var tip = GetOrCreate();
            if (tip == null || tip.canvasRoot == null) return;

            tip.headerText.text = $"{card.CardName} - Cost: {card.Cost}";
            tip.descriptionText.text = card.Description;
            tip.cardsHeader.gameObject.SetActive(false);
            tip.cardsText.text = "";

            tip.PlacePanel(screenPosition);
            tip.canvasRoot.SetActive(true);
        }

        public static void Hide()
        {
            if (instance != null && instance.canvasRoot != null)
                instance.canvasRoot.SetActive(false);
        }
    }
}
