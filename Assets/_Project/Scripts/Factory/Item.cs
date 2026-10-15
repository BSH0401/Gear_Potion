using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 논리 아이템. 물리 엔진을 쓰지 않고 그리드 틱으로만 이동하며(GDD 6.1),
    /// 화면 표시는 풀링된 뷰가 목표 좌표로 부드럽게 보간된다.
    /// </summary>
    public class Item
    {
        public ItemKind kind;
        public IngredientSO source;
        public Elements el;
        public float impurity;
        public PotionSO potion;
        public float bonusPurity;
        public bool greatSuccess;

        // 표시
        public ItemView view;
        public Vector3 target;

        public float Purity => Mathf.Clamp(100f - impurity + bonusPurity, 0f, 100f);
        public Grade Grade => GradeInfo.FromPurity(Purity);
        public bool IsLiquid => kind == ItemKind.Extract;
        public bool IsDefective => kind == ItemKind.Potion && (Grade == Grade.C || potion == GameManager.Db.murkyPotion);

        public static Item FromIngredient(IngredientSO ing) => new()
        {
            kind = ItemKind.Ingredient, source = ing, el = ing.elements, impurity = ing.impurity,
        };

        public static Item Sludge() => new()
        {
            kind = ItemKind.Sludge, el = new Elements(0, 0, 0, 5, 5), impurity = 80f,
        };

        public static Item Crystal() => new() { kind = ItemKind.Crystal, el = new Elements(0, 0, 0, 0, 20), impurity = 0f };

        public string Name => kind switch
        {
            ItemKind.Ingredient => source ? source.displayName : "원재료",
            ItemKind.Powder => (source ? source.displayName : "") + " 분말",
            ItemKind.Extract => greatSuccess ? "혼합 원액 ✦" : source ? source.displayName + " 원액" : "혼합 원액",
            ItemKind.Potion => potion ? $"{potion.displayName} [{GradeInfo.Label(Grade)}]" : "포션",
            ItemKind.Sludge => "마력 슬러지",
            _ => "마력 결정",
        };

        /// <summary>판매 기준가(매장 정가). 트렌드 배율은 Economy에서 따로 곱한다.</summary>
        public int BaseValue => kind switch
        {
            ItemKind.Potion => potion ? Mathf.RoundToInt(potion.basePrice * GradeInfo.PriceMultiplier(Grade)) : 1,
            ItemKind.Crystal => 35,
            ItemKind.Sludge => -5,
            ItemKind.Ingredient => source ? Mathf.Max(1, source.cost / 2) : 1,
            _ => Mathf.Max(1, Mathf.RoundToInt(el.Total * 0.4f)),
        };

        public Color Color => kind switch
        {
            ItemKind.Ingredient => source ? source.color : Color.white,
            ItemKind.Potion => potion ? potion.color : Color.white,
            ItemKind.Sludge => new Color(0.35f, 0.45f, 0.2f),
            ItemKind.Crystal => new Color(0.5f, 0.95f, 1f),
            ItemKind.Powder => Color.Lerp(source ? source.color : Color.white, Color.white, 0.35f),
            _ => el.BlendColor,
        };

        public string Describe()
        {
            var s = $"{Name}\n원소: {el.Describe()}\n불순물 {impurity:0}% · 순도 {Purity:0}%";
            if (kind == ItemKind.Potion) s += $" · 가치 {BaseValue}G";
            return s;
        }

        public Item Clone() => (Item)MemberwiseClone();

        // ───────────── 뷰 관리 ─────────────

        public void Show(Vector3 pos, bool snap)
        {
            target = pos;
            if (view == null)
            {
                view = ItemViews.Get(this);
                snap = true;
            }
            if (snap) view.transform.position = pos;
        }

        public void Hide()
        {
            if (view == null) return;
            ItemViews.Release(view);
            view = null;
        }
    }

    public class ItemView : MonoBehaviour
    {
        public SpriteRenderer body;
        public SpriteRenderer gradeDot;
        public Item item;
    }

    /// <summary>아이템 스프라이트 오브젝트 풀 (ObjectPool, GDD 6.3).</summary>
    public static class ItemViews
    {
        static readonly Stack<ItemView> pool = new();
        static readonly List<ItemView> active = new();
        static Transform root;

        public static int ActiveCount => active.Count;

        public static ItemView Get(Item item)
        {
            if (!root) root = new GameObject("Items").transform;
            ItemView v;
            if (pool.Count > 0) v = pool.Pop();
            else
            {
                var go = new GameObject("Item");
                go.transform.SetParent(root, false);
                v = go.AddComponent<ItemView>();
                v.body = Visuals.Sprite("Body", go.transform, Sprites.Circle, Color.white, 20, 0.36f);
                v.gradeDot = Visuals.Sprite("Grade", go.transform, Sprites.Circle, Color.white, 21, 0.13f, true);
                v.gradeDot.transform.localPosition = new Vector3(0.14f, 0.14f, 0f);
            }
            v.gameObject.SetActive(true);
            v.item = item;
            v.body.sprite = Sprites.ForItem(item.kind);
            v.body.color = item.Color;
            v.body.transform.localScale = Vector3.one * (item.kind == ItemKind.Potion ? 0.42f : 0.34f);
            bool graded = item.kind == ItemKind.Potion;
            v.gradeDot.enabled = graded;
            if (graded) v.gradeDot.color = GradeInfo.Color(item.Grade);
            active.Add(v);
            return v;
        }

        public static void Release(ItemView v)
        {
            v.item = null;
            v.gameObject.SetActive(false);
            active.Remove(v);
            pool.Push(v);
        }

        /// <summary>틱 사이의 시각 보간: 목표 좌표로 등속 이동.</summary>
        public static void Animate(float dt)
        {
            for (int i = 0; i < active.Count; i++)
            {
                var v = active[i];
                if (v.item == null) continue;
                var pos = v.transform.position;
                float dist = Vector3.Distance(pos, v.item.target);
                if (dist > 3f) { v.transform.position = v.item.target; continue; } // 텔레포트급 이동은 즉시
                v.transform.position = Vector3.MoveTowards(pos, v.item.target, Mathf.Max(1.6f, dist * 10f) * dt);
            }
        }

        public static void Clear()
        {
            for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
        }
    }
}
