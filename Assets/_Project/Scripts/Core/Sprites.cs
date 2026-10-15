using System;
using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 런타임 SDF로 그리는 플레이스홀더 스프라이트. 아트 에셋 없이 공방 전체를 표현한다.
    /// (실제 32x32 픽셀 아트로 교체할 때는 이 클래스의 프로퍼티만 바꾸면 된다)
    /// </summary>
    public static class Sprites
    {
        const int Res = 96;
        static readonly Dictionary<string, Sprite> cache = new();

        public static Sprite White => Get("white", p => Box(p, new Vector2(1.2f, 1.2f)));
        public static Sprite Rounded => Get("rounded", p => RoundBox(p, new Vector2(0.92f, 0.92f), 0.22f));
        public static Sprite RoundedOutline => Get("rounded_ol", p => Mathf.Abs(RoundBox(p, new Vector2(0.88f, 0.88f), 0.22f)) - 0.06f);
        public static Sprite Circle => Get("circle", p => p.magnitude - 0.9f);
        public static Sprite Ring => Get("ring", p => Mathf.Abs(p.magnitude - 0.8f) - 0.1f);
        public static Sprite Diamond => Get("diamond", p => (Mathf.Abs(p.x) + Mathf.Abs(p.y)) * 0.75f - 0.65f);
        public static Sprite Chevron => Get("chevron", p =>
        {
            // 오른쪽을 가리키는 꺾쇠 (벨트 방향 표시)
            var q = new Vector2(p.x + 0.15f, Mathf.Abs(p.y));
            float d = Mathf.Abs(Vector2.Dot(q - new Vector2(0.25f, 0f), new Vector2(0.7071f, 0.7071f))) - 0.11f;
            return Mathf.Max(d, Mathf.Max(Mathf.Abs(p.y) - 0.6f, Mathf.Abs(p.x) - 0.7f));
        });
        public static Sprite Bottle => Get("bottle", p =>
        {
            float body = (p - new Vector2(0f, -0.28f)).magnitude - 0.58f;
            float neck = RoundBox(p - new Vector2(0f, 0.42f), new Vector2(0.17f, 0.3f), 0.05f);
            float cork = RoundBox(p - new Vector2(0f, 0.78f), new Vector2(0.24f, 0.1f), 0.04f);
            return Mathf.Min(body, Mathf.Min(neck, cork));
        });
        public static Sprite Flask => Get("flask", p =>
        {
            float body = RoundBox(p - new Vector2(0f, -0.3f), new Vector2(0.6f, 0.5f), 0.3f);
            float neck = Box(p - new Vector2(0f, 0.4f), new Vector2(0.18f, 0.35f));
            return Mathf.Min(body, neck);
        });
        public static Sprite Blob => Get("blob", p =>
        {
            float a = Mathf.Atan2(p.y, p.x);
            return p.magnitude - (0.7f + 0.12f * Mathf.Sin(a * 5f));
        });
        public static Sprite Gear => Get("gear", p =>
        {
            float a = Mathf.Atan2(p.y, p.x);
            float teeth = Mathf.Clamp(Mathf.Sin(a * 8f) * 3f, -1f, 1f) * 0.1f;
            float outer = p.magnitude - (0.78f + teeth);
            float hole = 0.28f - p.magnitude;
            return Mathf.Max(outer, hole);
        });
        public static Sprite Star => Get("star", p =>
        {
            float a = Mathf.Atan2(p.y, p.x) + Mathf.PI / 2f;
            float r = 0.55f + 0.33f * Mathf.Cos(a * 5f);
            return p.magnitude - r;
        });
        public static Sprite Person => Get("person", p =>
        {
            float head = (p - new Vector2(0f, 0.5f)).magnitude - 0.32f;
            float body = RoundBox(p - new Vector2(0f, -0.35f), new Vector2(0.48f, 0.5f), 0.3f);
            return Mathf.Min(head, body);
        });
        public static Sprite Bubble => Get("bubble", p =>
        {
            float b = RoundBox(p - new Vector2(0f, 0.12f), new Vector2(0.9f, 0.7f), 0.35f);
            var t = p - new Vector2(-0.1f, -0.7f);
            float tail = Mathf.Max(Mathf.Max(-t.y, t.y - 0.3f), Mathf.Abs(t.x) - t.y * 0.6f);
            return Mathf.Min(b, tail);
        });
        public static Sprite Glow
        {
            get
            {
                if (cache.TryGetValue("glow", out var s)) return s;
                s = Build("glow", p => { float t = Mathf.Clamp01(1f - p.magnitude); return t * t; });
                cache["glow"] = s;
                return s;
            }
        }

        public static Sprite GridTile
        {
            get
            {
                if (cache.TryGetValue("grid", out var s)) return s;
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat, name = "grid"
                };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool line = x == 0 || y == 0;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(line ? 255 : 0));
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
                cache["grid"] = s;
                return s;
            }
        }

        public static Sprite ForItem(ItemKind kind) => kind switch
        {
            ItemKind.Ingredient => Circle,
            ItemKind.Powder => Diamond,
            ItemKind.Extract => Flask,
            ItemKind.Potion => Bottle,
            ItemKind.Sludge => Blob,
            _ => Diamond,
        };

        static Sprite Get(string key, Func<Vector2, float> sdf)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            const float px = 2f / Res;
            s = Build(key, p => Mathf.Clamp01(0.5f - sdf(p) / px));
            cache[key] = s;
            return s;
        }

        static Sprite Build(string name, Func<Vector2, float> alpha)
        {
            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = name
            };
            var pixels = new Color32[Res * Res];
            for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                var p = new Vector2((x + 0.5f) / Res * 2f - 1f, (y + 0.5f) / Res * 2f - 1f);
                pixels[y * Res + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(p)) * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            // pixelsPerUnit = Res/1 → 스프라이트 하나가 정확히 1x1 월드 유닛(=1타일)
            return Sprite.Create(tex, new Rect(0, 0, Res, Res), new Vector2(0.5f, 0.5f), Res, 0, SpriteMeshType.FullRect);
        }

        static float Box(Vector2 p, Vector2 b)
        {
            var q = new Vector2(Mathf.Abs(p.x) - b.x, Mathf.Abs(p.y) - b.y);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        }

        static float RoundBox(Vector2 p, Vector2 b, float r) => Box(p, b - new Vector2(r, r)) - r;
    }

    public static class Visuals
    {
        /// <summary>2D 조명을 받는 기본 머티리얼 (Sprite-Lit).</summary>
        public static Material SpriteMaterial;
        /// <summary>조명과 무관하게 스스로 빛나는 표시용 (Sprite-Unlit): 발광·고스트·UI성 표시.</summary>
        public static Material UnlitMaterial;
        static Font worldFont;

        public static Font Font => worldFont ? worldFont : worldFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Malgun Gothic", "Segoe UI", "Arial" }, 48);

        public static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, Color color, int order, float scale = 1f, bool emissive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            var mat = emissive && UnlitMaterial ? UnlitMaterial : SpriteMaterial;
            if (mat) sr.sharedMaterial = mat;
            return sr;
        }

        /// <summary>월드 공간 한글 라벨 (TextMesh + OS 폰트).</summary>
        public static TextMesh Label(string name, Transform parent, string text, Color color, int order, float size = 0.22f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.fontSize = 48;
            tm.characterSize = size * 0.21f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Font.material;
            mr.sortingOrder = order;
            return tm;
        }
    }
}
