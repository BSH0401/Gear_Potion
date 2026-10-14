using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>설비 데이터 (GDD 4.2).</summary>
    public class MachineSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public string shortName;
        public MachineKind kind;
        public BuildCategory category;
        public BuildZone zone = BuildZone.Factory;
        public Vector2Int size = Vector2Int.one;
        public int tier = 1;
        public int cost;
        [Tooltip("가동 중 초당 마력 소비 (마나 코어는 공급량)")]
        public float mana;
        public float processTime = 1f;
        [Tooltip("벨트: 초당 타일 / 진열대: 슬롯 수 / 가마: 혼합 원액 수")]
        public float param;
        [Tooltip("진열대 전용: 판매가 보너스 배율 (냉장 1.15, VIP 1.3)")]
        public float priceBonus = 1f;
        public Color color = Color.gray;
        [TextArea] public string description;
        public bool unlockedByDefault = true;
        public int researchCost;
        public bool removable = true;
    }
}
