using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 천장 배관 레이어 (GDD 3.1 Pipe Layer). 바닥 컨베이어와 간섭하지 않고 겹쳐 놓을 수 있다.
    /// 배관 한 칸은 표시·연결만 담당하고, 원액은 연결된 배관 전체가 공유하는 <see cref="PipeNetwork"/>가 운반한다.
    /// </summary>
    public class Pipe : Building
    {
        public PipeNetwork network;
        readonly SpriteRenderer[] arms = new SpriteRenderer[4];
        readonly SpriteRenderer[] fluids = new SpriteRenderer[4];
        SpriteRenderer node, nodeFluid;
        ViewMode lastMode = (ViewMode)(-1);

        static readonly Color Metal = new(0.55f, 0.6f, 0.7f);
        protected override bool HasOutput => false;

        protected override void BuildView()
        {
            go = new GameObject($"Pipe_{origin.x}_{origin.y}");
            go.transform.SetParent(grid.transform, false);
            go.transform.position = center;
            node = Visuals.Sprite("Node", go.transform, Sprites.Circle, Metal, 8, 0.36f);
            nodeFluid = Visuals.Sprite("NodeFluid", go.transform, Sprites.Circle, Color.clear, 9, 0.2f);
            for (int d = 0; d < 4; d++)
            {
                var rot = Quaternion.Euler(0f, 0f, d * 90f);
                arms[d] = Visuals.Sprite("Arm", go.transform, Sprites.White, Metal, 8);
                arms[d].transform.localPosition = Dir.V3(d) * 0.25f;
                arms[d].transform.localRotation = rot;
                arms[d].transform.localScale = new Vector3(0.5f, 0.2f, 1f);
                fluids[d] = Visuals.Sprite("Fluid", go.transform, Sprites.White, Color.clear, 9);
                fluids[d].transform.localPosition = Dir.V3(d) * 0.25f;
                fluids[d].transform.localRotation = rot;
                fluids[d].transform.localScale = new Vector3(0.5f, 0.08f, 1f);
                arms[d].enabled = fluids[d].enabled = false;
            }
        }

        /// <summary>이웃 배관, 그리고 원액을 주고받을 수 있는 가공 설비 쪽으로 팔을 뻗는다.</summary>
        public void RefreshConnections()
        {
            for (int d = 0; d < 4; d++)
            {
                var c = origin + Dir.V[d];
                bool on = grid.PipeAt(c) != null || grid.At(c) is Processor;
                arms[d].enabled = fluids[d].enabled = on;
            }
        }

        public override void Tick(float dt, float power) =>
            status = network != null && network.Count > 0 ? BuildingStatus.Working : BuildingStatus.Idle;

        public override void Animate(float dt, ViewMode mode)
        {
            if (mode != lastMode)
            {
                lastMode = mode;
                // 배관 뷰에서는 다른 레이어 위로 올라와 선명하게
                bool focus = mode == ViewMode.Pipes;
                node.sortingOrder = focus ? 34 : 8;
                nodeFluid.sortingOrder = focus ? 35 : 9;
                for (int d = 0; d < 4; d++)
                {
                    arms[d].sortingOrder = focus ? 34 : 8;
                    fluids[d].sortingOrder = focus ? 35 : 9;
                }
                var metal = focus ? new Color(0.75f, 0.82f, 0.95f) : Metal;
                node.color = metal;
                foreach (var a in arms) a.color = metal;
            }
            // 원액이 흐르는 배관은 원액 색으로 맥동 (흐름 방향감은 칸 위치로 위상차)
            Color c;
            if (network != null && network.Count > 0)
            {
                c = network.FluidColor;
                c.a = 0.55f + 0.4f * Mathf.Sin(Time.time * 6f - (origin.x + origin.y) * 0.9f);
            }
            else c = new Color(0.15f, 0.17f, 0.22f, 0.9f);
            nodeFluid.color = c;
            for (int d = 0; d < 4; d++) fluids[d].color = c;
        }

        public override string Info()
        {
            if (network == null) return $"<b>{def.displayName}</b>";
            var s = $"<b>{def.displayName}</b>  [배관 {network.pipes.Count}칸 네트워크]\n원액 {network.Count}/{network.Capacity}";
            s += network.consumers.Count == 0
                ? "\n<color=#ff9a8a>원액을 받을 설비가 연결되지 않음</color>"
                : $"\n공급 대상: {string.Join(", ", network.ConsumerNames())}";
            var next = network.Peek();
            if (next != null) s += $"\n<size=21><color=#ccc>다음: {next.Name} ({next.el.Describe()}, 불순물 {next.impurity:0}%)</color></size>";
            s += "\n<size=21><color=#aaa>설비의 출력 포트(▶) 칸에 배관을 놓으면 원액이 들어오고,\n배관에 닿은 여과기·증류탑·가마·병입기로 자동 공급됩니다.</color></size>";
            return s;
        }
    }

    /// <summary>
    /// 연결된 배관 묶음 하나. 원액 패킷을 FIFO로 보관하다가 이동 시간이 지나면
    /// 인접한 소비 설비에 라운드로빈으로 공급한다. 원액을 넣은 설비에는 되돌려 보내지 않는다.
    /// </summary>
    public class PipeNetwork
    {
        struct Packet
        {
            public Item item;
            public Building source;
            public float age;
        }

        public readonly List<Pipe> pipes = new();
        public readonly List<(Building building, Vector2Int cell, int dir)> consumers = new();
        readonly List<Packet> packets = new();
        readonly FactoryGrid grid;
        int rr;
        public Color FluidColor = new(0.4f, 0.6f, 1f);

        public PipeNetwork(FactoryGrid grid) => this.grid = grid;

        public int Count => packets.Count;
        /// <summary>배관이 길수록 많이 담는다 (2칸당 1 + 기본 2).</summary>
        public int Capacity => 2 + pipes.Count / 2;
        /// <summary>배관 길이에 비례한 이동 시간.</summary>
        float TransitTime => 0.2f + 0.05f * pipes.Count;

        public Item Peek() => packets.Count > 0 ? packets[0].item : null;

        public bool TryInsert(Item item, Building source)
        {
            if (packets.Count >= Capacity) return false;
            item.Hide();
            packets.Add(new Packet { item = item, source = source });
            FluidColor = item.Color;
            return true;
        }

        public void Absorb(PipeNetwork other)
        {
            foreach (var p in other.packets) packets.Add(p);
            if (other.packets.Count > 0) FluidColor = other.FluidColor;
        }

        public void FindConsumers()
        {
            consumers.Clear();
            foreach (var p in pipes)
            {
                for (int d = 0; d < 4; d++)
                {
                    var cell = p.origin + Dir.V[d];
                    if (!(grid.At(cell) is Processor b)) continue;
                    if (b.outCell == p.origin) continue; // 이 배관은 b의 출력 포트 → 입력으로 쓰지 않는다
                    bool dup = false;
                    foreach (var c in consumers) if (c.building == b) { dup = true; break; }
                    if (!dup) consumers.Add((b, cell, d));
                }
            }
        }

        public IEnumerable<string> ConsumerNames()
        {
            foreach (var c in consumers) yield return c.building.def.shortName;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < packets.Count; i++)
            {
                var p = packets[i];
                p.age += dt;
                packets[i] = p;
            }
            if (consumers.Count == 0) return;
            // 한 틱에 최대 2개까지, 이동 시간이 지난 원액부터 공급
            int delivered = 0;
            for (int i = 0; i < packets.Count && delivered < 2;)
            {
                var p = packets[i];
                if (p.age < TransitTime) break;
                if (Deliver(p)) { packets.RemoveAt(i); delivered++; }
                else i++;
            }
        }

        bool Deliver(Packet p)
        {
            for (int k = 0; k < consumers.Count; k++)
            {
                var (b, cell, d) = consumers[(rr + k) % consumers.Count];
                if (b == p.source) continue;
                if (!b.TryInsert(p.item, cell, d)) continue;
                rr = (rr + k + 1) % consumers.Count;
                return true;
            }
            return false;
        }
    }
}
