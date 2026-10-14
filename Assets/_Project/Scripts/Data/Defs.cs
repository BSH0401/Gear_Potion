using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    public enum MachineKind
    {
        Belt, Splitter, Sorter, Crate, Crusher, Extractor, Filter, Distiller, Cauldron, Bottler,
        SlimeVat, Trash, Export, Shelf, ManaCore, Counter,
    }

    public enum BuildZone { Factory, Shop, Any }
    public enum BuildCategory { Logistics, Processing, Shop, Power }
    public enum Grade { C, B, A, S, SSS }
    public enum ItemKind { Ingredient, Powder, Extract, Potion, Sludge, Crystal }

    [System.Flags]
    public enum DayPhaseMask { Day = 1, Evening = 2 }

    public static class GradeInfo
    {
        public static Grade FromPurity(float purity) =>
            purity >= 99f ? Grade.SSS : purity >= 90f ? Grade.S : purity >= 70f ? Grade.A : purity >= 40f ? Grade.B : Grade.C;

        public static float PriceMultiplier(Grade g) => g switch
        {
            Grade.C => 0.6f, Grade.B => 1f, Grade.A => 1.45f, Grade.S => 2.1f, _ => 3.5f,
        };

        public static int Reputation(Grade g) => g switch
        {
            Grade.C => -1, Grade.B => 0, Grade.A => 1, Grade.S => 3, _ => 5,
        };

        public static Color Color(Grade g) => g switch
        {
            Grade.C => new Color(0.6f, 0.55f, 0.5f),
            Grade.B => new Color(0.85f, 0.85f, 0.85f),
            Grade.A => new Color(0.4f, 0.9f, 0.5f),
            Grade.S => new Color(1f, 0.82f, 0.25f),
            _ => new Color(1f, 0.45f, 0.95f),
        };

        public static string Label(Grade g) => g.ToString();
    }
}
