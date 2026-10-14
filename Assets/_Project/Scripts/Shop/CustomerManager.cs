using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>시간대·평판·트렌드에 따라 손님을 입장시킨다.</summary>
    public class CustomerManager : MonoBehaviour
    {
        public FactoryGrid Grid;
        public GameClock Clock;
        readonly List<Customer> customers = new();
        float spawnTimer = 2f;

        public int Count => customers.Count;

        void Update()
        {
            customers.RemoveAll(c => c == null || c.Done);
            if (!Clock.Running || !Clock.ShopOpen) return;
            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;
            float baseInterval = Clock.Phase == DayPhase.Evening ? 6.5f : 9f;
            // 초반 며칠은 손님이 조금씩 늘어난다
            float growth = Mathf.Lerp(0.75f, 1.25f, Mathf.InverseLerp(1, 10, Clock.Day));
            spawnTimer = baseInterval / (Market.TrafficMultiplier * growth) * Random.Range(0.7f, 1.3f);
            if (customers.Count < 14) Spawn();
        }

        void Spawn()
        {
            var profile = PickProfile();
            if (!profile) return;
            var go = new GameObject($"Customer_{profile.id}");
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Customer>();
            c.Init(this, profile, PickWanted(profile), Grid.Door);
            customers.Add(c);
            Economy.Today.customers++;
        }

        CustomerProfileSO PickProfile()
        {
            var mask = Clock.Phase == DayPhase.Evening ? DayPhaseMask.Evening : DayPhaseMask.Day;
            float total = 0f;
            var pool = new List<CustomerProfileSO>();
            foreach (var p in GameManager.Db.customers)
            {
                if ((p.phases & mask) == 0 || Clock.Day < p.minDay) continue;
                pool.Add(p);
                total += p.spawnWeight;
            }
            float r = Random.value * total;
            foreach (var p in pool)
            {
                r -= p.spawnWeight;
                if (r <= 0f) return p;
            }
            return pool.Count > 0 ? pool[^1] : null;
        }

        /// <summary>발견한 포션 중에서 원하는 품목을 고른다. 트렌드 포션은 수요가 폭증.</summary>
        PotionSO PickWanted(CustomerProfileSO profile)
        {
            if (profile.wantsDefective) return null;
            var known = new List<PotionSO>();
            foreach (var p in GameManager.Db.potions) if (Discoveries.IsKnown(p)) known.Add(p);
            if (known.Count == 0) return GameManager.Db.potions.Count > 0 ? GameManager.Db.potions[0] : null;
            if (Random.value < 0.2f) return null;
            if (Market.TrendPotion && Discoveries.IsKnown(Market.TrendPotion) && Random.value < 0.35f) return Market.TrendPotion;
            // 고티어일수록 조금 덜 찾는다
            float total = 0f;
            foreach (var p in known) total += 1f / p.tier;
            float r = Random.value * total;
            foreach (var p in known)
            {
                r -= 1f / p.tier;
                if (r <= 0f) return p;
            }
            return known[^1];
        }

        public void SendEveryoneHome()
        {
            foreach (var c in customers) if (c) c.ForceLeave();
        }

        public void Clear()
        {
            foreach (var c in customers) if (c) Destroy(c.gameObject);
            customers.Clear();
        }
    }
}
