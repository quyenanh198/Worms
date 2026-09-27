using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Worms.Game.Render
{
    public enum QualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    /// <summary>
    /// Everything that touches the URP API lives here (docs/PLAN.md §3.14):
    /// camera post-processing, the global Volume and the quality tiers.
    /// Only features that run on WebGL2 are used.
    /// </summary>
    public static class UrpSetup
    {
        static Volume _volume;
        static Bloom _bloom;
        static Vignette _vignette;

        public static void ConfigureCamera(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            data.renderShadows = true;
        }

        public static void CreateVolume(Transform parent)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.25f);
            color.contrast.Override(8f);
            color.saturation.Override(14f);
            _bloom = profile.Add<Bloom>(true);
            _bloom.intensity.Override(0.55f);
            _bloom.threshold.Override(1.05f);
            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.22f);

            var go = new GameObject("Post Processing");
            go.transform.SetParent(parent, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1;
            _volume.profile = profile;
        }

        /// <summary>Same tiers on every platform; see docs/PLAN.md §3.14.</summary>
        public static void ApplyTier(QualityTier tier, Light sun)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = tier == QualityTier.Low ? 0.7f : tier == QualityTier.Medium ? 0.85f : 1f;
                urp.msaaSampleCount = tier == QualityTier.Low ? 1 : tier == QualityTier.Medium ? 2 : 4;
                urp.shadowDistance = 60f;
                // The terrain is a stack of pixel steps; a little extra bias keeps it free of shadow acne stripes.
                urp.shadowDepthBias = 1.6f;
                urp.shadowNormalBias = 1.4f;
                urp.shadowCascadeCount = tier == QualityTier.High ? 2 : 1;
            }
            if (sun != null) sun.shadows = tier == QualityTier.Low ? LightShadows.None : LightShadows.Soft;
            if (_bloom != null) _bloom.active = tier != QualityTier.Low;
            if (_vignette != null) _vignette.active = tier != QualityTier.Low;
        }
    }
}
