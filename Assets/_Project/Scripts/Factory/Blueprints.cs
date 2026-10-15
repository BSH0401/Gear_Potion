using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GearPotion
{
    [Serializable]
    public class BlueprintEntry
    {
        public string id;
        public int x, y, dir, config, oc;
    }

    /// <summary>설비 묶음 프리셋 (GDD 5.1 청사진). 좌표는 묶음의 좌하단 기준 상대값.</summary>
    [Serializable]
    public class Blueprint
    {
        public string name;
        public int width, height;
        public List<BlueprintEntry> entries = new();

        public Blueprint Clone()
        {
            var b = new Blueprint { name = name, width = width, height = height };
            foreach (var e in entries)
                b.entries.Add(new BlueprintEntry { id = e.id, x = e.x, y = e.y, dir = e.dir, config = e.config, oc = e.oc });
            return b;
        }

        public int Cost
        {
            get
            {
                int sum = 0;
                foreach (var e in entries) sum += EntryCost(e);
                return sum;
            }
        }

        public static int EntryCost(BlueprintEntry e)
        {
            var def = GameManager.Db.Machine(e.id);
            if (!def) return 0;
            int c = Contracts.BuildCost(def);
            if (e.oc != 0)
            {
                var gear = GameManager.Db.Machine(MachineKind.Overclock);
                if (gear) c += gear.cost;
            }
            return c;
        }

        /// <summary>묶음 전체를 반시계 방향으로 90° 회전 (각 설비의 방향도 함께).</summary>
        public void RotateCCW()
        {
            var cells = new List<Vector2Int>();
            foreach (var e in entries)
            {
                var def = GameManager.Db.Machine(e.id);
                if (!def) continue;
                Building.Footprint(def, new Vector2Int(e.x, e.y), e.dir, cells, out _, out _);
                var min = new Vector2Int(int.MaxValue, int.MaxValue);
                foreach (var c in cells) min = Vector2Int.Min(min, Dir.Rotate(c, 1));
                e.x = min.x;
                e.y = min.y;
                e.dir = (e.dir + 1) & 3;
            }
            Normalize();
        }

        /// <summary>모든 설비가 (0,0) 이상에 오도록 평행이동하고 크기를 다시 잰다.</summary>
        public void Normalize()
        {
            var cells = new List<Vector2Int>();
            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            var max = new Vector2Int(int.MinValue, int.MinValue);
            foreach (var e in entries)
            {
                var def = GameManager.Db.Machine(e.id);
                if (!def) continue;
                Building.Footprint(def, new Vector2Int(e.x, e.y), e.dir, cells, out _, out _);
                foreach (var c in cells) { min = Vector2Int.Min(min, c); max = Vector2Int.Max(max, c); }
            }
            if (min.x == int.MaxValue) { width = height = 0; return; }
            foreach (var e in entries) { e.x -= min.x; e.y -= min.y; }
            width = max.x - min.x + 1;
            height = max.y - min.y + 1;
        }
    }

    /// <summary>저장된 청사진 목록. 세이브와 별개인 전역 파일이라 어느 공방에서나 쓸 수 있다.</summary>
    public static class BlueprintLibrary
    {
        [Serializable]
        class FileData { public List<Blueprint> list = new(); }

        public const int MaxCount = 8;
        public static readonly List<Blueprint> Items = new();
        public static event Action Changed;
        static bool loaded;
        static string FilePath => Path.Combine(Application.persistentDataPath, "gearpotion_blueprints.json");

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (SaveSystem.Disabled || !File.Exists(FilePath)) return;
                var data = JsonUtility.FromJson<FileData>(File.ReadAllText(FilePath));
                if (data?.list != null) Items.AddRange(data.list);
            }
            catch (Exception e) { Debug.LogWarning($"[Blueprint] 불러오기 실패: {e.Message}"); }
        }

        static void Save()
        {
            Changed?.Invoke();
            if (SaveSystem.Disabled) return;
            try { File.WriteAllText(FilePath, JsonUtility.ToJson(new FileData { list = Items }, true)); }
            catch (Exception e) { Debug.LogWarning($"[Blueprint] 저장 실패: {e.Message}"); }
        }

        public static void Add(Blueprint b)
        {
            Items.Insert(0, b);
            while (Items.Count > MaxCount) Items.RemoveAt(Items.Count - 1);
            Save();
        }

        public static void Remove(Blueprint b)
        {
            Items.Remove(b);
            Save();
        }

        /// <summary>"청사진 N" — 목록에 있는 가장 큰 번호 + 1.</summary>
        public static string NextName()
        {
            int max = 0;
            foreach (var b in Items)
                if (b.name != null && b.name.StartsWith("청사진 ") && int.TryParse(b.name.Substring(4), out var n)) max = Mathf.Max(max, n);
            return $"청사진 {max + 1}";
        }
    }

    /// <summary>
    /// 청사진 도구: ① 영역을 드래그해 설비 묶음을 복사(목록에 저장) → ② 커서에 붙은 묶음을
    /// R로 회전하며 클릭해 골드를 내고 한 번에 설치. 우클릭/Esc로 종료.
    /// </summary>
    public class BlueprintTool : MonoBehaviour
    {
        enum State { Idle, Selecting, Pasting }

        public static BlueprintTool Instance;
        public static bool Active => Instance && Instance.state != State.Idle;

        State state;
        Blueprint held;
        Vector2Int dragStart;
        bool dragging;
        Camera cam;
        Transform ghostRoot;
        SpriteRenderer rectGhost;
        readonly List<SpriteRenderer> ghosts = new();
        readonly List<Vector2Int> cells = new();
        public string StatusText { get; private set; }

        void Awake()
        {
            Instance = this;
            BlueprintLibrary.Load();
        }

        void Start()
        {
            cam = Camera.main;
            ghostRoot = new GameObject("BlueprintGhost").transform;
            rectGhost = Visuals.Sprite("Rect", ghostRoot, Sprites.White, new Color(0.4f, 0.85f, 1f, 0.18f), 50, 1f, true);
            rectGhost.enabled = false;
        }

        public void BeginCopy()
        {
            GameManager.Instance.Builder.ClearSelection();
            state = State.Selecting;
            dragging = false;
            held = null;
        }

        public void BeginPaste(Blueprint b)
        {
            GameManager.Instance.Builder.ClearSelection();
            held = b.Clone();
            state = State.Pasting;
        }

        public void Cancel()
        {
            state = State.Idle;
            held = null;
            dragging = false;
        }

        void Update()
        {
            HideGhosts();
            StatusText = null;
            var gm = GameManager.Instance;
            if (state == State.Idle || !gm || !gm.InGame || gm.Modal) return;
            var builder = gm.Builder;
            if (builder.Selected || builder.RemoveMode) { Cancel(); return; }
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null) return;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (mouse.rightButton.wasPressedThisFrame && !HUD.PointerOverUI)) { Cancel(); return; }
            var cell = FactoryGrid.CellAt(cam.ScreenToWorldPoint(mouse.position.ReadValue()));
            if (state == State.Selecting) UpdateSelecting(mouse, cell);
            else UpdatePasting(mouse, kb, cell);
        }

        // ───────────── ① 영역 복사 ─────────────

        void UpdateSelecting(Mouse mouse, Vector2Int cell)
        {
            StatusText = "<color=#8fe0ff>청사진 복사</color> — 저장할 설비 묶음을 좌클릭 드래그로 감싸세요 · 우클릭 취소";
            if (!dragging)
            {
                ShowRect(cell, cell);
                if (mouse.leftButton.wasPressedThisFrame && !HUD.PointerOverUI) { dragging = true; dragStart = cell; }
                return;
            }
            ShowRect(dragStart, cell);
            if (!mouse.leftButton.wasReleasedThisFrame) return;
            dragging = false;
            var bp = Capture(dragStart, cell);
            if (bp == null)
            {
                Toasts.Show("선택한 영역에 복사할 설비가 없습니다", new Color(1f, 0.6f, 0.5f));
                return;
            }
            BlueprintLibrary.Add(bp);
            Toasts.Show($"{bp.name} 저장: 설비 {bp.entries.Count}개 · {bp.width}x{bp.height} · {bp.Cost:N0}G — 클릭해 붙여넣기", new Color(0.55f, 0.9f, 1f), 5f);
            Sfx.Play(SfxId.Sparkle, 0.6f);
            BeginPaste(bp);
        }

        /// <summary>영역 안에 완전히 들어오는 설비(배관 포함)만 담는다. 계산대처럼 철거 불가 설비는 제외.</summary>
        public static Blueprint Capture(Vector2Int a, Vector2Int b)
        {
            var min = Vector2Int.Min(a, b);
            var max = Vector2Int.Max(a, b);
            var bp = new Blueprint { name = BlueprintLibrary.NextName() };
            var seen = new HashSet<Building>();
            var grid = GameManager.Grid;
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            {
                var c = new Vector2Int(x, y);
                foreach (var bld in new[] { grid.At(c), (Building)grid.PipeAt(c) })
                {
                    if (bld == null || !bld.def.removable || !seen.Add(bld)) continue;
                    bool inside = true;
                    foreach (var bc in bld.cells)
                        if (bc.x < min.x || bc.y < min.y || bc.x > max.x || bc.y > max.y) { inside = false; break; }
                    if (!inside) continue;
                    bp.entries.Add(new BlueprintEntry
                    {
                        id = bld.def.id, x = bld.origin.x, y = bld.origin.y, dir = bld.dir, config = bld.config,
                        oc = bld is Processor { overclocked: true } ? 1 : 0,
                    });
                }
            }
            if (bp.entries.Count == 0) return null;
            // 배관은 바닥 설비 다음에 설치되도록 뒤로
            bp.entries.Sort((p, q) => (p.id == "MAC_PIPE").CompareTo(q.id == "MAC_PIPE"));
            bp.Normalize();
            return bp;
        }

        // ───────────── ② 붙여넣기 ─────────────

        void UpdatePasting(Mouse mouse, Keyboard kb, Vector2Int cell)
        {
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                int turns = kb.shiftKey.isPressed ? 3 : 1;
                for (int i = 0; i < turns; i++) held.RotateCCW();
            }
            // 커서가 묶음 가운데쯤 오도록
            var anchor = cell - new Vector2Int((held.width - 1) / 2, (held.height - 1) / 2);
            int ok = 0, blocked = 0, cost = 0;
            foreach (var e in held.entries)
            {
                bool can = CanPlace(e, anchor, out var def);
                if (def == null) continue;
                Building.Footprint(def, anchor + new Vector2Int(e.x, e.y), e.dir, cells, out _, out _);
                var color = can ? new Color(0.45f, 0.85f, 1f, e.id == "MAC_PIPE" ? 0.35f : 0.5f) : new Color(1f, 0.3f, 0.25f, 0.5f);
                foreach (var c in cells) Ghost(c, color, e.id == "MAC_PIPE" ? 0.45f : 0.92f);
                if (can) { ok++; cost += Blueprint.EntryCost(e); }
                else blocked++;
            }
            bool afford = Economy.CanAfford(cost);
            StatusText = $"<color=#8fe0ff>{held.name}</color> 설치 {ok}개 · <color={(afford ? "#ffd27a" : "#ff8877")}>{cost:N0}G</color>" +
                         (blocked > 0 ? $" · <color=#ff8877>막힘 {blocked}개(건너뜀)</color>" : "") + " — R 회전 · 좌클릭 설치 · 우클릭 종료";
            if (!mouse.leftButton.wasPressedThisFrame || HUD.PointerOverUI) return;
            Paste(held, anchor);
        }

        static bool CanPlace(BlueprintEntry e, Vector2Int anchor, out MachineSO def)
        {
            def = GameManager.Db.Machine(e.id);
            if (!def) return false;
            if (!Research.IsUnlocked(def)) return false;
            if (e.oc != 0)
            {
                var gear = GameManager.Db.Machine(MachineKind.Overclock);
                if (gear && !Research.IsUnlocked(gear)) return false;
            }
            return GameManager.Grid.CanPlace(def, anchor + new Vector2Int(e.x, e.y), e.dir, out _);
        }

        /// <summary>설치 가능한 설비만 골라 비용을 한 번에 지불하고 설치한다. 반환값 = 설치 개수.</summary>
        public static int Paste(Blueprint bp, Vector2Int anchor)
        {
            var todo = new List<BlueprintEntry>();
            int cost = 0;
            foreach (var e in bp.entries)
                if (CanPlace(e, anchor, out _)) { todo.Add(e); cost += Blueprint.EntryCost(e); }
            if (todo.Count == 0)
            {
                Toasts.Show("설치할 수 있는 칸이 없습니다", new Color(1f, 0.6f, 0.5f));
                Sfx.Play(SfxId.Error);
                return 0;
            }
            if (!Economy.TrySpend(cost, LedgerEntry.Construction))
            {
                Toasts.Show($"골드가 부족합니다 ({cost:N0}G 필요)", new Color(1f, 0.5f, 0.4f));
                Sfx.Play(SfxId.Error);
                return 0;
            }
            var grid = GameManager.Grid;
            int placed = 0;
            Vector3 center = Vector3.zero;
            foreach (var e in todo)
            {
                var def = GameManager.Db.Machine(e.id);
                var o = anchor + new Vector2Int(e.x, e.y);
                if (!grid.CanPlace(def, o, e.dir, out _)) { Economy.Earn(Blueprint.EntryCost(e), LedgerEntry.Refund, false); continue; }
                var b = grid.Place(def, o, e.dir, e.config);
                if (e.oc != 0 && b is Processor p) p.SetOverclock(true);
                center += b.center;
                placed++;
            }
            if (placed > 0) Fx.Float(center / placed, $"청사진 {placed}개 설치 -{cost:N0}G", new Color(0.55f, 0.9f, 1f));
            Sfx.Play(SfxId.Place);
            return placed;
        }

        // ───────────── 표시 ─────────────

        void ShowRect(Vector2Int a, Vector2Int b)
        {
            var min = Vector2Int.Min(a, b);
            var max = Vector2Int.Max(a, b);
            rectGhost.enabled = true;
            rectGhost.transform.position = new Vector3((min.x + max.x + 1) / 2f, (min.y + max.y + 1) / 2f, 0f);
            rectGhost.transform.localScale = new Vector3(max.x - min.x + 1, max.y - min.y + 1, 1f);
            var grid = GameManager.Grid;
            var seen = new HashSet<Building>();
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            {
                var bld = grid.At(new Vector2Int(x, y));
                if (bld != null && seen.Add(bld))
                    foreach (var c in bld.cells) Ghost(c, new Color(0.45f, 0.85f, 1f, 0.3f), 0.92f);
            }
        }

        int ghostIndex;

        void Ghost(Vector2Int c, Color color, float scale)
        {
            if (ghostIndex >= ghosts.Count) ghosts.Add(Visuals.Sprite("G", ghostRoot, Sprites.Rounded, Color.white, 51, 1f, true));
            var g = ghosts[ghostIndex++];
            g.enabled = true;
            g.transform.position = FactoryGrid.CellCenter(c);
            g.transform.localScale = Vector3.one * scale;
            g.color = color;
        }

        void HideGhosts()
        {
            for (int i = 0; i < ghostIndex; i++) ghosts[i].enabled = false;
            ghostIndex = 0;
            if (rectGhost) rectGhost.enabled = false;
        }
    }
}
