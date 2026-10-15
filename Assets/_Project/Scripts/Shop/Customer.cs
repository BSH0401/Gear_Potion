using System.Collections.Generic;
using UnityEngine;

namespace GearPotion
{
    /// <summary>
    /// 손님 FSM (GDD 3.3): 입장 → 진열대 탐색/가성비 판정 → 계산대 결제 → 별점 남기고 퇴장.
    /// 걷기 애니메이션 대신 좌우 기울기 + Sin 점프로 보드게임 말처럼 움직인다.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        enum State { Enter, Browse, ToShelf, ToCounter, Pay, Leave }

        public CustomerProfileSO profile;
        public PotionSO wanted; // null = 아무 포션
        State state;
        Vector3 target;
        float patience, timer, bobTime;
        readonly List<(Item item, int price)> basket = new();
        Shelf targetShelf;
        int rating = -1;
        string reason;
        Transform visual;
        SpriteRenderer bubble;
        TextMesh bubbleText;
        CustomerManager manager;
        Renderer[] renderers;

        public bool Done { get; private set; }
        bool IsBroker => profile.wantsDefective;

        public void Init(CustomerManager manager, CustomerProfileSO profile, PotionSO wanted, Vector3 door)
        {
            this.manager = manager;
            this.profile = profile;
            this.wanted = wanted;
            transform.position = door;
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            var body = Visuals.Sprite("Body", visual, Sprites.Person, profile.color, 40, 0.62f);
            body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            var shadow = Visuals.Sprite("Shadow", transform, Sprites.Circle, new Color(0f, 0f, 0f, 0.3f), 39, 0.4f);
            shadow.transform.localScale = new Vector3(0.45f, 0.16f, 1f);
            bubble = Visuals.Sprite("Bubble", visual, Sprites.Bubble, new Color(1f, 1f, 1f, 0.92f), 41, 0.7f, true);
            bubble.transform.localPosition = new Vector3(0.1f, 0.95f, 0f);
            bubble.transform.localScale = new Vector3(0.95f, 0.5f, 1f);
            bubbleText = Visuals.Label("Want", visual, WantLabel, wanted ? wanted.color * 0.75f + Color.black * 0.25f : new Color(0.2f, 0.2f, 0.25f), 42, 0.2f);
            bubbleText.transform.localPosition = new Vector3(0.1f, 1.0f, 0f);
            var c = bubbleText.color; c.a = 1f; bubbleText.color = c;
            renderers = visual.GetComponentsInChildren<Renderer>();
            patience = 14f;
            state = State.Enter;
            target = new Vector3(Mathf.Clamp(door.x + Random.Range(-2f, 2f), 1f, manager.Grid.Width - 1f), Random.Range(0.8f, 1.6f), 0f);
        }

        string WantLabel => IsBroker ? "불량품?" : profile.minGrade >= Grade.S ? $"{(wanted ? wanted.shortName : "포션")} S+" :
            wanted ? wanted.shortName : "아무거나";

        void Update()
        {
            float dt = Time.deltaTime;
            bool moving = MoveTowardsTarget(dt);
            Animate(dt, moving);
            foreach (var (item, _) in basket) item.target = transform.position + new Vector3(0.28f, 0.35f, 0f);

            switch (state)
            {
                case State.Enter:
                    if (!moving) state = State.Browse;
                    break;
                case State.Browse:
                    patience -= dt;
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        timer = 0.6f;
                        if (FindShelf()) state = State.ToShelf;
                        else if (!moving) target = RandomShopPoint();
                    }
                    if (!manager.Clock.ShopOpen || patience <= 0f)
                    {
                        if (basket.Count > 0) GoToCounter();
                        else Leave(2, wanted ? $"{wanted.displayName}이(가) 없어요" : "살 게 없어요");
                    }
                    break;
                case State.ToShelf:
                    if (targetShelf == null || !manager.Grid.buildings.Contains(targetShelf)) { state = State.Browse; break; }
                    if (!moving) TryBuy();
                    break;
                case State.ToCounter:
                    if (!moving) { state = State.Pay; timer = 0.5f; }
                    break;
                case State.Pay:
                    timer -= dt;
                    if (timer <= 0f) Pay();
                    break;
                case State.Leave:
                    if (!moving)
                    {
                        Done = true;
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        bool Matches(Item it)
        {
            if (it.kind != ItemKind.Potion) return false;
            if (IsBroker) return it.IsDefective;
            if (it.IsDefective && profile.minGrade > Grade.C) return false;
            if (it.Grade < profile.minGrade) return false;
            return wanted == null ? it.potion != GameManager.Db.murkyPotion : it.potion == wanted;
        }

        bool FindShelf()
        {
            Shelf best = null;
            float bestScore = float.MaxValue;
            foreach (var s in manager.Grid.All<Shelf>())
            {
                if (s.Find(Matches) < 0) continue;
                float score = Vector3.Distance(transform.position, s.StandPoint) + s.Markup * 2f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            if (best == null) return false;
            targetShelf = best;
            target = best.StandPoint + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.05f), 0f);
            return true;
        }

        void TryBuy()
        {
            int idx = targetShelf.Find(Matches);
            if (idx < 0) { state = State.Browse; return; }
            // 가성비 판정: 가격 배율이 손님의 허용치를 넘으면 망설이다 떠난다
            float markup = targetShelf.Markup;
            if (!IsBroker && markup > profile.priceTolerance && Random.value < (markup - profile.priceTolerance) * 2.5f)
            {
                Fx.Float(transform.position + Vector3.up * 0.6f, "비싸요…", new Color(1f, 0.6f, 0.5f));
                if (basket.Count > 0) GoToCounter();
                else Leave(2, "가격이 너무 비싸요");
                return;
            }
            var it = targetShelf.Take(idx);
            int price = IsBroker ? Market.BlackMarketPrice(it) : Mathf.RoundToInt(Market.ShelfPrice(it, targetShelf) * profile.payMultiplier);
            basket.Add((it, price));
            if (it.view) it.view.body.sortingOrder = 43;
            if (basket.Count < profile.buyCount && FindShelf()) { state = State.ToShelf; return; }
            GoToCounter();
        }

        void GoToCounter()
        {
            var counter = manager.Grid.Find<Counter>();
            target = counter != null ? counter.PayPoint + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f) : transform.position;
            state = State.ToCounter;
        }

