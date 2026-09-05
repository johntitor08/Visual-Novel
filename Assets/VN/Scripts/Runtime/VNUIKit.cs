// VNUIKit.cs -- fonts and the small set of builders every screen is assembled from.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VN
{
    /// <summary>
    /// Text uses a Windows system face when TextMeshPro can build one, and falls back to the
    /// package default otherwise. Either way no font asset has to be authored in the project.
    /// </summary>
    public static class VNFonts
    {
        static TMP_FontAsset _body;
        static TMP_FontAsset _display;
        static bool _resolved;

        public static TMP_FontAsset Body { get { Resolve(); return _body; } }
        public static TMP_FontAsset Display { get { Resolve(); return _display ?? _body; } }

        static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            TMP_FontAsset fallback = null;
            if (TMP_Settings.instance != null) fallback = TMP_Settings.defaultFontAsset;
            if (fallback == null)
            {
                Debug.LogWarning("[VN] TextMeshPro default font asset is missing. " +
                                 "Run Window > TextMeshPro > Import TMP Essential Resources.");
            }

            _body = TryOsFont("Segoe UI") ?? fallback;
            _display = TryOsFont("Segoe UI Semibold") ?? TryOsFont("Segoe UI") ?? fallback;
        }

        static TMP_FontAsset TryOsFont(string faceName)
        {
            try
            {
                var os = Font.CreateDynamicFontFromOSFont(faceName, 64);
                if (os == null) return null;
                var asset = TMP_FontAsset.CreateFontAsset(os);
                // A dynamic asset that cannot produce a glyph would render an empty game.
                if (asset == null || asset.material == null || !asset.HasCharacter('A')) return null;
                asset.hideFlags = HideFlags.HideAndDontSave;
                return asset;
            }
            catch
            {
                return null;   // Not fatal: the packaged font is perfectly usable.
            }
        }
    }

    /// <summary>Terse builders for uGUI objects, so the screen code stays readable.</summary>
    public static class UIKit
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(100f, 100f);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Panel(string name, Transform parent, Color color, int radius = 22, bool raycast = false)
        {
            return Img(name, parent, VNTextures.Rounded(radius), color, raycast);
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string content, float size, Color color,
                                           TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool display = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = display ? VNFonts.Display : VNFonts.Body;
            if (font != null) t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static CanvasGroup Group(RectTransform rt)
        {
            var g = rt.GetComponent<CanvasGroup>();
            if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
            return g;
        }

        /// <summary>A pill button with a label. Returns the button; its label is the first TMP child.</summary>
        public static Button Btn(string name, Transform parent, string label, Vector2 size, Action onClick,
                                 float fontSize = VNTheme.SizeButton, bool primary = false)
        {
            var img = Panel(name, parent, primary ? new Color(0.949f, 0.706f, 0.408f, 0.92f) : VNTheme.PanelLift, 18, true);
            var rt = img.rectTransform;
            rt.sizeDelta = size;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.28f, 1.28f, 1.28f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var text = Text(name + "Label", rt, label, fontSize,
                primary ? new Color(0.09f, 0.08f, 0.07f) : VNTheme.Ink, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 18f, 18f, 6f, 6f);

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static TextMeshProUGUI LabelOf(Button b)
        {
            return b == null ? null : b.GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>An invisible full-area button, used to catch "click anywhere to advance".</summary>
        public static Button ClickCatcher(string name, Transform parent, Action onClick)
        {
            var img = Img(name, parent, VNTextures.Solid(), new Color(0f, 0f, 0f, 0f), true);
            Stretch(img.rectTransform);
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>A slider styled to match the panels, wired to a value callback.</summary>
        public static Slider MakeSlider(string name, Transform parent, float min, float max, float value, Action<float> onChange)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(420f, 30f);

            var bg = Img(name + "Bg", rt, VNTextures.Rounded(12), new Color(1f, 1f, 1f, 0.16f), true);
            Stretch(bg.rectTransform, 0f, 0f, 11f, 11f);

            var fillArea = Rect("FillArea", rt);
            Stretch(fillArea, 0f, 0f, 11f, 11f);
            var fill = Img("Fill", fillArea, VNTextures.Rounded(12), VNTheme.Accent);
            Stretch(fill.rectTransform);

            var handleArea = Rect("HandleArea", rt);
            Stretch(handleArea, 12f, 12f, 0f, 0f);
            var handle = Img("Handle", handleArea, VNTextures.Circle(), VNTheme.Ink, true);
            handle.rectTransform.sizeDelta = new Vector2(26f, 26f);

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp(value, min, max);
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        /// <summary>A two-state pill used for the on/off options.</summary>
        public static Button Toggle(string name, Transform parent, string label, bool state, Action<bool> onChange)
        {
            bool current = state;
            Button btn = null;
            btn = Btn(name, parent, label + (current ? "   ON" : "   OFF"), new Vector2(300f, 62f), () =>
            {
                current = !current;
                var t = LabelOf(btn);
                if (t != null) t.text = label + (current ? "   ON" : "   OFF");
                if (onChange != null) onChange(current);
            }, VNTheme.SizeSmall + 2f);
            return btn;
        }

        /// <summary>Builds a TMP input field from scratch: background, viewport, text and placeholder.</summary>
        public static TMP_InputField Input(string name, Transform parent, string placeholder, Vector2 size)
        {
            var bg = Panel(name, parent, new Color(1f, 1f, 1f, 0.10f), 16, true);
            bg.rectTransform.sizeDelta = size;

            var viewport = Rect("TextArea", bg.rectTransform);
            Stretch(viewport, 24f, 24f, 12f, 12f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = Text("Text", viewport, "", 40f, VNTheme.Ink, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var hint = Text("Placeholder", viewport, placeholder, 40f, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Left);
            Stretch(hint.rectTransform);
            hint.textWrappingMode = TextWrappingModes.NoWrap;

            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = bg;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 16;
            field.caretWidth = 3;
            field.customCaretColor = true;
            field.caretColor = VNTheme.Accent;
            field.selectionColor = new Color(VNTheme.Accent.r, VNTheme.Accent.g, VNTheme.Accent.b, 0.35f);
            field.onFocusSelectAll = false;
            return field;
        }

        /// <summary>A vertical scrolling list. Returns the content transform to fill.</summary>
        public static RectTransform ScrollList(string name, Transform parent, out ScrollRect scroll, float spacing = 14f,
                                               RectOffset padding = null)
        {
            var root = Rect(name, parent);
            var mask = root.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;

            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 40f;

            var content = Rect("Content", root);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(10, 10, 10, 10);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.content = content;
            scroll.viewport = root;
            return content;
        }

        public static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            var c = g.color; c.a = a; g.color = c;
        }

        /// <summary>Creates the EventSystem if the scene has none, using whichever input module is available.</summary>
        public static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
#else
            var existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
            if (existing != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
