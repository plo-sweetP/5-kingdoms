using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>
    /// Touch button that fires on finger-down (snappier than uGUI's Button, which waits for release) and
    /// tracks multiple fingers, so it works while the other thumb is on the D-pad.
    /// </summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        readonly HashSet<int> pointers = new HashSet<int>();
        Image background;
        Text label;
        Color baseColor;
        bool interactable = true;

        /// <summary>Finger went down on the button while it is interactable.</summary>
        public event Action Pressed;

        /// <summary>Finger went down while the button is disabled (e.g. to explain why).</summary>
        public event Action DisabledPressed;

        public bool IsHeld => interactable && pointers.Count > 0;

        public bool Interactable
        {
            get => interactable;
            set
            {
                if (interactable == value) return;
                interactable = value;
                Refresh();
            }
        }

        public static HoldButton Create(Transform parent, string name, string text, Vector2 anchor, Vector2 position,
            Vector2 size, Color color, int fontSize, bool round)
        {
            var image = UiFactory.CreateImage(name, parent, round ? UiFactory.Circle : UiFactory.RoundedRect, color, raycast: true);
            UiFactory.Place(image.rectTransform, anchor, position, size);
            var button = image.gameObject.AddComponent<HoldButton>();
            button.background = image;
            button.baseColor = color;
            button.label = UiFactory.CreateText("Label", image.transform, text, fontSize, TextAnchor.MiddleCenter, Color.white);
            UiFactory.Stretch(button.label.rectTransform);
            button.Refresh();
            return button;
        }

        public void SetLabel(string text) => label.text = text;

        public void OnPointerDown(PointerEventData eventData)
        {
            pointers.Add(eventData.pointerId);
            Refresh();
            if (interactable) Pressed?.Invoke();
            else DisabledPressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointers.Remove(eventData.pointerId);
            Refresh();
        }

        void OnDisable()
        {
            pointers.Clear();
            Refresh();
        }

        void Refresh()
        {
            if (background == null) return;
            bool pressed = interactable && pointers.Count > 0;
            var color = baseColor;
            if (!interactable) color.a *= 0.4f;
            else if (pressed) color = Color.Lerp(color, Color.white, 0.35f);
            background.color = color;
            label.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            transform.localScale = pressed ? Vector3.one * 0.93f : Vector3.one;
        }
    }
}
