// ClassicBloomPass.cs
// The "Original" bloom option: the original's full-screen bloom (AbyssEngine::BloomShader; switched on by
// MGame::OnInitialize with Engine::SetPostEffect(0x1400000, true), RenderEffect 0x94740) as a URP render-graph pass after
// the post-processing, on every screen camera with post-processing (the flight level, the station, the menu backdrop):
//   bright pass   the scene sampled straight into 256 x 256 (fboLuma), the original's filter (about everything above half
//                 brightness, in gamma space)
//   blur          6 x (the "horizontal" pass into fboBlurH, which blurs diagonally, then the vertical pass into fboBlurV)
//   composite     clamp(scene + blurV) (Engine::switchBloom = 3 in the binary)
// Shader: Resources/GoF2PostFx/ClassicBloom. URP's own bloom (the "Remake" option) is off meanwhile (Bootstrap).
// The original draws the HUD into the same frame buffer before the effect; the remake's UI Toolkit HUD stays unbloomed.

using GoF2Remake.Data;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GoF2Remake.Visuals
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class ClassicBloomPass : ScriptableRenderPass
    {
        const int Size = 256, Iterations = 6;
        const int PassBright = 0, PassBlurH = 1, PassBlurV = 2, PassComposite = 3;
        static readonly int BloomTex = Shader.PropertyToID("_BloomTex");

        static ClassicBloomPass instance;
        static bool installed;
        Material material;

        /// <summary>Queues the pass for every screen camera while the option is on (Bootstrap).</summary>
        public static void Install()
        {
            if (installed) return;
            installed = true;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        static void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (!Settings.ClassicBloom || cam.cameraType != CameraType.Game || cam.targetTexture != null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null || !data.renderPostProcessing || data.renderType != CameraRenderType.Base || data.scriptableRenderer == null) return;
            instance ??= new ClassicBloomPass();
            if (instance.material == null)
            {
                var shader = Resources.Load<Shader>("GoF2PostFx/ClassicBloom");
                if (shader == null || !shader.isSupported) return;
                instance.material = CoreUtils.CreateEngineMaterial(shader);
            }
            data.scriptableRenderer.EnqueuePass(instance);
        }

        ClassicBloomPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        class CompositeData
        {
            public TextureHandle scene, bloom;
            public Material material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || material == null) return;
            var source = resources.activeColorTexture;

            var small = new TextureDesc(Size, Size)
            {
                colorFormat = GraphicsFormat.R8G8B8A8_UNorm,   // gamma-space values, like the original's RGBA FBOs
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ClassicBloom Luma",
            };
            var luma = renderGraph.CreateTexture(small);
            small.name = "ClassicBloom BlurH";
            var blurH = renderGraph.CreateTexture(small);
            small.name = "ClassicBloom BlurV";
            var blurV = renderGraph.CreateTexture(small);

            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, luma, material, PassBright), "ClassicBloom Bright");
            var from = luma;
            for (int i = 0; i < Iterations; i++)
            {
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(from, blurH, material, PassBlurH), "ClassicBloom BlurH");
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(blurH, blurV, material, PassBlurV), "ClassicBloom BlurV");
                from = blurV;
            }

            // The scene is read and written by the composite: copy it first.
            var sceneDesc = renderGraph.GetTextureDesc(source);
            sceneDesc.name = "ClassicBloom Scene";
            sceneDesc.clearBuffer = false;
            sceneDesc.msaaSamples = MSAASamples.None;
            sceneDesc.depthBufferBits = DepthBits.None;
            var scene = renderGraph.CreateTexture(sceneDesc);
            renderGraph.AddBlitPass(source, scene, Vector2.one, Vector2.zero, passName: "ClassicBloom Copy");

            using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("ClassicBloom Composite", out var data))
            {
                data.scene = scene;
                data.bloom = blurV;
                data.material = material;
                builder.UseTexture(scene);
                builder.UseTexture(blurV);
                builder.SetRenderAttachment(source, 0);
                builder.SetRenderFunc((CompositeData d, RasterGraphContext ctx) =>
                {
                    d.material.SetTexture(BloomTex, d.bloom);
                    Blitter.BlitTexture(ctx.cmd, d.scene, new Vector4(1f, 1f, 0f, 0f), d.material, PassComposite);
                });
            }
        }
    }
}
