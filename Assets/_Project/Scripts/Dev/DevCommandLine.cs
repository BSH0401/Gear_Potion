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
        public static bool AutoStart, AutoContinue, AutoNextDay, Demo, OpenResearch, PipeView;
        public static float TimeScale = 1f;
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
            if (shots != null || quitAfter > 0f) gm.StartCoroutine(Automation(shots, shotDir, quitAfter));
        }

        static void BuildDemoOnce(int day)
        {
            GameClock.DayStarted -= BuildDemoOnce;
            DemoLayout.Build(GameManager.Grid);
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
                              $"known={string.Join("/", Discoveries.Known)} buildings={GameManager.Grid.buildings.Count} items={ItemViews.ActiveCount} rating={Market.Rating:0.00} fires={Processor.FiresStarted}");
                }
            }
            if (quitAfter > 0f)
            {
                while (Time.realtimeSinceStartup - start < quitAfter) yield return null;
                Debug.Log($"[Dev] quit day={GameManager.Instance.Clock.Day} gold={Economy.Gold} sold={Discoveries.TotalSold} revenue={Discoveries.TotalRevenue} best={Discoveries.BestGrade}");
                Application.Quit();
            }
        }
    }
}
