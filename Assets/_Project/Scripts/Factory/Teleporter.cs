using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 에테르 텔레포트 패드 (GDD 4.2 MAC_10): 복잡한 배선 없이 같은 채널의 다른 패드로 아이템을 즉시 전송한다.
    /// 뒤·옆에서 들어온 아이템을 같은 채널 패드들에 번갈아 보내고, 받은 패드는 자기 앞(▶)으로 배출한다.
    /// 전송할 때만 마력을 크게 쓴다.
    /// </summary>
    public class Teleporter : Building
    {
        public const int Channels = 4;
        public static readonly Color[] ChannelColors =
        {
            new(0.4f, 0.95f, 1f), new(1f, 0.45f, 0.95f), new(1f, 0.9f, 0.35f), new(0.5f, 1f, 0.5f),
        };
        public static int Transfers;

        float activeTimer;
        int rr, sent, received;
        SpriteRenderer ring, glow;
        UnityEngine.Rendering.Universal.Light2D light;

        Color ChannelColor => ChannelColors[Mathf.Clamp(config, 0, Channels - 1)];
        protected override string LabelText => $"텔레 {config + 1}";
        public override string ClickHint => "클릭: 채널 변경 (같은 채널끼리 연결)";

        protected override void BuildView()
        {
            base.BuildView();
            glow = Visuals.Sprite("PadGlow", go.transform, Sprites.Glow, Color.clear, 9, 1.6f, true);
            ring = Visuals.Sprite("PadRing", go.transform, Sprites.Ring, Color.white, 12, 0.7f, true);
            light = Lights.Point("PadLight", go.transform, center, Color.white, 0.4f, 1.8f);
        }

        protected override void OnPlaced()
        {
            config = Mathf.Clamp(config, 0, Channels - 1);
            Refresh();
        }

        public override void OnClick()
        {
            config = (config + 1) % Channels;
            Refresh();
            Fx.Float(center, $"채널 {config + 1}", ChannelColor);
        }

        public override bool TryInsert(Item it, Vector2Int cell, int travelDir)
        {
            if (travelDir == Dir.Opp(dir)) return false;  // 앞(출구)으로는 받지 않음
            if (grid.PowerFactor < 0.5f) return false;     // 마력이 크게 모자라면 정지
            int count = 0;
            foreach (var t in grid.All<Teleporter>()) if (t != this && t.config == config) count++;
            if (count == 0) return false;
            for (int k = 0; k < count; k++)
            {
                int i = 0;
                foreach (var t in grid.All<Teleporter>())
                {
                    if (t == this || t.config != config) continue;
                    if (i++ != (rr + k) % count) continue;
                    if (!t.Emit(it)) break;
                    rr = (rr + k + 1) % count;
                    sent++;
                    activeTimer = 1f;
                    Transfers++;
                    return true;
                }
            }
            return false;
        }

        bool Emit(Item it)
        {
            if (!PushTo(it, outCell, dir)) return false;
            received++;
            activeTimer = 1f;
            return true;
        }

        public override void Tick(float dt, float power)
        {
            activeTimer = Mathf.Max(0f, activeTimer - dt);
            working = activeTimer > 0f;
            status = working ? BuildingStatus.Working : BuildingStatus.Idle;
        }

        public override void Animate(float dt, ViewMode mode)
        {
            base.Animate(dt, mode);
            if (!ring) return;
            var c = ChannelColor;
            ring.color = c;
            ring.transform.Rotate(0f, 0f, dt * (working ? 360f : 60f));
            float pulse = working ? 0.55f + 0.25f * Mathf.Sin(Time.time * 14f) : 0.18f + 0.08f * Mathf.Sin(Time.time * 2f);
            glow.color = new Color(c.r, c.g, c.b, pulse);
            light.color = c;
            light.intensity = working ? 1.1f : 0.35f;
        }

        public override string Info()
        {
            int peers = 0;
            foreach (var t in grid.All<Teleporter>()) if (t != this && t.config == config) peers++;
            var col = ColorUtility.ToHtmlStringRGB(ChannelColor);
            return base.Info() + $"\n<color=#{col}>채널 {config + 1}</color> · 같은 채널 패드 {peers}개" +
                   (peers == 0 ? " <color=#ff8877>(연결된 패드 없음)</color>" : "") +
                   $"\n보냄 {sent} · 받음 {received}" +
                   "\n<size=21><color=#aaa>뒤·옆에서 들어온 아이템을 같은 채널 패드로 즉시 보내고, 받은 패드는 앞(▶)으로 내보냅니다.</color></size>";
        }
    }
}
