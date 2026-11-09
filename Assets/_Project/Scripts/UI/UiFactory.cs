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

        /// <summary>
        /// A full-screen dialog on its own canvas: a dim layer that swallows taps meant for the game, and a panel
        /// inset by <paramref name="margin"/> pixels. Returns the panel; <paramref name="canvas"/> is what to show and hide.
        /// </summary>
        public static RectTransform Modal(string name, int sortingOrder, Color panelColor, Vector2 margin, out RectTransform canvas)
        {
            canvas = Canvas(name);
            canvas.GetComponent<Canvas>().sortingOrder = sortingOrder;
            var dim = Panel("Dim", canvas, new Color(0f, 0f, 0f, 0.75f));
            Stretch(dim.rectTransform);
            var panel = Panel("Panel", canvas, panelColor).rectTransform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = margin;
            panel.offsetMax = -margin;
            return panel;
        }

        /// <summary>A vertical scrolling list filling <paramref name="parent"/> minus the given insets; returns the content rect rows go into.</summary>
        public static RectTransform ScrollList(RectTransform parent, Vector2 offsetMin, Vector2 offsetMax, float spacing)
        {
            var view = Rect("Scroll View", parent);
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.offsetMin = offsetMin;
            view.offsetMax = offsetMax;
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);   // receives drags
            view.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            return content;
        }

        /// <summary>A panel row of fixed height inside a layout list.</summary>
        public static RectTransform ListRow(RectTransform content, string name, float height, Color color)
        {
            var bg = Panel(name, content, color);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return bg.rectTransform;
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

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
