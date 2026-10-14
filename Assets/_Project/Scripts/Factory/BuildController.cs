using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GearPotion
{
    /// <summary>
    /// 그리드 배치/회전/철거 입력 (GDD 7 - 1개월 차).
    /// 좌클릭 배치(벨트는 드래그로 연속 배치), R 회전, 우클릭 철거, Q 스포이트, Tab 히트맵.
    /// </summary>
    public class BuildController : MonoBehaviour
    {
        public const float RefundRate = 0.75f;

        public MachineSO Selected { get; private set; }
        public bool RemoveMode { get; private set; }
        public int Rotation { get; private set; }
        public Building Hovered { get; private set; }

        Camera cam;
        Transform ghostRoot;
        readonly List<SpriteRenderer> ghostCells = new();
        SpriteRenderer ghostArrow;
        readonly List<Vector2Int> footprint = new();
        Vector2Int lastDragCell = new(int.MinValue, 0);
        Building lastDragBelt;
        bool dragging, removeDragging;

        void Start()
        {
            cam = Camera.main;
            ghostRoot = new GameObject("Ghost").transform;
            ghostArrow = Visuals.Sprite("GhostArrow", ghostRoot, Sprites.Chevron, new Color(1f, 0.9f, 0.4f, 0.9f), 52, 0.45f);
        }

        public void Select(MachineSO def)
        {
            Selected = Selected == def ? null : def;
            RemoveMode = false;
            Sfx.Play(SfxId.Click, 0.4f);
        }

        public void ToggleRemoveMode()
        {
            RemoveMode = !RemoveMode;
            Selected = null;
        }

        public void ClearSelection()
        {
            Selected = null;
            RemoveMode = false;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            HideGhost();
            Hovered = null;
            if (!gm || !gm.InGame || gm.Modal) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null) return;

            if (kb != null)
            {
                if (kb.rKey.wasPressedThisFrame) Rotation = (Rotation + (kb.shiftKey.isPressed ? 3 : 1)) & 3;
                if (kb.escapeKey.wasPressedThisFrame && (Selected || RemoveMode)) ClearSelection();
                if (kb.xKey.wasPressedThisFrame) ToggleRemoveMode();
                if (kb.tabKey.wasPressedThisFrame)
                {
                    GameManager.Grid.Heatmap = !GameManager.Grid.Heatmap;
                    Toasts.Show(GameManager.Grid.Heatmap ? "효율 히트맵 뷰: 초록=가동 · 노랑=재료 대기 · 빨강=막힘/마력 부족" : "일반 뷰", UIFactory.Accent);
                }
            }

            bool overUI = HUD.PointerOverUI;
            var world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var cell = FactoryGrid.CellAt(world);
            var grid = GameManager.Grid;
            Hovered = overUI ? null : grid.At(cell);

            if (kb != null && kb.qKey.wasPressedThisFrame && Hovered != null && Hovered.def.removable)
            {
                Selected = Hovered.def;
                Rotation = Hovered.dir;
                RemoveMode = false;
            }

            if (mouse.leftButton.wasReleasedThisFrame) { dragging = false; lastDragBelt = null; lastDragCell = new Vector2Int(int.MinValue, 0); }
            if (mouse.rightButton.wasReleasedThisFrame) removeDragging = false;
            if (overUI) return;

            // 우클릭 = 철거 (드래그로 연속 철거). 선택 중이면 선택 해제.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (Selected) { Selected = null; return; }
                removeDragging = true;
            }
            if (removeDragging && mouse.rightButton.isPressed && Hovered != null) TryRemove(Hovered);

            if (RemoveMode)
            {
                if (Hovered != null) ShowGhost(Hovered.cells, Hovered.def.removable ? new Color(1f, 0.3f, 0.25f, 0.45f) : new Color(0.5f, 0.5f, 0.5f, 0.4f), -1, default);
                if (mouse.leftButton.isPressed && Hovered != null) TryRemove(Hovered);
                return;
            }

            if (!Selected)
            {
                if (mouse.leftButton.wasPressedThisFrame && Hovered != null)
                {
                    Hovered.OnClick();
                    Sfx.Play(SfxId.Click, 0.5f);
                }
                return;
            }

            // 배치 미리보기
            var origin = cell;
            Building.Footprint(Selected, origin, Rotation, footprint, out var outCell, out _);
            // 커서가 설비 중심 근처에 오도록 큰 설비는 원점을 보정
            var shift = new Vector2Int(0, 0);
            foreach (var c in footprint) shift = Vector2Int.Max(shift, c - origin);
            origin -= new Vector2Int(shift.x / 2, shift.y / 2);
            Building.Footprint(Selected, origin, Rotation, footprint, out outCell, out _);
            bool ok = grid.CanPlace(Selected, origin, Rotation, out var reason);
            bool afford = Economy.CanAfford(Selected.cost);
            ShowGhost(footprint, ok && afford ? new Color(0.4f, 1f, 0.5f, 0.45f) : new Color(1f, 0.3f, 0.25f, 0.45f), Rotation,
                Selected.kind == MachineKind.Belt ? FactoryGrid.CellCenter(origin) : FactoryGrid.CellCenter(outCell) - Dir.V3(Rotation) * 0.6f);
            GhostReason = ok ? (afford ? null : "골드 부족") : reason;

            if (mouse.leftButton.wasPressedThisFrame) dragging = true;
            if (!dragging || !mouse.leftButton.isPressed) return;

            if (Selected.kind == MachineKind.Belt)
            {
                // 벨트 드래그: 이동 방향으로 자동 회전하고 직전 벨트도 이어지도록 돌린다
                if (cell == lastDragCell) return;
                if (lastDragCell.x != int.MinValue)
                {
                    var delta = cell - lastDragCell;
                    if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                    {
                        for (int d = 0; d < 4; d++) if (Dir.V[d] == delta) Rotation = d;
                        if (lastDragBelt != null && lastDragBelt.dir != Rotation && grid.buildings.Contains(lastDragBelt))
                        {
                            var o = lastDragBelt.origin;
                            int cfg = lastDragBelt.config;
                            var def = lastDragBelt.def;
                            grid.Remove(lastDragBelt);
                            lastDragBelt = grid.Place(def, o, Rotation, cfg);
                        }
                    }
                }
                lastDragCell = cell;
                if (grid.CanPlace(Selected, cell, Rotation, out _)) lastDragBelt = TryPlace(cell);
                else lastDragBelt = grid.At(cell) is Belt b ? b : null;
            }
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!ok) { Toasts.Show(reason, new Color(1f, 0.5f, 0.4f)); Sfx.Play(SfxId.Error); return; }
                TryPlace(origin);
            }
        }

        public string GhostReason { get; private set; }

        Building TryPlace(Vector2Int origin)
        {
            var grid = GameManager.Grid;
            if (!grid.CanPlace(Selected, origin, Rotation, out _)) return null;
            if (!Economy.TrySpend(Selected.cost, LedgerEntry.Construction))
            {
                Toasts.Show($"골드가 부족합니다 ({Selected.cost}G)", new Color(1f, 0.5f, 0.4f));
                Sfx.Play(SfxId.Error);
                return null;
            }
            int config = Selected.kind == MachineKind.Shelf ? 1 : Selected.kind == MachineKind.Crate ? DefaultIngredient() : 0;
            var b = grid.Place(Selected, origin, Rotation, config);
            Sfx.Play(SfxId.Place, 0.7f);
            return b;
        }

        static int DefaultIngredient()
        {
            var db = GameManager.Db;
            for (int i = 0; i < db.ingredients.Count; i++) if (Research.IsUnlocked(db.ingredients[i])) return i;
            return -1;
        }

        void TryRemove(Building b)
        {
            if (!b.def.removable)
            {
                Toasts.Show($"{b.def.displayName}은(는) 철거할 수 없습니다", new Color(1f, 0.6f, 0.5f));
                removeDragging = false;
                return;
            }
            int refund = Mathf.RoundToInt(b.def.cost * RefundRate);
            GameManager.Grid.Remove(b);
            Economy.Earn(refund, LedgerEntry.Refund, false);
            Sfx.Play(SfxId.Remove, 0.6f);
            if (refund > 0) Fx.Float(b.center, $"+{refund}G", new Color(0.8f, 0.8f, 0.8f), 0.8f);
        }

        void ShowGhost(List<Vector2Int> cells, Color color, int dir, Vector3 arrowPos)
        {
            while (ghostCells.Count < cells.Count)
                ghostCells.Add(Visuals.Sprite("GhostCell", ghostRoot, Sprites.Rounded, Color.white, 51, 0.95f));
            for (int i = 0; i < ghostCells.Count; i++)
            {
                bool on = i < cells.Count;
                ghostCells[i].enabled = on;
                if (!on) continue;
                ghostCells[i].transform.position = FactoryGrid.CellCenter(cells[i]);
                ghostCells[i].color = color;
            }
            ghostArrow.enabled = dir >= 0;
            if (dir >= 0)
            {
                ghostArrow.transform.position = arrowPos;
                ghostArrow.transform.rotation = Quaternion.Euler(0f, 0f, dir * 90f);
            }
        }

        void HideGhost()
        {
            foreach (var g in ghostCells) g.enabled = false;
            if (ghostArrow) ghostArrow.enabled = false;
            GhostReason = null;
        }
    }
}
