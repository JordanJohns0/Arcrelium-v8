using UnityEngine;

namespace Characters
{
    /// <summary>
    /// Data component that marks a character for outline rendering.
    /// The actual drawing is handled by OutlineRendererFeature (URP).
    /// Drop on any character to give it a toggleable outline.
    /// </summary>
    public class OutlineEffect : MonoBehaviour
    {
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Range(0f, 0.05f)] private float outlineWidth = 0.02f;

        private bool outlineOn;
        private Renderer[] cachedRenderers;
        private Material materialInstance;
        private static readonly int colorID = Shader.PropertyToID("_OutlineColor");
        private static readonly int widthID = Shader.PropertyToID("_OutlineWidth");

        public bool IsOutlined => outlineOn;
        public Color OutlineColor => outlineColor;

        public void SetOutline(bool on)
        {
            outlineOn = on;
        }

        public void SetOutlineColor(Color color)
        {
            outlineColor = color;
            if (materialInstance != null) materialInstance.SetColor(colorID, color);
        }

        public void SetOutlineWidth(float width)
        {
            outlineWidth = width;
            if (materialInstance != null) materialInstance.SetFloat(widthID, width);
        }

        public Renderer[] GetRenderers()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
                cachedRenderers = GetComponentsInChildren<Renderer>();
            return cachedRenderers;
        }

        public Material GetOrCreateMaterial(Material baseMaterial)
        {
            if (baseMaterial == null) return null;
            if (materialInstance == null)
            {
                materialInstance = new Material(baseMaterial);
                materialInstance.SetColor(colorID, outlineColor);
                materialInstance.SetFloat(widthID, outlineWidth);
            }
            return materialInstance;
        }

        private void OnDestroy()
        {
            if (materialInstance != null)
                Destroy(materialInstance);
        }

        [ContextMenu("Toggle Outline")]
        private void ToggleDebug()
        {
            SetOutline(!IsOutlined);
            Debug.Log($"[{name}] Outline: {IsOutlined}", this);
        }
    }
}
