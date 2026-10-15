using System;
using UnityEngine;

namespace GearPotion
{
    public enum DayPhase { Dawn, Day, Evening, Night }

    /// <summary>
    /// 하루 4페이즈 타임라인 (GDD 2.2): 06~09 새벽(시계 0.5배속) · 09~17 낮 영업 ·
    /// 17~22 저녁 피크/VIP · 22~24 심야 마감 → 24시 정산.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        public const float StartMinute = 6 * 60f;
        public const float EndMinute = 24 * 60f;
        public const float RealSecondsPerHour = 35f;
        public const int BottleCost = 1;
        public const float ManaUpkeepPerMp = 1f;

        public int Day { get; private set; } = 1;
        public float Minute { get; private set; } = StartMinute;
        public bool Running { get; private set; }
        public static event Action<int> DayEnded;
        public static event Action<int> DayStarted;

        public DayPhase Phase => Minute < 9 * 60 ? DayPhase.Dawn : Minute < 17 * 60 ? DayPhase.Day : Minute < 22 * 60 ? DayPhase.Evening : DayPhase.Night;
        public bool ShopOpen => Phase is DayPhase.Day or DayPhase.Evening;
        public float DayProgress => Mathf.InverseLerp(StartMinute, EndMinute, Minute);

        public static string PhaseName(DayPhase p) => p switch
        {
            DayPhase.Dawn => "새벽 · 정비",
            DayPhase.Day => "낮 · 일반 영업",
            DayPhase.Evening => "저녁 · 피크 타임",
            _ => "심야 · 마감",
        };

        public string TimeText => $"{(int)(Minute / 60f):00}:{(int)(Minute % 60f):00}";

        public void StartDay(int day)
        {
            Day = day;
            Minute = StartMinute;
            Running = true;
            Economy.Today = new DayLedger();
            Market.RollNews(day);
            DayStarted?.Invoke(day);
        }

        public void Stop() => Running = false;

        /// <summary>개발/테스트용: 같은 날 안에서 시각을 옮긴다.</summary>
        public void SetMinute(float minute) => Minute = Mathf.Clamp(minute, StartMinute, EndMinute - 1f);

        void Update()
        {
            if (!Running) return;
            float rate = 60f / RealSecondsPerHour;
            if (Phase == DayPhase.Dawn) rate *= 0.5f;
            var before = Phase;
            Minute += Time.deltaTime * rate;
            if (Phase != before) OnPhaseChanged(Phase);
            if (Minute >= EndMinute)
            {
                Minute = EndMinute;
                Running = false;
                EndDay();
            }
        }

        void OnPhaseChanged(DayPhase p)
        {
            switch (p)
            {
                case DayPhase.Day: Toasts.Show("09:00 상점 오픈!", new Color(1f, 0.9f, 0.5f)); Sfx.Play(SfxId.Bell); break;
                case DayPhase.Evening: Toasts.Show("17:00 피크 타임 — 정예 모험가와 VIP 손님이 찾아옵니다", new Color(1f, 0.7f, 0.4f)); break;
                case DayPhase.Night: Toasts.Show("22:00 상점 마감", new Color(0.7f, 0.75f, 1f)); Sfx.Play(SfxId.Bell); break;
            }
        }

        void EndDay()
        {
            var grid = GameManager.Grid;
            int upkeep = Mathf.RoundToInt(grid.ManaSupply * ManaUpkeepPerMp);
            Economy.Spend(upkeep, LedgerEntry.ManaUpkeep);

            // 그림자 암시장: 의심 수치가 높으면 왕실 경비대 불시 검문
            if (Market.Suspicion >= 100f)
            {
                int fine = Mathf.Max(200, Economy.Gold / 5);
                Economy.Spend(fine, LedgerEntry.Fine);
                Market.Suspicion = 40f;
                Toasts.Show($"왕실 경비대 불시 검문! 벌금 {fine}G", new Color(1f, 0.4f, 0.35f));
            }
            Market.Suspicion = Mathf.Max(0f, Market.Suspicion - 10f);
            Sfx.Play(SfxId.DayEnd);
            DayEnded?.Invoke(Day);
        }
    }
}
