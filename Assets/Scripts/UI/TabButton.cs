using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UI
{
    /// <summary>
    /// A single bookmark tab. Lives on the same GameObject as the tab's Button and Image.
    /// Reports clicks through <see cref="Selected"/> so the owning EquipmentUI can show the
    /// matching page, and swaps the image between its normal and selected sprite when the tab
    /// becomes selected/deselected.
    ///
    /// Setup: add this next to a bookmark Button/Image, set <see cref="pageName"/> to the page
    /// GameObject's name under the Equipment UI ("Inventory Slots", "Decklist", "Special"), and
    /// assign the tab's Normal sprite to the Image. In the editor the Selected twin is filled
    /// in automatically from the sprite name (e.g. Tab02_Right_Normal -> Tab02_Right_Selected).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TabButton : MonoBehaviour
    {
        [Tooltip("Name of the page GameObject under the Equipment UI that this tab shows.")]
        [SerializeField] private string pageName;

        [Tooltip("Image shown while this tab is NOT selected. Falls back to the Image's own sprite.")]
        [SerializeField] private Sprite normalSprite;

        [Tooltip("Image shown while this tab is selected. Auto-filled in the editor from the Normal sprite.")]
        [SerializeField] private Sprite selectedSprite;

        /// <summary>Invoked when this tab is clicked. Assigned by EquipmentUI.</summary>
        public System.Action Selected;

        /// <summary>Page GameObject name this tab activates.</summary>
        public string PageName => pageName;

        private Button button;
        private Image image;
        private bool wired;

        private void Awake()
        {
            Wire();
        }

        /// <summary>
        /// Caches the Button/Image and hooks the click. Idempotent, and safe to call on an
        /// inactive GameObject (Awake may be deferred).
        /// </summary>
        public void Wire()
        {
            if (wired) return;
            wired = true;

            button = GetComponent<Button>();
            image = button != null && button.targetGraphic != null
                ? button.targetGraphic as Image
                : GetComponent<Image>();
            if (image == null) image = GetComponent<Image>();

            if (button != null)
                button.onClick.AddListener(() => Selected?.Invoke());
        }

        /// <summary>Sets the tab to its selected or normal appearance.</summary>
        public void SetSelected(bool selected)
        {
            Wire();
            if (image == null) return;

            Sprite target = selected
                ? (selectedSprite != null ? selectedSprite : normalSprite)
                : (normalSprite != null ? normalSprite : selectedSprite);
            if (target != null) image.sprite = target;
        }

        /// <summary>Assigns the page this tab activates (used when tabs are wired in code).</summary>
        public void SetPageName(string page)
        {
            pageName = page;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Fill whichever sprite is missing from the other's name, so a tab only needs
            // one assigned sprite in the Inspector.
            if (normalSprite == null && selectedSprite != null)
                normalSprite = FindTwin(selectedSprite, "_Selected", "_Normal");
            if (selectedSprite == null && normalSprite != null)
                selectedSprite = FindTwin(normalSprite, "_Normal", "_Selected");

            if (normalSprite != null && selectedSprite != null) return;

            var img = GetComponent<Image>();
            if (img == null || img.sprite == null) return;

            if (normalSprite == null && selectedSprite == null)
            {
                if (img.sprite.name.EndsWith("_Normal"))
                {
                    normalSprite = img.sprite;
                    selectedSprite = FindTwin(img.sprite, "_Normal", "_Selected");
                }
                else if (img.sprite.name.EndsWith("_Selected"))
                {
                    selectedSprite = img.sprite;
                    normalSprite = FindTwin(img.sprite, "_Selected", "_Normal");
                }
            }
        }

        private static Sprite FindTwin(Sprite source, string from, string to)
        {
            if (source == null || string.IsNullOrEmpty(source.name) || !source.name.EndsWith(from))
                return null;
            string twin = source.name.Substring(0, source.name.Length - from.Length) + to;

            foreach (string guid in AssetDatabase.FindAssets(twin + " t:Sprite"))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null && sprite.name == twin)
                    return sprite;
            }
            return null;
        }
#endif
    }
}
