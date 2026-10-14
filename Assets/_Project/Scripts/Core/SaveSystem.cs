using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 타일별 (설비ID, X, Y, 회전, 설정값)만 JSON으로 저장해 수 KB 이내로 유지한다 (GDD 6.3).
    /// 하루가 시작될 때(06:00) 자동 저장된다.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int day = 1;
        public int gold;
        public int rp;
        public int expansion;
        public float rating = 3f;
        public float suspicion;
        public int totalSold, totalRevenue, bestGrade = -1, brewed;
        public List<string> unlocked = new();
        public List<string> discovered = new();
        public List<BuildingSave> buildings = new();
    }

    [Serializable]
    public struct BuildingSave
    {
        public string id;
        public int x, y, dir, config;
    }

    public static class SaveSystem
    {
        public static bool Disabled;
        public static string PathFor(int slot = 0) => Path.Combine(Application.persistentDataPath, $"gearpotion_save{slot}.json");
        public static bool HasSave => File.Exists(PathFor());

        public static SaveData Capture(FactoryGrid grid, int day)
        {
            var d = new SaveData
            {
                day = day,
                gold = Economy.Gold,
                rp = Economy.ResearchPoints,
                expansion = grid.ExpansionLevel,
                rating = Market.Rating,
                suspicion = Market.Suspicion,
                totalSold = Discoveries.TotalSold,
                totalRevenue = Discoveries.TotalRevenue,
                bestGrade = Discoveries.BestGrade,
                brewed = Discoveries.Brewed,
                unlocked = new List<string>(Research.Unlocked),
                discovered = new List<string>(Discoveries.Known),
            };
            foreach (var b in grid.buildings)
                d.buildings.Add(new BuildingSave { id = b.def.id, x = b.origin.x, y = b.origin.y, dir = b.dir, config = b.config });
            return d;
        }

        public static void Save(SaveData data)
        {
            if (Disabled) return;
            try
            {
                File.WriteAllText(PathFor(), JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 저장 실패: {e.Message}");
            }
        }

        public static SaveData Load()
        {
            try
            {
                return HasSave ? JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor())) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 불러오기 실패: {e.Message}");
                return null;
            }
        }

        public static void Apply(SaveData d, FactoryGrid grid)
        {
            grid.Clear();
            grid.SetExpansion(d.expansion);
            Economy.Reset(d.gold, d.rp);
            Market.ResetReputation(d.rating, d.suspicion);
            Research.Reset(d.unlocked);
            Discoveries.Reset(d.discovered, d.totalSold, d.totalRevenue, d.bestGrade, d.brewed);
            foreach (var b in d.buildings)
            {
                var def = GameManager.Db.Machine(b.id);
                if (!def) continue;
                var origin = new Vector2Int(b.x, b.y);
                if (grid.CanPlace(def, origin, b.dir, out _)) grid.Place(def, origin, b.dir, b.config);
            }
        }
    }
}
