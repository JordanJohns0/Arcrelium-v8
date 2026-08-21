using UnityEngine;

namespace HexGrid
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HexMeshGenerator : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private float size = 1f;
        [Range(0.01f, 0.99f)]
        [SerializeField] private float borderWidthRatio = 0.15f;
        [SerializeField] private float borderHeight = 0.1f;
        [SerializeField] private bool generateOnStart = true;

        [Header("Pillar")]
        [SerializeField] private float pillarDepth = 4f;
        [SerializeField] private float depthRandomRange = 0.8f;

        [Header("Surface Offset")]
        [SerializeField] private float surfaceOffsetRange = 0.2f;

        [Header("Materials")]
        [SerializeField] private Material interiorMaterial;
        [SerializeField] private Material borderMaterial;

        [Header("Texture Mapping")]
        [Tooltip("Texture repeats per world unit across the board. Higher = smaller, more repeated texture.")]
        [Range(0.05f, 10f)]
        [SerializeField] private float textureTiling = 0.5f;
        [Tooltip("Extra UV offset applied on top of the coordinate-based mapping.")]
        [SerializeField] private Vector2 textureOffset = Vector2.zero;

        [Header("Highlight")]
        [Tooltip("Transparent material for the highlight tint over the whole hex. If empty, a temporary Sprites/Default material is used.")]
        [SerializeField] private Material highlightOverlayMaterial;

        private MeshRenderer meshRenderer;
        private MeshRenderer highlightOverlayRenderer;
        private Material ownedFallbackMaterial;
        private float actualDepth;

        private static readonly int colorID = Shader.PropertyToID("_Color");
        private static readonly int baseColorID = Shader.PropertyToID("_BaseColor");
        private const string OverlayName = "HighlightOverlay";

        private void Start()
        {
            float yOffset = Random.Range(-surfaceOffsetRange, surfaceOffsetRange);
            transform.localPosition += new Vector3(0, yOffset, 0);
            actualDepth = pillarDepth + Random.Range(-depthRandomRange, depthRandomRange);
            if (generateOnStart) Generate(size);
        }

        public void Generate(float hexSize)
        {
            size = hexSize;
            float innerRadius = size * (1f - borderWidthRatio);

            var mesh = new Mesh { name = "HexTile" };

            int centerVert = 1;
            int rings = 5;
            var vertices = new Vector3[centerVert + 6 * rings];

            vertices[0] = Vector3.zero;

            for (int i = 0; i < 6; i++)
            {
                float angleRad = Mathf.Deg2Rad * (60f * i);
                float cos = Mathf.Cos(angleRad);
                float sin = Mathf.Sin(angleRad);

                Vector3 innerDir = new Vector3(cos, 0f, sin) * innerRadius;
                Vector3 outerDir = new Vector3(cos, 0f, sin) * size;

                int ring0 = centerVert + i;
                int ring1 = centerVert + 6 + i;
                int ring2 = centerVert + 12 + i;
                int ring3 = centerVert + 18 + i;
                int ring4 = centerVert + 24 + i;

                vertices[ring0] = new Vector3(innerDir.x, 0f, innerDir.z);
                vertices[ring1] = new Vector3(innerDir.x, borderHeight, innerDir.z);
                vertices[ring2] = new Vector3(outerDir.x, borderHeight, outerDir.z);
                vertices[ring3] = new Vector3(outerDir.x, 0f, outerDir.z);
                vertices[ring4] = new Vector3(outerDir.x, -actualDepth, outerDir.z);
            }

            var interiorTriangles = new int[6 * 3];
            int ti = 0;
            for (int i = 0; i < 6; i++)
            {
                int a = centerVert + i;
                int b = centerVert + (i + 1) % 6;
                interiorTriangles[ti++] = 0;
                interiorTriangles[ti++] = b;
                interiorTriangles[ti++] = a;
            }

            var borderTriangles = new int[6 * 2 * 3 * 4];
            int tb = 0;
            AddQuadRing(borderTriangles, ref tb, centerVert, centerVert + 6);
            AddQuadRing(borderTriangles, ref tb, centerVert + 6, centerVert + 12);
            AddQuadRing(borderTriangles, ref tb, centerVert + 12, centerVert + 18);
            AddQuadRing(borderTriangles, ref tb, centerVert + 18, centerVert + 24);

            mesh.vertices = vertices;
            mesh.uv = BuildUVs(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(interiorTriangles, 0);
            mesh.SetTriangles(borderTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GetComponent<MeshFilter>().mesh = mesh;

            // Keep an existing highlight overlay in sync with the rebuilt mesh.
            if (highlightOverlayRenderer != null)
            {
                MeshFilter overlayFilter = highlightOverlayRenderer.GetComponent<MeshFilter>();
                if (overlayFilter != null) overlayFilter.sharedMesh = mesh;
            }

            MeshCollider col = GetComponent<MeshCollider>();
            if (col == null) col = gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;

            ApplyMaterials();
        }

        private void ApplyMaterials()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new[] { interiorMaterial, borderMaterial };

            // UVs now grow unbounded across the board, so the interior texture must wrap
            // to repeat seamlessly when it runs out.
            if (interiorMaterial != null && interiorMaterial.mainTexture != null)
            {
                interiorMaterial.mainTexture.wrapMode = TextureWrapMode.Repeat;
            }
        }

        /// <summary>
        /// Maps a board-space XZ position into texture space: coordinate origin of this
        /// hex plus the vertex's local offset, scaled by the tiling slider, shifted by
        /// the manual offset. Shared edge positions between neighboring cells produce
        /// identical UVs on both sides, keeping the material seamless.
        /// </summary>
        private Vector2 WorldUV(Vector2 worldXZ)
        {
            return worldXZ * textureTiling + textureOffset;
        }

        /// <summary>
        /// Builds UVs for every vertex from its board-space position (this cell's
        /// coordinate-derived grid position + local offset), so the texture flows
        /// continuously across neighboring hexes instead of restarting on each one.
        /// </summary>
        private Vector2[] BuildUVs(Vector3[] vertices)
        {
            HexCell ownerCell = GetComponent<HexCell>();
            Vector3 coordWorldPos = ownerCell != null
                ? ownerCell.Coordinates.ToWorldPosition(size)
                : Vector3.zero;
            Vector2 coordOrigin = new Vector2(coordWorldPos.x, coordWorldPos.z);

            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                uvs[i] = WorldUV(coordOrigin + new Vector2(v.x, v.z));
            }
            return uvs;
        }

        /// <summary>
        /// Re-applies the current tiling/offset to the existing mesh without rebuilding
        /// geometry — used for live inspector tweaks at runtime.
        /// </summary>
        public void ApplyTextureMapping()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            filter.sharedMesh.uv = BuildUVs(filter.sharedMesh.vertices);
        }

        /// <summary>
        /// Shows a coloured tint over the entirety of the hex (interior, rim and walls) by
        /// rendering a duplicate of the tile mesh slightly above it with a transparent
        /// material. Independent of whatever materials/textures the hex itself uses.
        /// </summary>
        public void SetHighlight(bool on, Color color)
        {
            if (!on)
            {
                if (highlightOverlayRenderer != null) highlightOverlayRenderer.enabled = false;
                return;
            }

            EnsureHighlightOverlay();
            if (highlightOverlayRenderer == null) return;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(colorID, color);
            block.SetColor(baseColorID, color);
            highlightOverlayRenderer.SetPropertyBlock(block);
            highlightOverlayRenderer.enabled = true;
        }

        private void EnsureHighlightOverlay()
        {
            Transform existing = transform.Find(OverlayName);
            if (existing != null)
            {
                highlightOverlayRenderer = existing.GetComponent<MeshRenderer>();
                return;
            }

            MeshFilter sourceFilter = GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) return;

            var overlay = new GameObject(OverlayName);
            overlay.transform.SetParent(transform, false);
            // Float just above the raised rim so the tint never z-fights with the tile.
            overlay.transform.localPosition = new Vector3(0f, borderHeight * 2f + 0.02f, 0f);

            MeshFilter filter = overlay.AddComponent<MeshFilter>();
            filter.sharedMesh = sourceFilter.sharedMesh;

            highlightOverlayRenderer = overlay.AddComponent<MeshRenderer>();
            highlightOverlayRenderer.sharedMaterial = ResolveOverlayMaterial();
            highlightOverlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            highlightOverlayRenderer.receiveShadows = false;
        }

        private Material ResolveOverlayMaterial()
        {
            if (highlightOverlayMaterial != null) return highlightOverlayMaterial;

            Shader fallback = Shader.Find("Sprites/Default");
            if (fallback == null) return null;

            ownedFallbackMaterial = new Material(fallback);
            Debug.LogWarning($"[{name}] No Highlight Overlay Material assigned on HexMeshGenerator - using a temporary Sprites/Default material.", this);
            return ownedFallbackMaterial;
        }

        private static void AddQuadRing(int[] triangles, ref int t, int ringAStart, int ringBStart)
        {
            for (int i = 0; i < 6; i++)
            {
                int a0 = ringAStart + i;
                int a1 = ringAStart + (i + 1) % 6;
                int b0 = ringBStart + i;
                int b1 = ringBStart + (i + 1) % 6;

                triangles[t++] = a0;
                triangles[t++] = b1;
                triangles[t++] = b0;

                triangles[t++] = a0;
                triangles[t++] = a1;
                triangles[t++] = b1;
            }
        }

        private void OnDestroy()
        {
            if (ownedFallbackMaterial != null)
                Destroy(ownedFallbackMaterial);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (GetComponent<MeshFilter>() == null) return;

            if (Application.isPlaying)
            {
                // Keep the runtime-randomized pillar depth; just refresh texture mapping live.
                ApplyTextureMapping();
            }
            else
            {
                actualDepth = pillarDepth;
                Generate(size);
            }
        }
#endif
    }
}
