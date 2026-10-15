using System;
using UnityEngine;
using UnityEngine.UI;

namespace GearPotion
{
    /// <summary>튜토리얼 진행 단계 (-1 = 끝). 세이브에 저장된다.</summary>
    public static class Tutorial
    {
        public static int Step = -1;
        public static bool Active => Step >= 0;
    }

    /// <summary>
    /// 초반 15분 튜토리얼 가이드 (GDD 7 - 5개월 차). 단계마다 안내 문구 + 공방 위 화살표 또는 버튼 강조,
    /// 조건을 만족하면 자동으로 다음 단계로 넘어간다. 언제든 건너뛸 수 있다.
    /// </summary>
    public partial class HUD
    {
        class TutorialStep
        {
            public string text;
            public Func<bool> done;      // null이면 [다음] 버튼으로 진행
            public Func<Vector2Int?> cell; // 공방 위 가리킬 칸
            public Func<RectTransform> ui; // 강조할 버튼
        }

        RectTransform tutorialPanel;
        Text tutorialText;
        Button tutorialNext;
        Transform worldPointer;
        SpriteRenderer pointerArrow, pointerRing;
        RectTransform uiHighlight;
        Image uiHighlightImage;
        int shownStep = -2;
        float hoverTime;
        TutorialStep[] steps;

        TutorialStep[] Steps => steps ??= new[]
        {
            new TutorialStep
            {
                text = "<b>기어 앤 포션에 오신 걸 환영합니다!</b>\n위쪽은 자동화 <b>공방</b>, 아래쪽은 손님이 오는 <b>매장</b>입니다. " +
                       "이미 깔린 시작 라인이 이슬 맺힌 약초로 <color=#ff7a8a>하급 치유 물약</color>을 만들고 있어요.",
            },
            new TutorialStep
            {
                text = "<b>① 투입구 살펴보기</b>\n화살표가 가리키는 <b>원재료 투입구</b>에 마우스를 올려 보세요. 오른쪽 아래에 재료 가격·원소·불순물이 나옵니다.\n(클릭하면 넣을 재료가 바뀝니다)",
                done = () => hoverTime > 0.6f,
                cell = () => GameManager.Grid.Find<SupplyCrate>()?.origin,
            },
            new TutorialStep
            {
                text = "<b>② 첫 판매</b>\n투입구 → 벨트 → 추출기 → <color=#8fd0ff>배관(원액)</color> → 병입기 → 벨트 → 진열대 순서로 흐릅니다. " +
                       "09:00에 상점이 열리면 손님이 사 갑니다. 위의 <b>2x·3x</b>로 시간을 빠르게 해 보세요.",
                done = () => Discoveries.TotalSold >= 1,
                ui = () => (RectTransform)speedButtons[3].transform,
            },
            new TutorialStep
            {
                text = "<b>③ 진열대 늘리기</b>\n하단 <b>[4 상점]</b> 탭 → <b>나무 진열대</b>를 골라 매장 맨 윗줄(벽 바로 아래)에 놓으세요. 위쪽에서 벨트가 꽂히면 재고가 자동으로 채워집니다.",
                done = () => GameManager.Grid.CountOf(MachineKind.Shelf) >= 2,
                ui = () => (RectTransform)tabButtons[(int)BuildCategory.Shop].transform,
            },
            new TutorialStep
            {
                text = "<b>④ 분배기로 나누기</b>\n화살표가 가리키는 벨트를 <b>우클릭</b>으로 철거하고, <b>[1 물류] → 분배기</b>를 그 자리에 놓으세요(R로 회전해 아래를 향하게). " +
                       "옆으로 나온 쪽에 벨트를 이어 새 진열대로 보내면 됩니다.",
                done = () => GameManager.Grid.CountOf(MachineKind.Splitter) >= 1,
                cell = () =>
                {
                    var s = GameManager.Grid.Find<Shelf>();
                    return s != null ? s.origin + Vector2Int.up : null;
                },
            },
            new TutorialStep
            {
                text = "<b>⑤ 연구</b>\n<b>연구(T)</b>를 열어 <b>원심 불순물 여과기</b>를 해금해 보세요. 연구 포인트(RP)는 매장 판매 20G당 1씩 쌓입니다. " +
                       "여과기는 불순물을 줄여 포션 등급과 가격을 올립니다.",
                done = () => Research.IsUnlocked(GameManager.Db.Machine("MAC_FILTER")),
                ui = () => (RectTransform)researchButton.transform,
            },
            new TutorialStep
            {
                text = "<b>⑥ 뷰 전환</b>\n<b>Tab</b>을 눌러 보세요. 배관 뷰에서는 원액 흐름이, 효율 히트맵에서는 막히거나 마력이 부족한 설비(빨강)가 한눈에 보입니다.",
                done = () => GameManager.Grid.View != ViewMode.Normal,
            },
            new TutorialStep
            {
                text = "<b>⑦ 세력 계약</b>\nDay 2부터 새벽마다 길드·학회·상인 연합의 의뢰가 옵니다. <b>계약(C)</b>에서 수락하고 공방의 <b>납품 상자</b>로 보내면 큰 보상과 영구 혜택을 받아요.",
                done = () => Contracts.Active.Count > 0 || Array.Exists(Contracts.Perks, p => p),
                ui = () => (RectTransform)contractButton.transform,
            },
            new TutorialStep
            {
                text = "<b>튜토리얼 완료!</b>\n왼쪽 아래 <b>목표</b>를 따라 화염 활력제 → 마나 엘릭서 → … → <color=#ffd84a>액상 현자의 돌</color>까지 공방을 키워 보세요. " +
                       "모든 규칙은 <b>F1 도움말</b>에 있습니다.",
            },
        };

