using FiveKingdoms.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>
    /// Eight-way touch D-pad. Press anywhere on it and slide: the direction comes from the finger's angle
    /// around the center in 45° slices, so diagonals are as easy as straight moves.
    /// </summary>
    public sealed class DPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        const float DeadZone = 0.14f; // Fraction of the pad's width around the center that means "no direction".
        static readonly Color ArrowIdle = new Color(1f, 1f, 1f, 0.6f);
        static readonly Color ArrowActive = new Color(1f, 0.88f, 0.4f, 1f);

        readonly Image[] arrows = new Image[8];
        RectTransform rect;
        CanvasGroup group;
        bool tracking;
        int pointerId;
        bool interactable = true;

        public Direction8? Direction { get; private set; }

        /// <summary>Pressed while disabled (e.g. during auto-pilot), so the HUD can explain why nothing happens.</summary>
        public event System.Action DisabledPressed;

        /// <summary>Disabled, the pad is dimmed and reports no direction.</summary>
        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                group.alpha = value ? 1f : 0.35f;
                if (!value)
                {
                    tracking = false;
                    SetDirection(null);
                }
            }
        }

        /// <summary>
        /// The pad listens over a square of <paramref name="size"/>; its disc, hub and arrows are HUD art drawn at their
        /// own size in the middle of it.
        /// </summary>
        public static DPad Create(Transform parent, Vector2 anchor, Vector2 position, float size)
        {
            var area = UiFactory.CreateImage("DPad", parent, null, Color.clear, raycast: true);
            UiFactory.Place(area.rectTransform, anchor, position, new Vector2(size, size));
            var pad = area.gameObject.AddComponent<DPad>();
            pad.rect = area.rectTransform;
            pad.group = area.gameObject.AddComponent<CanvasGroup>();

            var disc = UiFactory.CreateIcon("Disc", area.transform, "dpad");
            Center(disc.rectTransform, Vector2.zero);
            var hub = UiFactory.CreateIcon("Hub", area.transform, "dpad_hub");
            Center(hub.rectTransform, Vector2.zero);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var offset = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * size * 0.33f;
                // Straight and diagonal arrows are two drawings, each only ever turned by quarter turns, so they stay crisp.
                bool diagonal = i % 2 == 1;
                var arrow = UiFactory.CreateIcon("Arrow " + (Direction8)i, area.transform, diagonal ? "dpad_arrow_diagonal" : "dpad_arrow", ArrowIdle);
                Center(arrow.rectTransform, offset);
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(diagonal ? angle - 45f : angle));
                pad.arrows[i] = arrow;
            }
            return pad;
        }

        static void Center(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable)
            {
                DisabledPressed?.Invoke();
                return;
            }
            if (tracking) return;
            tracking = true;
            pointerId = eventData.pointerId;
            Track(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (tracking && eventData.pointerId == pointerId) Track(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!tracking || eventData.pointerId != pointerId) return;
            tracking = false;
            SetDirection(null);
        }

        void OnDisable()
        {
            tracking = false;
            SetDirection(null);
        }

        void Track(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local))
                return;
            if (local.magnitude < rect.rect.width * DeadZone)
            {
                SetDirection(null);
                return;
            }
            float angle = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg; // 0° = up, growing clockwise like Direction8.
            int slice = Mathf.RoundToInt((angle + 360f) % 360f / 45f) % 8;
            SetDirection((Direction8)slice);
        }

        void SetDirection(Direction8? direction)
        {
            Direction = direction;
            for (int i = 0; i < arrows.Length; i++)
                if (arrows[i] != null)
                    arrows[i].color = direction.HasValue && (int)direction.Value == i ? ArrowActive : ArrowIdle;
        }
    }
}
