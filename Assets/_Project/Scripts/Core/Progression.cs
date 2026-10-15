using System;
using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>마도 공학 연구: RP로 설비·원재료를 해금 (GDD 2.2 심야 연구).</summary>
    public static class Research
    {
        public static readonly HashSet<string> Unlocked = new();
        public static bool UnlockAll;
        public static event Action Changed;

        public static bool IsUnlocked(MachineSO m) => UnlockAll || m.unlockedByDefault || Unlocked.Contains(m.id);
        public static bool IsUnlocked(IngredientSO i) => UnlockAll || i.unlockedByDefault || Unlocked.Contains(i.id);

        public static bool TryUnlock(string id, int cost, string name)
        {
            if (Unlocked.Contains(id) || Economy.ResearchPoints < cost) return false;
            Economy.ResearchPoints -= cost;
            Unlocked.Add(id);
            Toasts.Show($"연구 완료: {name}", new Color(0.6f, 0.9f, 1f));
            Sfx.Play(SfxId.Sparkle);
            Changed?.Invoke();
            return true;
        }

        public static void Reset(IEnumerable<string> ids)
        {
            Unlocked.Clear();
            if (ids != null) foreach (var id in ids) Unlocked.Add(id);
            Changed?.Invoke();
        }
    }

    /// <summary>발견한 포션 도감 + 누적 통계.</summary>
    public static class Discoveries
    {
        public static readonly HashSet<string> Known = new();
        public static int TotalSold, TotalRevenue, BestGrade = -1, Brewed, GreatSuccesses;
        public static event Action<PotionSO> Discovered;

        public static bool IsKnown(PotionSO p) => p && Known.Contains(p.id);

        public static void OnBrewed(Item potion)
        {
            Brewed++;
            if (potion.greatSuccess) GreatSuccesses++;
            if (potion.potion != GameManager.Db.murkyPotion) BestGrade = Mathf.Max(BestGrade, (int)potion.Grade);
            if (potion.potion && Known.Add(potion.potion.id))
            {
                Toasts.Show($"새 포션 발견! {potion.potion.displayName}", potion.potion.color);
                Sfx.Play(SfxId.Sparkle);
                Discovered?.Invoke(potion.potion);
            }
        }

        public static void Reset(IEnumerable<string> known, int sold, int revenue, int bestGrade, int brewed)
        {
            Known.Clear();
            if (known != null) foreach (var k in known) Known.Add(k);
            TotalSold = sold;
            TotalRevenue = revenue;
            BestGrade = bestGrade;
            Brewed = brewed;
            GreatSuccesses = 0;
        }
    }

    /// <summary>초반 가이드 목표 (튜토리얼 대용, GDD 7 - 5개월 차 가이드의 축소판).</summary>
    public static class Goals
    {
        public class Goal
        {
            public string text;
            public Func<bool> done;
            public Goal(string text, Func<bool> done) { this.text = text; this.done = done; }
        }

        public static readonly List<Goal> List = new()
        {
            new("첫 포션 판매하기", () => Discoveries.TotalSold >= 1),
            new("진열대를 하나 더 놓고 분배기로 연결하기", () => GameManager.Grid.CountOf(MachineKind.Shelf) >= 2),
            new("분쇄기로 '화염 활력제' 만들기", () => Known("POT_FIRE")),
            new("연구: 원심 불순물 여과기 해금", () => Research.IsUnlocked(GameManager.Db.Machine("MAC_FILTER"))),
            new("A등급 이상 포션 만들기", () => Discoveries.BestGrade >= (int)Grade.A),
            new("오버클럭 태엽을 달고 얼음 정령석으로 식히기", () =>
            {
                foreach (var p in GameManager.Grid.All<Processor>()) if (p.overclocked && p.Cooling > Processor.PassiveCooling) return true;
                return false;
            }),
            new("마도 합성 가마로 '고순도 마나 엘릭서' 만들기", () => Known("POT_MANA")),
            new("누적 매출 5,000G 달성", () => Discoveries.TotalRevenue >= 5000),
            new("'폭풍 질주 포션' 만들기", () => Known("POT_STORM")),
            new("S등급 포션 만들기", () => Discoveries.BestGrade >= (int)Grade.S),
            new("'드래곤 브레스 영약' 만들기", () => Known("POT_DRAGON")),
            new("최종 목표: '액상 현자의 돌' 연성", () => Known("POT_STONE")),
        };

        static bool Known(string id) => Discoveries.Known.Contains(id);

        public static int CurrentIndex
        {
            get
            {
                for (int i = 0; i < List.Count; i++) if (!List[i].done()) return i;
                return List.Count;
            }
        }
    }
}
