using UnityEngine;
using UnityEngine.UI;

namespace GearPotion
{
    /// <summary>세력 납품 계약 게시판: 들어온 의뢰 수락 · 진행 중인 계약 · 세력 평판과 혜택.</summary>
    public partial class HUD
    {
        GameObject contractPanel;
        bool contractOwnsModal;

        public void ShowContracts()
        {
            if (contractPanel) { CloseContracts(); return; }
            var gm = GameManager.Instance;
            if (!gm.InGame) return;
            var o = Overlay("Contracts", 0.6f);
            contractPanel = o.gameObject;
            if (!settlementPanel) { gm.PushModal(); contractOwnsModal = true; }
            var p = UIFactory.Rect("Panel", o, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 900f));
            UIFactory.PanelBg(p, UIFactory.PanelLight);
            UIFactory.Label(UIFactory.Rect("Head", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1100f, 56f)),
                "<b>세력 대량 납품 계약</b>", 34, UIFactory.Accent);
            UIFactory.Button(p, "닫기", new Vector2(1f, 1f), new Vector2(-80f, -44f), new Vector2(120f, 50f), CloseContracts, 24);
            UIFactory.Label(UIFactory.Rect("Hint", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(1100f, 30f)),
                $"공방의 납품 상자로 조건에 맞는 포션을 보내면 진행됩니다 · 동시에 {Contracts.MaxActive}건 · 보상은 완료 시 일괄 지급",
                20, UIFactory.Dim);

            float y = -112f;
            Section(p, ref y, "진행 중인 계약");
            if (Contracts.Active.Count == 0) Line(p, ref y, "<color=#999>없음 — 아래 의뢰를 수락하세요</color>");
            foreach (var c in Contracts.Active)
            {
                int left = c.deadlineDay - gm.Clock.Day;
                var row = Row(p, ref y, c);
                UIFactory.Label(UIFactory.Rect("State", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(260f, 60f)),
                    $"<b>{c.delivered}/{c.quantity}</b>\n<size=18>{(left <= 0 ? "<color=#ff8877>오늘 자정까지</color>" : $"D-{left} (Day {c.deadlineDay})")}</size>",
                    24, UIFactory.TextColor, TextAnchor.MiddleRight);
            }

            y -= 10f;
            Section(p, ref y, $"오늘 들어온 의뢰 (Day {gm.Clock.Day})");
            if (gm.Clock.Day < Contracts.FirstDay) Line(p, ref y, $"<color=#999>Day {Contracts.FirstDay}부터 매일 새벽 의뢰가 들어옵니다</color>");
            else if (Contracts.Offers.Count == 0) Line(p, ref y, "<color=#999>남은 의뢰가 없습니다. 내일 새벽에 새 의뢰가 들어옵니다</color>");
            foreach (var c in Contracts.Offers)
            {
                var row = Row(p, ref y, c);
                var cc = c;
                var b = UIFactory.Button(row, "수락", new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(130f, 56f), () =>
                {
                    if (Contracts.Accept(cc)) { CloseContracts(); ShowContracts(); }
                }, 24);
                b.interactable = Contracts.Active.Count < Contracts.MaxActive;
            }

            y -= 10f;
            Section(p, ref y, "세력 평판과 혜택 (첫 계약 성공 시 영구 해금)");
            for (int i = 0; i < 4; i++)
            {
                var col = ColorUtility.ToHtmlStringRGB(Contracts.FactionColors[i]);
                string state = Contracts.Perks[i] ? "<color=#a8f0a0>해금됨</color>" : "<color=#888>미해금</color>";
                Line(p, ref y, $"<color=#{col}><b>{Contracts.FactionNames[i]}</b></color>  평판 {Contracts.Rank[i]} · {state}\n<size=18><color=#bbb>{Contracts.PerkText[i]}</color></size>", 52f);
            }
        }

        void CloseContracts()
        {
            if (!contractPanel) return;
            Destroy(contractPanel);
            contractPanel = null;
            if (contractOwnsModal) GameManager.Instance.PopModal();
            contractOwnsModal = false;
        }

        static void Section(RectTransform p, ref float y, string title)
        {
            UIFactory.Label(UIFactory.Rect("Section", p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(1100f, 34f)),
                $"<b>{title}</b>", 24, UIFactory.Accent, TextAnchor.MiddleLeft);
            y -= 40f;
        }

        static void Line(RectTransform p, ref float y, string text, float h = 34f)
        {
            UIFactory.Label(UIFactory.Rect("Line", p, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(1120f, h)),
                text, 21, UIFactory.TextColor, TextAnchor.UpperLeft);
            y -= h + 4f;
        }

        static RectTransform Row(RectTransform p, ref float y, Contract c)
        {
            var row = UIFactory.Rect("Contract", p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 2f), new Vector2(1160f, 74f));
            row.pivot = new Vector2(0.5f, 1f);
            UIFactory.Image(row, new Color(0.12f, 0.1f, 0.14f, 0.92f));
            var bar = UIFactory.Rect("Faction", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 74f));
            UIFactory.Image(bar, Contracts.FactionColors[c.faction]);
            UIFactory.Label(UIFactory.Rect("Title", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -6f), new Vector2(820f, 34f)),
                $"<color=#{ColorUtility.ToHtmlStringRGB(Contracts.FactionColors[c.faction])}>[{Contracts.FactionNames[c.faction]}]</color> <b>{c.Title}</b>",
                23, UIFactory.TextColor, TextAnchor.UpperLeft);
            UIFactory.Label(UIFactory.Rect("Sub", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -40f), new Vector2(820f, 30f)),
                $"보상 <color=#ffd27a>{c.reward:N0} G</color> · 기한 Day {c.deadlineDay} 자정 · 실패 시 평판 -1" +
                (c.Faction == Faction.Shadow ? " · <color=#d080ff>완료 시 의심 수치 상승</color>" : ""),
                19, UIFactory.Dim, TextAnchor.UpperLeft);
            y -= 82f;
            return row;
        }
    }
}