        void Pay()
        {
            int total = 0, gradeScore = 0;
            foreach (var (item, price) in basket)
            {
                total += price;
                gradeScore += item.IsDefective ? 1 : (int)item.Grade;
                item.Hide();
            }
            var entry = IsBroker ? LedgerEntry.BlackMarket : LedgerEntry.Retail;
            Economy.Earn(total, entry);
            Discoveries.TotalSold += basket.Count;
            Discoveries.TotalRevenue += total;
            Economy.Today.potionsSold += basket.Count;
            Economy.Today.served++;
            if (IsBroker) Market.Suspicion = Mathf.Min(120f, Market.Suspicion + profile.suspicionPerVisit * Contracts.SuspicionMultiplier);
            Fx.Float(transform.position + Vector3.up * 0.5f, $"+{total}G", new Color(1f, 0.85f, 0.25f));
            Sfx.Play(SfxId.Coin);

            float avgGrade = gradeScore / (float)basket.Count; // C=0 … SSS=4
            int stars = Mathf.Clamp(Mathf.RoundToInt(2f + avgGrade), 1, 5);
            if (targetShelf != null && targetShelf.Markup > profile.priceTolerance) stars--;
            basket.Clear();
            Leave(Mathf.Clamp(stars, 1, 5), null);
        }

        void Leave(int stars, string why)
        {
            if (rating < 0)
            {
                rating = stars;
                reason = why;
                if (!IsBroker)
                {
                    Market.AddRating(stars);
                    Economy.Today.ratingSum += stars;
                    Economy.Today.ratingCount++;
                }
                if (why != null) Fx.Float(transform.position + Vector3.up * 0.7f, why, new Color(1f, 0.55f, 0.5f), 1.8f);
                else Fx.Float(transform.position + Vector3.up * 0.9f, new string('★', stars), new Color(1f, 0.9f, 0.4f));
            }
            foreach (var (item, _) in basket) item.Hide(); // 결제 전에 떠나면(마감 등) 들고 있던 물건은 사라진다
            basket.Clear();
            bubble.enabled = false;
            bubbleText.gameObject.SetActive(false);
            target = manager.Grid.Door + new Vector3(0f, -0.6f, 0f);
            state = State.Leave;
        }

        public void ForceLeave() { if (state != State.Leave) Leave(basket.Count > 0 ? 3 : 2, null); }

        Vector3 RandomShopPoint() =>
            new(Random.Range(1f, manager.Grid.Width - 1f), Random.Range(0.6f, FactoryGrid.ShopRows - 1.4f), 0f);

        bool MoveTowardsTarget(float dt)
        {
            var pos = transform.position;
            if ((pos - target).sqrMagnitude < 0.0025f) return false;
            transform.position = Vector3.MoveTowards(pos, target, profile.moveSpeed * dt);
            return true;
        }

        void Animate(float dt, bool moving)
        {
            if (moving) bobTime += dt * 11f;
            else bobTime = Mathf.MoveTowards(bobTime, Mathf.Round(bobTime / Mathf.PI) * Mathf.PI, dt * 8f);
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(bobTime) * 8f);
            visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bobTime)) * 0.1f, 0f);
            // 아래쪽(앞쪽) 손님이 위에 그려지도록
            int order = 40 + Mathf.RoundToInt((10f - transform.position.y) * 4f);
            for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder = order + i;
        }
    }
}
