using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GearPotion
{
    /// <summary>
    /// 메인 HUD (GDD 5.1): 상단 정보 바 · 하단 빌드 독 · 정보 패널 · 목표 · 토스트.
    /// 모달 패널(타이틀/정산/연구/도움말)은 HUD.Panels.cs.
    /// </summary>
    public partial class HUD : MonoBehaviour
    {
        public static bool PointerOverUI { get; private set; }

        Canvas canvas;
        RectTransform root;
        Text clockText, newsText, statsText, infoText, goalsText, modeText;
        RectTransform infoPanel;
        readonly Button[] speedButtons = new Button[4];
        RectTransform dockItems;
        readonly List<(Button button, MachineSO def)> dockButtons = new();
        readonly Button[] tabButtons = new Button[4];
        Button removeButton, researchButton;
        BuildCategory currentTab = BuildCategory.Logistics;
        GameObject hudGroup;

        static readonly string[] TabNames = { "1 물류", "2 가공·연성", "3 상점", "4 마력" };

        void Awake()
        {
            UIFactory.EnsureEventSystem();
            canvas = UIFactory.CreateCanvas("HUDCanvas", 10);
            canvas.transform.SetParent(transform, false);
            root = (RectTransform)canvas.transform;
            hudGroup = UIFactory.Stretch("HudGroup", root).gameObject;
            BuildTopBar(hudGroup.transform);
            BuildDock(hudGroup.transform);
            BuildInfo(hudGroup.transform);
            BuildGoals(hudGroup.transform);
            BuildToasts(root);
            Research.Changed += Rebuild;
        }

        void OnDestroy() => Research.Changed -= Rebuild;

        // ═══════════════ 상단 바 ═══════════════

        void BuildTopBar(Transform parent)
        {
            var bar = UIFactory.Rect("TopBar", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1920f, 70f));
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.sizeDelta = new Vector2(0f, 70f);
            UIFactory.PanelBg(bar);

            clockText = UIFactory.Label(UIFactory.Rect("Clock", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(380f, 60f)),
                "", 26, UIFactory.TextColor, TextAnchor.MiddleLeft);
            string[] labels = { "||", "1x", "2x", "3x" };
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                speedButtons[i] = UIFactory.Button(bar, labels[i], new Vector2(0f, 0.5f), new Vector2(430f + i * 62f, 0f), new Vector2(56f, 48f),
                    () => { if (k == 0) GameManager.Instance.TogglePause(); else GameManager.Instance.SetSpeed(k); }, 24);
            }
            newsText = UIFactory.Label(UIFactory.Rect("News", bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(760f, 60f)),
                "", 23, UIFactory.Accent);
            statsText = UIFactory.Label(UIFactory.Rect("Stats", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(560f, 60f)),
                "", 25, UIFactory.TextColor, TextAnchor.MiddleRight);
        }

        // ═══════════════ 하단 빌드 독 ═══════════════

        void BuildDock(Transform parent)
        {
            var dock = UIFactory.Rect("Dock", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 175f));
            dock.anchorMin = new Vector2(0f, 0f);
            dock.anchorMax = new Vector2(1f, 0f);
            UIFactory.PanelBg(dock);

            for (int i = 0; i < 4; i++)
            {
                var cat = (BuildCategory)i;
                tabButtons[i] = UIFactory.Button(dock, TabNames[i], new Vector2(0f, 1f), new Vector2(105f + i * 190f, -28f), new Vector2(180f, 42f),
                    () => { currentTab = cat; Rebuild(); }, 22);
            }
            removeButton = UIFactory.Button(dock, "X 철거", new Vector2(0f, 1f), new Vector2(105f + 4 * 190f, -28f), new Vector2(180f, 42f),
                () => { GameManager.Instance.Builder.ToggleRemoveMode(); }, 22);
            researchButton = UIFactory.Button(dock, "연구", new Vector2(1f, 1f), new Vector2(-310f, -28f), new Vector2(190f, 42f), ShowResearch, 22);
            UIFactory.Button(dock, "도움말 F1", new Vector2(1f, 1f), new Vector2(-110f, -28f), new Vector2(190f, 42f), ShowHelp, 22);
            modeText = UIFactory.Label(UIFactory.Rect("Mode", dock, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(975f, -28f), new Vector2(520f, 40f)),
                "", 19, UIFactory.Dim, TextAnchor.MiddleLeft);

            dockItems = UIFactory.Rect("Items", dock, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(15f, 12f), new Vector2(1890f, 100f));
        }

        /// <summary>현재 탭의 설비 버튼 목록을 다시 만든다 (연구 해금 시 호출).</summary>
        public void Rebuild()
        {
            if (!dockItems) return;
            foreach (Transform c in dockItems) Destroy(c.gameObject);
            dockButtons.Clear();
            if (GameManager.Db == null) return;
            float x = 0f;
            foreach (var def in GameManager.Db.machines)
            {
                if (def.category != currentTab || !def.removable) continue;
                bool unlocked = Research.IsUnlocked(def);
                var d = def;
                string size = def.size == Vector2Int.one ? "" : $" · {def.size.x}x{def.size.y}";
                string text = unlocked ? $"{def.displayName}\n<color=#ffd27a>{def.cost}G</color><size=17>{size}</size>"
                                       : $"<color=#888>{def.displayName}\n연구 {def.researchCost} RP</color>";
                var b = UIFactory.Button(dockItems, text, new Vector2(0f, 0.5f), new Vector2(x + 105f, 0f), new Vector2(200f, 92f),
                    () => { if (Research.IsUnlocked(d)) GameManager.Instance.Builder.Select(d); else ShowResearch(); }, 21);
                var t = UIFactory.ButtonText(b);
                t.lineSpacing = 0.95f;
                dockButtons.Add((b, def));
                x += 210f;
            }
        }

        // ═══════════════ 정보 패널 · 목표 ═══════════════

        void BuildInfo(Transform parent)
        {
            infoPanel = UIFactory.Rect("Info", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 190f), new Vector2(480f, 200f));
            UIFactory.Image(infoPanel, UIFactory.Panel);
            infoText = UIFactory.Label(UIFactory.Rect("Text", infoPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(452f, 180f)),
                "", 22, UIFactory.TextColor, TextAnchor.UpperLeft, true);
        }

        RectTransform goalsPanel;

        void BuildGoals(Transform parent)
        {
            var panel = goalsPanel = UIFactory.Rect("Goals", parent, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 190f), new Vector2(430f, 120f));
            UIFactory.Image(panel, new Color(0.1f, 0.08f, 0.12f, 0.75f));
            goalsText = UIFactory.Label(UIFactory.Rect("Text", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(410f, 110f)),
                "", 20, UIFactory.TextColor, TextAnchor.UpperLeft, true);
        }

        // ═══════════════ 매 프레임 갱신 ═══════════════

        void Update()
        {
            PointerOverUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            var gm = GameManager.Instance;
            if (!gm) return;
            HandleHotkeys(gm);
            UpdateToasts();
            hudGroup.SetActive(gm.InGame);
            if (!gm.InGame) return;

            var clock = gm.Clock;
            clockText.text = $"<b>Day {clock.Day}</b>  {clock.TimeText}  <size=21><color=#cbb>{GameClock.PhaseName(clock.Phase)}</color></size>";
            for (int i = 0; i < 4; i++)
            {
                bool on = i == 0 ? gm.Paused : !gm.Paused && Mathf.Approximately(gm.Speed, i);
                speedButtons[i].image.color = on ? new Color(1f, 0.75f, 0.35f) : Color.white;
            }
            newsText.text = Market.News;
            var grid = GameManager.Grid;
            string mpColor = grid.PowerFactor < 0.999f ? "#ff7766" : "#9fd8ff";
            string sus = Market.Suspicion > 0.5f ? $"  <color=#d080ff>의심 {Market.Suspicion:0}</color>" : "";
            statsText.text = $"<color=#ffd27a><b>{Economy.Gold:N0} G</b></color>   <color={mpColor}>MP {grid.ManaUse:0}/{grid.ManaSupply:0}</color>   " +
                             $"<color=#ffe680>★ {Market.Rating:0.0}</color>   <color=#a8f0c0>RP {Economy.ResearchPoints}</color>{sus}";

            for (int i = 0; i < 4; i++) tabButtons[i].image.color = (int)currentTab == i ? new Color(1f, 0.75f, 0.35f) : Color.white;
            var builder = gm.Builder;
            removeButton.image.color = builder.RemoveMode ? new Color(1f, 0.45f, 0.4f) : Color.white;
            foreach (var (button, def) in dockButtons)
            {
                bool sel = builder.Selected == def;
                button.image.color = sel ? new Color(1f, 0.75f, 0.35f) : Research.IsUnlocked(def) ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }
            researchButton.GetComponentInChildren<Text>().text = $"연구 (RP {Economy.ResearchPoints})";

            modeText.text = builder.RemoveMode ? "<color=#ff8877>철거 모드</color> — 클릭해 철거 (75% 환불)" :
                builder.Selected ? $"<color=#ffd27a>{builder.Selected.displayName}</color> — R 회전 · 우클릭 취소" +
                                   (builder.GhostReason != null ? $"  <color=#ff8877>({builder.GhostReason})</color>" : "") :
                grid.Heatmap ? "<color=#9f9>효율 히트맵 뷰</color> (Tab)" : "설비 클릭: 설정 · 우클릭: 철거 · Q: 스포이트";

            UpdateInfo(gm);
            UpdateGoals();
        }

        void UpdateInfo(GameManager gm)
        {
            string text = null;
            var builder = gm.Builder;
            if (builder.Selected)
            {
                var d = builder.Selected;
                text = $"<b>{d.displayName}</b>  <color=#ffd27a>{d.cost}G</color>  T{d.tier}  {d.size.x}x{d.size.y}";
                if (d.mana > 0f) text += d.kind == MachineKind.ManaCore ? $"\n마력 공급 +{d.mana:0} MP/s" : $"\n마력 소비 {d.mana:0} MP/s";
                if (!string.IsNullOrEmpty(d.description)) text += $"\n<color=#ccc>{d.description}</color>";
            }
            else if (builder.Hovered != null)
            {
                text = builder.Hovered.Info();
                if (builder.Hovered.ClickHint != null) text += $"\n<color=#9fd8ff>{builder.Hovered.ClickHint}</color>";
            }
            infoPanel.gameObject.SetActive(text != null);
            if (text == null) return;
            infoText.text = text;
            infoPanel.sizeDelta = new Vector2(480f, infoText.preferredHeight + 22f);
        }

        void UpdateGoals()
        {
            int idx = Goals.CurrentIndex;
            var sb = new System.Text.StringBuilder("<b><color=#ffd27a>목표</color></b>");
            if (idx >= Goals.List.Count) sb.Append("\n모든 목표 달성! 샌드박스 모드로 공방을 키워 보세요.");
            for (int i = idx; i < Mathf.Min(idx + 3, Goals.List.Count); i++)
                sb.Append(i == idx ? $"\n▶ {Goals.List[i].text}" : $"\n<color=#999>· {Goals.List[i].text}</color>");
            goalsText.text = sb.ToString();
            goalsPanel.sizeDelta = new Vector2(430f, goalsText.preferredHeight + 18f);
        }

        void HandleHotkeys(GameManager gm)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) { if (helpPanel) CloseHelp(); else ShowHelp(); }
            if (!gm.InGame || modalOpen) return;
            if (kb.spaceKey.wasPressedThisFrame) gm.TogglePause();
            if (kb.digit1Key.wasPressedThisFrame) { currentTab = BuildCategory.Logistics; Rebuild(); }
            if (kb.digit2Key.wasPressedThisFrame) { currentTab = BuildCategory.Processing; Rebuild(); }
            if (kb.digit3Key.wasPressedThisFrame) { currentTab = BuildCategory.Shop; Rebuild(); }
            if (kb.digit4Key.wasPressedThisFrame) { currentTab = BuildCategory.Power; Rebuild(); }
            if (kb.tKey.wasPressedThisFrame) ShowResearch();
        }
    }

    /// <summary>화면 상단 알림 메시지.</summary>
    public static class Toasts
    {
        internal static System.Action<string, Color, float> Sink;
        public static void Show(string text, Color color, float duration = 3.5f) => Sink?.Invoke(text, color, duration);
    }
}
