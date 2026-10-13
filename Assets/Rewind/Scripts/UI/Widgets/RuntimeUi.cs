using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rewind.UI
{
    /// <summary>
    /// Builds uGUI in code, in the same colours as the generated screens. Used by screens that are made at runtime
    /// (the in-game level editor) so they need no prefab.
    /// </summary>
    public static class RuntimeUi
    {
        public static readonly Color Ink = Hex(0x090A14), Bezel = Hex(0x12142B), Deck = Hex(0x1C1F40), Line = Hex(0x2B3060);
        public static readonly Color Fg = Hex(0xEBE9F8), Muted = Hex(0x9A9DC6), Rec = Hex(0xFF4B55), Cyan = Hex(0x48D8FF), Exit = Hex(0xC4FF6A), Amber = Hex(0xFFB547);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Fill(RectTransform rt, Color color, bool raycast)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static void Size(Component c, float width, float height)
        {
            var le = c.GetComponent<LayoutElement>();   // no ?? here: Unity's fake null would slip past it in the Editor
            if (!le) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) le.minWidth = le.preferredWidth = width;
            if (height >= 0) le.minHeight = le.preferredHeight = height;
        }

        public static void Flexible(RectTransform parent)
        {
            var spacer = Rect("Space", parent);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        public static TextMeshProUGUI Label(RectTransform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align, FontStyles style = FontStyles.Bold)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.characterSpacing = style == FontStyles.Bold ? 2f : 0f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return h;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing, RectOffset padding = null)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            if (padding != null) v.padding = padding;
            return v;
        }

        /// <summary>A flat button with a centred label. Keyboard navigation is off so arrow keys never move focus.</summary>
        public static UnityEngine.UI.Button Button(RectTransform parent, string label, float width, float height, UnityAction onClick,
            out TextMeshProUGUI text, float fontSize = 24f)
        {
            var rt = Rect(label, parent);
            var bg = Fill(rt, Deck, true);
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(2f, -2f);
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.5f, 1.5f, 1.8f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.9f);
            colors.colorMultiplier = 1.5f;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            text = Label(rt, "Label", label, fontSize, Fg, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 8, 0, 8, 0);
            Size(rt, width, height);
            if (onClick != null) button.onClick.AddListener(onClick);
            button.onClick.AddListener(UIEvents.RaiseActivated);
            return button;
        }

        public static UnityEngine.UI.Button Button(RectTransform parent, string label, float width, float height, UnityAction onClick, float fontSize = 24f) =>
            Button(parent, label, width, height, onClick, out _, fontSize);

        /// <summary>A one-line text field. Build it under an inactive parent so it wakes up fully wired.</summary>
        public static TMP_InputField InputField(RectTransform parent, string placeholder, float width, float height, int characterLimit)
        {
            var rt = Rect("Field", parent);
            var bg = Fill(rt, Ink, true);
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(2f, -2f);
            var area = Rect("TextArea", rt);
            Stretch(area, 14, 4, 14, 4);
            area.gameObject.AddComponent<RectMask2D>();
            var text = Label(area, "Text", "", 26, Fg, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            Stretch(text.rectTransform);
            var hint = Label(area, "Placeholder", placeholder, 26, Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
            Stretch(hint.rectTransform);
            var field = rt.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = characterLimit;
            var nav = field.navigation;
            nav.mode = Navigation.Mode.None;
            field.navigation = nav;
            Size(rt, width, height);
            return field;
        }
    }
}
