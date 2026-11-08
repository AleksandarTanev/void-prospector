using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VoidProspector.UI
{
    /// <summary>Builds greybox uGUI elements in code. Reference resolution 1080×1920 (portrait).</summary>
    public static class UiFactory
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);

        /// <summary>Path of the outline preset that ships with TMP Essential Resources (inside a Resources folder).</summary>
        const string OutlineMaterialPath = "Fonts & Materials/LiberationSans SDF - Outline";

        static Material _outline;

        /// <summary>The default font's material with a dark outline, for text drawn over the game (popups, toasts).</summary>
        public static Material OutlineMaterial
        {
            get
            {
                if (_outline == null) _outline = Resources.Load<Material>(OutlineMaterialPath);
                return _outline;
            }
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Canvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>A TextMeshPro label with the default font, no wrapping and overflow allowed (sizes are in reference pixels).</summary>
        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TextAlignmentOptions alignment, Color color)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = alignment;
            t.color = color;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Gives a label a dark outline so it reads over the meteor field.</summary>
        public static void AddOutline(TMP_Text t)
        {
            var mat = OutlineMaterial;
            if (mat != null) t.fontSharedMaterial = mat;
            else Debug.LogWarning($"UiFactory: outline material '{OutlineMaterialPath}' not found; import TMP Essential Resources");
        }

        public static Button Button(Transform parent, string name, Color color, UnityAction onClick, out TextMeshProUGUI label, int fontSize)
        {
            var img = Panel(name, parent, color);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            b.colors = colors;
            b.onClick.AddListener(onClick);

            label = Text(img.transform, "Label", name, fontSize, TextAlignmentOptions.Center, Color.white);
            Stretch(label.rectTransform);
            return b;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Anchors horizontally between x0..x1 (0–1) at a pixel offset from the parent's bottom.</summary>
        public static void PlaceFromBottom(RectTransform rt, float x0, float x1, float bottom, float height, float padX)
        {
            rt.anchorMin = new Vector2(x0, 0);
            rt.anchorMax = new Vector2(x1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(padX, bottom);
            rt.offsetMax = new Vector2(-padX, bottom + height);
        }

        /// <summary>A strip along the top (fromTop = true) or bottom edge of the parent, <paramref name="height"/> pixels tall.</summary>
        public static void Strip(RectTransform rt, bool fromTop, float height)
        {
            float y = fromTop ? 1 : 0;
            rt.anchorMin = new Vector2(0, y);
            rt.anchorMax = new Vector2(1, y);
            rt.pivot = new Vector2(0.5f, y);
            rt.offsetMin = fromTop ? new Vector2(0, -height) : Vector2.zero;
            rt.offsetMax = fromTop ? Vector2.zero : new Vector2(0, height);
        }
    }
}
