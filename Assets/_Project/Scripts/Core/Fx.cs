using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>판매 시 떠오르는 "+38G" 같은 월드 텍스트 (풀링, GDD 6.3).</summary>
    public class Fx : MonoBehaviour
    {
        class Floater
        {
            public TextMesh text;
            public float age, life;
            public Vector3 start;
        }

        static Fx instance;
        readonly List<Floater> active = new();
        readonly Stack<Floater> pool = new();

        void Awake() => instance = this;

        public static void Float(Vector3 pos, string text, Color color, float life = 1.3f)
        {
            if (!instance) return;
            instance.Spawn(pos, text, color, life);
        }

        void Spawn(Vector3 pos, string s, Color color, float life)
        {
            var f = pool.Count > 0 ? pool.Pop() : new Floater { text = Visuals.Label("Float", transform, "", Color.white, 60, 0.26f) };
            f.text.gameObject.SetActive(true);
            f.text.text = s;
            f.text.color = color;
            f.age = 0f;
            f.life = life;
            f.start = pos + new Vector3(0f, 0.4f, 0f);
            f.text.transform.position = f.start;
            active.Add(f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var f = active[i];
                f.age += dt;
                float t = f.age / f.life;
                if (t >= 1f)
                {
                    f.text.gameObject.SetActive(false);
                    active.RemoveAt(i);
                    pool.Push(f);
                    continue;
                }
                f.text.transform.position = f.start + new Vector3(0f, t * 0.8f, 0f);
                var c = f.text.color;
                c.a = 1f - t * t;
                f.text.color = c;
            }
        }
    }
}
