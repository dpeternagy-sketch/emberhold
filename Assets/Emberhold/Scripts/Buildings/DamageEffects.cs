using UnityEngine;
using UnityEngine.Rendering;

namespace Emberhold
{
    /// <summary>Smoke, embers and rubble for damaged and ruined buildings.</summary>
    public static class DamageEffects
    {
        static Material smokeMat, emberMat;

        static Texture2D softDot;
        public static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int n = 64;
                softDot = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x - n / 2f + 0.5f) / (n / 2f), dy = (y - n / 2f + 0.5f) / (n / 2f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                softDot.SetPixels32(px);
                softDot.Apply();
                return softDot;
            }
        }

        public static Material ParticleMaterial(bool additive, Texture tex = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex != null ? tex : SoftDot);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex != null ? tex : SoftDot);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        public static ParticleSystem Smoke(Transform parent, Vector3 localPos, float strength)
        {
            if (smokeMat == null) smokeMat = ParticleMaterial(false);
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * strength + 0.3f, 1.1f * strength + 0.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.17f, 0.16f, 0.55f), new Color(0.42f, 0.4f, 0.38f, 0.45f));
            main.gravityModifier = -0.04f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var em = ps.emission;
            em.rateOverTime = 2f + 6f * strength;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.25f + 0.3f * strength;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.45f); // drifting with the wind
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = smokeMat;
            r.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        public static ParticleSystem Embers(Transform parent, Vector3 localPos)
        {
            if (emberMat == null) emberMat = ParticleMaterial(true);
            var go = new GameObject("Embers");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f, 1f), new Color(1f, 0.8f, 0.3f, 1f));
            main.gravityModifier = -0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 50;
            var em = ps.emission;
            em.rateOverTime = 10f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.4f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = emberMat;
            ps.Play();
            return ps;
        }

        /// <summary>Broken stones and charred beams scattered over a ruined footprint.</summary>
        public static Transform Rubble(Transform parent, Vector2Int size)
        {
            var root = new GameObject("Rubble").transform;
            root.SetParent(parent, false);
            var cols = new[] { new Color(0.32f, 0.3f, 0.28f), new Color(0.2f, 0.18f, 0.16f), new Color(0.48f, 0.45f, 0.42f) };
            int n = 6 + size.x * size.y * 2;
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(Random.Range(-0.45f, 0.45f) * size.x, 0.05f, Random.Range(-0.45f, 0.45f) * size.y);
                var g = Util.Prim(PrimitiveType.Cube, root, p, new Vector3(Random.Range(0.1f, 0.3f), Random.Range(0.06f, 0.18f), Random.Range(0.1f, 0.3f)), cols[Random.Range(0, cols.Length)]);
                g.transform.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
            }
            for (int i = 0; i < 3; i++)
            {
                var beam = Util.Prim(PrimitiveType.Cube, root, new Vector3(Random.Range(-0.35f, 0.35f) * size.x, 0.12f, Random.Range(-0.35f, 0.35f) * size.y),
                    new Vector3(0.08f, 0.08f, Random.Range(0.6f, 1.1f)), new Color(0.12f, 0.09f, 0.07f));
                beam.transform.localRotation = Quaternion.Euler(Random.Range(5f, 30f), Random.Range(0f, 360f), 0f);
            }
            return root;
        }

        /// <summary>One-off dust/smoke burst (cannon ball landing, building collapsing).</summary>
        public static void Puff(Vector3 worldPos, float size)
        {
            if (smokeMat == null) smokeMat = ParticleMaterial(false);
            var go = new GameObject("Puff");
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f * size, 2.5f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f * size, 0.9f * size);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.4f, 0.34f, 0.7f), new Color(0.6f, 0.55f, 0.48f, 0.6f));
            main.gravityModifier = 0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(10 * size + 6)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.3f * size;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMat;
            ps.Play();
            Object.Destroy(go, 3f);
        }

        static Material sparkMat;

        /// <summary>Gold sparkle burst when an upgrade finishes ("Level up!").</summary>
        public static void LevelUp(Vector3 worldPos, float size)
        {
            if (sparkMat == null) sparkMat = ParticleMaterial(true);
            var go = new GameObject("LevelUp");
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f * size, 4.5f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(0.6f, 1f, 1f));
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)28) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35f; shape.radius = 0.4f * size;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkMat;
            ps.Play();
            Object.Destroy(go, 3f);
        }

        /// <summary>Soft white spray at the foot of a waterfall.</summary>
        public static ParticleSystem Mist(Transform parent, Vector3 worldPos)
        {
            if (smokeMat == null) smokeMat = ParticleMaterial(false);
            var go = new GameObject("Mist");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.95f, 1f, 0.35f), new Color(1f, 1f, 1f, 0.2f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 14f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35f; shape.radius = 0.8f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMat;
            ps.Play();
            return ps;
        }
    }
}
