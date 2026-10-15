using System;
using UnityEngine;

namespace GearPotion
{
    public enum LedgerEntry { Retail, Bulk, BlackMarket, Ingredients, Bottles, Disposal, Construction, Refund, ManaUpkeep, Fine, Contract }

    /// <summary>하루 손익계산서 (GDD 2.2 심야 정산).</summary>
    [Serializable]
    public class DayLedger
    {
        public int[] amounts = new int[Enum.GetValues(typeof(LedgerEntry)).Length];
        public int customers, served, potionsSold, ratingSum, ratingCount, rpEarned, contractsFailed;

        public int this[LedgerEntry e] { get => amounts[(int)e]; set => amounts[(int)e] = value; }
        public int Income => this[LedgerEntry.Retail] + this[LedgerEntry.Bulk] + this[LedgerEntry.BlackMarket] + this[LedgerEntry.Refund] + this[LedgerEntry.Contract];
        public int Expense => this[LedgerEntry.Ingredients] + this[LedgerEntry.Bottles] + this[LedgerEntry.Disposal] +
                              this[LedgerEntry.Construction] + this[LedgerEntry.ManaUpkeep] + this[LedgerEntry.Fine];
        public int Net => Income - Expense;
        public float AverageRating => ratingCount == 0 ? 0f : (float)ratingSum / ratingCount;
    }

    public static class Economy
    {
        public static int Gold;
        public static int ResearchPoints;
        public static DayLedger Today = new();
        public static event Action<int> GoldChanged;

        /// <summary>판매 20G당 연구 포인트 1 (v0.7 밸런스: 10G → 20G).</summary>
        public const int GoldPerRP = 20;
        static int rpRemainder;

        public static bool CanAfford(int amount) => Gold >= amount;

        public static bool TrySpend(int amount, LedgerEntry entry)
        {
            if (amount > Gold) return false;
            Spend(amount, entry);
            return true;
        }

        /// <summary>강제 지출 (폐기 비용·벌금처럼 잔고가 모자라도 빠져나가는 돈).</summary>
        public static void Spend(int amount, LedgerEntry entry)
        {
            if (amount == 0) return;
            Gold -= amount;
            Today[entry] += amount;
            GoldChanged?.Invoke(-amount);
        }

        public static void Earn(int amount, LedgerEntry entry, bool givesResearch = true)
        {
            if (amount == 0) return;
            Gold += amount;
            Today[entry] += amount;
            if (givesResearch && amount > 0)
            {
                rpRemainder += amount;
                int rp = rpRemainder / GoldPerRP;
                rpRemainder -= rp * GoldPerRP;
                ResearchPoints += rp;
                Today.rpEarned += rp;
            }
            GoldChanged?.Invoke(amount);
        }

        public static void Reset(int gold, int rp)
        {
            Gold = gold;
            ResearchPoints = rp;
            rpRemainder = 0;
            Today = new DayLedger();
        }
    }
}
