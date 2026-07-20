using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlockEscape.UI
{
    internal static class NeonMenuTheme
    {
        private static readonly Color OverlayColor = new(0.005f, 0.01f, 0.035f, 0.9f);
        private static readonly Color PanelColor = new(0.025f, 0.055f, 0.1f, 0.98f);
        private static readonly Color ButtonColor = new(0.025f, 0.1f, 0.17f, 1f);
        private static readonly Color Cyan = new(0.2f, 0.85f, 1f, 1f);
        private static readonly Color Green = new(0.35f, 1f, 0.62f, 1f);
        private static readonly Color Magenta = new(0.9f, 0.2f, 1f, 1f);
        private static readonly Color Orange = new(1f, 0.48f, 0.18f, 1f);
        private static readonly Color BodyText = new(0.78f, 0.87f, 1f, 1f);

        public static void ApplyOverlay(GameObject overlay)
        {
            if (overlay == null)
                return;

            var overlayImage = overlay.GetComponent<Image>();
            if (overlayImage != null)
                overlayImage.color = OverlayColor;

            for (var i = 0; i < overlay.transform.childCount; i++)
            {
                var panel = overlay.transform.GetChild(i).GetComponent<Image>();
                if (panel == null)
                    continue;
                panel.color = PanelColor;
                AddOutline(panel.gameObject, Cyan, new Vector2(3f, -3f));
            }

            foreach (var text in overlay.GetComponentsInChildren<Text>(true))
                StyleText(text);
            foreach (var button in overlay.GetComponentsInChildren<Button>(true))
                StyleButton(button);
            foreach (var slider in overlay.GetComponentsInChildren<Slider>(true))
                StyleSlider(slider);
            foreach (var toggle in overlay.GetComponentsInChildren<Toggle>(true))
                StyleToggle(toggle);
        }

        public static void ApplyButtons(params Button[] buttons)
        {
            if (buttons == null)
                return;
            foreach (var button in buttons)
                StyleButton(button);
        }

        private static void StyleText(Text text)
        {
            if (text == null)
                return;

            var name = text.gameObject.name;
            if (Contains(name, "Title"))
            {
                text.color = Contains(name, "Game Over") ? Magenta :
                    Contains(name, "Confirmation") ? Orange : Cyan;
                var shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(Magenta.r, Magenta.g, Magenta.b, 0.55f);
                shadow.effectDistance = new Vector2(2f, -2f);
                shadow.useGraphicAlpha = true;
            }
            else if (Contains(name, "Statistics") || Contains(name, "Summary"))
            {
                text.color = Green;
            }
            else
            {
                text.color = BodyText;
            }
        }

        private static void StyleButton(Button button)
        {
            if (button == null)
                return;

            var danger = Contains(button.gameObject.name, "Confirm") ||
                Contains(button.gameObject.name, "Reset") ||
                Contains(button.gameObject.name, "Exit");
            var accent = danger ? Orange : Cyan;
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                image.color = ButtonColor;
                button.targetGraphic = image;
            }

            var colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = new Color(0.04f, 0.3f, 0.4f, 1f);
            colors.pressedColor = new Color(0.38f, 0.06f, 0.42f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.04f, 0.06f, 0.09f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            AddOutline(button.gameObject, accent, new Vector2(2f, -2f));
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.color = Color.white;
        }

        private static void StyleSlider(Slider slider)
        {
            if (slider == null)
                return;

            foreach (var image in slider.GetComponentsInChildren<Image>(true))
                image.color = new Color(0.04f, 0.09f, 0.15f, 1f);
            if (slider.fillRect != null && slider.fillRect.TryGetComponent<Image>(out var fill))
                fill.color = Cyan;
            if (slider.handleRect != null && slider.handleRect.TryGetComponent<Image>(out var handle))
            {
                handle.color = Magenta;
                AddOutline(handle.gameObject, Cyan, Vector2.one);
            }
        }

        private static void StyleToggle(Toggle toggle)
        {
            if (toggle == null)
                return;

            if (toggle.targetGraphic is Image background)
            {
                background.color = ButtonColor;
                AddOutline(background.gameObject, Cyan, Vector2.one);
            }
            if (toggle.graphic is Image checkmark)
                checkmark.color = Green;
        }

        private static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            var outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static bool Contains(string value, string part) =>
            value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
