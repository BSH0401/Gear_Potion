using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 공방 전체를 2차원 배열로 관리하는 결정론적 그리드 + TickManager (GDD 6.1).
    /// 아래 4줄은 매장(쇼룸), 그 위 6줄은 자동화 공방이다.
    /// </summary>
    public class FactoryGrid : MonoBehaviour
    {
        public const int MaxWidth = 32;
        public const int ShopRows = 4;
        public const int FactoryRows = 6;
        public const int Height = ShopRows + FactoryRows;
        public const float TickInterval = 0.1f;
        public static readonly int[] ExpansionWidths = { 14, 20, 26, 32 };
        public static readonly int[] ExpansionCosts = { 0, 1200, 3500, 8000 };

        public int Width { get; private set; } = 14;
        public int ExpansionLevel { get; private set; }
        public float ManaSupply { get; private set; }
        public float ManaUse { get; private set; }
        public float PowerFactor { get; private set; } = 1f;
        public bool Heatmap;

        readonly Building[,] cells = new Building[MaxWidth, Height];
        public readonly List<Building> buildings = new();
        float tickTimer;
        Transform floorRoot;
        readonly List<Vector2Int> scratch = new();

        public static Vector3 CellCenter(Vector2Int c) => new(c.x + 0.5f, c.y + 0.5f, 0f);
        public static Vector2Int CellAt(Vector3 world) => new(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;
        public static bool IsShop(Vector2Int c) => c.y < ShopRows;
        public Building At(Vector2Int c) => InBounds(c) ? cells[c.x, c.y] : null;
        public Vector3 Door => new(Width / 2f, -0.6f, 0f);

        public void SetExpansion(int level)
        {
            ExpansionLevel = Mathf.Clamp(level, 0, ExpansionWidths.Length - 1);
            Width = ExpansionWidths[ExpansionLevel];
            BuildFloor();
        }

        void BuildFloor()
        {
            if (floorRoot) Destroy(floorRoot.gameObject);
            floorRoot = new GameObject("Floor").transform;
            floorRoot.SetParent(transform, false);

            // 공방 바닥 (석재) / 매장 바닥 (나무)
            var factory = Visuals.Sprite("FactoryFloor", floorRoot, Sprites.White, new Color(0.16f, 0.15f, 0.19f), 0);
            factory.transform.position = new Vector3(Width / 2f, ShopRows + FactoryRows / 2f, 0f);
            factory.transform.localScale = new Vector3(Width, FactoryRows, 1f);
            var shop = Visuals.Sprite("ShopFloor", floorRoot, Sprites.White, new Color(0.36f, 0.25f, 0.17f), 0);
            shop.transform.position = new Vector3(Width / 2f, ShopRows / 2f, 0f);
            shop.transform.localScale = new Vector3(Width, ShopRows, 1f);

            var grid = new GameObject("GridLines").AddComponent<SpriteRenderer>();
            grid.transform.SetParent(floorRoot, false);
            grid.sprite = Sprites.GridTile;
            grid.drawMode = SpriteDrawMode.Tiled;
            grid.size = new Vector2(Width, Height);
            grid.color = new Color(1f, 1f, 1f, 0.06f);
            grid.sortingOrder = 1;
            if (Visuals.SpriteMaterial) grid.sharedMaterial = Visuals.SpriteMaterial;
            grid.transform.position = new Vector3(Width / 2f, Height / 2f, 0f);

            // 공방과 매장 사이의 벽 (컨베이어는 통과 가능)
            var wall = Visuals.Sprite("Wall", floorRoot, Sprites.White, new Color(0.45f, 0.32f, 0.22f), 2);
            wall.transform.position = new Vector3(Width / 2f, ShopRows, 0f);
            wall.transform.localScale = new Vector3(Width, 0.12f, 1f);
            var outline = Visuals.Sprite("Outline", floorRoot, Sprites.White, new Color(0.08f, 0.07f, 0.1f), -1);
            outline.transform.position = new Vector3(Width / 2f, Height / 2f, 0f);
            outline.transform.localScale = new Vector3(Width + 0.4f, Height + 0.4f, 1f);

            // 출입문
            var door = Visuals.Sprite("Door", floorRoot, Sprites.White, new Color(0.9f, 0.75f, 0.45f), 2);
            door.transform.position = new Vector3(Width / 2f, -0.1f, 0f);
            door.transform.localScale = new Vector3(1.4f, 0.2f, 1f);
            var doorLabel = Visuals.Label("DoorLabel", floorRoot, "입구", new Color(1f, 0.9f, 0.7f), 3, 0.2f);
            doorLabel.transform.position = new Vector3(Width / 2f, -0.4f, 0f);

            var shopTitle = Visuals.Label("ShopTitle", floorRoot, "매장", new Color(1f, 0.9f, 0.75f, 0.25f), 1, 0.5f);
            shopTitle.transform.position = new Vector3(0.9f, ShopRows - 0.45f, 0f);
            var factoryTitle = Visuals.Label("FactoryTitle", floorRoot, "공방", new Color(0.8f, 0.85f, 1f, 0.2f), 1, 0.5f);
            factoryTitle.transform.position = new Vector3(0.9f, Height - 0.45f, 0f);
        }

        public bool CanPlace(MachineSO def, Vector2Int origin, int dir, out string reason)
        {
            Building.Footprint(def, origin, dir, scratch, out _, out _);
            foreach (var c in scratch)
            {
                if (!InBounds(c)) { reason = "부지 밖"; return false; }
                if (cells[c.x, c.y] != null) { reason = "이미 설비가 있음"; return false; }
                if (def.zone == BuildZone.Factory && IsShop(c)) { reason = "공방 구역 전용 설비"; return false; }
                if (def.zone == BuildZone.Shop && !IsShop(c)) { reason = "매장 구역 전용 설비"; return false; }
            }
            reason = null;
            return true;
        }

        public Building Place(MachineSO def, Vector2Int origin, int dir, int config = 0)
        {
            var b = Building.Create(def);
            b.Init(this, def, origin, dir, config);
            foreach (var c in b.cells) cells[c.x, c.y] = b;
            buildings.Add(b);
            return b;
        }

        public void Remove(Building b)
        {
            foreach (var c in b.cells)
                if (InBounds(c) && cells[c.x, c.y] == b) cells[c.x, c.y] = null;
            buildings.Remove(b);
            b.Destroy();
        }

        public void Clear()
        {
            foreach (var b in buildings.ToArray()) Remove(b);
            ItemViews.Clear();
        }

        public T Find<T>() where T : Building
        {
            foreach (var b in buildings) if (b is T t) return t;
            return null;
        }

        public int CountOf(MachineKind kind)
        {
            int n = 0;
            foreach (var b in buildings) if (b.def.kind == kind) n++;
            return n;
        }

        public IEnumerable<T> All<T>() where T : Building
        {
            foreach (var b in buildings) if (b is T t) yield return t;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            tickTimer += dt;
            int guard = 0;
            while (tickTimer >= TickInterval && guard++ < 8)
            {
                tickTimer -= TickInterval;
                Tick(TickInterval);
            }
            if (guard >= 8) tickTimer = 0f; // 초고배속에서 틱이 밀리면 버린다
            ItemViews.Animate(dt);
            for (int i = 0; i < buildings.Count; i++) buildings[i].Animate(dt, Heatmap);
        }

        /// <summary>마력 전력망: 수요가 공급을 넘으면 모든 설비가 비례해서 느려진다.</summary>
        void Tick(float dt)
        {
            float supply = 0f, demand = 0f;
            for (int i = 0; i < buildings.Count; i++)
            {
                supply += buildings[i].ManaSupply;
                demand += buildings[i].ManaDemand;
            }
            ManaSupply = supply;
            ManaUse = demand;
            PowerFactor = demand <= supply || demand <= 0.001f ? 1f : supply / demand;
            for (int i = 0; i < buildings.Count; i++) buildings[i].Tick(dt, PowerFactor);
        }
    }
}
