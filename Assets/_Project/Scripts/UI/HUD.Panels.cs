using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GearPotion
{
    public partial class HUD
    {
        GameObject titlePanel, settlementPanel, researchPanel, helpPanel;
        bool modalOpen;

        RectTransform Overlay(string name, float alpha = 0.7f)
        {
            var o = UIFactory.Stretch(name, root);
            UIFactory.Image(o, new Color(0.02f, 0.01f, 0.03f, alpha), true);
            return o;
        }

        // ═══════════════ 타이틀 ═══════════════

        public void ShowTitle()
        {
            if (titlePanel) return;
            var o = Overlay("Title", 0.88f);
            titlePanel = o.gameObject;
            var title = UIFactory.Label(UIFactory.Rect("Name", o, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1200f, 130f)),
                "<b>기어 앤 포션</b>", 92, UIFactory.Accent);
            title.GetComponent<Shadow>().effectDistance = new Vector2(4f, -4f);
            UIFactory.Label(UIFactory.Rect("Sub", o, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1200f, 50f)),
                "Gear & Potion: Automate the Atelier — 2D 마도 자동화 공방 타이쿤", 30, UIFactory.TextColor);
            UIFactory.Label(UIFactory.Rect("Tag", o, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 105f), new Vector2(1200f, 40f)),
                "뒷방에서는 컨베이어와 증류탑이 돌아가고, 앞가게에서는 손님이 포션을 사 간다.", 22, UIFactory.Dim);
            float y = 10f;
            if (SaveSystem.HasSave)
            {
                var save = SaveSystem.Load();
                UIFactory.Button(o, $"이어하기 (Day {save?.day ?? 1})", new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(420f, 70f), () => GameManager.Instance.Continue(), 30);
                y -= 90f;
            }
            UIFactory.Button(o, "새 게임", new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(420f, 70f), () => GameManager.Instance.NewGame(), 30);
            y -= 90f;
            UIFactory.Button(o, "도움말", new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(420f, 70f), ShowHelp, 30);
            y -= 90f;
            UIFactory.Button(o, "종료", new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(420f, 70f), Application.Quit, 30);
            UIFactory.Label(UIFactory.Rect("Ver", o, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(600f, 30f)),
                $"v{Application.version} · Unity 1인 개발 프로토타입", 18, UIFactory.Dim, TextAnchor.LowerRight);
        }

        public void HideTitle()
        {
            if (titlePanel) Destroy(titlePanel);
            titlePanel = null;
        }

        // ═══════════════ 심야 정산 ═══════════════

        public void ShowSettlement(int day, DayLedger l, System.Action onNext)
        {
            modalOpen = true;
            CloseResearch();
            CloseContracts();
            var o = Overlay("Settlement");
            settlementPanel = o.gameObject;
            var p = UIFactory.Rect("Panel", o, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 780f));
            UIFactory.PanelBg(p, UIFactory.PanelLight);
            UIFactory.Label(UIFactory.Rect("Head", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 60f)),
                $"<b>Day {day} 일일 손익계산서</b>", 40, UIFactory.Accent);

            // 장부는 항목/금액 두 열로 정렬
            var names = new System.Text.StringBuilder();
            var values = new System.Text.StringBuilder();
            void Row(string name, int v, bool expense)
            {
                if (v == 0) return;
                names.Append(name).Append('\n');
                values.Append($"<color={(expense ? "#ff9a8a" : "#a8f0a0")}>{(expense ? "-" : "+")}{v:N0} G</color>\n");
            }
            void Head(string name) { names.Append($"<b>{name}</b>\n"); values.Append('\n'); }
            Head("수입");
            Row("  매장 판매", l[LedgerEntry.Retail], false);
            Row("  세력 계약 보상", l[LedgerEntry.Contract], false);
            Row("  도매 납품", l[LedgerEntry.Bulk], false);
            Row("  암시장 거래", l[LedgerEntry.BlackMarket], false);
            Row("  철거 환불", l[LedgerEntry.Refund], false);
            Head("지출");
            Row("  원재료비", l[LedgerEntry.Ingredients], true);
            Row("  유리병", l[LedgerEntry.Bottles], true);
            Row("  건설비", l[LedgerEntry.Construction], true);
            Row("  마력 유지비", l[LedgerEntry.ManaUpkeep], true);
            Row("  환경 부담금(폐기)", l[LedgerEntry.Disposal], true);
            Row("  벌금", l[LedgerEntry.Fine], true);
            names.Append("<b>순이익</b>");
            values.Append(l.Net >= 0 ? $"<b><color=#a8f0a0>+{l.Net:N0} G</color></b>" : $"<b><color=#ff9a8a>{l.Net:N0} G</color></b>");
            var nameText = UIFactory.Label(UIFactory.Rect("Names", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-60f, -100f), new Vector2(520f, 400f)),
                names.ToString(), 24, UIFactory.TextColor, TextAnchor.UpperLeft);
            UIFactory.Label(UIFactory.Rect("Values", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-60f, -100f), new Vector2(520f, 400f)),
                values.ToString(), 24, UIFactory.TextColor, TextAnchor.UpperRight);

            var sb = new System.Text.StringBuilder();
            sb.Append($"방문 손님 {l.customers}명 · 결제 {l.served}명 · 판매 포션 {l.potionsSold}병\n");
            sb.Append(l.ratingCount > 0 ? $"오늘 평균 별점 ★ {l.AverageRating:0.0} → 내일 방문객 x{Market.TrafficMultiplier:0.00}\n" : "오늘은 별점이 없습니다\n");
            sb.Append($"획득 연구 포인트 <color=#a8f0c0>+{l.rpEarned} RP</color> (보유 {Economy.ResearchPoints})\n");
            if (l.contractsFailed > 0) sb.Append($"<color=#ff9a8a>기한을 넘긴 계약 {l.contractsFailed}건 — 세력 평판 하락</color>\n");
            if (Market.Suspicion > 0.5f) sb.Append($"<color=#d080ff>왕실 경비대 의심 수치 {Market.Suspicion:0}/100</color>\n");
            sb.Append($"보유 골드 <color=#ffd27a><b>{Economy.Gold:N0} G</b></color>");
            UIFactory.Label(UIFactory.Rect("Body", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f - nameText.preferredHeight), new Vector2(640f, 220f)),
                sb.ToString(), 24, UIFactory.TextColor, TextAnchor.UpperLeft, true);

            void Next()
            {
                if (!settlementPanel) return;
                Destroy(settlementPanel);
                settlementPanel = null;
                modalOpen = false;
                onNext();
            }
            UIFactory.Button(p, "연구하기", new Vector2(0.5f, 0f), new Vector2(-170f, 55f), new Vector2(300f, 66f), ShowResearch, 26);
            UIFactory.Button(p, $"Day {day + 1} 시작 ▶", new Vector2(0.5f, 0f), new Vector2(170f, 55f), new Vector2(300f, 66f), Next, 26);
            if (DevCommandLine.AutoNextDay) StartCoroutine(AutoNext(Next));
        }

        static System.Collections.IEnumerator AutoNext(System.Action next)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            next();
        }

        // ═══════════════ 연구 ═══════════════

        public void ShowResearch()
        {
            if (researchPanel) { CloseResearch(); return; }
            var gm = GameManager.Instance;
            if (!gm.InGame) return;
            var o = Overlay("Research", 0.6f);
            researchPanel = o.gameObject;
            if (!settlementPanel) { gm.PushModal(); researchOwnsModal = true; }
            var p = UIFactory.Rect("Panel", o, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 900f));
            UIFactory.PanelBg(p, UIFactory.PanelLight);
            UIFactory.Label(UIFactory.Rect("Head", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1100f, 56f)),
                $"<b>마도 공학 연구</b>   <size=26><color=#a8f0c0>보유 {Economy.ResearchPoints} RP</color> · 판매 {Economy.GoldPerRP}G당 1RP</size>", 36, UIFactory.Accent);
            UIFactory.Button(p, "닫기", new Vector2(1f, 1f), new Vector2(-80f, -44f), new Vector2(120f, 50f), CloseResearch, 24);

            var entries = new List<(string id, string name, string desc, int cost, bool done, bool gold, System.Action unlock)>();
            foreach (var m in GameManager.Db.machines)
            {
                if (m.unlockedByDefault) continue;
                var mm = m;
                entries.Add((m.id, $"[T{m.tier} 설비] {m.displayName}", m.description, m.researchCost, Research.IsUnlocked(m), false,
                    () => Research.TryUnlock(mm.id, mm.researchCost, mm.displayName)));
            }
            foreach (var i in GameManager.Db.ingredients)
            {
                if (i.unlockedByDefault) continue;
                var ii = i;
                entries.Add((i.id, $"[원재료 수급] {i.displayName}", $"{i.elements.Describe()} · 불순물 {i.impurity:0}% · {i.cost}G — {i.note}", i.researchCost,
                    Research.IsUnlocked(i), false, () => Research.TryUnlock(ii.id, ii.researchCost, ii.displayName)));
            }
            entries.Sort((a, b) => a.done != b.done ? a.done.CompareTo(b.done) : a.cost.CompareTo(b.cost));
            int level = GameManager.Grid.ExpansionLevel;
            if (level + 1 < FactoryGrid.ExpansionWidths.Length)
                entries.Insert(0, ("EXPAND", $"[부지 확장] 공방 폭 {FactoryGrid.ExpansionWidths[level]} → {FactoryGrid.ExpansionWidths[level + 1]}타일",
                    "공방과 매장을 오른쪽으로 넓힙니다. (골드로 구매)", FactoryGrid.ExpansionCosts[level + 1], false, true, () => gm.TryExpand()));

            float y = -95f;
            int col = 0;
            foreach (var e in entries)
            {
                float x = col == 0 ? -285f : 285f;
                var row = UIFactory.Rect("Row", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(560f, 100f));
                UIFactory.Image(row, e.done ? new Color(0.15f, 0.25f, 0.18f, 0.9f) : new Color(0.12f, 0.1f, 0.14f, 0.9f));
                UIFactory.Label(UIFactory.Rect("Name", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -6f), new Vector2(400f, 34f)),
                    $"<b>{e.name}</b>", 22, e.done ? new Color(0.6f, 1f, 0.7f) : UIFactory.TextColor, TextAnchor.UpperLeft);
                UIFactory.Label(UIFactory.Rect("Desc", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -38f), new Vector2(410f, 70f)),
                    e.desc, 17, UIFactory.Dim, TextAnchor.UpperLeft, true);
                if (e.done)
                {
                    UIFactory.Label(UIFactory.Rect("Done", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(120f, 40f)),
                        "해금됨", 22, new Color(0.6f, 1f, 0.7f), TextAnchor.MiddleRight);
                }
                else
                {
                    var unlock = e.unlock;
                    bool can = e.gold ? Economy.Gold >= e.cost : Economy.ResearchPoints >= e.cost;
                    var b = UIFactory.Button(row, e.gold ? $"{e.cost:N0} G" : $"{e.cost} RP", new Vector2(1f, 0.5f), new Vector2(-72f, 0f), new Vector2(124f, 60f),
                        () => { unlock(); CloseResearch(); ShowResearch(); }, 22);
                    b.interactable = can;
                }
                col++;
                if (col == 2) { col = 0; y -= 108f; }
            }
        }

        bool researchOwnsModal;

        void CloseResearch()
        {
            if (!researchPanel) return;
            Destroy(researchPanel);
            researchPanel = null;
            if (researchOwnsModal) GameManager.Instance.PopModal();
            researchOwnsModal = false;
        }

        // ═══════════════ 도움말 ═══════════════

        public void ShowHelp()
        {
            if (helpPanel) return;
            var o = Overlay("Help", 0.75f);
            helpPanel = o.gameObject;
            o.SetAsLastSibling();
            bool paused = GameManager.Instance.InGame;
            if (paused) GameManager.Instance.PushModal();
            helpPausing = paused;
            var p = UIFactory.Rect("Panel", o, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 990f));
            UIFactory.PanelBg(p, UIFactory.PanelLight);
            UIFactory.Label(UIFactory.Rect("Head", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1200f, 56f)),
                "<b>공방 운영 안내</b>", 38, UIFactory.Accent);
            const string text =
                "<b><color=#ffd27a>조작</color></b>\n" +
                "· 하단 독에서 설비 선택 → 좌클릭 배치 (벨트는 드래그로 연속 배치, 끌어간 방향으로 자동 회전)\n" +
                "· R 회전 (Shift+R 역회전) · 우클릭 철거/선택 취소 (75% 환불) · X 철거 모드 · Q 스포이트\n" +
                "· 설비 클릭: 투입구 재료 / 진열대 가격 배율 / 분류기 조건 / 합성 가마 혼합 모드 변경\n" +
                "· B 청사진: 영역 드래그로 설비 묶음 복사·저장 → R 회전 · 클릭으로 골드를 내고 통째로 설치 (막힌 칸은 건너뜀)\n" +
                "· VIP 쇼케이스 손님이 결제하면 흥정 창: 바늘이 초록 구간일 때 H(흥정!) → +25%, 빗나가면 -10%·별 감점\n" +
                "· Space 일시정지 · 1~4 독 탭 · T 연구 · C 계약 · Tab 뷰 전환(일반→배관→히트맵) · WASD/휠/휠버튼 드래그 카메라\n\n" +
                "<b><color=#ffd27a>물류 · 배관</color></b>\n" +
                "· 고체(재료·분말·포션·슬러지)는 벨트로, 액체 원액은 <color=#8fd0ff>마력 배관</color>으로만 이동합니다. 설비는 노란 꺾쇠(▶) 쪽으로 배출합니다.\n" +
                "· 원액 설비의 출력 칸에 배관을 놓으면 이어진 배관 전체(네트워크)를 타고 닿아 있는 여과기·증류탑·가마·병입기로 공급됩니다.\n" +
                "· 배관은 벨트 위에 겹쳐 놓을 수 있습니다(교차). 설비끼리 바로 맞닿아 있으면 배관 없이도 직접 넘어갑니다.\n" +
                "· 원심 여과기는 왼쪽 측면으로 슬러지 배출 → 폐기통(5G)/슬라임 배양조. 벨트는 벽을 지나 진열대에 꽂힙니다.\n\n" +
                "<b><color=#ffd27a>과열 · 냉각</color></b>\n" +
                "· 가동 중인 가공 설비는 열이 쌓이고(오른쪽 게이지), 화 원소 재료는 더 뜨겁습니다. 기본 방열 5/s.\n" +
                "· 오버클럭 태엽(클릭해 부착): 속도 150%·발열 2배. 열 100이면 화재 — 6초 정지, 재료 소실, 이웃에 열 전파.\n" +
                "· 얼음 정령석을 맞닿게 두면 -6/s, 물 원소 원액이 흐르는 배관(냉각수)이 닿으면 -5/s.\n\n" +
                "<b><color=#ffd27a>연금술</color></b>\n" +
                "· 모든 재료는 화·수·풍·지·에테르 원소와 불순물(%)을 가집니다. 병입기에서 원소 조건으로 포션이 결정됩니다.\n" +
                "· 분쇄: 주 원소 x1.3, 불순물 +10%p · 여과: 불순물 -20%p(최소 3%) · 증류: 주 원소만 남기고 x1.5 · 합성 가마: 원액 2개 합산\n" +
                "· 순도 = 100 - 불순물. C(40↓) x0.6 · B x1.0 · A(70+) x1.45 · S(90+) x2.1 · SSS(99+, 가마 대성공 필요) x3.5\n" +
                "· 하급 치유(수10+지5) · 화염 활력제(화20) · 마나 엘릭서(수25+에테르15) · 폭풍 질주(풍30+수15)\n" +
                "  드래곤 브레스(화60+에테르30) · 액상 현자의 돌(전 원소 50 + 불순물 0%)\n\n" +
                "<b><color=#ffd27a>세력 계약 (C)</color></b>\n" +
                "· Day 2부터 매일 새벽 길드·학회·상인 연합·암시장 의뢰 도착, 2건까지 수락. 납품 상자로 보내 기한 내 채우면 보상 + 평판.\n" +
                "· 세력별 첫 성공 시 영구 혜택(재료 해금·할인·설계도·의심 감소). 기한을 넘기면 평판 -1.\n\n" +
                "<b><color=#ffd27a>하루</color></b>\n" +
                "· 06~09 새벽(시계 0.5배) 정비 → 09 오픈 → 17 피크(정예 모험가·학회 감정사·암시장 브로커) → 22 마감 → 24 정산\n" +
                "· 마력(MP) 소비가 공급을 넘으면 모든 설비가 느려집니다. 마나 코어를 추가하세요. (하루 유지비 3G/MP)\n" +
                "· 별점이 높을수록 다음 날 손님이 늘어납니다. 암시장 거래는 3배 가격이지만 의심 수치 100이면 검문 벌금!";
            UIFactory.Label(UIFactory.Rect("Body", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1200f, 700f)),
                text, 18, UIFactory.TextColor, TextAnchor.UpperLeft, true);
            UIFactory.Button(p, "닫기", new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(260f, 60f), CloseHelp, 26);
        }

        bool helpPausing;

        void CloseHelp()
        {
            if (!helpPanel) return;
            Destroy(helpPanel);
            helpPanel = null;
            if (helpPausing) GameManager.Instance.PopModal();
            helpPausing = false;
        }

        // ═══════════════ 토스트 ═══════════════

        class Toast
        {
            public Text text;
            public float age, life;
        }

        readonly List<Toast> toasts = new();
        RectTransform toastRoot;

        void BuildToasts(Transform parent)
        {
            toastRoot = UIFactory.Rect("Toasts", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(1400f, 300f));
            Toasts.Sink = (s, c, d) =>
            {
                // 같은 알림이 이미 떠 있으면 새로 쌓지 않고 시간만 연장
                foreach (var existing in toasts)
                    if (existing.text.text == s) { existing.age = 0f; existing.life = d; return; }
                var rt = UIFactory.Rect("Toast", toastRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1400f, 40f));
                var t = UIFactory.Label(rt, s, 26, c);
                toasts.Insert(0, new Toast { text = t, life = d });
                while (toasts.Count > 5)
                {
                    Destroy(toasts[^1].text.gameObject);
                    toasts.RemoveAt(toasts.Count - 1);
                }
            };
        }

        void UpdateToasts()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                t.age += dt;
                if (t.age >= t.life)
                {
                    Destroy(t.text.gameObject);
                    toasts.RemoveAt(i);
                    continue;
                }
                var c = t.text.color;
                c.a = Mathf.Clamp01((t.life - t.age) / 0.6f);
                t.text.color = c;
            }
            for (int i = 0; i < toasts.Count; i++)
            {
                var rt = (RectTransform)toasts[i].text.transform;
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, new Vector2(0f, -i * 42f), Time.unscaledDeltaTime * 12f);
            }
        }
    }
}
