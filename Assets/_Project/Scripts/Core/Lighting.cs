using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GearPotion
{
    /// <summary>
    /// 2D URP 조명 연출 (GDD 5개월 차 폴리싱): 시간대별 전역광(새벽 푸름 → 낮 → 저녁 노을 → 심야 어둠),
    /// 해가 지면 켜지는 매장 램프, 그리고 설비가 직접 다는 점광원(<see cref="Lights.Point"/>).
    /// 배관 뷰·히트맵에서는 가독성을 위해 항상 밝게.
    /// </summary>
    public class Lighting : MonoBehaviour
    {
        Light2D global;
        readonly List<Light2D> lamps = new();
        readonly List<SpriteRenderer> lampGlows = new();
        int lampWidth = -1;
        Camera cam;

        // 06:00 → 24:00 진행도(0~1)에 따른 전역광 색/세기
        static readonly (float t, Color color, float intensity)[] Keys =
        {
            (0.00f, new Color(0.55f, 0.65f, 1f), 0.55f),   // 06 새벽
            (0.17f, new Color(0.85f, 0.9f, 1f), 0.9f),     // 09 오픈
            (0.30f, new Color(1f, 0.98f, 0.94f), 1.0f),    // 11 한낮
            (0.55f, new Color(1f, 0.96f, 0.88f), 0.97f),   // 16
            (0.70f, new Color(1f, 0.7f, 0.45f), 0.72f),    // 18:30 노을
            (0.85f, new Color(0.55f, 0.5f, 0.85f), 0.45f), // 21:20 해 짐
            (1.00f, new Color(0.35f, 0.38f, 0.7f), 0.32f), // 24 심야
        };

        public float Darkness { get; private set; }

        void Awake()
        {
            cam = Camera.main;
            global = Lights.Create("GlobalLight", transform, Light2D.LightType.Global);
            global.intensity = 1f;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            var grid = GameManager.Grid;
            if (grid.Width != lampWidth) BuildLamps(grid.Width);

            Evaluate(gm.InGame ? gm.Clock.DayProgress : 0.3f, out var color, out var intensity);
            if (grid.View != ViewMode.Normal) { color = Color.white; intensity = 1f; }
            global.color = color;
            global.intensity = intensity;
            Darkness = Mathf.Clamp01((1f - intensity) / 0.68f);

            // 해가 지면 램프가 켜진다 (깜빡임 약간)
            for (int i = 0; i < lamps.Count; i++)
            {
                float flicker = 1f + 0.06f * Mathf.Sin(Time.time * 7f + i * 1.7f);
                lamps[i].intensity = Mathf.Lerp(0.15f, 1.1f, Darkness) * flicker;
                var g = lampGlows[i].color;
                g.a = Mathf.Lerp(0.15f, 0.9f, Darkness);
                lampGlows[i].color = g;
            }
            if (cam) cam.backgroundColor = Color.Lerp(new Color(0.08f, 0.07f, 0.1f), new Color(0.03f, 0.03f, 0.06f), Darkness);
        }

        static void Evaluate(float t, out Color color, out float intensity)
        {
            for (int i = 1; i < Keys.Length; i++)
            {
                if (t > Keys[i].t) continue;
                float k = Mathf.InverseLerp(Keys[i - 1].t, Keys[i].t, t);
                color = Color.Lerp(Keys[i - 1].color, Keys[i].color, k);
                intensity = Mathf.Lerp(Keys[i - 1].intensity, Keys[i].intensity, k);
                return;
            }
            color = Keys[^1].color;
            intensity = Keys[^1].intensity;
        }

        /// <summary>매장 벽(공방과의 경계)을 따라 4~5칸마다 따뜻한 램프.</summary>
        void BuildLamps(int width)
        {
            lampWidth = width;
            foreach (var l in lamps) if (l) Destroy(l.gameObject);
            lamps.Clear();
            lampGlows.Clear();
            int count = Mathf.Max(2, Mathf.RoundToInt(width / 4.5f));
            for (int i = 0; i < count; i++)
            {
                float x = (i + 0.5f) * width / count;
                var l = Lights.Point($"ShopLamp_{i}", transform, new Vector3(x, FactoryGrid.ShopRows - 0.15f, 0f),
                    new Color(1f, 0.78f, 0.45f), 0.5f, 3.6f);
                lamps.Add(l);
                var glow = Visuals.Sprite("LampGlow", l.transform, Sprites.Glow, new Color(1f, 0.8f, 0.45f, 0.5f), 4, 0.9f, true);
                var bulb = Visuals.Sprite("Bulb", l.transform, Sprites.Circle, new Color(1f, 0.9f, 0.6f), 4, 0.16f, true);
                lampGlows.Add(glow);
                _ = bulb;
            }
        }
    }

    public static class Lights
    {
        public static Light2D Create(string name, Transform parent, Light2D.LightType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light2D>();
            // 기본값이 점광원. 타입 변경은 활성 상태에서 해야 Light2DManager 등록이 깨지지 않는다.
            if (type != Light2D.LightType.Point) l.lightType = type;
            return l;
        }

        /// <summary>설비·램프용 점광원.</summary>
        public static Light2D Point(string name, Transform parent, Vector3 worldPos, Color color, float intensity, float radius)
        {
            var l = Create(name, parent, Light2D.LightType.Point);
            l.transform.position = worldPos;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            l.falloffIntensity = 0.6f;
            return l;
        }
    }
}
