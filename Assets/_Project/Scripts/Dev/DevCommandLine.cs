using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 자동 테스트용 커맨드라인 플래그.
    /// -autostart -continue -timescale N -gold N -rp N -unlockall -nosave -autonext -demo
    /// -shots t1,t2,... (실시간 초) -shotdir 경로 -quitafter 초
    /// </summary>
    public static class DevCommandLine
    {
        public static bool AutoStart, AutoContinue, AutoNextDay, Demo, OpenResearch, PipeView, AutoContract, OpenContracts, OpenHelp, OpenBlueprints, BlueprintTest;
        public static float TimeScale = 1f;
        static float startHour = -1f;
        public static int ExtraGold, ExtraRP;

        public static void Apply(GameManager gm)
        {
            var args = Environment.GetCommandLineArgs();
            float quitAfter = -1f;
            string shots = null, shotDir = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (a)
                {
                    case "-autostart": AutoStart = true; break;
                    case "-autonext": AutoNextDay = true; break;
                    case "-continue": AutoContinue = true; break;
                    case "-research": OpenResearch = true; break;
                    case "-pipeview": PipeView = true; break;
                    case "-autocontract": AutoContract = true; break;
                    case "-contracts": OpenContracts = true; break;
                    case "-help": OpenHelp = true; break;
                    case "-blueprints": OpenBlueprints = true; break;
                    case "-bptest": BlueprintTest = true; break;
                    case "-hour": startHour = ParseF(next, -1f); break;
                    case "-unlockall": Research.UnlockAll = true; break;
                    case "-nosave": SaveSystem.Disabled = true; break;
                    case "-demo": Demo = true; AutoStart = true; break;
                    case "-timescale": TimeScale = ParseF(next, 1f); break;
                    case "-gold": ExtraGold = (int)ParseF(next, 0f); break;
                    case "-rp": ExtraRP = (int)ParseF(next, 0f); break;
                    case "-quitafter": quitAfter = ParseF(next, -1f); break;
                    case "-shots": shots = next; break;
                    case "-shotdir": shotDir = next; break;
                }
            }
            if (Demo) GameClock.DayStarted += BuildDemoOnce;
            if (BlueprintTest) GameClock.DayStarted += RunBlueprintTestOnce;
            if (startHour >= 0f) GameClock.DayStarted += SetHourOnce;
            if (AutoContract) GameClock.DayStarted += _ => { while (Contracts.Offers.Count > 0 && Contracts.Active.Count < Contracts.MaxActive) Contracts.Accept(Contracts.Offers[0]); };
            if (shots != null || quitAfter > 0f) gm.StartCoroutine(Automation(shots, shotDir, quitAfter));
        }

        static void BuildDemoOnce(int day)
        {
            GameClock.DayStarted -= BuildDemoOnce;
            DemoLayout.Build(GameManager.Grid);
        }

        /// <summary>-demo -bptest: 버섯 라인(14~19, 8~9)을 복사해 확장 부지에 그대로/180° 회전해 붙여넣는다.</summary>
        static void RunBlueprintTestOnce(int day)
        {
            GameClock.DayStarted -= RunBlueprintTestOnce;
            var grid = GameManager.Grid;
            grid.SetExpansion(3);
            GameManager.Instance.CameraRig.Fit(grid.Width);
            var bp = BlueprintTool.Capture(new Vector2Int(14, 8), new Vector2Int(19, 9));
            if (bp == null) { Debug.LogError("[Dev] blueprint capture empty"); return; }
            BlueprintLibrary.Add(bp);
            Debug.Log($"[Dev] blueprint captured {bp.entries.Count} entries {bp.width}x{bp.height} cost={bp.Cost}");
            int a = BlueprintTool.Paste(bp, new Vector2Int(26, 8));
            var rotated = bp.Clone();
            rotated.RotateCCW();
            rotated.RotateCCW();
            int b = BlueprintTool.Paste(rotated, new Vector2Int(26, 6));
            var tall = bp.Clone();
            tall.RotateCCW();
            Debug.Log($"[Dev] blueprint pasted straight={a} rotated180={b} rotated90size={tall.width}x{tall.height}");
        }

        static void SetHourOnce(int day)
        {
            GameClock.DayStarted -= SetHourOnce;
            GameManager.Instance.Clock.SetMinute(startHour * 60f);
        }

        static float ParseF(string s, float fallback) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

        static IEnumerator Automation(string shots, string dir, float quitAfter)
        {
            float start = Time.realtimeSinceStartup;
            if (shots != null)
            {
                dir ??= Path.Combine(Application.persistentDataPath, "Shots");
                Directory.CreateDirectory(dir);
                int n = 0;
                foreach (var part in shots.Split(','))
                {
                    float t = ParseF(part, 0f);
                    while (Time.realtimeSinceStartup - start < t) yield return null;
                    yield return new WaitForEndOfFrame();
                    string path = Path.Combine(dir, $"shot_{n++:00}_{t:0}s.png");
                    ScreenCapture.CaptureScreenshot(path);
                    var gm = GameManager.Instance;
                    Debug.Log($"[Dev] screenshot {path} day={gm.Clock.Day} time={gm.Clock.TimeText} gold={Economy.Gold} sold={Discoveries.TotalSold} " +
                              $"known={string.Join("/", Discoveries.Known)} buildings={GameManager.Grid.buildings.Count} items={ItemViews.ActiveCount} rating={Market.Rating:0.00} fires={Processor.FiresStarted} contracts={Contracts.Completed}/{Contracts.Active.Count} offers={Contracts.Offers.Count}");
                }
            }
            if (quitAfter > 0f)
            {
                while (Time.realtimeSinceStartup - start < quitAfter) yield return null;
                Debug.Log($"[Dev] quit contracts={Contracts.Completed} ranks={string.Join(",", Contracts.Rank)} day={GameManager.Instance.Clock.Day} gold={Economy.Gold} sold={Discoveries.TotalSold} revenue={Discoveries.TotalRevenue} best={Discoveries.BestGrade}");
                Application.Quit();
            }
        }
    }
}
