using UnityEngine;

namespace GearPotion
{
    /// <summary>청사진 목록 (B): 새 영역 복사 · 저장된 묶음 붙여넣기/삭제.</summary>
    public partial class HUD
    {
        GameObject blueprintPanel;
        bool blueprintOwnsModal;

        public void ShowBlueprints()
        {
            if (blueprintPanel) { CloseBlueprints(); return; }
            var gm = GameManager.Instance;
            if (!gm.InGame || settlementPanel) return;
            BlueprintTool.Instance.Cancel();
            var o = Overlay("Blueprints", 0.6f);
            blueprintPanel = o.gameObject;
            gm.PushModal();
            blueprintOwnsModal = true;
            var p = UIFactory.Rect("Panel", o, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 860f));
            UIFactory.PanelBg(p, UIFactory.PanelLight);
            UIFactory.Label(UIFactory.Rect("Head", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(900f, 56f)),
                "<b>청사진</b>", 34, UIFactory.Accent);
            UIFactory.Label(UIFactory.Rect("Hint", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(940f, 30f)),
                $"잘 만든 라인을 드래그로 복사해 저장하고, 골드만 내면 통째로 설치합니다 · 최대 {BlueprintLibrary.MaxCount}개 (모든 공방 공용)",
                19, UIFactory.Dim);
            UIFactory.Button(p, "닫기", new Vector2(1f, 1f), new Vector2(-80f, -44f), new Vector2(120f, 50f), CloseBlueprints, 24);
            UIFactory.Button(p, "+ 영역 복사해서 새 청사진", new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(460f, 60f), () =>
            {
                CloseBlueprints();
                BlueprintTool.Instance.BeginCopy();
            }, 24);

            float y = -195f;
            if (BlueprintLibrary.Items.Count == 0)
                UIFactory.Label(UIFactory.Rect("Empty", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 30f), new Vector2(900f, 60f)),
                    "<color=#999>저장된 청사진이 없습니다.\n위 버튼을 누르고 공방에서 설비 묶음을 드래그해 감싸세요.</color>", 21, UIFactory.TextColor);
            foreach (var bp in BlueprintLibrary.Items)
            {
                var b = bp;
                var row = UIFactory.Rect("Row", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(920f, 70f));
                row.pivot = new Vector2(0.5f, 1f);
                UIFactory.Image(row, new Color(0.12f, 0.1f, 0.14f, 0.92f));
                bool locked = false;
                foreach (var e in b.entries)
                {
                    var def = GameManager.Db.Machine(e.id);
                    if (def && !Research.IsUnlocked(def)) { locked = true; break; }
                }
                UIFactory.Label(UIFactory.Rect("Name", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(560f, 60f)),
                    $"<b>{b.name}</b>  <size=19><color=#bbb>{b.width}x{b.height} · 설비 {b.entries.Count}개 · </color><color=#ffd27a>{b.Cost:N0}G</color>" +
                    (locked ? " · <color=#ff8877>미해금 설비 포함(건너뜀)</color>" : "") + "</size>",
                    22, UIFactory.TextColor, TextAnchor.MiddleLeft);
                UIFactory.Button(row, "붙여넣기", new Vector2(1f, 0.5f), new Vector2(-200f, 0f), new Vector2(150f, 50f), () =>
                {
                    CloseBlueprints();
                    BlueprintTool.Instance.BeginPaste(b);
                }, 22);
                UIFactory.Button(row, "삭제", new Vector2(1f, 0.5f), new Vector2(-62f, 0f), new Vector2(100f, 50f), () =>
                {
                    BlueprintLibrary.Remove(b);
                    CloseBlueprints();
                    ShowBlueprints();
                }, 22);
                y -= 78f;
            }
        }

        void CloseBlueprints()
        {
            if (!blueprintPanel) return;
            Destroy(blueprintPanel);
            blueprintPanel = null;
            if (blueprintOwnsModal) GameManager.Instance.PopModal();
            blueprintOwnsModal = false;
        }
    }
}
