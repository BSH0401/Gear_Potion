using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>원재료 데이터 (GDD 4.1).</summary>
    public class IngredientSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public string category;
        public int cost;
        public Elements elements;
        [Range(0, 100)] public float impurity;
        public Color color = Color.white;
        [Tooltip("분쇄 시 주 원소에 추가되는 고정 수치 (예: 화염 뿌리 +8)")]
        public float crushBonus;
        [Tooltip("추출 시 추가 원액이 나올 확률 (이슬 약초 압착 수율 120%)")]
        [Range(0, 1)] public float extractBonusChance;
        [TextArea] public string note;
        public bool unlockedByDefault = true;
        public int researchCost;
    }
}
