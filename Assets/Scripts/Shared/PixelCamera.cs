using UnityEngine;

namespace FiveKingdoms
{
    /// <summary>
    /// Orthographic camera that keeps pixel art crisp on any screen: it picks the largest whole-number zoom
    /// that still shows about <see cref="TilesHigh"/> tiles vertically and snaps to screen pixels.
    /// Also follows a target smoothly and can shake.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PixelCamera : MonoBehaviour
    {
        public Transform Target;
        public float TilesHigh = 11f;
        public float FollowSharpness = 14f;

        Camera cam;
        Vector3 focus;
        float shakeTime;
        float shakeStrength;
        int screenHeight;
        float worldPerScreenPixel = 1f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            UpdateZoom();
        }

        public void SnapToTarget()
        {
            if (Target != null) focus = Target.position;
            Apply();
        }

        public void Shake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        void LateUpdate()
        {
            if (Screen.height != screenHeight) UpdateZoom();
            if (Target != null)
                focus = Vector3.Lerp(focus, Target.position, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));
            Apply();
        }

        void UpdateZoom()
        {
            screenHeight = Screen.height;
            int ppu = SpriteLibrary.PixelsPerUnit;
            int zoom = Mathf.Max(1, Mathf.RoundToInt(screenHeight / (ppu * TilesHigh)));
            cam.orthographicSize = screenHeight / (2f * ppu * zoom);
            worldPerScreenPixel = 1f / (ppu * zoom);
        }

        void Apply()
        {
            var position = focus;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                position += (Vector3)(Random.insideUnitCircle * shakeStrength);
                if (shakeTime <= 0f) shakeStrength = 0f;
            }
            // Snap to whole screen pixels so sprites don't shimmer while the camera scrolls.
            position.x = Mathf.Round(position.x / worldPerScreenPixel) * worldPerScreenPixel;
            position.y = Mathf.Round(position.y / worldPerScreenPixel) * worldPerScreenPixel;
            position.z = -10f;
            transform.position = position;
        }
    }
}
