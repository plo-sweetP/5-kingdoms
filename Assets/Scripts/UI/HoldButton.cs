using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FiveKingdoms.UI
{
    /// <summary>
    /// Touch button that fires on finger-down (snappier than uGUI's Button, which waits for release) and
    /// tracks multiple fingers, so it works while the other thumb is on the D-pad. Drawn with a piece of the HUD art
    /// (a round or rectangular button of the kit, or a tinted frame); pressed, it shows the art's pressed version
    /// where there is one and its label sinks with it. Brackets close around it while it is the one being aimed.
    /// </summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        static readonly Color DisabledTint = new Color(0.62f, 0.62f, 0.66f, 0.6f);
        static readonly Color PressedTint = new Color(0.8f, 0.8f, 0.8f, 1f);

        readonly HashSet<int> pointers = new HashSet<int>();
        Image background;
        Text label;
        RectTransform labelRect;
        Image brackets;
        Image icon;
        float iconOffset, labelShift;
        string art;
        Color tint = Color.white;
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

        /// <summary><paramref name="art"/>: a piece of HUD art by name ("tiny_blue", "button_dark", "frame", ...).</summary>
        public static HoldButton Create(Transform parent, string name, string text, Vector2 anchor, Vector2 position,
            Vector2 size, string art, int fontSize, Color? tint = null)
        {
            var image = UiFactory.CreatePanel(name, parent, art, tint, raycast: true);
            UiFactory.Place(image.rectTransform, anchor, position, size);
            var button = image.gameObject.AddComponent<HoldButton>();
            button.background = image;
            button.art = art;
            button.tint = tint ?? Color.white;
            button.label = UiFactory.CreateText("Label", image.transform, text, fontSize, TextAnchor.MiddleCenter, Color.white);
            button.labelRect = UiFactory.Stretch(button.label.rectTransform);
            button.Refresh();
            return button;
        }

        public void SetLabel(string text) => label.text = text;

        /// <summary>
        /// A glyph on the button's face (a piece of HUD art, drawn at its own size), <paramref name="offset"/> from the
        /// middle; the label moves <paramref name="labelShift"/> to the side to stand next to it (the Berry's count).
        /// </summary>
        public void SetIcon(string name, float offset = 0f, float labelShift = 0f)
        {
            if (icon == null) icon = UiFactory.CreateIcon("Icon", transform, name);
            else
            {
                icon.sprite = UiFactory.Art(name);
                UiArtScaler.Size(icon);
            }
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.transform.SetSiblingIndex(0); // Under the label and the aiming brackets.
            iconOffset = offset;
            this.labelShift = labelShift;
            Refresh();
        }

        /// <summary>Switches to another piece of art (a skill that is ready turns gold, say).</summary>
        public void SetArt(string name)
        {
            if (art == name) return;
            art = name;
            Refresh();
        }

        /// <summary>The colour the art is multiplied with (tinted frames: party cards, tags).</summary>
        public void SetTint(Color color)
        {
            tint = color;
            Refresh();
        }

        /// <summary>The kit's selection brackets around the button: it is the one being aimed.</summary>
        public void SetHighlight(bool on)
        {
            if (on && brackets == null)
            {
                brackets = UiFactory.CreatePanel("Brackets", transform, "brackets");
                var rect = UiFactory.Stretch(brackets.rectTransform);
                rect.offsetMin = new Vector2(-14f, -14f);
                rect.offsetMax = new Vector2(14f, 14f);
            }
            if (brackets != null) brackets.gameObject.SetActive(on);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointers.Add(eventData.pointerId);
            Refresh();
            Press();
        }

        /// <summary>What a finger going down does, for a key or a controller button that stands for it (the pause menu).</summary>
        public void Press()
        {
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
            bool hasPressedArt = UiFactory.HasArt(art + "_down");
            background.sprite = UiFactory.Art(pressed && hasPressedArt ? art + "_down" : art);
            background.color = !interactable ? tint * DisabledTint : pressed && !hasPressedArt ? tint * PressedTint : tint;
            label.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            // The label sits on the button's face: up a little while the button stands on its side, down when pressed.
            float lift = (pressed ? -1f : 2f) * UiArtScaler.UnitsPerArtPixel;
            labelRect.offsetMin = new Vector2(labelShift, lift);
            labelRect.offsetMax = new Vector2(labelShift, lift);
            if (icon == null) return;
            icon.color = interactable ? Color.white : DisabledTint;
            icon.rectTransform.anchoredPosition = new Vector2(iconOffset, lift);
        }
    }
}
