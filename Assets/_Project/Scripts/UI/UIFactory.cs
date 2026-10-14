using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GearPotion
{
    /// <summary>프리팹 없이 uGUI를 코드로 구성하는 작은 빌더.</summary>
    public static class UIFactory
    {
        public static readonly Color Panel = new(0.1f, 0.08f, 0.12f, 0.92f);
        public static readonly Color PanelLight = new(0.2f, 0.16f, 0.22f, 0.95f);
        public static readonly Color Accent = new(1f, 0.78f, 0.35f);
        public static readonly Color TextColor = new(0.96f, 0.93f, 0.88f);
        public static readonly Color Dim = new(0.65f, 0.62f, 0.6f);

        static Font font;
        public static Font Font => font ? font : font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Segoe UI", "Arial" }, 32);

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>()) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(RectTransform rt, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>마우스가 UI 위에 있는지 판정하기 위해 패널 배경은 raycastTarget을 켠다.</summary>
        public static Image PanelBg(RectTransform rt, Color? color = null) => Image(rt, color ?? Panel, true);

        public static Text Label(RectTransform rt, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool wrap = false)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick, int fontSize = 26)
        {
            var rt = Rect($"Btn_{label}", parent, anchor, new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.27f, 0.2f, 0.3f, 0.97f);
            colors.highlightedColor = new Color(0.45f, 0.33f, 0.45f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.75f, 0.55f, 0.3f, 1f);
            colors.disabledColor = new Color(0.16f, 0.14f, 0.17f, 0.9f);
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            btn.onClick.AddListener(() => Sfx.Play(SfxId.Click, 0.5f));
            if (onClick != null) btn.onClick.AddListener(onClick);
            Label(Stretch("Label", rt, 4f), label, fontSize, TextColor);
            return btn;
        }

        public static Text ButtonText(Button b) => b.GetComponentInChildren<Text>();
    }
}
