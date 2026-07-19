using UnityEngine;

namespace FantasyBrawl.HexGrid
{
    /// <summary>
    /// Generates a flat-top hexagon mesh with a raised rim/border near the outer edge
    /// (like a poker-chip lip), plus a flat interior. Orientation matches
    /// HexCoordinates.ToWorldPosition (flat edges on top/bottom, points left/right).
    /// Overall footprint radius is still `size`, so grid spacing is unaffected —
    /// only the border height/width are new.
    ///
    /// The interior and the border are separate submeshes with separate materials, so the
    /// border can be a fixed dark gray while the interior color is customizable per-tile
    /// (set `interiorColor` in the inspector, or call SetInteriorColor at runtime) without
    /// creating extra material instances.
    /// Add this to your hex tile prefab alongside HexCell.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HexMeshGenerator : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField] private float size = 1f;
        [Tooltip("How far in from the outer edge the raised border starts, as a fraction of size (0-1).")]
        [Range(0.01f, 0.99f)]
        [SerializeField] private float borderWidthRatio = 0.15f;
        [Tooltip("How far the border sticks up above the flat interior.")]
        [SerializeField] private float borderHeight = 0.1f;
        [SerializeField] private bool generateOnStart = true;

        [Header("Materials & Colors")]
        [Tooltip("Material used for the flat interior. Its color is overridden per-instance by interiorColor.")]
        [SerializeField] private Material interiorMaterial;
        [Tooltip("Material used for the raised border. Its color is overridden per-instance by borderColor.")]
        [SerializeField] private Material borderMaterial;
        [SerializeField] private Color interiorColor = Color.white;
        [SerializeField] private Color borderColor = new Color(0.25f, 0.25f, 0.25f); // dark gray

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;

        private void Start()
        {
            if (generateOnStart) Generate(size);
        }

        public void Generate(float hexSize)
        {
            size = hexSize;
            float innerRadius = size * (1f - borderWidthRatio);

            var mesh = new Mesh { name = "HexTile" };

            // Layout: center (1) + 4 rings of 6 verts each.
            // ring0 = inner edge, floor height (flat interior boundary)
            // ring1 = inner edge, top height   (inner wall of the border)
            // ring2 = outer edge, top height   (top surface of the border)
            // ring3 = outer edge, floor height (outer wall down to the tile's base edge)
            var vertices = new Vector3[1 + 6 * 4];
            var uvs = new Vector2[vertices.Length];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < 6; i++)
            {
                float angleRad = Mathf.Deg2Rad * (60f * i);
                float cos = Mathf.Cos(angleRad);
                float sin = Mathf.Sin(angleRad);

                Vector3 innerDir = new Vector3(cos, 0f, sin) * innerRadius;
                Vector3 outerDir = new Vector3(cos, 0f, sin) * size;

                int ring0 = 1 + i;
                int ring1 = 1 + 6 + i;
                int ring2 = 1 + 12 + i;
                int ring3 = 1 + 18 + i;

                vertices[ring0] = new Vector3(innerDir.x, 0f, innerDir.z);
                vertices[ring1] = new Vector3(innerDir.x, borderHeight, innerDir.z);
                vertices[ring2] = new Vector3(outerDir.x, borderHeight, outerDir.z);
                vertices[ring3] = new Vector3(outerDir.x, 0f, outerDir.z);

                uvs[ring0] = new Vector2(0.5f + 0.5f * (innerRadius / size) * cos, 0.5f + 0.5f * (innerRadius / size) * sin);
                uvs[ring1] = uvs[ring0];
                uvs[ring2] = new Vector2(0.5f + 0.5f * cos, 0.5f + 0.5f * sin);
                uvs[ring3] = uvs[ring2];
            }

            // Interior fan (submesh 0) and border rings (submesh 1) are built as separate
            // triangle lists so they can take separate materials/colors.
            var interiorTriangles = new int[6 * 3];
            int ti = 0;
            for (int i = 0; i < 6; i++)
            {
                int a = 1 + i;
                int b = 1 + (i + 1) % 6;
                interiorTriangles[ti++] = 0;
                interiorTriangles[ti++] = b;
                interiorTriangles[ti++] = a;
            }

            var borderTriangles = new int[6 * 2 * 3 * 3]; // 3 rings of quads
            int tb = 0;
            AddQuadRing(borderTriangles, ref tb, 1, 1 + 6);       // inner wall
            AddQuadRing(borderTriangles, ref tb, 1 + 6, 1 + 12);  // border top surface
            AddQuadRing(borderTriangles, ref tb, 1 + 12, 1 + 18); // outer wall

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(interiorTriangles, 0);
            mesh.SetTriangles(borderTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GetComponent<MeshFilter>().mesh = mesh;

            MeshCollider col = GetComponent<MeshCollider>();
            if (col == null) col = gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;

            ApplyMaterials();
        }

        private void ApplyMaterials()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new[] { interiorMaterial, borderMaterial };
            ApplyColors();
        }

        /// <summary>Re-applies the current interiorColor/borderColor without regenerating the mesh.</summary>
        public void ApplyColors()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            SetSubmeshColor(0, interiorColor);
            SetSubmeshColor(1, borderColor);
        }

        private void SetSubmeshColor(int materialIndex, Color color)
        {
            meshRenderer.GetPropertyBlock(propertyBlock, materialIndex);
            // Covers both the Built-in Standard shader (_Color) and URP/HDRP Lit (_BaseColor).
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_BaseColor", color);
            meshRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }

        /// <summary>Returns the current interior color of this tile.</summary>
        public Color InteriorColor => interiorColor;

        /// <summary>Call at runtime to recolor a tile's interior (e.g. highlight a valid move target).</summary>
        public void SetInteriorColor(Color color)
        {
            interiorColor = color;
            ApplyColors();
        }

        public void SetBorderColor(Color color)
        {
            borderColor = color;
            ApplyColors();
        }

        /// <summary>
        /// Builds 6 quads (12 tris) connecting two 6-vertex rings, wound to match the
        /// interior fan's orientation so normals are consistent across the whole mesh.
        /// </summary>
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && GetComponent<MeshFilter>() != null)
            {
                Generate(size);
            }
        }
#endif
    }
}