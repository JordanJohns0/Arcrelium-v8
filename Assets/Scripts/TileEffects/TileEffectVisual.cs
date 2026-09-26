using UnityEngine;

namespace TileEffects
{
    /// <summary>
    /// Renders a tile-effect decoration (fire glow, oil slick) on top of a hex. Uses no
    /// material asset: it picks whichever shader exists at runtime (URP first, built-in
    /// fallback) on Start, so the prefab is exposure-safe and renders in any pipeline.
    /// </summary>
    public class TileEffectVisual : MonoBehaviour
    {
        public Color tint = Color.white;
        [Tooltip("Color pulse speed in cycles/second; 0 = static.")]
        public float pulseSpeed;
        [Tooltip("How far the tint swings around its base color (0..1 is a good range).")]
        public float pulseAmount;

        private Renderer cachedRenderer;
        private Material material;
        private Color baseColor;

        private void Start()
        {
            cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            material = new Material(shader);
            baseColor = tint;
            ApplyColor(baseColor);
            cachedRenderer.sharedMaterial = material;
        }

        private void Update()
        {
            if (material == null || pulseSpeed <= 0f || pulseAmount <= 0f) return;
            float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f; // 0..1
            ApplyColor(baseColor * (1f + pulseAmount * wave));
        }

        private void ApplyColor(Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }
}
