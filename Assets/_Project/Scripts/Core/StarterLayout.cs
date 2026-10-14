using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 새 게임 시작 시 깔려 있는 튜토리얼용 라인 (GDD 4.3 T1: 약초 추출 → 병입 2단계).
    /// 투입구 → 벨트 → 증기 압착 추출기 → 배관(원액) → 병입기 → 벨트(벽 통과) → 진열대.
    /// </summary>
    public static class StarterLayout
    {
        public static void Build(FactoryGrid grid)
        {
            var db = GameManager.Db;
            Place(grid, db.Machine(MachineKind.Counter), 1, 1, 0);
            Place(grid, db.Machine(MachineKind.ManaCore), 12, 8, 0);
            Place(grid, db.Machine(MachineKind.Crate), 1, 7, 0, db.ingredients.IndexOf(db.Ingredient("MAT_01")));
            Place(grid, db.Machine("MAC_BELT"), 2, 7, 0);
            Place(grid, db.Machine(MachineKind.Extractor), 3, 6, 0);
            Place(grid, db.Machine("MAC_PIPE"), 5, 7, 0);
            Place(grid, db.Machine(MachineKind.Bottler), 6, 6, 0);
            for (int y = 7; y >= 4; y--) Place(grid, db.Machine("MAC_BELT"), 8, y, 3);
            Place(grid, db.Machine("MAC_SHELF"), 8, 3, 0, 1);
        }

        static void Place(FactoryGrid grid, MachineSO def, int x, int y, int dir, int config = 0)
        {
            if (!def) { Debug.LogError("[Starter] 설비 데이터가 없습니다 — Gear & Potion/Setup Project 를 다시 실행하세요."); return; }
            var origin = new Vector2Int(x, y);
            if (grid.CanPlace(def, origin, dir, out var reason)) grid.Place(def, origin, dir, config);
            else Debug.LogWarning($"[Starter] {def.id} @ {origin}: {reason}");
        }
    }
}
