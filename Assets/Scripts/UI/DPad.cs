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
        static readonly Color ArrowIdle = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color ArrowActive = new Color(1f, 0.88f, 0.4f, 1f);

        readonly Image[] arrows = new Image[8];
        RectTransform rect;
        bool tracking;
        int pointerId;

        public Direction8? Direction { get; private set; }

        public static DPad Create(Transform parent, Vector2 anchor, Vector2 position, float size)
        {
            var background = UiFactory.CreateImage("DPad", parent, UiFactory.Circle, new Color(0.04f, 0.04f, 0.09f, 0.45f), raycast: true);
            UiFactory.Place(background.rectTransform, anchor, position, new Vector2(size, size));
            var pad = background.gameObject.AddComponent<DPad>();
            pad.rect = background.rectTransform;

            var hub = UiFactory.CreateImage("Hub", background.transform, UiFactory.Circle, new Color(1f, 1f, 1f, 0.08f));
            UiFactory.Place(hub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.36f, size * 0.36f));

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var offset = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * size * 0.34f;
                float arrowSize = size * (i % 2 == 0 ? 0.17f : 0.12f);
                var arrow = UiFactory.CreateImage("Arrow " + (Direction8)i, background.transform, UiFactory.Triangle, ArrowIdle);
                UiFactory.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), offset, new Vector2(arrowSize, arrowSize));
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
                pad.arrows[i] = arrow;
            }
            return pad;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
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
