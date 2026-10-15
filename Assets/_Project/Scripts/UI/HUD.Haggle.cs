using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GearPotion
{
    /// <summary>VIP 흥정 요청 창구: 한 번에 한 손님만 흥정한다.</summary>
    public static class Haggle
    {
        public const float SuccessBonus = 1.25f;
        public const float FailDiscount = 0.9f;
        public const float TimeLimit = 6f;
        public static Customer Current;
        public static bool Auto; // 개발용: 초록 구간에서 자동 성공
        public static int Wins, Losses;

        public static bool Request(Customer c)
        {
            if (Current != null) return false;
            Current = c;
            return true;
        }
    }

    /// <summary>
    /// VIP 쇼케이스 흥정 미니게임 (GDD 3.3): 바늘이 초록 구간에 있을 때 [흥정!](H)을 누르면 +25%,
    /// 빗나가면 손님이 언짢아해 -10%·별 하나 감점. 손대지 않으면 정가로 결제.
    /// </summary>
    public partial class HUD
    {
        RectTransform hagglePanel, haggleZone, haggleNeedle;
        Text haggleText;
        Customer haggleCustomer;
        float haggleStart, zoneMin, zoneMax;
        const float BarWidth = 440f;

        void BuildHaggle(Transform parent)
        {
            hagglePanel = UIFactory.Rect("Haggle", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(520f, 150f));
            UIFactory.PanelBg(hagglePanel, new Color(0.18f, 0.12f, 0.06f, 0.96f));
            haggleText = UIFactory.Label(UIFactory.Rect("Text", hagglePanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(500f, 34f)),
                "", 21, UIFactory.Accent);
            var bar = UIFactory.Rect("Bar", hagglePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(BarWidth, 26f));
            UIFactory.Image(bar, new Color(0.35f, 0.15f, 0.12f));
            haggleZone = UIFactory.Rect("Zone", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(80f, 26f));
            UIFactory.Image(haggleZone, new Color(0.35f, 0.85f, 0.4f));
            haggleNeedle = UIFactory.Rect("Needle", bar, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 40f));
            UIFactory.Image(haggleNeedle, Color.white);
            UIFactory.Button(hagglePanel, "흥정! (H)", new Vector2(0.5f, 0f), new Vector2(-90f, 30f), new Vector2(170f, 42f), () => ResolveHaggle(false), 22);
            UIFactory.Button(hagglePanel, "정가로 팔기", new Vector2(0.5f, 0f), new Vector2(100f, 30f), new Vector2(170f, 42f), () => FinishHaggle(1f, 0, "정가 결제"), 20);
            hagglePanel.gameObject.SetActive(false);
        }

        float NeedlePos => Mathf.PingPong((Time.unscaledTime - haggleStart) * 0.9f, 1f);

        void UpdateHaggle()
        {
            var c = Haggle.Current;
            if (c == null || c.Done || !c.Haggling)
            {
                Haggle.Current = null;
                haggleCustomer = null;
                hagglePanel.gameObject.SetActive(false);
                return;
            }
            if (haggleCustomer != c)
            {
                // 새 흥정: 초록 구간 위치를 무작위로
                haggleCustomer = c;
                haggleStart = Time.unscaledTime;
                zoneMin = Random.Range(0.15f, 0.7f);
                zoneMax = zoneMin + 0.18f;
                haggleZone.anchoredPosition = new Vector2(zoneMin * BarWidth, 0f);
                haggleZone.sizeDelta = new Vector2((zoneMax - zoneMin) * BarWidth, 26f);
                hagglePanel.gameObject.SetActive(true);
                Sfx.Play(SfxId.Bell, 0.5f);
            }
            float left = Haggle.TimeLimit - (Time.unscaledTime - haggleStart);
            haggleText.text = $"<b>{c.profile.displayName}</b> 손님과 VIP 흥정 — 초록 구간에서 누르면 +25%  <color=#ccc>({Mathf.Max(0f, left):0.0}초)</color>";
            float t = NeedlePos;
            haggleNeedle.anchoredPosition = new Vector2(t * BarWidth, 0f);
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) ResolveHaggle(false);
            else if (Haggle.Auto && t > zoneMin + 0.02f && t < zoneMax - 0.02f) ResolveHaggle(true);
            else if (left <= 0f) FinishHaggle(1f, 0, "정가 결제");
        }

        void ResolveHaggle(bool auto)
        {
            if (haggleCustomer == null) return;
            float t = NeedlePos;
            bool hit = t >= zoneMin && t <= zoneMax;
            if (hit)
            {
                Haggle.Wins++;
                Sfx.Play(SfxId.Coin);
                FinishHaggle(Haggle.SuccessBonus, 0, "흥정 성공! +25%");
            }
            else
            {
                Haggle.Losses++;
                Sfx.Play(SfxId.Error);
                FinishHaggle(Haggle.FailDiscount, -1, "흥정 실패… -10%");
            }
        }

        void FinishHaggle(float mult, int starDelta, string text)
        {
            var c = haggleCustomer;
            Haggle.Current = null;
            haggleCustomer = null;
            hagglePanel.gameObject.SetActive(false);
            if (c != null && !c.Done) c.HaggleResult(mult, starDelta, text);
        }
    }
}
