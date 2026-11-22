using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Emberhold
{
    /// <summary>
    /// Global look from the style guide: Neutral tonemapping (keeps cartoon colours), a little more
    /// exposure, contrast and saturation, soft bloom only on bright emissive accents, light vignette.
    /// No SSAO, DOF or motion blur (phone performance).
    /// </summary>
    public static class PostFX
    {
        static ColorAdjustments grade;
        static WhiteBalance balance;

        /// <summary>0 = day grade, 1 = night grade (less saturated, cooler), driven by the day/night cycle.</summary>
        public static void SetNight(float dark)
        {
            if (grade != null) grade.saturation.Override(Mathf.Lerp(0f, -25f, dark));
            if (balance != null) balance.temperature.Override(Mathf.Lerp(0f, -20f, dark));
        }

        public static void Setup(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                // SMAA keeps the painted art sharp (FXAA blurred every building and tree)
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
            }
            var go = new GameObject("GlobalVolume");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();

            var tm = p.Add<Tonemapping>(true);
            // Peter's painted colours go to the screen untouched: no tonemapping curve, no extra
            // exposure, contrast or saturation (those washed the white buildings out and made the grass neon)
            tm.mode.Override(TonemappingMode.None);

            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0f);
            ca.contrast.Override(0f);
            ca.saturation.Override(0f);
            grade = ca;
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(0f);
            balance = wb;

            var bl = p.Add<Bloom>(true);
            bl.threshold.Override(1.25f);
            bl.intensity.Override(0.25f);
            bl.scatter.Override(0.6f);

            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(0.12f);
            vg.smoothness.Override(0.4f);

            v.sharedProfile = p;
        }
    }
}
