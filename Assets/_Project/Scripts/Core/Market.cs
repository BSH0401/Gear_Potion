using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>시세·트렌드·평판·암시장 의심 수치 (GDD 2.2 왕국 뉴스, 3.3 경제).</summary>
    public static class Market
    {
        public const float TrendBonus = 1.3f;
        public static PotionSO TrendPotion;
        public static IngredientSO ShiftedIngredient;
        public static float IngredientShift = 1f;
        public static string News = "";

        // 평판: 최근 별점의 이동 평균 (1~5)
        public static float Rating = 3f;
        public static float Suspicion;
        static readonly Queue<int> recentRatings = new();

        public static int IngredientPrice(IngredientSO ing)
        {
            float m = ing == ShiftedIngredient ? IngredientShift : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(ing.cost * m));
        }

        public static float TrendMultiplier(PotionSO p) => p != null && p == TrendPotion ? TrendBonus : 1f;

        public static int PotionPrice(Item it) =>
            Mathf.Max(1, Mathf.RoundToInt(it.BaseValue * TrendMultiplier(it.potion)));

        public static int ShelfPrice(Item it, Shelf shelf) =>
            Mathf.Max(1, Mathf.RoundToInt(PotionPrice(it) * shelf.Markup * shelf.def.priceBonus));

        /// <summary>암시장 브로커는 정가(B급 기준)의 3배를 낸다.</summary>
        public static int BlackMarketPrice(Item it) =>
            Mathf.Max(12, (it.potion ? it.potion.basePrice : 4)) * 3;

        public static void AddRating(int stars)
        {
            recentRatings.Enqueue(stars);
            while (recentRatings.Count > 25) recentRatings.Dequeue();
            float sum = 0f;
            foreach (var r in recentRatings) sum += r;
            // 초기값 3.0에서 서서히 실제 평균으로 수렴
            Rating = (sum + 3f * 3f) / (recentRatings.Count + 3f);
        }

        public static void ResetReputation(float rating, float suspicion)
        {
            recentRatings.Clear();
            Rating = rating;
            Suspicion = suspicion;
        }

        /// <summary>평균 별점에 따라 다음 날 방문객 수가 변동 (GDD 3.3 WordOfMouth).</summary>
        public static float TrafficMultiplier => Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(1f, 5f, Rating));

        /// <summary>오늘의 왕국 뉴스: 특정 포션 수요 폭증 + 원재료 시세 변동.</summary>
        public static void RollNews(int day)
        {
            var rng = new System.Random(day * 7919 + 17);
            var known = new List<PotionSO>();
            foreach (var p in GameManager.Db.potions)
                if (Discoveries.IsKnown(p) || p.tier <= 1) known.Add(p);
            TrendPotion = known.Count > 0 ? known[rng.Next(known.Count)] : null;
            var ings = new List<IngredientSO>();
            foreach (var i in GameManager.Db.ingredients) if (Research.IsUnlocked(i)) ings.Add(i);
            ShiftedIngredient = ings.Count > 0 ? ings[rng.Next(ings.Count)] : null;
            IngredientShift = rng.NextDouble() < 0.5 ? 0.8f : 1.25f;

            string[] headlines =
            {
                "왕국 기사단 훈련 주간", "북부 던전 레이드 시즌", "마법 학회 정기 학술제", "수확 축제 개막",
                "역병 소문 확산", "용 목격담 퍼져", "상인 연합 대상단 입항", "왕실 무도회 준비",
            };
            string head = headlines[rng.Next(headlines.Length)];
            News = TrendPotion ? $"{head}: {TrendPotion.displayName} 가격 +30%" : head;
            if (ShiftedIngredient)
                News += $" · {ShiftedIngredient.displayName} 시세 {(IngredientShift < 1f ? "-20%" : "+25%")}";
        }
    }
}
