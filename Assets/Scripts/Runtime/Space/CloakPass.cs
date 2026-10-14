// CloakPass.cs
// The cloaked hulls (GoF2/Cloak, LightMode "GoF2Cloak") drawn after URP's transparent pass from a copy of the screen as it is
// then, the way the original does it: PaintCanvas::End3d draws every opaque, alpha and additive material first, then
// Engine::CopyFBO copies the frame into the refraction FBO (DrawFBOShader: a plain full-size copy), and only then the
// cloak's material type 0xe (BumpShaderCloak). So the original's cloaked hull refracts the ship's own lights, engine glow,
// shots and the sun's glare, displaced by up to 100 px by the normal map and moving with the wobble: the red patches that
// crawl over a cloaked ship (#47). The remake refracted URP's opaque texture, taken before the transparents, so the hull
// showed only the background. Only while something is cloaked (Request); enqueued from beginCameraRendering like BackdropPass.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GoF2Remake.Visuals
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public sealed class CloakPass : ScriptableRenderPass
    {
        static readonly ShaderTagId Tag = new ShaderTagId("GoF2Cloak");
        static readonly int SceneId = Shader.PropertyToID("_GoF2CloakScene");
        static readonly HashSet<object> owners = new HashSet<object>();
        static CloakPass instance;
        static bool installed;

        /// <summary>A cloak in use ('on') or done: the pass runs while any is.</summary>
        public static void Request(object owner, bool on)
        {
            if (owner == null) return;
            if (on) owners.Add(owner); else owners.Remove(owner);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOwners() => owners.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (installed) return;
            installed = true;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        static void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (owners.Count == 0) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;
            instance ??= new CloakPass();
            data.scriptableRenderer.EnqueuePass(instance);
        }

        CloakPass() => renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        class PassData
        {
            public RendererListHandle list;
            public TextureHandle scene;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var rendering = frameData.Get<UniversalRenderingData>();
            var camera = frameData.Get<UniversalCameraData>();
            var lights = frameData.Get<UniversalLightData>();
            if (resources.isActiveTargetBackBuffer) return;

            // Engine::CopyFBO: the frame so far, full size.
            var desc = renderGraph.GetTextureDesc(resources.activeColorTexture);
            desc.name = "GoF2 Cloak Scene";
            desc.msaaSamples = MSAASamples.None;
            desc.depthBufferBits = DepthBits.None;
            desc.clearBuffer = false;
            var scene = renderGraph.CreateTexture(desc);
            renderGraph.AddBlitPass(resources.activeColorTexture, scene, Vector2.one, Vector2.zero, passName: "GoF2 Cloak Copy");

            var drawing = RenderingUtils.CreateDrawingSettings(Tag, rendering, camera, lights, SortingCriteria.CommonTransparent);
            var filtering = new FilteringSettings(RenderQueueRange.all);
            var list = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("GoF2 Cloak", out var data))
            {
                data.list = list;
                data.scene = scene;
                builder.UseRendererList(list);
                builder.UseTexture(scene);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(resources.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(SceneId, d.scene);
                    ctx.cmd.DrawRendererList(d.list);
                });
            }
        }
    }
}
