using System;
using UnityEngine;

namespace GearPotion
{
    public enum Element { Ignis, Aqua, Ventus, Terra, Aether }

    /// <summary>5대 원소 벡터 (화·수·풍·지·에테르).</summary>
    [Serializable]
    public struct Elements
    {
        public float ignis, aqua, ventus, terra, aether;

        public const int Count = 5;
        public static readonly string[] Names = { "화", "수", "풍", "지", "에테르" };
        public static readonly Color[] Colors =
        {
            new(1f, 0.42f, 0.2f), new(0.3f, 0.62f, 1f), new(0.55f, 0.95f, 0.6f),
            new(0.72f, 0.52f, 0.3f), new(0.78f, 0.45f, 1f),
        };

        public Elements(float ignis, float aqua, float ventus, float terra, float aether)
        {
            this.ignis = ignis; this.aqua = aqua; this.ventus = ventus; this.terra = terra; this.aether = aether;
        }

        public float this[int i]
        {
            get => i switch { 0 => ignis, 1 => aqua, 2 => ventus, 3 => terra, _ => aether };
            set
            {
                switch (i)
                {
                    case 0: ignis = value; break;
                    case 1: aqua = value; break;
                    case 2: ventus = value; break;
                    case 3: terra = value; break;
                    default: aether = value; break;
                }
            }
        }

        public float this[Element e] { get => this[(int)e]; set => this[(int)e] = value; }

        public int MainIndex
        {
            get
            {
                int best = 0;
                for (int i = 1; i < Count; i++) if (this[i] > this[best]) best = i;
                return best;
            }
        }

        public float Total
        {
            get { float t = 0f; for (int i = 0; i < Count; i++) t += this[i]; return t; }
        }

        public static Elements operator +(Elements a, Elements b)
        {
            var r = a;
            for (int i = 0; i < Count; i++) r[i] += b[i];
            return r;
        }

        /// <summary>요구 원소 조건을 모두 만족하는가.</summary>
        public bool Satisfies(Elements req)
        {
            for (int i = 0; i < Count; i++) if (this[i] + 0.001f < req[i]) return false;
            return true;
        }

        public Color BlendColor
        {
            get
            {
                float t = Total;
                if (t <= 0.01f) return new Color(0.6f, 0.6f, 0.6f);
                var c = Color.black;
                for (int i = 0; i < Count; i++) c += Colors[i] * (this[i] / t);
                c.a = 1f;
                return c;
            }
        }

        public string Describe(bool skipZero = true)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Count; i++)
            {
                if (skipZero && this[i] < 0.05f) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Names[i]).Append(' ').Append(Mathf.RoundToInt(this[i]));
            }
            return sb.Length == 0 ? "-" : sb.ToString();
        }
    }
}
