using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using GrandWorld.Rendering;
using GrandWorld.UI;
using static GrandWorld.EditorTools.ContentBuilder;

namespace GrandWorld.EditorTools
{
    /// <summary>Создание элементов интерфейса в едином стиле: стекло, крупная типографика, тёплый акцент.</summary>
    public static class UIFactory
    {
        public static readonly Color Accent = new Color(1f, 0.74f, 0.42f, 1f);
        public static readonly Color TextMain = new Color(1f, 1f, 1f, 0.92f);
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color GlassTint = new Color(0.06f, 0.08f, 0.11f, 0.42f);

        // ---------------------------------------------------------------- базовые

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static Image Image(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Text(RectTransform rt, string text, float size, bool bold = false,
                                           TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, Color? color = null)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? FontBold : FontRegular;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextMain;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>Стеклянная панель с размытым фоном и тонкой светлой обводкой.</summary>
        public static Image Glass(RectTransform rt, Color tint)
        {
            var img = Image(rt, RoundedSprite, tint, true);
            img.material = BlurPanelMaterial;
            rt.gameObject.AddComponent<UIBlurPanel>();
            // Едва заметный светлый блик сверху — стекло выглядит объёмнее
            var sheen = Image(Rect("Sheen", rt), GradientSprite, new Color(1f, 1f, 1f, 0.05f));
            sheen.rectTransform.anchorMin = new Vector2(0, 1);
            sheen.rectTransform.anchorMax = new Vector2(1, 1);
            sheen.rectTransform.pivot = new Vector2(0.5f, 1f);
            sheen.rectTransform.sizeDelta = new Vector2(-24, 3);
            sheen.rectTransform.anchoredPosition = new Vector2(0, -10);
            return img;
        }

        // ---------------------------------------------------------------- элементы управления

        /// <summary>Большая кнопка меню: текст слева, полоса-акцент и подсветка при наведении.</summary>
        public static Button MenuButton(Transform parent, string name, string label, Vector2 size)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = size;
            var bg = Image(rt, RoundedSprite, new Color(1f, 1f, 1f, 0.0f), true);
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bg;

            var glow = Image(Stretch(Rect("Glow", rt)), GradientSprite, new Color(Accent.r, Accent.g, Accent.b, 0.22f));
            var bar = Image(Place(Rect("Bar", glow.rectTransform), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(5, size.y * 0.55f)),
                            PillSprite, Accent);
            bar.type = UnityEngine.UI.Image.Type.Sliced;

            var text = Text(Stretch(Rect("Label", rt), 28, 12), label, 34, true);
            var fx = rt.gameObject.AddComponent<UIButtonFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("label").objectReferenceValue = text;
            so.FindProperty("hoverScale").floatValue = 1.02f;
            so.FindProperty("glowAlpha").floatValue = 0.28f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>Строка настроек: подпись слева, элемент справа.</summary>
        public static RectTransform Row(Transform parent, string name, string label, float width)
        {
            var row = Rect(name, parent);
            row.sizeDelta = new Vector2(width, 64);
            Text(Place(Rect("Label", row), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(width * 0.45f, 60)),
                 label, 28, false, TextAlignmentOptions.MidlineLeft, TextMain);
            return row;
        }

        static RectTransform ControlArea(RectTransform row, float width)
        {
            return Place(Rect("Control", row), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(width, 56));
        }

        public static Slider Slider(RectTransform row, out TextMeshProUGUI valueLabel)
        {
            var area = ControlArea(row, 360);
            var sliderRt = Place(Rect("Slider", area), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(270, 40));
            var track = Image(Place(Rect("Track", sliderRt), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(270, 8)),
                              PillSprite, new Color(1, 1, 1, 0.16f), true);
            track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0, 8);

            var fillArea = Rect("Fill Area", sliderRt);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 8);
            var fill = Image(Rect("Fill", fillArea), PillSprite, Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("Handle Area", sliderRt);
            Stretch(handleArea, 12, 12);
            var handle = Image(Rect("Handle", handleArea), CircleSprite, Color.white, true);
            handle.rectTransform.sizeDelta = new Vector2(28, 28);
            var ring = Image(Place(Rect("Focus", handle.rectTransform), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44)),
                             RingSprite, new Color(Accent.r, Accent.g, Accent.b, 0f));

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.ColorTint;
            var colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Accent;
            colors.selectedColor = Accent;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.fadeDuration = 0.12f;
            slider.colors = colors;
            AddFocusFx(sliderRt, ring);

