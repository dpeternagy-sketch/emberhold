using UnityEngine;

namespace Emberhold
{
    /// <summary>Angled top-down camera with pan (drag) and zoom (pinch / wheel), clamped to the map.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Vector3 focus = new Vector3(0f, 0f, 9f);
        // Phone framing: a long lens from far back (FOV 24, 50 degrees down) keeps buildings chunky and
        // undistorted. Upright screen, so the mountain sits at the top and the shore at the bottom.
        public float distance = 78f;
        const float MinDistance = 42f, MaxDistance = 105f;   // 2.5x pinch range
        const float Pitch = 50f, Yaw = 0f;

        public void Init(Camera cam)
        {
            Cam = cam;
            Cam.fieldOfView = 24f;
            Cam.nearClipPlane = 5f;
            Cam.farClipPlane = 500f;
            Apply();
        }

        public void Pan(Vector3 worldDelta)
        {
            focus -= new Vector3(worldDelta.x, 0f, worldDelta.z);
            focus.x = Mathf.Clamp(focus.x, -22f, 22f);
            focus.z = Mathf.Clamp(focus.z, -20f, 20f);
            Apply();
        }

        /// <summary>Centres the view on a point (e.g. the building a builder is working on).</summary>
        public void FocusOn(Vector3 world)
        {
            focus = new Vector3(Mathf.Clamp(world.x, -22f, 22f), 0f, Mathf.Clamp(world.z, -20f, 20f));
            Apply();
        }

        public void Zoom(float delta)
        {
            if (Mathf.Abs(delta) < 0.0001f) return;
            distance = Mathf.Clamp(distance * (1f - delta * 0.15f), MinDistance, MaxDistance);
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Cam.transform.rotation = rot;
            Cam.transform.position = focus - rot * Vector3.forward * distance;
            // haze starts just behind the town, so the far map edge melts into the sky at any zoom
            RenderSettings.fogStartDistance = distance + 12f;
            RenderSettings.fogEndDistance = distance + 110f;
        }

        /// <summary>Where a screen point hits the ground (y = 0).</summary>
        public bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            var ray = Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            float enter;
            if (plane.Raycast(ray, out enter)) { point = ray.GetPoint(enter); return true; }
            point = Vector3.zero;
            return false;
        }
    }
}
