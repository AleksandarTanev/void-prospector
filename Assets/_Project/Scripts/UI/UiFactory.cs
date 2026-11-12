using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VoidProspector.UI
{
    /// <summary>
    /// Builds the uGUI in code with the sci-fi theme (<see cref="UiTheme"/>). Reference resolution 1080×1920 (portrait).
    /// </summary>
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

        /// <summary>A flat coloured rectangle (dim layers, drag catchers, codex cells).</summary>
        public static Image Panel(string name, Transform parent, Color color)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>
        /// A 9-sliced frame from <c>Resources/UI/Frames</c>. <paramref name="borderScale"/> scales the frame's corners
        /// (0.5 = half size, for small elements). Falls back to <paramref name="fallback"/> as a flat colour if the
        /// sprite is missing.
        /// </summary>
        public static Image Frame(string name, Transform parent, string sprite, Color fallback, float borderScale = 1f)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            SetFrame(img, sprite, fallback, borderScale);
            return img;
        }

        public static void SetFrame(Image img, string sprite, Color fallback, float borderScale = 1f)
        {
            var s = UiTheme.Frame(sprite);
            if (s == null)
            {
                img.sprite = null;
                img.color = fallback;
                return;
            }
            img.sprite = s;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / borderScale;
            img.color = Color.white;
        }

        /// <summary>An icon image (white art, tinted by <paramref name="color"/>), aspect kept. Hidden if the sprite is missing.</summary>
        public static Image Icon(Transform parent, string name, Sprite sprite, Color color)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = sprite != null;
            return img;
        }

        /// <summary>Places <paramref name="rt"/> as a square of <paramref name="size"/> centred on a point relative to its parent's anchors.</summary>
        public static void Square(RectTransform rt, Vector2 anchor, Vector2 offset, float size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(size, size);
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

        /// <summary>A themed button with a centred label (bold, white).</summary>
        public static UiButton Button(Transform parent, string name, ButtonStyle style, UnityAction onClick, out TextMeshProUGUI label, int fontSize)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            var b = img.gameObject.AddComponent<UiButton>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;     // mobile: no hover
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = Color.white;        // the disabled frame does the work
            colors.fadeDuration = 0.06f;
            b.colors = colors;
            b.onClick.AddListener(onClick);

            label = Text(img.transform, "Label", name, fontSize, TextAlignmentOptions.Center, Color.white);
            label.fontStyle = FontStyles.Bold;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12, 4);
            label.rectTransform.offsetMax = new Vector2(-12, -4);
            b.Setup(style, label);
            return b;
        }

        /// <summary>A square icon button (frame + icon), e.g. close or depth arrows.</summary>
        public static UiButton IconButton(Transform parent, string name, string icon, UnityAction onClick, float iconSize = 0.6f)
        {
            var img = Frame(name, parent, UiTheme.IconButton, UiTheme.ButtonFallback(ButtonStyle.Secondary), 0.7f);
            var b = img.gameObject.AddComponent<UiButton>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.85f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.06f;
            b.colors = colors;
            b.onClick.AddListener(onClick);

            var ic = Icon(img.transform, "Icon", UiTheme.Icon(icon), UiTheme.Text);
            var irt = ic.rectTransform;
            irt.anchorMin = new Vector2(0.5f - iconSize / 2, 0.5f - iconSize / 2);
            irt.anchorMax = new Vector2(0.5f + iconSize / 2, 0.5f + iconSize / 2);
            irt.offsetMin = irt.offsetMax = Vector2.zero;
            b.SetupFixedFrame(ic);
            return b;
        }

        /// <summary>
        /// A dialog on its own canvas: a dim layer that swallows taps meant for the game, a framed panel with a title
        /// plate and an X close button. With <paramref name="aboveTabBar"/> the dim and the panel stop above the HUD's
        /// tab bar, so the tabs stay usable. Returns the content area under the title; <paramref name="canvas"/> is what
        /// to show and hide.
        /// </summary>
        public static RectTransform Modal(string name, string title, int sortingOrder, bool aboveTabBar, UnityAction onClose, out RectTransform canvas)
        {
            canvas = Canvas(name);
            canvas.GetComponent<Canvas>().sortingOrder = sortingOrder;
            float bottom = aboveTabBar ? Hud.TabBarHeight : 0f;

            var dim = Panel("Dim", canvas, UiTheme.Dim);
            Stretch(dim.rectTransform);
            dim.rectTransform.offsetMin = new Vector2(0, bottom);

            var panel = Frame("Panel", canvas, UiTheme.Panel, UiTheme.PanelFallback).rectTransform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(24, bottom + 20);
            panel.offsetMax = new Vector2(-24, -60);

            var plate = Frame("Title Plate", panel, UiTheme.Header, UiTheme.PanelFallback).rectTransform;
            Strip(plate, true, 110);
            plate.offsetMin += new Vector2(20, -16);
            plate.offsetMax += new Vector2(-150, -16);
            var t = Text(plate, "Title", title, 54, TextAlignmentOptions.Center, UiTheme.Text);
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 6;
            Stretch(t.rectTransform);

            var close = IconButton(panel, "Close", "close", onClose);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(1, 1);
            crt.anchoredPosition = new Vector2(-20, -16);
            crt.sizeDelta = new Vector2(110, 110);

            var content = Rect("Content", panel);
            Stretch(content);
            content.offsetMin = new Vector2(24, 24);
            content.offsetMax = new Vector2(-24, -140);
            return content;
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

        /// <summary>A framed row of fixed height inside a layout list.</summary>
        public static RectTransform ListRow(RectTransform content, string name, float height)
        {
            var bg = Frame(name, content, UiTheme.Row, new Color(1f, 1f, 1f, 0.06f), 0.8f);
            bg.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return bg.rectTransform;
        }

        /// <summary>A bold section header inside a layout list.</summary>
        public static TextMeshProUGUI ListHeader(RectTransform content, string text)
        {
            var t = Text(content, text, text, 32, TextAlignmentOptions.BottomLeft, UiTheme.Accent);
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 4;
            t.margin = new Vector4(8, 0, 0, 6);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            return t;
        }

        /// <summary>
        /// A progress bar (track + fill). Set the fill with <c>fill.anchorMax = new Vector2(fraction, 1)</c>.
        /// <paramref name="tint"/> colours the fill (the art is white); change it later through <paramref name="fillImage"/>.
        /// </summary>
        public static RectTransform Bar(Transform parent, string name, Color tint, out Image fillImage)
        {
            var track = Frame(name, parent, UiTheme.BarTrack, new Color(1, 1, 1, 0.12f), 0.5f).rectTransform;
            var fill = Rect("Fill", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            fill.offsetMin = new Vector2(3, 3);
            fill.offsetMax = new Vector2(0, -3);
            fillImage = fill.gameObject.AddComponent<Image>();
            SetFrame(fillImage, UiTheme.BarFill, tint, 0.5f);
            if (fillImage.sprite != null) fillImage.color = tint;
            return fill;
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
