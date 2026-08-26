using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Characters
{
    public class OutlineRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private bool verboseLogging;

        private OutlineRenderPass pass;
        private bool warnedNoMaterial;

        public override void Create()
        {
            pass = new OutlineRenderPass();
            pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            pass.SetVerbose(verboseLogging);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (outlineMaterial == null)
            {
                if (!warnedNoMaterial)
                {
                    Debug.LogWarning("[OutlineRendererFeature] No Outline Material assigned on the feature. Outlines cannot render.", this);
                    warnedNoMaterial = true;
                }
                return;
            }

            if (renderingData.cameraData.cameraType == CameraType.Preview
                || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            pass.SetMaterial(outlineMaterial);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Cleanup();
            pass = null;
        }
    }

    public class OutlineRenderPass : ScriptableRenderPass
    {
        private Material baseMaterial;
        private readonly List<(Renderer renderer, Material mat)> drawList = new();
        private bool verbose;
        private int lastOutlinedCount = -1;

        public void SetMaterial(Material mat)
        {
            baseMaterial = mat;
        }

        public void SetVerbose(bool on)
        {
            verbose = on;
        }

#if UNITY_6000_0_OR_NEWER
        private class PassData
        {
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            CollectDrawList();
            if (drawList.Count == 0) return;

            var resourceData = frameData.Get<UniversalResourceData>();

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Character Outlines", out _))
            {
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData _, RasterGraphContext ctx) =>
                {
                    foreach ((Renderer r, Material mat) in drawList)
                        ctx.cmd.DrawRenderer(r, mat);
                });
            }
        }
#endif

#pragma warning disable 618, 672
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CollectDrawList();
            if (drawList.Count == 0) return;

            CommandBuffer cmd = new CommandBuffer { name = "Character Outlines" };
            cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);

            foreach ((Renderer r, Material mat) in drawList)
                cmd.DrawRenderer(r, mat);

            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }
#pragma warning restore 618, 672

        private void CollectDrawList()
        {
            drawList.Clear();

            OutlineEffect[] outlines = Object.FindObjectsByType<OutlineEffect>(FindObjectsSortMode.None);

            int outlinedCount = 0;
            foreach (OutlineEffect outline in outlines)
            {
                if (!outline.IsOutlined) continue;
                outlinedCount++;

                Material mat = outline.GetOrCreateMaterial(baseMaterial);
                if (mat == null) continue;

                Renderer[] renderers = outline.GetRenderers();
                if (verbose && renderers.Length == 0)
                    Debug.LogWarning($"[OutlinePass] '{outline.name}' is outlined but has no Renderers.", outline);

                foreach (Renderer r in renderers)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    drawList.Add((r, mat));
                }
            }

            if (verbose && outlinedCount != lastOutlinedCount)
            {
                lastOutlinedCount = outlinedCount;
                Debug.Log($"[OutlinePass] outlined components: {outlinedCount}, draw items: {drawList.Count}");
            }
        }

        public void Cleanup()
        {
        }
    }
}
