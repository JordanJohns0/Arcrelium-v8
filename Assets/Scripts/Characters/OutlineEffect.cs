using System.Collections.Generic;
using UnityEngine;

namespace Characters
{
    /// <summary>
    /// Toggleable outline for a character using the inverted-hull technique.
    /// Holds a reference to an outline material (using the Arcrelium/Outline shader,
    /// Cull Front + inflated back faces). When SetOutline(true) is called, the outline
    /// material is appended as an extra material slot on each child renderer; when set
    /// to false it is removed again.
    /// </summary>
    public class OutlineEffect : MonoBehaviour
    {
        [SerializeField] private Material outlineMat;
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Range(0f, 0.05f)] private float outlineWidth = 0.02f;

        private bool outlineOn;
        private Renderer[] cachedRenderers;
        private Material materialInstance;
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        private static readonly int colorID = Shader.PropertyToID("_OutlineColor");
        private static readonly int widthID = Shader.PropertyToID("_OutlineWidth");

        public bool IsOutlined => outlineOn;
        public Material OutlineMaterial => outlineMat;

        public void SetOutline(bool on)
        {
            if (on == outlineOn) return;

            outlineOn = on;
            if (on)
                EnableOutline();
            else
                DisableOutline();
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

        private void EnableOutline()
        {
            if (outlineMat == null) return;

            if (materialInstance == null)
            {
                materialInstance = new Material(outlineMat)
                {
                    name = outlineMat.name + " (Instance)"
                };
            }
            materialInstance.SetColor(colorID, outlineColor);
            materialInstance.SetFloat(widthID, outlineWidth);

            foreach (Renderer renderer in GetRenderers())
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!originalMaterials.ContainsKey(renderer))
                {
                    Material[] current = renderer.sharedMaterials;
                    originalMaterials[renderer] = current;
                    List<Material> withOutline = new List<Material>(current) { materialInstance };
                    renderer.sharedMaterials = withOutline.ToArray();
                }
            }
        }

        private void DisableOutline()
        {
            foreach (Renderer renderer in GetRenderers())
            {
                if (renderer == null) continue;

                if (originalMaterials.TryGetValue(renderer, out Material[] originals))
                {
                    renderer.sharedMaterials = originals;
                    originalMaterials.Remove(renderer);
                }
            }
        }

        private void OnDestroy()
        {
            DisableOutline();
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
