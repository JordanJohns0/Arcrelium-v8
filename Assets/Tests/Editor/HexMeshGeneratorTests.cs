using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HexGrid;

namespace Tests.HexGrid
{
    [TestFixture]
    public class HexMeshGeneratorTests
    {
        private static readonly FieldInfo ownedFallbackField =
            typeof(HexMeshGenerator).GetField("ownedFallbackMaterial", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo interiorMaterialField =
            typeof(HexMeshGenerator).GetField("interiorMaterial", BindingFlags.NonPublic | BindingFlags.Instance);

        // ---- helpers ----

        private readonly System.Collections.Generic.List<Object> tracked = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in tracked)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            tracked.Clear();
        }

        private static void Configure(HexMeshGenerator generator, Material interiorMaterial, float tiling, Vector2 offset)
        {
            interiorMaterialField.SetValue(generator, interiorMaterial);
            typeof(HexMeshGenerator).GetField("textureTiling", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(generator, tiling);
            typeof(HexMeshGenerator).GetField("textureOffset", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(generator, offset);
        }

        private HexMeshGenerator MakeHex(HexCoordinates coords)
        {
            var go = new GameObject($"Hex {coords}");
            tracked.Add(go);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();

            HexCell cell = go.AddComponent<HexCell>();
            cell.Initialize(coords);

            return go.AddComponent<HexMeshGenerator>();
        }

        private static Vector2[] UVs(HexMeshGenerator generator)
        {
            return generator.GetComponent<MeshFilter>().sharedMesh.uv;
        }

        private Material MakeInteriorMaterial(Texture texture = null)
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            if (texture != null) material.mainTexture = texture;
            tracked.Add(material);
            return material;
        }

        // ---- Texture mapping (seamless world-space tiling) ----

        [Test]
        public void UVs_CenterVertex_OffsetByCellCoordinates()
        {
            var coords = new HexCoordinates(2, 3);
            HexMeshGenerator gen = MakeHex(coords);
            Configure(gen, null, 0.5f, new Vector2(0.25f, 0.75f));

            gen.Generate(1f);

            Vector3 origin = coords.ToWorldPosition(1f);
            Vector2 expected = new Vector2(origin.x, origin.z) * 0.5f + new Vector2(0.25f, 0.75f);
            Assert.AreEqual(expected.x, UVs(gen)[0].x, 1e-4f, "Center UV must derive from the cell's grid position.");
            Assert.AreEqual(expected.y, UVs(gen)[0].y, 1e-4f);
        }

        [Test]
        public void UVs_NeighboringCells_ShareContinuousField()
        {
            HexMeshGenerator cellA = MakeHex(new HexCoordinates(0, 0));
            HexMeshGenerator cellB = MakeHex(new HexCoordinates(1, 0));
            Configure(cellA, null, 0.5f, Vector2.zero);
            Configure(cellB, null, 0.5f, Vector2.zero);

            cellA.Generate(1f);
            cellB.Generate(1f);

            Vector3 posA = new HexCoordinates(0, 0).ToWorldPosition(1f);
            Vector3 posB = new HexCoordinates(1, 0).ToWorldPosition(1f);
            Vector2 boardDelta = new Vector2(posB.x - posA.x, posB.z - posA.z) * 0.5f;

            Vector2[] uvsA = UVs(cellA);
            Vector2[] uvsB = UVs(cellB);
            Assert.AreEqual(uvsA.Length, uvsB.Length);

            for (int i = 0; i < uvsA.Length; i++)
            {
                Assert.AreEqual(uvsA[i].x + boardDelta.x, uvsB[i].x, 1e-3f,
                    "Neighbor cells must sample the same continuous texture field (vertex {0}).", i);
                Assert.AreEqual(uvsA[i].y + boardDelta.y, uvsB[i].y, 1e-3f,
                    "Neighbor cells must sample the same continuous texture field (vertex {0}).", i);
            }
        }

        [Test]
        public void Generate_ForcesTextureWrapModeRepeat()
        {
            var texture = new Texture2D(4, 4) { wrapMode = TextureWrapMode.Clamp };
            tracked.Add(texture);
            Material interior = MakeInteriorMaterial(texture);

            HexMeshGenerator gen = MakeHex(new HexCoordinates(0, 0));
            Configure(gen, interior, 0.5f, Vector2.zero);
            gen.Generate(1f);

            Assert.AreEqual(TextureWrapMode.Repeat, texture.wrapMode,
                "Board-space UVs grow unbounded, so the interior texture must wrap.");
        }

        // ---- Highlight overlay (colored tint over the whole hex) ----

        [Test]
        public void SetHighlight_FalseBeforeEnable_DoesNotCreateOverlay()
        {
            HexMeshGenerator gen = MakeHex(new HexCoordinates(0, 0));

            gen.SetHighlight(false, Color.green);

            Assert.IsNull(gen.transform.Find("HighlightOverlay"));
        }

        [Test]
        public void SetHighlight_True_CreatesEnabledTintedOverlay()
        {
            HexMeshGenerator gen = MakeHex(new HexCoordinates(0, 0));
            gen.Generate(1f);
            Color tint = new Color(0.4f, 0.85f, 0.4f, 0.6f);

            gen.SetHighlight(true, tint);

            Transform overlay = gen.transform.Find("HighlightOverlay");
            Assert.IsNotNull(overlay, "Highlighting must spawn the overlay child.");

            MeshRenderer overlayRenderer = overlay.GetComponent<MeshRenderer>();
            Assert.IsTrue(overlayRenderer.enabled);

            var block = new MaterialPropertyBlock();
            overlayRenderer.GetPropertyBlock(block);
            AssertColorsClose(tint, block.GetColor("_BaseColor"));
            AssertColorsClose(tint, block.GetColor("_Color"));

            Assert.AreSame(gen.GetComponent<MeshFilter>().sharedMesh, overlay.GetComponent<MeshFilter>().sharedMesh,
                "The overlay must duplicate the tile mesh so it covers the entire hex.");
        }

        private static void AssertColorsClose(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-4f));
        }

        [Test]
        public void SetHighlight_FalseAfterTrue_DisablesOverlay()
        {
            HexMeshGenerator gen = MakeHex(new HexCoordinates(0, 0));
            gen.Generate(1f);
            gen.SetHighlight(true, Color.green);

            gen.SetHighlight(false, Color.clear);

            Transform overlay = gen.transform.Find("HighlightOverlay");
            Assert.IsNotNull(overlay, "Clearing must not destroy the overlay (it is reused).");
            Assert.IsFalse(overlay.GetComponent<MeshRenderer>().enabled);
        }

        [Test]
        public void SetHighlight_Twice_ReusesSingleOverlay()
        {
            HexMeshGenerator gen = MakeHex(new HexCoordinates(0, 0));
            gen.Generate(1f);

            gen.SetHighlight(true, Color.green);
            gen.SetHighlight(true, Color.red);

            int count = 0;
            foreach (Transform child in gen.transform)
                if (child.name == "HighlightOverlay") count++;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void SetHighlight_NoMaterialAssigned_UsesFallbackSpriteMaterial()
        {
            HexMeshGenerator gen = MakeHex(new HexCoordinates(0, 0));
            gen.Generate(1f);

            gen.SetHighlight(true, Color.green);

            MeshRenderer overlayRenderer = gen.transform.Find("HighlightOverlay").GetComponent<MeshRenderer>();
            Assert.IsNotNull(overlayRenderer.sharedMaterial, "Fallback material must be created when none is assigned.");
            Assert.AreEqual("Sprites/Default", overlayRenderer.sharedMaterial.shader.name);

            // Clean up the internally-owned fallback material.
            Material fallback = (Material)ownedFallbackField.GetValue(gen);
            if (fallback != null) Object.DestroyImmediate(fallback);
        }

        [Test]
        public void Generate_AfterHighlight_KeepsOverlayMeshInSync()
        {
            HexMeshGenerator gen = MakeHex(new HexCoordinates(0, 0));
            gen.Generate(1f);
            gen.SetHighlight(true, Color.green);
            Mesh oldMesh = gen.GetComponent<MeshFilter>().sharedMesh;

            gen.Generate(1f); // regenerate the tile mesh

            MeshFilter overlayFilter = gen.transform.Find("HighlightOverlay").GetComponent<MeshFilter>();
            Assert.AreNotSame(oldMesh, gen.GetComponent<MeshFilter>().sharedMesh, "Sanity: regeneration builds a new mesh.");
            Assert.AreSame(gen.GetComponent<MeshFilter>().sharedMesh, overlayFilter.sharedMesh,
                "The overlay must follow the regenerated tile mesh.");
        }
    }
}
