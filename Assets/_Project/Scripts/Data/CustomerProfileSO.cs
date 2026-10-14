using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>손님 프로필 (GDD 6.2 CustomerProfileSO).</summary>
    public class CustomerProfileSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public Color color = Color.white;
        [Tooltip("허용 가격 배율 — 진열대 가격 배율이 이보다 높으면 구매를 망설임")]
        public float priceTolerance = 1.2f;
        public float moveSpeed = 2.2f;
        public Grade minGrade = Grade.C;
        [Tooltip("이 등급 이하만 원하는 손님 (암시장 브로커)")]
        public bool wantsDefective;
        public int buyCount = 1;
        public float payMultiplier = 1f;
        public float spawnWeight = 1f;
        public DayPhaseMask phases = DayPhaseMask.Day | DayPhaseMask.Evening;
        public int minDay = 1;
        public float suspicionPerVisit;
    }
}
