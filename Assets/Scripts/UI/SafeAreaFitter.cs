using UnityEngine;

namespace FiveKingdoms.UI
{
    /// <summary>Keeps this rect inside Screen.safeArea, clear of notches, punch-hole cameras and rounded corners.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        Vector2Int screen;

        void Update()
        {
            var safe = Screen.safeArea;
            if (safe == applied && screen.x == Screen.width && screen.y == Screen.height) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            applied = safe;
            screen = new Vector2Int(Screen.width, Screen.height);
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
