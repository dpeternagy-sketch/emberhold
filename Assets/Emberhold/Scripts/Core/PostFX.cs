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
            if (grade != null) grade.saturation.Override(Mathf.Lerp(18f, -25f, dark));
            if (balance != null) balance.temperature.Override(Mathf.Lerp(0f, -20f, dark));
        }

        public static void Setup(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            }
            var go = new GameObject("GlobalVolume");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();

            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.Neutral);

            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.15f);
            ca.contrast.Override(12f);
            ca.saturation.Override(18f);
            grade = ca;
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(0f);
            balance = wb;

            var bl = p.Add<Bloom>(true);
            bl.threshold.Override(1.1f);
            bl.intensity.Override(0.35f);
            bl.scatter.Override(0.6f);

            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(0.18f);
            vg.smoothness.Override(0.4f);

            v.sharedProfile = p;
        }
    }
}
