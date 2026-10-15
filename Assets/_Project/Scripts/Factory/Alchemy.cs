using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 속성 반응형 연금술 규칙 (GDD 3.2). 모든 공정은 원소 벡터와 불순물만 바꾸며,
    /// 최종 포션 종류는 병입 시점의 원소 조건으로 결정된다.
    /// </summary>
    public static class Alchemy
    {
        public const float CrushAmplify = 1.3f;
        public const float CrushImpurity = 10f;
        public const float FilterRemoval = 20f;
        /// <summary>여과로는 이 아래로 못 내린다 → SSS(순도 99+)는 합성 가마 대성공 보너스로만.</summary>
        public const float FilterFloor = 3f;
        public const float DistillConcentrate = 1.5f;
        public const float GreatSuccessChance = 0.15f;
        public const float GreatSuccessBonus = 8f;

        /// <summary>분쇄: 주 원소 +30% (+재료 고유 보너스), 불순물 +10%p.</summary>
        public static Item Crush(Item it)
        {
            var r = it.Clone();
            r.kind = ItemKind.Powder;
            int main = r.el.MainIndex;
            r.el[main] = r.el[main] * CrushAmplify + (it.source ? it.source.crushBonus : 0f);
            r.impurity = Mathf.Min(100f, r.impurity + CrushImpurity);
            r.view = null;
            return r;
        }

        /// <summary>압착 추출: 고체 → 액체 원액 (원소·불순물 유지).</summary>
        public static Item Extract(Item it)
        {
            var r = it.Clone();
            r.kind = ItemKind.Extract;
            r.view = null;
            return r;
        }

        /// <summary>원심 여과: 불순물 -20%p (최소 3%). 슬러지는 호출 측에서 따로 배출.</summary>
        public static Item Filter(Item it)
        {
            var r = it.Clone();
            r.impurity = Mathf.Max(Mathf.Min(r.impurity, FilterFloor), r.impurity - FilterRemoval);
            r.view = null;
            return r;
        }

        /// <summary>증류: 잡성분 제거, 가장 높은 단일 원소 1.5배 농축.</summary>
        public static Item Distill(Item it)
        {
            var r = it.Clone();
            int main = r.el.MainIndex;
            float v = r.el[main] * DistillConcentrate;
            r.el = default;
            r.el[main] = v;
            r.view = null;
            return r;
        }

        /// <summary>마도 합성: 원액들의 원소 합산, 불순물 평균. 일정 확률로 대성공.</summary>
        public static Item Mix(IList<Item> parts, float greatChance)
        {
            var r = new Item { kind = ItemKind.Extract };
            float imp = 0f, bonus = 0f;
            foreach (var p in parts)
            {
                r.el = r.el + p.el;
                imp += p.impurity;
                bonus = Mathf.Max(bonus, p.bonusPurity);
            }
            r.impurity = imp / Mathf.Max(1, parts.Count);
            r.bonusPurity = bonus;
            if (Random.value < greatChance)
            {
                r.greatSuccess = true;
                r.bonusPurity += GreatSuccessBonus;
            }
            return r;
        }

        /// <summary>병입: 원소 조건에 맞는 가장 높은 티어의 포션으로 확정.</summary>
        public static Item Bottle(Item it)
        {
            var r = it.Clone();
            r.kind = ItemKind.Potion;
            r.potion = Match(it.el, it.impurity);
            r.view = null;
            return r;
        }

        public static PotionSO Match(Elements el, float impurity)
        {
            PotionSO best = null;
            foreach (var p in GameManager.Db.potions)
            {
                if (!el.Satisfies(p.requirement) || impurity > p.maxImpurity + 0.001f) continue;
                if (best == null || p.tier > best.tier || (p.tier == best.tier && p.basePrice > best.basePrice)) best = p;
            }
            return best ? best : GameManager.Db.murkyPotion;
        }
    }
}
