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

        [Header("Materials & Colors")]
        [SerializeField] private Material interiorMaterial;
        [SerializeField] private Material borderMaterial;
        [SerializeField] private Color interiorColor = Color.white;
        [SerializeField] private Color borderColor = new Color(0.25f, 0.25f, 0.25f);

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private float actualDepth;

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

                uvs[ring0] = new Vector2(0.5f + 0.5f * (innerRadius / size) * cos, 0.5f + 0.5f * (innerRadius / size) * sin);
                uvs[ring1] = uvs[ring0];
                uvs[ring2] = new Vector2(0.5f + 0.5f * cos, 0.5f + 0.5f * sin);
                uvs[ring3] = uvs[ring2];
                uvs[ring4] = uvs[ring2];
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
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_BaseColor", color);
            meshRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }

        public Color InteriorColor => interiorColor;

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
                actualDepth = pillarDepth;
                Generate(size);
            }
        }
#endif
    }
}
