using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 오버클럭 태엽과 과열·냉각 (GDD 3.1 주요 물류 제어 기믹).
    /// 가동 중인 설비는 열이 쌓이고, 기본 방열 + 인접한 얼음 정령석 + 냉각수 배관이 열을 식힌다.
    /// 오버클럭 태엽을 달면 처리 속도 150%, 발열 2배. 열이 100에 닿으면 화재가 나 잠시 멈추고
    /// 안에 있던 재료를 잃으며, 이웃 설비에도 열이 번진다.
    /// </summary>
    public partial class Processor
    {
        public const float PassiveCooling = 5f;
        public const float IceCooling = 6f;
        public const float CoolantCooling = 5f;
        public const float FireThreshold = 100f;
        public const float WarnThreshold = 70f;
        public const float FireDuration = 6f;
        public const float FireSpreadHeat = 25f;
        public const float OverclockSpeed = 1.5f;
        public const float OverclockHeat = 2f;
        public const float FireElementHeat = 1f;

        public static int FiresStarted;
        static readonly List<Building> scratchCoolers = new();
        static readonly List<PipeNetwork> scratchNetworks = new();

        public bool overclocked;
        public float heat;
        float fireTimer;
        int iceCount, coolantCount;

        SpriteRenderer heatBar, heatBarBg, fireGlow, gearIcon;
        float heatBarHeight;

        public bool OnFire => fireTimer > 0f;
        float SpeedMultiplier => overclocked ? OverclockSpeed : 1f;

        float BaseHeat => def.kind switch
        {
            MachineKind.Crusher => 3f,
            MachineKind.Extractor => 4f,
            MachineKind.Filter => 3f,
            MachineKind.Distiller => 5f,
            MachineKind.Cauldron => 5f,
            MachineKind.Bottler => 2f,
            MachineKind.SlimeVat => 2f,
            _ => 0f,
        };

        /// <summary>화 원소가 주성분인 재료(화염 뿌리·독샘)를 가공하면 추가로 발열한다.</summary>
        bool FieryInput => input.Count > 0 && input[0].el.MainIndex == (int)Element.Ignis;

        public float HeatGeneration => working ? (BaseHeat + (FieryInput ? FireElementHeat : 0f)) * (overclocked ? OverclockHeat : 1f) : 0f;
        public float Cooling => PassiveCooling + iceCount * IceCooling + coolantCount * CoolantCooling;

        void UpdateHeat(float dt)
        {
            CountCoolers();
            heat = Mathf.Clamp(heat + (HeatGeneration - Cooling) * dt, 0f, 120f);
            if (heat >= FireThreshold) StartFire();
        }

        /// <summary>설비 둘레에 붙은 얼음 정령석(각각)과 냉각수(물 원소 원액)가 흐르는 배관 네트워크(각각)를 센다.</summary>
        void CountCoolers()
        {
            scratchCoolers.Clear();
            scratchNetworks.Clear();
            foreach (var c in cells)
            {
                for (int d = 0; d < 4; d++)
                {
                    var n = c + Dir.V[d];
                    if (cells.Contains(n)) continue;
                    if (grid.At(n) is Cooler cool && !scratchCoolers.Contains(cool)) scratchCoolers.Add(cool);
                    var pipe = grid.PipeAt(n);
                    if (pipe?.network != null && pipe.network.IsCoolant && !scratchNetworks.Contains(pipe.network)) scratchNetworks.Add(pipe.network);
                }
            }
            iceCount = scratchCoolers.Count;
            coolantCount = scratchNetworks.Count;
        }

        bool TickFire(float dt)
        {
            if (fireTimer <= 0f) return false;
            fireTimer -= dt;
            working = false;
            progress = 0f;
            status = BuildingStatus.OnFire;
            if (fireTimer <= 0f) heat = 40f;
            return true;
        }

        void StartFire()
        {
            fireTimer = FireDuration;
            FiresStarted++;
            int lost = input.Count + output.Count;
            foreach (var it in input) it.Hide();
            foreach (var o in output) o.item.Hide();
            input.Clear();
            output.Clear();
            Fx.Float(center, "화재 발생!", new Color(1f, 0.45f, 0.2f), 1.8f);
            Toasts.Show($"{def.displayName} 과열 화재! {(lost > 0 ? $"재료 {lost}개 소실 · " : "")}냉각 설비를 붙이세요", new Color(1f, 0.5f, 0.3f));
            Sfx.Play(SfxId.Fire);
            // 이웃 가공 설비로 열이 번진다
            foreach (var c in cells)
                for (int d = 0; d < 4; d++)
                    if (grid.At(c + Dir.V[d]) is Processor p && p != this && !p.OnFire)
                        p.heat = Mathf.Min(p.heat + FireSpreadHeat / Mathf.Max(1, cells.Count), 119f);
        }

        public bool SetOverclock(bool on)
        {
            if (overclocked == on) return false;
            overclocked = on;
            if (gearIcon) gearIcon.enabled = on;
            return true;
        }

        // ───────────── 표시 ─────────────

        protected override void BuildView()
        {
            base.BuildView();
            var size = new Vector2(Mathf.Abs(Dir.Rotate(def.size, dir).x), Mathf.Abs(Dir.Rotate(def.size, dir).y));
            heatBarHeight = size.y - 0.3f;
            var x = size.x / 2f - 0.13f;
            heatBarBg = Visuals.Sprite("HeatBg", go.transform, Sprites.White, new Color(0f, 0f, 0f, 0.45f), 12, 1f, true);
            heatBarBg.transform.localPosition = new Vector3(x, 0f, 0f);
            heatBarBg.transform.localScale = new Vector3(0.07f, heatBarHeight, 1f);
            heatBar = Visuals.Sprite("Heat", go.transform, Sprites.White, Color.red, 13, 1f, true);
            heatBar.transform.localPosition = new Vector3(x, -heatBarHeight / 2f, 0f);
            fireGlow = Visuals.Sprite("Fire", go.transform, Sprites.Glow, new Color(1f, 0.45f, 0.1f, 0f), 15, Mathf.Max(size.x, size.y) * 1.6f, true);
            fireGlow.enabled = false;
            gearIcon = Visuals.Sprite("Overclock", go.transform, Sprites.Gear, new Color(1f, 0.8f, 0.3f), 14, 0.36f, true);
            gearIcon.transform.localPosition = new Vector3(-size.x / 2f + 0.24f, size.y / 2f - 0.24f, 0f);
            gearIcon.enabled = overclocked;
            machineLight = Lights.Point("MachineLight", go.transform, center, WorkColor, 0f, Mathf.Max(size.x, size.y) * 1.4f + 0.6f);
        }

        UnityEngine.Rendering.Universal.Light2D machineLight;
        float lightLevel;

        Color WorkColor => def.kind switch
        {
            MachineKind.Cauldron => new Color(1f, 0.55f, 0.9f),
            MachineKind.Distiller => new Color(0.7f, 0.6f, 1f),
            MachineKind.Extractor => new Color(0.5f, 1f, 0.6f),
            MachineKind.Filter => new Color(0.45f, 0.7f, 1f),
            MachineKind.Bottler => new Color(0.5f, 0.95f, 1f),
            MachineKind.SlimeVat => new Color(0.55f, 1f, 0.4f),
            _ => new Color(1f, 0.8f, 0.55f),
        };

        float WorkGlow => def.kind switch
        {
            MachineKind.Cauldron => 1.0f,
            MachineKind.Distiller or MachineKind.SlimeVat => 0.6f,
            MachineKind.Crusher => 0.15f,
            _ => 0.4f,
        };

        void AnimateLight(float dt)
        {
            if (!machineLight) return;
            float target = working ? WorkGlow : 0f;
            var color = WorkColor;
            float h = Mathf.InverseLerp(50f, FireThreshold, heat);
            if (h > 0f) { color = Color.Lerp(color, new Color(1f, 0.35f, 0.15f), h); target = Mathf.Max(target, 0.9f * h); }
            if (OnFire)
            {
                color = new Color(1f, 0.5f, 0.15f);
                target = 1.4f + 0.6f * Mathf.PerlinNoise(Time.time * 9f, origin.y * 2.3f);
            }
            else if (def.kind == MachineKind.Cauldron && working) target *= 0.85f + 0.15f * Mathf.Sin(Time.time * 5f);
            lightLevel = Mathf.MoveTowards(lightLevel, target, dt * 3f);
            machineLight.intensity = lightLevel;
            machineLight.color = color;
        }

        public override void Animate(float dt, ViewMode mode)
        {
            base.Animate(dt, mode);
            if (!heatBar) return;
            float h = Mathf.Clamp01(heat / FireThreshold);
            bool showHeat = heat > 3f || OnFire;
            heatBar.enabled = heatBarBg.enabled = showHeat;
            if (showHeat)
            {
                heatBar.transform.localScale = new Vector3(0.07f, heatBarHeight * h, 1f);
                heatBar.transform.localPosition = new Vector3(heatBar.transform.localPosition.x, -heatBarHeight / 2f + heatBarHeight * h / 2f, 0f);
                heatBar.color = Color.Lerp(new Color(0.4f, 0.9f, 0.4f), new Color(1f, 0.2f, 0.1f), h);
            }
            // 열이 높으면 본체가 붉게 달아오른다
            var baseColor = def.color;
            float glow = heat > 50f ? (heat - 50f) / 50f * 0.6f : 0f;
            if (OnFire) glow = 0.75f;
            var c = Color.Lerp(baseColor, new Color(1f, 0.25f, 0.1f), Mathf.Clamp01(glow));
            c.a = bodySr.color.a;
            bodySr.color = c;
            fireGlow.enabled = OnFire || heat > WarnThreshold;
            if (fireGlow.enabled)
            {
                float flicker = 0.5f + 0.5f * Mathf.PerlinNoise(Time.time * 8f, origin.x * 3.1f);
                fireGlow.color = new Color(1f, 0.45f, 0.1f, OnFire ? 0.55f + 0.35f * flicker : 0.15f * flicker);
            }
            if (gearIcon && overclocked && working) gearIcon.transform.Rotate(0f, 0f, -dt * 240f);
            AnimateLight(dt);
        }

        string HeatInfo()
        {
            var s = $"\n열 <color={(heat > WarnThreshold ? "#ff7a5a" : "#ffd27a")}>{heat:0}/100</color>  발열 {HeatGeneration:0.#}/s · 냉각 {Cooling:0.#}/s";
            if (iceCount > 0 || coolantCount > 0) s += $"\n<size=21>냉각: 얼음 정령석 {iceCount} · 냉각수 배관 {coolantCount}</size>";
            if (overclocked) s += "\n<color=#ffd27a>⚙ 오버클럭: 속도 150% · 발열 2배</color>";
            if (OnFire) s += $"\n<color=#ff6a3a>화재 진압 중… {fireTimer:0.0}초</color>";
            return s;
        }
    }

    /// <summary>얼음 정령석: 맞닿은 가공 설비를 초당 6씩 식힌다.</summary>
    public class Cooler : Building
    {
        protected override bool HasOutput => false;

        protected override void BuildView()
        {
            base.BuildView();
            var glow = Visuals.Sprite("Frost", go.transform, Sprites.Glow, new Color(0.5f, 0.9f, 1f, 0.35f), 9, 1.8f, true);
            Lights.Point("FrostLight", go.transform, center, new Color(0.55f, 0.9f, 1f), 0.6f, 2.2f);
            glow.transform.localPosition = Vector3.zero;
        }

        public override void Tick(float dt, float power) => status = BuildingStatus.Working;

        public override string Info() =>
            base.Info() + $"\n맞닿은 가공 설비의 열을 초당 {Processor.IceCooling:0} 식힙니다.\n<size=21><color=#aaa>설비 둘레 어디에 붙여도 되며, 여러 개를 붙이면 효과가 더해집니다.</color></size>";
    }
}
