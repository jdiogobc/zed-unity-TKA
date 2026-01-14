using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature that draws ZED point clouds procedurally per camera.
/// Works with XR multipass and Vulkan.
/// </summary>
public class ZEDPointCloudRenderFeature : ScriptableRendererFeature
{
    class PointCloudPass : ScriptableRenderPass
    {
        private readonly string profilerTag = "ZED PointCloud Pass";
        private ProfilingSampler profilingSampler;

        public PointCloudPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            profilingSampler = new ProfilingSampler(profilerTag);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            var cmd = CommandBufferPool.Get(profilerTag);
            using (new ProfilingScope(cmd, profilingSampler))
            {
                var instances = ZEDPointCloudURPRegistry.Instances;
                for (int i = 0; i < instances.Count; i++)
                {
                    var mgr = instances[i];
                    if (mgr == null) continue;
                    if (!mgr.ShouldRenderForCamera(camera)) continue;

                    var mat = mgr.GetMaterial();
                    int count = mgr.GetPointCount();
                    if (mat == null || count <= 0) continue;

                    // Draw points procedurally. Use 1 vertex, 'count' instances.
                    cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Points, 1, count);
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public Settings settings = new Settings();
    private PointCloudPass pass;

    public override void Create()
    {
        pass = new PointCloudPass(settings.passEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }
}


