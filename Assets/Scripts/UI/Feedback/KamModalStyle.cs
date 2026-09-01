using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KAM.UI.Feedback
{
    internal static class KamModalStyle
    {
        private const string FontPath = "Assets/TextMesh Pro/Fonts/CinzelDecorative-Bold SDF.asset";
        private const string PanelPath = "Assets/Assets/Art/Kaan UI V2/kam_ui_panel_v4.png";
        private const string HeaderPath = "Assets/Assets/Art/Kaan UI V2/kam_ui_panel_header_v4.png";
        private const string ButtonPath = "Assets/Assets/Art/Kaan UI V2/kam_ui_button_frame_v4.png";
        private const string KamGlyphPath = "Assets/Assets/Art/Kaan UI V2/kam_ui_glyph_kam_v4.png";
        private const string SkullGlyphPath = "Assets/Assets/Art/Kaan UI V2/kam_ui_glyph_skull_v4.png";
        private const string ResourcesPath = "KamUI/";

        private static TMP_FontAsset _font;
        private static Sprite _panelSprite;
        private static Sprite _headerSprite;
        private static Sprite _buttonSprite;
        private static Sprite _kamGlyph;
        private static Sprite _skullGlyph;

        public static Color BackdropColor => new Color(0.01f, 0.008f, 0.006f, 0.74f);
        public static Color PanelFallbackColor => new Color(0.05f, 0.055f, 0.075f, 0.98f);
        public static Color HeaderFallbackColor => new Color(0.08f, 0.075f, 0.1f, 0.98f);
        public static Color ButtonFallbackColor => new Color(0.09f, 0.095f, 0.13f, 0.98f);
        public static Color Gold => new Color(1f, 0.82f, 0.12f, 1f);
        public static Color Bone => new Color(0.9f, 0.86f, 0.74f, 1f);
        public static Color Red => new Color(0.72f, 0.04f, 0.04f, 1f);

        public static Sprite PanelSprite => _panelSprite != null ? _panelSprite : _panelSprite = LoadSprite(PanelPath);
        public static Sprite HeaderSprite => _headerSprite != null ? _headerSprite : _headerSprite = LoadSprite(HeaderPath);
        public static Sprite ButtonSprite => _buttonSprite != null ? _buttonSprite : _buttonSprite = LoadSprite(ButtonPath);
        public static Sprite KamGlyph => _kamGlyph != null ? _kamGlyph : _kamGlyph = LoadSprite(KamGlyphPath);
        public static Sprite SkullGlyph => _skullGlyph != null ? _skullGlyph : _skullGlyph = LoadSprite(SkullGlyphPath);

        public static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        public static void ConfigureCanvas(Canvas canvas, CanvasScaler scaler, int sortingOrder)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        public static Image AddImage(GameObject obj, Sprite sprite, Color fallbackColor, bool preserveAspect = false)
        {
            Image image = obj.AddComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            return image;
        }

        public static TextMeshProUGUI AddText(
            GameObject obj,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            FontStyles style = FontStyles.Normal)
        {
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;

            TMP_FontAsset font = GetFont();
            if (font != null)
                text.font = font;

            return text;
        }

        public static void ApplyTextStyle(
            TextMeshProUGUI text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            FontStyles style = FontStyles.Normal)
        {
            if (text == null)
                return;

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_FontAsset font = GetFont();
            if (font != null)
                text.font = font;
        }

        public static void ApplyImageStyle(Image image, Sprite sprite, Color fallbackColor, bool preserveAspect = false)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.type = Image.Type.Simple;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
        }

        public static Button AddStyledButton(GameObject obj, UnityEngine.Events.UnityAction onClick)
        {
            Sprite buttonSprite = ButtonSprite;
            Image image = AddImage(obj, buttonSprite, ButtonFallbackColor);

            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.normalColor = buttonSprite != null ? Color.white : ButtonFallbackColor;
            colors.highlightedColor = buttonSprite != null
                ? new Color(1f, 0.92f, 0.55f, 1f)
                : new Color(0.18f, 0.16f, 0.2f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = buttonSprite != null
                ? new Color(0.78f, 0.62f, 0.22f, 1f)
                : new Color(0.24f, 0.2f, 0.16f, 1f);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.38f, 0.55f);
            button.colors = colors;

            return button;
        }

        private static TMP_FontAsset GetFont()
        {
            if (_font != null)
                return _font;

#if UNITY_EDITOR
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
#endif
            return _font;
        }

        private static Sprite LoadSprite(string path)
        {
            string resourceName = System.IO.Path.GetFileNameWithoutExtension(path);

            Sprite resourceSprite = Resources.Load<Sprite>(ResourcesPath + resourceName);
            if (resourceSprite != null)
                return resourceSprite;

            Texture2D resourceTexture = Resources.Load<Texture2D>(ResourcesPath + resourceName);
            if (resourceTexture != null)
            {
                return Sprite.Create(
                    resourceTexture,
                    new Rect(0f, 0f, resourceTexture.width, resourceTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
            return null;
#endif
        }
    }
}
