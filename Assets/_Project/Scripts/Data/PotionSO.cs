using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>포션 레시피 규칙 (GDD 4.3, RecipeRuleSO).</summary>
    public class PotionSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public string shortName;
        public int tier;
        public Elements requirement;
        [Tooltip("이 값보다 불순물이 많으면 이 포션으로 인정되지 않음 (현자의 돌 = 0)")]
        public float maxImpurity = 100f;
        public int basePrice;
        public Color color = Color.white;
        [TextArea] public string note;
    }
}