            valueLabel = Text(Place(Rect("Value", area), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(80, 50)),
                              "", 26, true, TextAlignmentOptions.MidlineRight, TextDim);
            return slider;
        }

        public static Toggle Toggle(RectTransform row)
        {
            var area = ControlArea(row, 360);
            var rt = Place(Rect("Toggle", area), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(84, 44));
            var bg = Image(Stretch(Rect("Background", rt)), PillSprite, new Color(1, 1, 1, 0.16f), true);
            var on = Image(Stretch(Rect("On", rt)), PillSprite, Accent);
            var knob = Image(Place(Rect("Knob", rt), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(34, 34)),
                             CircleSprite, Color.white);
            var ring = Image(Place(Rect("Focus", rt), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100, 60)),
                             PillSprite, new Color(Accent.r, Accent.g, Accent.b, 0f));
            ring.fillCenter = false;

            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = on;
            toggle.transition = Selectable.Transition.None;
            var sw = rt.gameObject.AddComponent<ToggleSwitchFx>();
            var so = new SerializedObject(sw);
            so.FindProperty("knob").objectReferenceValue = knob.rectTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            AddFocusFx(rt, ring);
            return toggle;
        }

        public static OptionSelector Selector(RectTransform row)
        {
            var area = ControlArea(row, 360);
            var rt = Place(Rect("Selector", area), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(300, 50));
            var bg = Image(Stretch(Rect("Background", rt)), PillSprite, new Color(1, 1, 1, 0.08f), true);
            Text(Place(Rect("Left", rt), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(30, 50)),
                 "‹", 34, true, TextAlignmentOptions.Center, TextDim);
            Text(Place(Rect("Right", rt), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(30, 50)),
                 "›", 34, true, TextAlignmentOptions.Center, TextDim);
            var value = Text(Stretch(Rect("Value", rt), 44, 44), "Высокое", 27, true, TextAlignmentOptions.Center, TextMain);
            var ring = Image(Place(Rect("Focus", rt), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(312, 62)),
                             PillSprite, new Color(Accent.r, Accent.g, Accent.b, 0f));
            ring.fillCenter = false;

            var selector = rt.gameObject.AddComponent<OptionSelector>();
            selector.targetGraphic = bg;
            selector.transition = Selectable.Transition.None;
            var so = new SerializedObject(selector);
            so.FindProperty("valueLabel").objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            AddFocusFx(rt, ring);
            return selector;
        }

        /// <summary>Подсветка-рамка вокруг элемента при выделении (геймпад/клавиатура/мышь).</summary>
        static void AddFocusFx(RectTransform rt, Graphic ring)
        {
            var fx = rt.gameObject.AddComponent<UIButtonFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("glow").objectReferenceValue = ring;
            so.FindProperty("glowAlpha").floatValue = 0.9f;
            so.FindProperty("hoverScale").floatValue = 1.0f;
            so.FindProperty("pressScale").floatValue = 1.0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- система событий

        public static EventSystem EventSystem(InputActionAsset actions)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = go.GetComponent<InputSystemUIInputModule>();
            module.actionsAsset = actions;
            module.point = Ref(actions, "UI/Point");
            module.leftClick = Ref(actions, "UI/Click");
            module.scrollWheel = Ref(actions, "UI/ScrollWheel");
            module.move = Ref(actions, "UI/Navigate");
            module.submit = Ref(actions, "UI/Submit");
            module.cancel = Ref(actions, "UI/Cancel");
            module.rightClick = null;
            module.middleClick = null;
            module.trackedDevicePosition = null;
            module.trackedDeviceOrientation = null;
            module.deselectOnBackgroundClick = false;
            return go.GetComponent<EventSystem>();
        }

        /// <summary>Ссылка на действие из ассета ввода (сабассет InputActionReference).</summary>
        public static InputActionReference Ref(InputActionAsset asset, string path)
        {
            var action = asset.FindAction(path, true);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)))
                if (o is InputActionReference r && r.action != null && r.action.id == action.id) return r;
            Debug.LogWarning("[Grand World] Нет InputActionReference для " + path);
            return InputActionReference.Create(action);
        }
    }
}