        void BuildTutorial(Transform parent)
        {
            tutorialPanel = UIFactory.Rect("Tutorial", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -84f), new Vector2(430f, 200f));
            UIFactory.PanelBg(tutorialPanel, new Color(0.14f, 0.1f, 0.2f, 0.95f));
            var bar = UIFactory.Rect("Accent", tutorialPanel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 200f));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.sizeDelta = new Vector2(6f, 0f);
            UIFactory.Image(bar, UIFactory.Accent);
            tutorialText = UIFactory.Label(UIFactory.Rect("Text", tutorialPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(396f, 150f)),
                "", 20, UIFactory.TextColor, TextAnchor.UpperLeft, true);
            tutorialNext = UIFactory.Button(tutorialPanel, "다음 ▶", new Vector2(1f, 0f), new Vector2(-62f, 26f), new Vector2(110f, 38f), AdvanceTutorial, 20);
            UIFactory.Button(tutorialPanel, "튜토리얼 끄기", new Vector2(0f, 0f), new Vector2(76f, 26f), new Vector2(130f, 38f), () => { Tutorial.Step = -1; }, 18);
            UIFactory.Button(tutorialPanel, "단계 건너뛰기", new Vector2(0f, 0f), new Vector2(220f, 26f), new Vector2(150f, 38f), AdvanceTutorial, 18);

            worldPointer = new GameObject("TutorialPointer").transform;
            pointerArrow = Visuals.Sprite("Arrow", worldPointer, Sprites.Chevron, UIFactory.Accent, 70, 0.7f, true);
            pointerArrow.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            pointerRing = Visuals.Sprite("Ring", worldPointer, Sprites.Ring, UIFactory.Accent, 69, 1.25f, true);
            worldPointer.gameObject.SetActive(false);
        }

        void AdvanceTutorial()
        {
            if (!Tutorial.Active) return;
            Tutorial.Step++;
            hoverTime = 0f;
            if (Tutorial.Step >= Steps.Length) Tutorial.Step = -1;
            else Sfx.Play(SfxId.Sparkle, 0.4f);
        }

        void UpdateTutorial(GameManager gm)
        {
            bool show = gm.InGame && Tutorial.Active && !modalOpen;
            tutorialPanel.gameObject.SetActive(show);
            if (!show)
            {
                worldPointer.gameObject.SetActive(false);
                SetUiHighlight(null);
                return;
            }
            var step = Steps[Mathf.Clamp(Tutorial.Step, 0, Steps.Length - 1)];
            if (shownStep != Tutorial.Step)
            {
                shownStep = Tutorial.Step;
                tutorialText.text = $"<color=#ffd27a><size=17>튜토리얼 {Tutorial.Step + 1}/{Steps.Length}</size></color>\n{step.text}";
                tutorialPanel.sizeDelta = new Vector2(430f, tutorialText.preferredHeight + 80f);
                UIFactory.ButtonText(tutorialNext).text = Tutorial.Step == Steps.Length - 1 ? "시작하기" : "다음 ▶";
            }
            tutorialNext.gameObject.SetActive(step.done == null);

            // 진행 조건
            if (gm.Builder.Hovered is SupplyCrate) hoverTime += Time.unscaledDeltaTime;
            if (step.done != null && step.done()) AdvanceTutorial();

            // 공방 위 화살표
            var cell = step.cell?.Invoke();
            worldPointer.gameObject.SetActive(cell.HasValue);
            if (cell.HasValue)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 0.25f;
                var c = FactoryGrid.CellCenter(cell.Value);
                worldPointer.position = c;
                pointerArrow.transform.position = c + new Vector3(0f, 0.95f + bob, 0f);
                pointerRing.transform.localScale = Vector3.one * (1.15f + 0.1f * Mathf.Sin(Time.unscaledTime * 5f));
            }
            SetUiHighlight(step.ui?.Invoke());
        }

        /// <summary>강조할 버튼 위에 깜빡이는 테두리.</summary>
        void SetUiHighlight(RectTransform target)
        {
            if (target == null)
            {
                if (uiHighlight) uiHighlight.gameObject.SetActive(false);
                return;
            }
            if (!uiHighlight || uiHighlight.parent != target)
            {
                if (uiHighlight) Destroy(uiHighlight.gameObject);
                uiHighlight = UIFactory.Stretch("TutorialHighlight", target, -5f);
                uiHighlightImage = UIFactory.Image(uiHighlight, UIFactory.Accent);
                var outline = uiHighlight.gameObject.AddComponent<Outline>();
                outline.effectColor = UIFactory.Accent;
                outline.effectDistance = new Vector2(3f, -3f);
                uiHighlight.SetAsFirstSibling();
            }
            uiHighlight.gameObject.SetActive(true);
            var col = UIFactory.Accent;
            col.a = 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            uiHighlightImage.color = col;
        }
    }
}
