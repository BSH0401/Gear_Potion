using System;
using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    public enum Faction { Guild, Academy, Merchants, Shadow }

    /// <summary>세력 대량 납품 계약 한 건. 납품 상자로 조건에 맞는 포션을 보내면 진행도가 오른다.</summary>
    [Serializable]
    public class Contract
    {
        public int faction;
        public string potionId; // 비어 있으면 아무 포션
        public int minGrade;
        public bool defective;  // 암시장: 부작용(C등급·탁한 물약)만 받음
        public int quantity;
        public int delivered;
        public int deadlineDay; // 이 날 자정까지
        public int reward;

        public Faction Faction => (Faction)faction;
        public PotionSO Potion => string.IsNullOrEmpty(potionId) ? null : GameManager.Db.Potion(potionId);
        public bool Done => delivered >= quantity;

        public bool Accepts(Item it)
        {
            if (it.kind != ItemKind.Potion || Done) return false;
            if (defective) return it.IsDefective;
            if (it.IsDefective || (int)it.Grade < minGrade) return false;
            return Potion == null || it.potion == Potion;
        }

        public string Title => defective
            ? $"부작용 포션 {quantity}병 비밀 거래"
            : $"{GradeInfo.Label((Grade)minGrade)}급 이상 {(Potion ? Potion.displayName : "포션")} {quantity}병";

        public string Short => defective ? "부작용 포션" : $"{(Potion ? Potion.shortName : "포션")} {GradeInfo.Label((Grade)minGrade)}+";
    }

    /// <summary>
    /// 4대 세력 대량 납품 계약 (GDD 3.3). 매일 새벽 의뢰가 들어오고(Day 2부터), 최대 2건까지 수락한다.
    /// 첫 성공 시 세력별 혜택이 영구 해금된다.
    /// </summary>
    public static class Contracts
    {
        public const int MaxActive = 2;
        public const int FirstDay = 2;

        public static readonly List<Contract> Active = new();
        public static readonly List<Contract> Offers = new();
        public static readonly int[] Rank = new int[4];
        /// <summary>첫 성공으로 얻는 영구 혜택 (평판이 떨어져도 유지).</summary>
        public static readonly bool[] Perks = new bool[4];
        public static int Completed;
        public static event Action Changed;

        public static readonly string[] FactionNames = { "모험가 길드", "왕립 마법 학회", "대륙 상인 연합", "그림자 암시장" };
        public static readonly Color[] FactionColors =
        {
            new(1f, 0.6f, 0.35f), new(0.75f, 0.55f, 1f), new(1f, 0.85f, 0.35f), new(0.6f, 0.6f, 0.7f),
        };
        public static readonly string[] PerkText =
        {
            "던전 보급선: 얼음 정령의 파편·용암 도마뱀의 독샘 수급 해금, 두 재료 10% 할인",
            "설계도 공유: 얼음 정령석·연금 슬라임 배양조·VIP 쇼케이스 해금 + 연구 포인트 60",
            "도매 협정: 원재료 매입가 15% 할인, 벨트·배관·분배기 건설비 50% 절감",
            "비밀 지하실: 암시장 거래로 오르는 의심 수치 절반",
        };

        public static bool HasPerk(Faction f) => Perks[(int)f];

        // ───────────── 경제 보정 ─────────────

        public static float IngredientMultiplier(IngredientSO ing)
        {
            float m = 1f;
            if (HasPerk(Faction.Merchants)) m *= 0.85f;
            if (HasPerk(Faction.Guild) && (ing.id == "MAT_06" || ing.id == "MAT_07")) m *= 0.9f;
            return m;
        }

        public static int BuildCost(MachineSO def)
        {
            if (HasPerk(Faction.Merchants) && def.kind is MachineKind.Belt or MachineKind.Pipe or MachineKind.Splitter)
                return Mathf.Max(1, Mathf.RoundToInt(def.cost * 0.5f));
            return def.cost;
        }

        public static float SuspicionMultiplier => HasPerk(Faction.Shadow) ? 0.5f : 1f;

        // ───────────── 흐름 ─────────────

        /// <summary>새벽마다 의뢰 3건 생성 (날짜·평판 기반 결정론적 — 이어하기 시 같은 의뢰).</summary>
        public static void OnDayStart(int day)
        {
            Offers.Clear();
            if (day >= FirstDay)
            {
                var rng = new System.Random(day * 4099 + Rank[0] * 7 + Rank[1] * 31 + Rank[2] * 131 + Rank[3] * 521);
                var factions = new List<Faction> { Faction.Guild, Faction.Merchants, Faction.Academy };
                if (day >= 3) factions.Add(Faction.Shadow);
                for (int i = 0; i < 3 && factions.Count > 0; i++)
                {
                    var f = factions[rng.Next(factions.Count)];
                    factions.Remove(f);
                    var c = Generate(f, day, rng);
                    if (c != null) Offers.Add(c);
                }
                if (Offers.Count > 0) Toasts.Show($"새 납품 의뢰 {Offers.Count}건이 도착했습니다 — [계약] 버튼 (C)", FactionColors[(int)Faction.Merchants], 5f);
            }
            Changed?.Invoke();
        }

        static Contract Generate(Faction f, int day, System.Random rng)
        {
            var known = new List<PotionSO>();
            foreach (var p in GameManager.Db.potions) if (Discoveries.IsKnown(p)) known.Add(p);
            if (known.Count == 0) known.Add(GameManager.Db.potions[0]);
            var potion = known[rng.Next(known.Count)];
            int rank = Rank[(int)f];
            var c = new Contract { faction = (int)f, potionId = potion.id };
            float tierDiv = Mathf.Max(1, potion.tier);
            switch (f)
            {
                case Faction.Guild: // B급 이상 중량, 3일
                    c.minGrade = (int)Grade.B;
                    c.quantity = Mathf.RoundToInt((10 + day * 2 + rank * 4) / tierDiv);
                    c.deadlineDay = day + 2;
                    break;
                case Faction.Academy: // 고순도 소량, 2일
                    c.minGrade = day < 6 ? (int)Grade.A : (int)Grade.S;
                    c.quantity = Mathf.RoundToInt((5 + day + rank * 2) / tierDiv);
                    c.deadlineDay = day + 1;
                    break;
                case Faction.Merchants: // 등급 무관 대량, 4일
                    c.minGrade = (int)Grade.C;
                    c.quantity = Mathf.RoundToInt((20 + day * 3 + rank * 6) / tierDiv);
                    c.deadlineDay = day + 3;
                    break;
                default: // 암시장: 부작용 포션, 2일
                    c.defective = true;
                    c.potionId = "";
                    c.quantity = 6 + day / 2 + rank * 2;
                    c.deadlineDay = day + 1;
                    break;
            }
            c.quantity = Mathf.Max(3, c.quantity);
            float unit = c.defective ? 40f : potion.basePrice * GradeInfo.PriceMultiplier((Grade)c.minGrade);
            float factor = f switch { Faction.Guild => 1.6f, Faction.Academy => 2.2f, Faction.Merchants => 1.2f, _ => 3f };
            c.reward = Mathf.RoundToInt(c.quantity * unit * factor * (1f + rank * 0.1f) / 5f) * 5;
            return c;
        }

        public static bool Accept(Contract c)
        {
            if (Active.Count >= MaxActive || !Offers.Contains(c)) return false;
            Offers.Remove(c);
            Active.Add(c);
            Toasts.Show($"계약 수락: [{FactionNames[c.faction]}] {c.Title} — Day {c.deadlineDay}까지 납품 상자로", FactionColors[c.faction], 5f);
            Sfx.Play(SfxId.Bell);
            Changed?.Invoke();
            return true;
        }

        /// <summary>납품 상자에 들어온 포션을 진행 중인 계약에 먼저 배정한다.</summary>
        public static bool TryDeliver(Item it, Vector3 at)
        {
            foreach (var c in Active)
            {
                if (!c.Accepts(it)) continue;
                c.delivered++;
                Fx.Float(at, $"계약 {c.delivered}/{c.quantity}", FactionColors[c.faction]);
                if (c.Done) Complete(c);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        static void Complete(Contract c)
        {
            Active.Remove(c);
            Economy.Earn(c.reward, LedgerEntry.Contract);
            bool first = !Perks[c.faction];
            Perks[c.faction] = true;
            Rank[c.faction]++;
            Completed++;
            if (c.Faction == Faction.Shadow) Market.Suspicion = Mathf.Min(120f, Market.Suspicion + 20f * SuspicionMultiplier);
            Toasts.Show($"계약 완료! [{FactionNames[c.faction]}] +{c.reward:N0}G · 평판 {Rank[c.faction]}", FactionColors[c.faction], 5f);
            Sfx.Play(SfxId.Sparkle);
            if (first) GrantPerk(c.Faction);
        }

        static void GrantPerk(Faction f)
        {
            switch (f)
            {
                case Faction.Guild:
                    Research.Grant("MAT_06");
                    Research.Grant("MAT_07");
                    break;
                case Faction.Academy:
                    Research.Grant("MAC_COOLER");
                    Research.Grant("MAC_SLIME");
                    Research.Grant("MAC_VIP");
                    Economy.ResearchPoints += 60;
                    break;
            }
            Toasts.Show($"세력 혜택 해금 — {PerkText[(int)f]}", FactionColors[(int)f], 6f);
        }

        /// <summary>자정 정산 직전: 기한이 지난 계약은 실패, 평판 -1.</summary>
        public static void OnDayEnd(int day)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var c = Active[i];
                if (c.deadlineDay > day) continue;
                Active.RemoveAt(i);
                Rank[c.faction] = Mathf.Max(0, Rank[c.faction] - 1);
                Economy.Today.contractsFailed++;
                Toasts.Show($"계약 실패: [{FactionNames[c.faction]}] {c.Title} ({c.delivered}/{c.quantity}) — 평판 하락", new Color(1f, 0.5f, 0.4f), 5f);
            }
            Changed?.Invoke();
        }

        public static void Reset(List<Contract> active, int[] ranks, bool[] perks)
        {
            Active.Clear();
            Offers.Clear();
            if (active != null) Active.AddRange(active);
            for (int i = 0; i < Rank.Length; i++)
            {
                Rank[i] = ranks != null && i < ranks.Length ? ranks[i] : 0;
                Perks[i] = perks != null && i < perks.Length && perks[i];
            }
            Completed = 0;
            Changed?.Invoke();
        }
    }
}
