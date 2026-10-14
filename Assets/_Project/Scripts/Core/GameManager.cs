using UnityEngine;

namespace GearPotion
{
    /// <summary>씬의 유일한 진입점. 모든 시스템을 코드로 생성·연결한다.</summary>
    public class GameManager : MonoBehaviour
    {
        public const int StartGold = 500;

        [SerializeField] GameDatabase database;
        [SerializeField] Material spriteMaterial;

        public static GameDatabase Db;
        public static FactoryGrid Grid;
        public static GameManager Instance;

        public GameClock Clock { get; private set; }
        public CustomerManager Customers { get; private set; }
        public BuildController Builder { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public HUD Hud { get; private set; }

        public float Speed { get; private set; } = 1f;
        public bool Paused { get; private set; }
        public bool InGame { get; private set; }
        int modalCount;
        public bool Modal => modalCount > 0;

        void Awake()
        {
            Instance = this;
            Db = database;
            Visuals.SpriteMaterial = spriteMaterial;
            Application.targetFrameRate = 144;

            Grid = new GameObject("FactoryGrid").AddComponent<FactoryGrid>();
            Clock = gameObject.AddComponent<GameClock>();
            Customers = new GameObject("Customers").AddComponent<CustomerManager>();
            Customers.Grid = Grid;
            Customers.Clock = Clock;
            new GameObject("Fx").AddComponent<Fx>();
            new GameObject("Sfx").AddComponent<Sfx>();
            CameraRig = Camera.main.gameObject.AddComponent<CameraRig>();
            Builder = gameObject.AddComponent<BuildController>();
            Hud = new GameObject("HUD").AddComponent<HUD>();
            GameClock.DayEnded += OnDayEnded;
        }

        void OnDestroy() => GameClock.DayEnded -= OnDayEnded;

        void Start()
        {
            Grid.SetExpansion(0);
            CameraRig.Fit(Grid.Width);
            DevCommandLine.Apply(this);
            if (DevCommandLine.AutoContinue) Continue();
            else if (DevCommandLine.AutoStart) NewGame();
            else Hud.ShowTitle();
            if (DevCommandLine.OpenResearch && InGame) Hud.ShowResearch();
            if (DevCommandLine.PipeView) Grid.View = ViewMode.Pipes;
        }

        // ───────────── 게임 흐름 ─────────────

        public void NewGame()
        {
            Customers.Clear();
            Grid.Clear();
            Grid.SetExpansion(0);
            Research.Reset(null);
            Discoveries.Reset(null, 0, 0, -1, 0);
            Market.ResetReputation(3f, 0f);
            Economy.Reset(StartGold + DevCommandLine.ExtraGold, DevCommandLine.ExtraRP);
            StarterLayout.Build(Grid);
            BeginSession(1);
            Toasts.Show("기어 앤 포션에 오신 것을 환영합니다! 이미 놓인 치유 물약 라인이 돌아가기 시작합니다.", UIFactory.Accent, 6f);
        }

        public void Continue()
        {
            var data = SaveSystem.Load();
            if (data == null) { NewGame(); return; }
            Customers.Clear();
            SaveSystem.Apply(data, Grid);
            BeginSession(data.day);
            Toasts.Show($"Day {data.day} 새벽부터 이어서 시작합니다.", UIFactory.Accent);
        }

        void BeginSession(int day)
        {
            InGame = true;
            modalCount = 0;
            Paused = false;
            CameraRig.Fit(Grid.Width);
            Clock.StartDay(day);
            SaveSystem.Save(SaveSystem.Capture(Grid, day));
            Hud.HideTitle();
            Hud.Rebuild();
            ApplyTimeScale();
        }

        void OnDayEnded(int day)
        {
            Customers.SendEveryoneHome();
            PushModal();
            Hud.ShowSettlement(day, Economy.Today, () =>
            {
                PopModal();
                Clock.StartDay(day + 1);
                SaveSystem.Save(SaveSystem.Capture(Grid, day + 1));
                Toasts.Show($"Day {day + 1} — {Market.News}", UIFactory.Accent, 5f);
            });
        }

        public bool TryExpand()
        {
            int next = Grid.ExpansionLevel + 1;
            if (next >= FactoryGrid.ExpansionWidths.Length) return false;
            int cost = FactoryGrid.ExpansionCosts[next];
            if (!Economy.TrySpend(cost, LedgerEntry.Construction))
            {
                Toasts.Show($"골드가 부족합니다 ({cost}G 필요)", new Color(1f, 0.5f, 0.4f));
                Sfx.Play(SfxId.Error);
                return false;
            }
            Grid.SetExpansion(next);
            // 기존 계산대·설비는 왼쪽 정렬 좌표라 그대로 유지된다
            CameraRig.Fit(Grid.Width);
            Toasts.Show($"공방 부지 확장! 폭 {Grid.Width}타일", UIFactory.Accent);
            Sfx.Play(SfxId.Sparkle);
            return true;
        }

        // ───────────── 시간 제어 ─────────────

        public void SetSpeed(float s)
        {
            Speed = s;
            Paused = false;
            ApplyTimeScale();
        }

        public void TogglePause()
        {
            Paused = !Paused;
            ApplyTimeScale();
        }

        public void PushModal() { modalCount++; ApplyTimeScale(); }
        public void PopModal() { modalCount = Mathf.Max(0, modalCount - 1); ApplyTimeScale(); }

        void ApplyTimeScale() => Time.timeScale = !InGame || Paused || Modal ? 0f : Speed * DevCommandLine.TimeScale;
    }
}
