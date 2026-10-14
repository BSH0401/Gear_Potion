using UnityEngine;

namespace GearPotion
{
    /// <summary>-demo 플래그: 화염 활력제 라인(분쇄→추출→여과→병입)과 분배기·두 번째 진열대를 추가한다.</summary>
    public static class DemoLayout
    {
        public static void Build(FactoryGrid grid)
        {
            var db = GameManager.Db;
            Research.UnlockAll = true;
            int fireRoot = db.ingredients.IndexOf(db.Ingredient("MAT_02"));
            Place(grid, "MAC_CRATE", 0, 5, 0, fireRoot);
            Place(grid, "MAC_CRUSHER", 1, 4, 0);
            Place(grid, "MAC_EXTRACTOR", 2, 4, 0);
            Place(grid, "MAC_FILTER", 4, 4, 0);
            Place(grid, "MAC_TRASH", 5, 6, 0);
            Place(grid, "MAC_BOTTLER", 6, 4, 0);
            Place(grid, "MAC_CORE", 10, 8, 0);
            var old = grid.At(new Vector2Int(8, 4));
            if (old != null) grid.Remove(old);
            Place(grid, "MAC_SPLITTER", 8, 4, 3);
            Place(grid, "MAC_BELT", 9, 4, 3);
            Place(grid, "MAC_SHELF", 9, 3, 0, 1);
        }

        static void Place(FactoryGrid grid, string id, int x, int y, int dir, int config = 0)
        {
            var def = GameManager.Db.Machine(id);
            var o = new Vector2Int(x, y);
            string reason = "데이터 없음";
            if (def && grid.CanPlace(def, o, dir, out reason)) grid.Place(def, o, dir, config);
            else Debug.LogWarning($"[Demo] {id} @ {o} 배치 실패 {reason}");
        }
    }
}
