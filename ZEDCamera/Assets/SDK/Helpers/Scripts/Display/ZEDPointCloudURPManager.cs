using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// URP-compatible point cloud manager that feeds ZED textures to a URP shader and registers
/// instances so a URP renderer feature can draw procedurally per camera (XR multipass/Vulkan safe).
/// </summary>
public class ZEDPointCloudURPManager : MonoBehaviour
{
    [Tooltip("Set to a camera if you do not want that camera to see the point cloud.")]
    public Camera hiddenObjectFromCamera;

    [Tooltip("Whether the point cloud should be visible or not.")]
    public bool display = true;

    [Tooltip("Whether to update the point cloud textures each frame.")]
    public bool update = true;

    public ZEDManager zedManager = null;
    private sl.ZEDCamera zed = null;

    private Texture2D xyzTexture;
    private Texture2D colorTexture;
    private RenderTexture xyzTextureCopy = null;
    private RenderTexture colorTextureCopy = null;

    [Tooltip("Material using 'ZED/ZED PointCloud URP' shader.")]
    public Material material;

    private static int? _positionid;
    private static int positionID
    {
        get
        {
            if (_positionid == null) _positionid = Shader.PropertyToID("_Position");
            return (int)_positionid;
        }
    }

    private static int? _colortexid;
    private static int colorTexID
    {
        get
        {
            if (_colortexid == null) _colortexid = Shader.PropertyToID("_ColorTex");
            return (int)_colortexid;
        }
    }

    private static int? _xyztexid;
    private static int xyzTexID
    {
        get
        {
            if (_xyztexid == null) _xyztexid = Shader.PropertyToID("_XYZTex");
            return (int)_xyztexid;
        }
    }

    private int numberPoints = 0;

    private void OnEnable()
    {
        if (zedManager == null)
        {
            zedManager = FindObjectOfType<ZEDManager>();
        }
        if (zedManager != null) zed = zedManager.zedCamera;

        if (material == null)
        {
            // Try to find the URP shader; user can also assign via inspector
            var shader = Shader.Find("ZED/ZED PointCloud URP");
            if (shader != null)
            {
                material = new Material(shader);
            }
        }

        RegisterInstance();
    }

    private void OnDisable()
    {
        UnregisterInstance();
    }

    private void Update()
    {
        if (zed == null || !zed.IsCameraReady) return;

        if (numberPoints == 0)
        {
            xyzTexture = zed.CreateTextureMeasureType(sl.MEASURE.XYZ);
            colorTexture = zed.CreateTextureImageType(sl.VIEW.LEFT);
            numberPoints = zed.ImageWidth * zed.ImageHeight;

            if (material != null)
            {
                material.SetTexture(xyzTexID, xyzTexture);
                material.SetTexture(colorTexID, colorTexture);
            }
        }

        if (!update)
        {
            // ensure copies exist
            if (xyzTexture != null && xyzTextureCopy == null) 
            {
                xyzTextureCopy = new RenderTexture(xyzTexture.width, xyzTexture.height, 0, RenderTextureFormat.ARGBFloat);
            }
            if (colorTexture != null && colorTextureCopy == null)
            {
                colorTextureCopy = new RenderTexture(colorTexture.width, colorTexture.height, 0, RenderTextureFormat.ARGB32);
            }

            if (xyzTextureCopy != null) Graphics.Blit(xyzTexture, xyzTextureCopy);
            if (colorTextureCopy != null) Graphics.Blit(colorTexture, colorTextureCopy);

            if (material != null)
            {
                material.SetTexture(xyzTexID, xyzTextureCopy);
                material.SetTexture(colorTexID, colorTextureCopy);
            }
        }
        else
        {
            if (material != null)
            {
                material.SetTexture(xyzTexID, xyzTexture);
                material.SetTexture(colorTexID, colorTexture);
            }
        }

        // update instance data for renderer feature
        if (material != null)
        {
            material.SetMatrix(positionID, transform.localToWorldMatrix);
            material.SetFloat("_ScaleSizeMultiplier", transform.lossyScale.magnitude / 1.732f); //1.732 = sqrt(3) = lenght of unit vector 3D
        }
    }

    private void OnDestroy()
    {
        material = null;
        if (xyzTextureCopy != null) xyzTextureCopy.Release();
        if (colorTextureCopy != null) colorTextureCopy.Release();
    }

    private void RegisterInstance()
    {
        ZEDPointCloudURPRegistry.Register(this);
    }

    private void UnregisterInstance()
    {
        ZEDPointCloudURPRegistry.Unregister(this);
    }

    public bool ShouldRenderForCamera(Camera cam)
    {
        if (!display) return false;
        if (hiddenObjectFromCamera != null && cam == hiddenObjectFromCamera) return false;
        return true;
    }

    public Material GetMaterial() { return material; }
    public int GetPointCount() { return numberPoints; }
}

public static class ZEDPointCloudURPRegistry
{
    private static readonly List<ZEDPointCloudURPManager> instances = new List<ZEDPointCloudURPManager>();

    public static void Register(ZEDPointCloudURPManager mgr)
    {
        if (!instances.Contains(mgr)) instances.Add(mgr);
    }

    public static void Unregister(ZEDPointCloudURPManager mgr)
    {
        instances.Remove(mgr);
    }

    public static IReadOnlyList<ZEDPointCloudURPManager> Instances => instances;
}


