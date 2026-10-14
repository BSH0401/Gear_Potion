using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    public class GameDatabase : ScriptableObject
    {
        public List<IngredientSO> ingredients = new();
        public List<MachineSO> machines = new();
        public List<PotionSO> potions = new();
        public List<CustomerProfileSO> customers = new();
        [Tooltip("어떤 레시피에도 맞지 않을 때 만들어지는 실패작")]
        public PotionSO murkyPotion;

        public IngredientSO Ingredient(string id) => ingredients.Find(i => i.id == id);
        public MachineSO Machine(string id) => machines.Find(m => m.id == id);
        public PotionSO Potion(string id) => id == murkyPotion?.id ? murkyPotion : potions.Find(p => p.id == id);
        public MachineSO Machine(MachineKind kind) => machines.Find(m => m.kind == kind);
    }
}
