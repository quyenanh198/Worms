// Compile-only stand-ins for the URP/Core RP APIs the game calls, copied from
// the signatures in com.unity.render-pipelines.{core,universal} 17.3.0. The
// real packages cannot be compiled here (they need registry-only packages).
// Keep this in sync when calling new URP APIs.
using System;

namespace UnityEngine.Rendering
{
    public class VolumeComponent : ScriptableObject { public bool active = true; }

    public abstract class VolumeParameter<T>
    {
        public void Override(T x) { }
    }

    public class FloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : FloatParameter { }
    public class ClampedFloatParameter : FloatParameter { }

    public sealed class VolumeProfile : ScriptableObject
    {
        public T Add<T>(bool overrides = false) where T : VolumeComponent { throw new NotImplementedException(); }
    }

    public class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public float priority = 0f;
        public float weight = 1f;
        public VolumeProfile profile { get; set; }
    }
}

namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode { None, Neutral, ACES, Custom, External }
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }
    public sealed class Tonemapping : VolumeComponent { public TonemappingModeParameter mode; }
    public sealed class Bloom : VolumeComponent { public MinFloatParameter threshold; public MinFloatParameter intensity; }
    public sealed class ColorAdjustments : VolumeComponent { public FloatParameter postExposure; public ClampedFloatParameter contrast; public ClampedFloatParameter saturation; }
    public sealed class Vignette : VolumeComponent { public ClampedFloatParameter intensity; }

    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }

    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderShadows { get; set; }
        public bool renderPostProcessing { get; set; }
        public AntialiasingMode antialiasing { get; set; }
    }

    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) { throw new NotImplementedException(); }
    }

    public enum RendererType { Custom, UniversalRenderer, _2DRenderer }
    public abstract class ScriptableRendererData : ScriptableObject { }

    public partial class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        protected override RenderPipeline CreatePipeline() { return null; }
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) { throw new NotImplementedException(); }
        public ScriptableRendererData LoadBuiltinRendererData(RendererType type = RendererType.UniversalRenderer) { throw new NotImplementedException(); }
        public bool supportsCameraDepthTexture { get; set; }
        public bool supportsHDR { get; set; }
        public int msaaSampleCount { get; set; }
        public float renderScale { get; set; }
        public int mainLightShadowmapResolution { get; set; }
        public float shadowDistance { get; set; }
        public int shadowCascadeCount { get; set; }
    }
}
