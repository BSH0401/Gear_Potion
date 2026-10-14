using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    public enum BuildingStatus { Idle, Working, NoInput, Blocked, LowPower, NoGold, Off }

    public static class Dir
    {
        public static readonly Vector2Int[] V = { new(1, 0), new(0, 1), new(-1, 0), new(0, -1) };
        public static int Opp(int d) => (d + 2) & 3;
        public static int Left(int d) => (d + 1) & 3;
        public static int Right(int d) => (d + 3) & 3;
        public static Vector3 V3(int d) => new(V[d].x, V[d].y, 0f);

        public static Vector2Int Rotate(Vector2Int p, int d) => d switch
        {
            0 => p,
            1 => new Vector2Int(-p.y, p.x),
            2 => new Vector2Int(-p.x, -p.y),
            _ => new Vector2Int(p.y, -p.x),
        };
    }

    /// <summary>
    /// 그리드에 배치되는 모든 설비의 베이스. MonoBehaviour가 아닌 순수 C# 객체로 두고
    /// TickManager(=FactoryGrid)가 초당 10회 OnTick을 호출한다 (GDD 6.1).
    /// </summary>
    public abstract class Building
    {
        public MachineSO def;
        public FactoryGrid grid;
        public Vector2Int origin;
        public int dir;
        public int config;
        public readonly List<Vector2Int> cells = new();
        public Vector2Int outCell, sideCell;
        public Vector3 center;
        public BuildingStatus status = BuildingStatus.Idle;
        public bool working;
        public float progress;

        protected GameObject go;
        protected Transform body;
        protected SpriteRenderer bodySr;
        protected SpriteRenderer progressBar, statusOverlay;
        protected Vector3 bodyScale;
        protected TextMesh label;
        float animTime;

        public virtual float ManaDemand => working ? def.mana : 0f;
        public virtual float ManaSupply => 0f;
        public GameObject GameObject => go;

        public static Building Create(MachineSO def) => def.kind switch
        {
            MachineKind.Belt => new Belt(),
            MachineKind.Splitter => new Splitter(),
            MachineKind.Sorter => new Sorter(),
            MachineKind.Crate => new SupplyCrate(),
            MachineKind.Trash => new TrashBin(),
            MachineKind.Export => new ExportCrate(),
            MachineKind.Shelf => new Shelf(),
            MachineKind.ManaCore => new ManaCore(),
            MachineKind.Counter => new Counter(),
            MachineKind.Pipe => new Pipe(),
            _ => new Processor(),
        };

        /// <summary>회전된 점유 타일과 출력 포트 좌표를 계산 (원점 = 회전 후 좌하단).</summary>
        public static void Footprint(MachineSO def, Vector2Int origin, int dir, List<Vector2Int> outCells, out Vector2Int outCell, out Vector2Int sideCell)
        {
            outCells.Clear();
            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            for (int i = 0; i < def.size.x; i++)
            for (int j = 0; j < def.size.y; j++)
                min = Vector2Int.Min(min, Dir.Rotate(new Vector2Int(i, j), dir));
            for (int i = 0; i < def.size.x; i++)
            for (int j = 0; j < def.size.y; j++)
                outCells.Add(Dir.Rotate(new Vector2Int(i, j), dir) - min + origin);
            outCell = Dir.Rotate(new Vector2Int(def.size.x, def.size.y / 2), dir) - min + origin;
            sideCell = Dir.Rotate(new Vector2Int(def.size.x / 2, def.size.y), dir) - min + origin;
        }

        public void Init(FactoryGrid grid, MachineSO def, Vector2Int origin, int dir, int config)
        {
            this.grid = grid;
            this.def = def;
            this.origin = origin;
            this.dir = dir;
            this.config = config;
            Footprint(def, origin, dir, cells, out outCell, out sideCell);
            center = Vector3.zero;
            foreach (var c in cells) center += FactoryGrid.CellCenter(c);
            center /= cells.Count;
            BuildView();
            OnPlaced();
        }

        protected virtual void OnPlaced() { }

        protected virtual void BuildView()
        {
            go = new GameObject($"{def.id}_{origin.x}_{origin.y}");
            go.transform.SetParent(grid.transform, false);
            go.transform.position = center;
            var size = new Vector2(Mathf.Abs(Dir.Rotate(def.size, dir).x), Mathf.Abs(Dir.Rotate(def.size, dir).y));
            bodySr = Visuals.Sprite("Body", go.transform, Sprites.Rounded, def.color, 10);
            body = bodySr.transform;
            bodyScale = new Vector3(size.x - 0.08f, size.y - 0.08f, 1f);
            body.localScale = bodyScale;
            var rim = Visuals.Sprite("Rim", body, Sprites.RoundedOutline, new Color(0f, 0f, 0f, 0.45f), 11);
            rim.transform.localScale = Vector3.one;
            if (HasOutput)
            {
                var arrow = Visuals.Sprite("Out", go.transform, Sprites.Chevron, new Color(1f, 0.85f, 0.4f, 0.9f), 13, 0.32f);
                arrow.transform.position = FactoryGrid.CellCenter(outCell) - Dir.V3(dir) * 0.62f;
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, dir * 90f);
            }
            if (!string.IsNullOrEmpty(def.shortName))
            {
                label = Visuals.Label("Label", go.transform, def.shortName, new Color(1f, 1f, 1f, 0.95f), 14,
                    Mathf.Min(size.x, size.y) >= 2f ? 0.3f : 0.2f);
                label.transform.localPosition = new Vector3(0f, size.y > 1.5f ? 0.15f : 0.05f, 0f);
            }
            progressBar = Visuals.Sprite("Progress", go.transform, Sprites.White, new Color(1f, 0.9f, 0.45f, 0.9f), 13);
            progressBar.transform.localPosition = new Vector3(-(size.x - 0.3f) / 2f, -size.y / 2f + 0.14f, 0f);
            progressBar.transform.localScale = new Vector3(0f, 0.06f, 1f);
            barWidth = size.x - 0.3f;
            statusOverlay = Visuals.Sprite("Heat", go.transform, Sprites.Rounded, Color.clear, 30);
            statusOverlay.transform.localScale = bodyScale;
            statusOverlay.enabled = false;
        }

        float barWidth;
        protected virtual bool HasOutput => true;

        public virtual void Animate(float dt, ViewMode mode)
        {
            ApplyFade(mode);
            if (progressBar)
            {
                float p = Mathf.Clamp01(progress);
                progressBar.enabled = p > 0.01f;
                var t = progressBar.transform;
                t.localScale = new Vector3(barWidth * p, 0.06f, 1f);
                t.localPosition = new Vector3(-barWidth / 2f + barWidth * p / 2f, t.localPosition.y, 0f);
            }
            if (body)
            {
                if (working)
                {
                    // 프로시저럴 Squash & Stretch (GDD 6.3)
                    animTime += dt * 9f;
                    float s = Mathf.Sin(animTime) * 0.05f;
                    body.localScale = new Vector3(bodyScale.x * (1f + s), bodyScale.y * (1f - s), 1f);
                }
                else body.localScale = Vector3.Lerp(body.localScale, bodyScale, dt * 10f);
            }
            if (statusOverlay)
            {
                bool heatmap = mode == ViewMode.Heatmap;
                statusOverlay.enabled = heatmap;
                if (heatmap) statusOverlay.color = StatusColor(status) * new Color(1f, 1f, 1f, 0.55f);
            }
        }

        // 배관 뷰에서는 바닥 설비를 반투명하게 (GDD 5.1 다중 레이어 투시 뷰)
        ViewMode fadeMode;
        SpriteRenderer[] fadeTargets;
        float[] fadeAlphas;
        MeshRenderer fadeLabel;

        protected void ApplyFade(ViewMode mode)
        {
            if (mode == fadeMode || !go) return;
            fadeMode = mode;
            if (fadeTargets == null)
            {
                fadeTargets = go.GetComponentsInChildren<SpriteRenderer>(true);
                fadeAlphas = new float[fadeTargets.Length];
                for (int i = 0; i < fadeTargets.Length; i++) fadeAlphas[i] = fadeTargets[i].color.a;
                if (label) fadeLabel = label.GetComponent<MeshRenderer>();
            }
            float k = mode == ViewMode.Pipes ? 0.28f : 1f;
            for (int i = 0; i < fadeTargets.Length; i++)
            {
                if (fadeTargets[i] == statusOverlay) continue;
                var c = fadeTargets[i].color;
                c.a = fadeAlphas[i] * k;
                fadeTargets[i].color = c;
            }
            if (fadeLabel) fadeLabel.enabled = mode != ViewMode.Pipes;
        }

        public static Color StatusColor(BuildingStatus s) => s switch
        {
            BuildingStatus.Working => new Color(0.3f, 1f, 0.4f),
            BuildingStatus.NoInput or BuildingStatus.Idle => new Color(1f, 0.85f, 0.2f),
            BuildingStatus.Off => new Color(0.5f, 0.5f, 0.5f),
            _ => new Color(1f, 0.25f, 0.2f),
        };

        public static string StatusText(BuildingStatus s) => s switch
        {
            BuildingStatus.Working => "가동 중",
            BuildingStatus.NoInput => "재료 대기",
            BuildingStatus.Blocked => "배출구 막힘",
            BuildingStatus.LowPower => "마력 부족",
            BuildingStatus.NoGold => "골드 부족",
            BuildingStatus.Off => "정지",
            _ => "대기",
        };

        public abstract void Tick(float dt, float power);
        public virtual bool TryInsert(Item item, Vector2Int cell, int travelDir) => false;
        public virtual void OnClick() { }
        public virtual string ClickHint => null;

        public virtual string Info()
        {
            var s = $"<b>{def.displayName}</b>  [{StatusText(status)}]";
            if (def.mana > 0f && def.kind != MachineKind.ManaCore) s += $"\n마력 {def.mana:0} MP/s";
            return s;
        }

        public virtual IEnumerable<Item> HeldItems() { yield break; }

        public virtual void Destroy()
        {
            foreach (var it in HeldItems()) it?.Hide();
            if (go) Object.Destroy(go);
        }

        /// <summary>이웃 타일의 설비에 아이템을 밀어 넣는다.</summary>
        protected bool PushTo(Item item, Vector2Int targetCell, int travelDir)
        {
            var b = grid.At(targetCell);
            if (b == null || b == this) return false;
            return b.TryInsert(item, targetCell, travelDir);
        }

        protected void Refresh() { if (label && !string.IsNullOrEmpty(LabelText)) label.text = LabelText; }
        protected virtual string LabelText => def.shortName;
    }

    // ═══════════════════════════════ 물류 ═══════════════════════════════

    /// <summary>태엽 컨베이어 벨트: 타일당 아이템 1개, 무동력 (MAC_01).</summary>
    public class Belt : Building
    {
        public Item item;
        int entryDir;
        float Speed => def.param > 0f ? def.param : 1.5f;
        protected override bool HasOutput => false;

        protected override void BuildView()
        {
            go = new GameObject($"Belt_{origin.x}_{origin.y}");
            go.transform.SetParent(grid.transform, false);
            go.transform.position = center;
            bodySr = Visuals.Sprite("Base", go.transform, Sprites.Rounded, def.color, 5, 0.94f);
            var chev = Visuals.Sprite("Arrow", go.transform, Sprites.Chevron, new Color(1f, 0.8f, 0.35f, 0.55f), 6, 0.5f);
            chev.transform.rotation = Quaternion.Euler(0f, 0f, dir * 90f);
            statusOverlay = null;
            progressBar = null;
        }

        public override void Animate(float dt, ViewMode mode) => ApplyFade(mode);

        public override bool TryInsert(Item it, Vector2Int cell, int travelDir)
        {
            // 액체(원액)는 벨트에 올릴 수 없다 — 배관으로만 이동
            if (item != null || it.IsLiquid || travelDir == Dir.Opp(dir)) return false;
            item = it;
            entryDir = travelDir;
            progress = 0f;
            it.Show(PositionAt(0f), false);
            return true;
        }

        Vector3 PositionAt(float p)
        {
            var entry = center - Dir.V3(entryDir) * 0.5f;
            var exit = center + Dir.V3(dir) * 0.5f;
            return p < 0.5f ? Vector3.Lerp(entry, center, p * 2f) : Vector3.Lerp(center, exit, (p - 0.5f) * 2f);
        }

        public override void Tick(float dt, float power)
        {
            if (item == null) { status = BuildingStatus.Idle; return; }
            progress = Mathf.Min(1f, progress + Speed * dt);
            item.target = PositionAt(progress);
            if (progress >= 1f)
            {
                var it = item;
                item = null;
                if (!PushTo(it, origin + Dir.V[dir], dir))
                {
                    item = it;
                    status = BuildingStatus.Blocked;
                    return;
                }
            }
            status = BuildingStatus.Working;
        }

        public override IEnumerable<Item> HeldItems() { if (item != null) yield return item; }

        public override string Info() => item != null ? $"<b>{def.displayName}</b>\n{item.Describe()}" : $"<b>{def.displayName}</b>\n비어 있음";
    }

    /// <summary>분배기: 들어온 아이템을 앞·오른쪽·왼쪽으로 번갈아 보낸다.</summary>
    public class Splitter : Building
    {
        protected Item held;
        protected int rr;

        public override bool TryInsert(Item it, Vector2Int cell, int travelDir)
        {
            if (held != null || it.IsLiquid || travelDir == Dir.Opp(dir)) return false;
            held = it;
            it.Show(center, false);
            return true;
        }

        protected virtual int[] Outputs(Item it) => new[] { dir, Dir.Right(dir), Dir.Left(dir) };

        public override void Tick(float dt, float power)
        {
            if (held == null) { status = BuildingStatus.Idle; working = false; return; }
            var outs = Outputs(held);
            for (int k = 0; k < outs.Length; k++)
            {
                int d = outs[(rr + k) % outs.Length];
                var it = held;
                held = null;
                if (PushTo(it, origin + Dir.V[d], d))
                {
                    rr = (rr + k + 1) % outs.Length;
                    status = BuildingStatus.Working;
                    return;
                }
                held = it;
            }
            status = BuildingStatus.Blocked;
        }

        public override IEnumerable<Item> HeldItems() { if (held != null) yield return held; }
    }

    /// <summary>스마트 분류기: 조건에 맞는 아이템은 앞으로, 나머지는 좌/우로 (MAC_08).</summary>
    public class Sorter : Splitter
    {
        public static readonly string[] Modes =
        {
            "슬러지", "포션 S등급 이상", "포션 A등급 이상", "불량 포션(C·탁함)", "포션 전체", "분말", "마력 결정",
        };

        bool Match(Item it) => config switch
        {
            0 => it.kind == ItemKind.Sludge,
            1 => it.kind == ItemKind.Potion && it.Grade >= Grade.S && !it.IsDefective,
            2 => it.kind == ItemKind.Potion && it.Grade >= Grade.A && !it.IsDefective,
            3 => it.IsDefective,
            4 => it.kind == ItemKind.Potion,
            5 => it.kind == ItemKind.Powder,
            _ => it.kind == ItemKind.Crystal,
        };

        protected override int[] Outputs(Item it) => Match(it) ? new[] { dir } : new[] { Dir.Left(dir), Dir.Right(dir) };
        protected override string LabelText => "분류";
        public override string ClickHint => "클릭: 분류 조건 변경";

        public override void OnClick()
        {
            config = (config + 1) % Modes.Length;
            Fx.Float(center, Modes[config], new Color(0.7f, 0.9f, 1f));
        }

        public override string Info() => base.Info() + $"\n앞으로: <color=#9fe>{Modes[config]}</color>\n그 외: 좌/우";
    }

    // ═══════════════════════════════ 공급·판매 ═══════════════════════════════

    /// <summary>원재료 투입구: 선택한 재료를 매입가를 내고 일정 주기로 배출한다.</summary>
    public class SupplyCrate : Building
    {
        Item pending;
        public IngredientSO Ingredient => config >= 0 && config < GameManager.Db.ingredients.Count ? GameManager.Db.ingredients[config] : null;

        protected override void OnPlaced() => Refresh();
        protected override string LabelText => Ingredient ? Ingredient.displayName.Split(' ')[^1] : "정지";
        public override string ClickHint => "클릭: 투입 재료 변경";

        public override void OnClick()
        {
            var db = GameManager.Db;
            for (int k = 1; k <= db.ingredients.Count + 1; k++)
            {
                int next = (config + 1 + db.ingredients.Count + 1) % (db.ingredients.Count + 1);
                config = next == db.ingredients.Count ? -1 : next;
                if (config < 0 || Research.IsUnlocked(db.ingredients[config])) break;
            }
            pending?.Hide();
            pending = null;
            Refresh();
            Fx.Float(center, Ingredient ? Ingredient.displayName : "투입 정지", new Color(1f, 0.95f, 0.6f));
        }

        public override void Tick(float dt, float power)
        {
            working = false;
            var ing = Ingredient;
            if (ing == null) { status = BuildingStatus.Off; progress = 0f; return; }
            if (pending != null)
            {
                if (PushTo(pending, outCell, dir)) { pending = null; }
                else { status = BuildingStatus.Blocked; return; }
            }
            int cost = Market.IngredientPrice(ing);
            if (!Economy.CanAfford(cost)) { status = BuildingStatus.NoGold; return; }
            working = true;
            status = BuildingStatus.Working;
            progress += dt / def.processTime;
            if (progress < 1f) return;
            progress = 0f;
            Economy.Spend(cost, LedgerEntry.Ingredients);
            pending = Item.FromIngredient(ing);
            pending.Show(center, true);
            if (PushTo(pending, outCell, dir)) pending = null;
        }

        public override IEnumerable<Item> HeldItems() { if (pending != null) yield return pending; }

        public override string Info()
        {
            var ing = Ingredient;
            if (!ing) return base.Info() + "\n투입 재료 없음";
            return base.Info() + $"\n재료: {ing.displayName} ({Market.IngredientPrice(ing)}G/개, {def.processTime:0.#}초)\n" +
                   $"원소 {ing.elements.Describe()} · 불순물 {ing.impurity:0}%\n<size=22><color=#bbb>{ing.note}</color></size>";
        }
    }

    /// <summary>폐기통: 무엇이든 받아 없앤다. 슬러지는 처리비 5G.</summary>
    public class TrashBin : Building
    {
        protected override bool HasOutput => false;
        int destroyed;

        public override bool TryInsert(Item it, Vector2Int cell, int travelDir)
        {
            if (it.kind == ItemKind.Sludge) Economy.Spend(5, LedgerEntry.Disposal);
            it.Hide();
            destroyed++;
            status = BuildingStatus.Working;
            return true;
        }

        public override void Tick(float dt, float power) { }
        public override string Info() => base.Info() + $"\n폐기한 아이템 {destroyed}개 (슬러지 1개당 처리비 5G)";
    }

    /// <summary>납품 상자: 포션·결정을 상인 연합에 도매가(70%)로 넘긴다.</summary>
    public class ExportCrate : Building
    {
        protected override bool HasOutput => false;
        int sold, revenue;

        public override bool TryInsert(Item it, Vector2Int cell, int travelDir)
        {
            if (it.kind != ItemKind.Potion && it.kind != ItemKind.Crystal) return false;
            int value = it.kind == ItemKind.Crystal ? it.BaseValue : Mathf.Max(1, Mathf.RoundToInt(Market.PotionPrice(it) * 0.7f));
            Economy.Earn(value, LedgerEntry.Bulk);
            Fx.Float(center, $"+{value}G", new Color(1f, 0.85f, 0.3f));
            it.Hide();
            sold++;
            revenue += value;
            status = BuildingStatus.Working;
            return true;
        }

        public override void Tick(float dt, float power) { }
        public override string Info() => base.Info() + $"\n포션(도매 70%)·마력 결정 납품\n누적 {sold}개 / {revenue}G";
    }

    /// <summary>마나 코어: 마력 전력망에 MP를 공급한다.</summary>
    public class ManaCore : Building
    {
        protected override bool HasOutput => false;
        public override float ManaSupply => def.mana;
        public override float ManaDemand => 0f;
        public override void Tick(float dt, float power) { working = grid.ManaUse > 0.01f; status = BuildingStatus.Working; }
        public override string Info() => base.Info() + $"\n공급 {def.mana:0} MP/s · 하루 유지비 {Mathf.RoundToInt(def.mana * GameClock.ManaUpkeepPerMp)}G";
    }

    /// <summary>마도 자동 계산대: 손님이 결제하는 곳.</summary>
    public class Counter : Building
    {
        protected override bool HasOutput => false;
        public Vector3 PayPoint => center + new Vector3(0f, 0.85f, 0f);
        public override void Tick(float dt, float power) { }
        public override string Info() => base.Info() + "\n손님이 이곳에서 결제합니다.";
    }

    // ═══════════════════════════════ 진열대 ═══════════════════════════════

    public class Shelf : Building
    {
        public static readonly float[] Markups = { 0.8f, 1f, 1.2f, 1.5f };
        public readonly List<Item> slots = new();
        public int reserved;
        public int Capacity => Mathf.Max(1, Mathf.RoundToInt(def.param));
        public float Markup => Markups[Mathf.Clamp(config, 0, Markups.Length - 1)];
        protected override bool HasOutput => false;
        public Vector3 StandPoint => center + new Vector3(0f, -0.75f, 0f);

        protected override void OnPlaced()
        {
            if (config < 0 || config >= Markups.Length) config = 1;
            if (label)
            {
                // 진열된 포션과 겹치지 않도록 라벨을 진열대 아래쪽에 작게 표시
                label.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                label.characterSize *= 0.7f;
            }
            Refresh();
        }

        protected override string LabelText => $"{def.shortName} {Markup * 100f:0}%";
        public override string ClickHint => "클릭: 가격 배율 변경";

        public override bool TryInsert(Item it, Vector2Int cell, int travelDir)
        {
            if (it.kind != ItemKind.Potion || slots.Count >= Capacity) return false;
            slots.Add(it);
            Layout();
            return true;
        }

        void Layout()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                float x = Capacity == 1 ? 0f : -0.3f + 0.6f * (i % 2);
                float y = Capacity <= 2 ? 0.1f : 0.25f - 0.32f * (i / 2);
                slots[i].Show(center + new Vector3(x * 0.9f, y - 0.05f, 0f), false);
                if (slots[i].view) slots[i].view.body.sortingOrder = 22;
            }
        }

        public int Find(System.Func<Item, bool> pred)
        {
            for (int i = 0; i < slots.Count; i++) if (pred(slots[i])) return i;
            return -1;
        }

        public Item Take(int index)
        {
            var it = slots[index];
            slots.RemoveAt(index);
            Layout();
            return it;
        }

        public override void OnClick()
        {
            config = (config + 1) % Markups.Length;
            Refresh();
            Fx.Float(center, $"가격 {Markup * 100f:0}%", new Color(1f, 0.9f, 0.5f));
        }

        public override void Tick(float dt, float power) => status = slots.Count > 0 ? BuildingStatus.Working : BuildingStatus.NoInput;
        public override IEnumerable<Item> HeldItems() => slots;

        public override string Info()
        {
            var s = base.Info() + $"\n재고 {slots.Count}/{Capacity} · 가격 배율 {Markup * 100f:0}%";
            if (def.priceBonus > 1f) s += $" · 판매 보너스 +{(def.priceBonus - 1f) * 100f:0}%";
            foreach (var it in slots) s += $"\n  {it.Name} → {Market.ShelfPrice(it, this)}G";
            return s;
        }
    }

    // ═══════════════════════════════ 가공 설비 ═══════════════════════════════

    /// <summary>분쇄기·추출기·여과기·증류탑·합성 가마·병입기·슬라임 배양조 공용 처리기.</summary>
    public class Processor : Building
    {
        const int InCap = 2, OutCap = 2;
        readonly List<Item> input = new();
        readonly List<(Item item, bool side)> output = new();
        int made;

        int Need => def.kind switch
        {
            MachineKind.Cauldron => Mathf.Max(2, Mathf.RoundToInt(def.param)),
            MachineKind.SlimeVat => 2,
            _ => 1,
        };

        bool Accepts(Item it) => def.kind switch
        {
            MachineKind.Crusher => it.kind == ItemKind.Ingredient,
            MachineKind.Extractor => it.kind is ItemKind.Ingredient or ItemKind.Powder,
            MachineKind.Filter or MachineKind.Distiller or MachineKind.Cauldron or MachineKind.Bottler => it.kind == ItemKind.Extract,
            MachineKind.SlimeVat => it.kind == ItemKind.Sludge,
            _ => false,
        };

        public override bool TryInsert(Item it, Vector2Int cell, int travelDir)
        {
            if (input.Count >= Mathf.Max(InCap, Need + 1) || !Accepts(it)) return false;
            if (def.kind == MachineKind.Cauldron && config == 0 && !AddsVariety(it)) return false;
            it.Hide();
            input.Add(it);
            return true;
        }

        /// <summary>
        /// 합성 가마 기본 모드: 같은 주 원소의 원액만으로 가마가 채워지지 않게 한다
        /// (두 배관에서 들어오는 서로 다른 원액을 짝지어 섞기 위함).
        /// </summary>
        bool AddsVariety(Item it)
        {
            if (input.Count < Need - 1) return true;
            int main = it.el.MainIndex;
            foreach (var x in input) if (x.el.MainIndex != main) return true;
            return false;
        }

        public override string ClickHint => def.kind == MachineKind.Cauldron ? "클릭: 혼합 모드 변경" : null;

        public override void OnClick()
        {
            if (def.kind != MachineKind.Cauldron) return;
            config = config == 0 ? 1 : 0;
            Fx.Float(center, config == 0 ? "서로 다른 원액끼리" : "같은 원액도 혼합", new Color(1f, 0.7f, 1f));
        }

        public override void Tick(float dt, float power)
        {
            // 1) 배출 대기열 비우기
            for (int i = 0; i < output.Count;)
            {
                var (it, side) = output[i];
                bool ok = side ? PushTo(it, sideCell, Dir.Left(dir)) : it.IsLiquid ? PushLiquid(it) : PushTo(it, outCell, dir);
                if (ok) output.RemoveAt(i);
                else i++;
            }
            if (output.Count >= OutCap) { Stop(BuildingStatus.Blocked); return; }
            if (input.Count < Need) { Stop(BuildingStatus.NoInput); return; }
            working = true;
            status = power < 0.999f ? BuildingStatus.LowPower : BuildingStatus.Working;
            progress += dt * power / def.processTime;
            if (progress < 1f) return;
            progress = 0f;
            Process();
        }

        /// <summary>원액은 출력 포트의 배관 네트워크로, 배관이 없으면 바로 앞 설비로 직접 넘긴다.</summary>
        bool PushLiquid(Item it)
        {
            var pipe = grid.PipeAt(outCell);
            if (pipe != null) return pipe.network != null && pipe.network.TryInsert(it, this);
            return PushTo(it, outCell, dir);
        }

        public bool ProducesLiquid => def.kind is MachineKind.Extractor or MachineKind.Filter or MachineKind.Distiller or MachineKind.Cauldron;

        void Stop(BuildingStatus s)
        {
            working = false;
            status = s;
        }

        // 기계 안의 아이템은 보이지 않으며, 받는 쪽 벨트가 표시를 시작한다.
        void Emit(Item it, bool side = false) => output.Add((it, side));

        void Process()
        {
            made++;
            switch (def.kind)
            {
                case MachineKind.Crusher:
                    Emit(Alchemy.Crush(Pop()));
                    break;
                case MachineKind.Extractor:
                {
                    var src = Pop();
                    var ex = Alchemy.Extract(src);
                    Emit(ex);
                    if (src.source && Random.value < src.source.extractBonusChance) Emit(ex.Clone());
                    break;
                }
                case MachineKind.Filter:
                    Emit(Alchemy.Filter(Pop()));
                    Emit(Item.Sludge(), true);
                    break;
                case MachineKind.Distiller:
                    Emit(Alchemy.Distill(Pop()));
                    break;
                case MachineKind.Cauldron:
                {
                    var parts = input.GetRange(0, Need);
                    input.RemoveRange(0, Need);
                    var mix = Alchemy.Mix(parts, Alchemy.GreatSuccessChance);
                    if (mix.greatSuccess)
                    {
                        Fx.Float(center, "대성공!", new Color(1f, 0.6f, 1f));
                        Sfx.Play(SfxId.Sparkle);
                    }
                    Emit(mix);
                    break;
                }
                case MachineKind.Bottler:
                {
                    Economy.Spend(GameClock.BottleCost, LedgerEntry.Bottles);
                    var potion = Alchemy.Bottle(Pop());
                    Discoveries.OnBrewed(potion);
                    Emit(potion);
                    break;
                }
                case MachineKind.SlimeVat:
                    input.RemoveRange(0, 2);
                    Emit(Item.Crystal());
                    break;
            }
        }

        Item Pop()
        {
            var it = input[0];
            input.RemoveAt(0);
            return it;
        }

        public override IEnumerable<Item> HeldItems()
        {
            foreach (var it in input) yield return it;
            foreach (var o in output) yield return o.item;
        }

        public override string Info()
        {
            var s = base.Info() + $"\n투입 {input.Count} · 배출 대기 {output.Count} · 처리 {def.processTime:0.#}초 · 누적 {made}";
            if (ProducesLiquid) s += "\n<size=22><color=#8fd0ff>출력 포트(▶)에 배관을 연결하면 원액이 배관으로 나갑니다</color></size>";
            if (def.kind is MachineKind.Filter or MachineKind.Distiller or MachineKind.Cauldron or MachineKind.Bottler)
                s += "\n<size=22><color=#8fd0ff>인접한 배관에서 원액을 받습니다</color></size>";
            if (def.kind == MachineKind.Filter) s += "\n<size=22>왼쪽 측면 포트로 슬러지 배출</size>";
            if (def.kind == MachineKind.Cauldron)
                s += $"\n<size=22>원액 {Need}개를 섞어 원소 합산 · 대성공 {Alchemy.GreatSuccessChance * 100f:0}%\n" +
                     $"혼합 모드: <color=#f0b0ff>{(config == 0 ? "서로 다른 원액끼리 (주 원소가 다른 것)" : "같은 원액도 혼합 (예: 독샘+독샘)")}</color></size>";
            if (input.Count > 0) s += $"\n<size=22><color=#ccc>다음: {input[0].Name} ({input[0].el.Describe()}, 불순물 {input[0].impurity:0}%)</color></size>";
            if (!string.IsNullOrEmpty(def.description)) s += $"\n<size=22><color=#aaa>{def.description}</color></size>";
            return s;
        }
    }
}
