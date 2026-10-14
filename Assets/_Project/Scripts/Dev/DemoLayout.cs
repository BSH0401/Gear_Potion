using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// -demo 플래그: 부지를 26칸으로 넓히고 두 라인을 추가한다.
    /// ① 화염 활력제: 분쇄 → 추출 → 여과(슬러지 → 폐기통) → 병입 → 분배기 → 진열대 2개
    /// ② 마나 엘릭서: 버섯 분쇄·추출 / 얼음 추출 → 배관 2계통 → 합성 가마 → 배관 → 병입 → 진열대
    /// </summary>
    public static class DemoLayout
    {
        public static void Build(FactoryGrid grid)
        {
            var db = GameManager.Db;
            Research.UnlockAll = true;
            grid.SetExpansion(2);
            GameManager.Instance.CameraRig.Fit(grid.Width);

            // ① 화염 활력제 라인 (설비끼리 바로 맞닿아 직접 전달)
            Place(grid, "MAC_CRATE", 0, 5, 0, Ing(db, "MAT_02"));
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

            // ② 마나 엘릭서 라인 (배관 레이어)
            Place(grid, "MAC_CRATE", 14, 9, 0, Ing(db, "MAT_05"));
            Place(grid, "MAC_CRUSHER", 15, 8, 0);
            Place(grid, "MAC_EXTRACTOR", 16, 8, 0);
            foreach (var c in new[] { (18, 9), (19, 9), (19, 8) }) Place(grid, "MAC_PIPE", c.Item1, c.Item2, 0);
            Place(grid, "MAC_CRATE", 14, 5, 0, Ing(db, "MAT_06"));
            Place(grid, "MAC_EXTRACTOR", 15, 4, 0);
            foreach (var c in new[] { (17, 5), (18, 5), (19, 5), (19, 6) }) Place(grid, "MAC_PIPE", c.Item1, c.Item2, 0);
            Place(grid, "MAC_CAULDRON", 20, 6, 3);
            Place(grid, "MAC_PIPE", 21, 5, 0);
            Place(grid, "MAC_BOTTLER", 22, 4, 3);
            Place(grid, "MAC_COLDSHELF", 23, 3, 0, 1);
            Place(grid, "MAC_CORE", 24, 8, 0);
            // 배관이 벨트 위를 지나가는 교차 예시 (서로 간섭하지 않음)
            Place(grid, "MAC_BELT", 18, 6, 1);
            Place(grid, "MAC_BELT", 18, 7, 1);
            Place(grid, "MAC_PIPE", 18, 6, 0);
        }

        static int Ing(GameDatabase db, string id) => db.ingredients.IndexOf(db.Ingredient(id));

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
