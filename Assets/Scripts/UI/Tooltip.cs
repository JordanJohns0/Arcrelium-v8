using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Equipment;

namespace UI
{
    public class Tooltip : MonoBehaviour
    {
        private GameObject canvasRoot;
        private RectTransform panel;
        private TMP_Text headerText;
        private TMP_Text descriptionText;
        private TMP_Text cardsHeader;
        private TMP_Text cardsText;

        private static Tooltip instance;

        private void Awake()
        {
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
            canvas.sortingOrder = 200;

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
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 400);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.12f, 0.12f, 0.12f, 1);

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 6f;
            layout.padding = new RectOffset(10, 10, 10, 10);

            var headerObj = new GameObject("Header", typeof(TextMeshProUGUI));
            headerObj.transform.SetParent(panel, false);
            headerText = headerObj.GetComponent<TextMeshProUGUI>();
            headerText.fontSize = 30;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = Color.white;
            headerText.textWrappingMode = TextWrappingModes.Normal;
            headerText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 480);

            var descObj = new GameObject("Description", typeof(TextMeshProUGUI));
            descObj.transform.SetParent(panel, false);
            descriptionText = descObj.GetComponent<TextMeshProUGUI>();
            descriptionText.fontSize = 24;
            descriptionText.color = new Color(0.8f, 0.8f, 0.8f);
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 480);

            var chObj = new GameObject("CardsHeader", typeof(TextMeshProUGUI));
            chObj.transform.SetParent(panel, false);
            cardsHeader = chObj.GetComponent<TextMeshProUGUI>();
            cardsHeader.fontSize = 27;
            cardsHeader.fontStyle = FontStyles.Bold;
            cardsHeader.color = new Color(0.6f, 0.8f, 1f);
            cardsHeader.text = "Adds these cards:";
            cardsHeader.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 480);

            var cardsObj = new GameObject("CardsList", typeof(TextMeshProUGUI));
            cardsObj.transform.SetParent(panel, false);
            cardsText = cardsObj.GetComponent<TextMeshProUGUI>();
            cardsText.fontSize = 22;
            cardsText.color = new Color(0.7f, 0.7f, 0.7f);
            cardsText.textWrappingMode = TextWrappingModes.Normal;
            cardsText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 480);
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
            var obj = new GameObject("Tooltip", typeof(Tooltip));
            return instance;
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
                    sb.Append($"<b>{card.CardName}</b>\n{card.Description}");
                    if (i < data.Cards.Count - 1) sb.Append("\n\n");
                }
                tip.cardsText.text = sb.ToString();
            }
            else
            {
                tip.cardsHeader.gameObject.SetActive(false);
                tip.cardsText.text = "";
            }

            tip.panel.anchoredPosition = screenPosition + new Vector2(15, -10);
            tip.canvasRoot.SetActive(true);
        }

        public static void Hide()
        {
            if (instance != null && instance.canvasRoot != null)
                instance.canvasRoot.SetActive(false);
        }
    }
}
