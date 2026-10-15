using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GearPotion.EditorTools
{
    /// <summary>
    /// 기획서(GDD 4장) 밸런스 테이블을 ScriptableObject 에셋으로 생성/갱신한다.
    /// 밸런스·콘텐츠의 원본은 이 파일이며, Setup Project를 다시 실행하면 에셋 값이 덮어써진다.
    /// </summary>
    public static class DefaultContent
    {
        static Color C(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        public static T Upsert<T>(string path, System.Action<T> fill) where T : ScriptableObject
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = !asset;
            if (created) asset = ScriptableObject.CreateInstance<T>();
            fill(asset);
            if (created) AssetDatabase.CreateAsset(asset, path);
            else EditorUtility.SetDirty(asset);
            return asset;
        }

        // ═══════════════ 원재료 (GDD 4.1) ═══════════════

        public static List<IngredientSO> Ingredients(string root)
        {
            var list = new List<IngredientSO>();
            void Add(string id, string name, string cat, int cost, Elements el, float imp, string color, string note,
                     float crushBonus = 0f, float extraChance = 0f, bool unlocked = true, int rp = 0)
            {
                list.Add(Upsert<IngredientSO>($"{root}/Data/Ingredients/{id}.asset", i =>
                {
                    i.id = id; i.displayName = name; i.category = cat; i.cost = cost; i.elements = el; i.impurity = imp;
                    i.color = C(color); i.note = note; i.crushBonus = crushBonus; i.extractBonusChance = extraChance;
                    i.unlockedByDefault = unlocked; i.researchCost = rp;
                }));
            }
            Add("MAT_01", "이슬 맺힌 약초", "식물", 5, new Elements(0, 10, 0, 5, 0), 15, "#6fd36a", "압착 시 액체 수율 120%", extraChance: 0.2f);
            Add("MAT_02", "화염 뿌리", "식물", 8, new Elements(15, 0, 0, 5, 0), 25, "#ff7a3a", "분쇄 시 화 원소 +8 추가", crushBonus: 8f);
            Add("MAT_03", "바람 민들레 홀씨", "식물", 10, new Elements(0, 0, 18, 0, 0), 10, "#c8f5d0", "가벼운 풍 원소. 증류로 농축하세요", unlocked: false, rp: 250);
            Add("MAT_04", "구리빛 마광석", "광물", 12, new Elements(5, 0, 0, 20, 0), 35, "#c9824a", "분쇄 후 증류하면 고농도 지 원액", unlocked: false, rp: 80);
            Add("MAT_05", "동굴 발광 버섯", "균류", 15, new Elements(0, 8, 0, 0, 12), 30, "#b98cff", "에테르 재료. 마나 엘릭서의 핵심", unlocked: false, rp: 100);
            Add("MAT_06", "얼음 정령의 파편", "결정", 28, new Elements(0, 35, 0, 0, 0), 5, "#9be8ff", "고순도 수 원소 (길드 보급품)", unlocked: false, rp: 120);
            Add("MAT_07", "용암 도마뱀의 독샘", "생체", 40, new Elements(30, 0, 0, 0, 15), 45, "#e8413a", "정제 시 최고급 영약, 미정제 시 맹독", unlocked: false, rp: 250);
            return list;
        }

        // ═══════════════ 설비 (GDD 4.2) ═══════════════

        public static List<MachineSO> Machines(string root)
        {
            var list = new List<MachineSO>();
            MachineSO Add(string id, string name, string shortName, MachineKind kind, BuildCategory cat, BuildZone zone, Vector2Int size,
                          int tier, int cost, float mana, float time, string color, string desc, bool unlocked = true, int rp = 0,
                          float param = 0f, float priceBonus = 1f, bool removable = true)
            {
                var m = Upsert<MachineSO>($"{root}/Data/Machines/{id}.asset", m =>
                {
                    m.id = id; m.displayName = name; m.shortName = shortName; m.kind = kind; m.category = cat; m.zone = zone;
                    m.size = size; m.tier = tier; m.cost = cost; m.mana = mana; m.processTime = time; m.color = C(color);
                    m.description = desc; m.unlockedByDefault = unlocked; m.researchCost = rp; m.param = param;
                    m.priceBonus = priceBonus; m.removable = removable;
                });
                list.Add(m);
                return m;
            }
            var one = Vector2Int.one;
            // 물류
            Add("MAC_BELT", "태엽 컨베이어 벨트", "", MachineKind.Belt, BuildCategory.Logistics, BuildZone.Any, one, 1, 5, 0, 0, "#4a3f36",
                "고체 아이템을 초당 1.5타일 이송. 무동력. 드래그로 연속 배치", param: 1.5f);
            Add("MAC_FASTBELT", "고속 태엽 벨트", "", MachineKind.Belt, BuildCategory.Logistics, BuildZone.Any, one, 2, 12, 0, 0, "#3f4a66",
                "초당 3타일 이송하는 T2 벨트", false, 100, param: 3f);
            Add("MAC_SPLITTER", "분배기", "분배", MachineKind.Splitter, BuildCategory.Logistics, BuildZone.Any, one, 1, 20, 0, 0, "#6b5a3a",
                "들어온 아이템을 앞·오른쪽·왼쪽으로 번갈아 분배");
            Add("MAC_SORTER", "스마트 등급 분류기", "분류", MachineKind.Sorter, BuildCategory.Logistics, BuildZone.Any, one, 2, 150, 2, 0, "#3a6b6b",
                "조건(슬러지·등급·종류)에 맞는 아이템은 앞으로, 나머지는 좌/우로. 클릭해 조건 변경", false, 120);
            Add("MAC_CRATE", "원재료 투입구", "투입", MachineKind.Crate, BuildCategory.Logistics, BuildZone.Factory, one, 1, 20, 0, 2f, "#8a6a3a",
                "선택한 원재료를 매입가를 지불하고 2초마다 배출. 클릭해 재료 변경");
            Add("MAC_EXPORT", "납품 상자", "납품", MachineKind.Export, BuildCategory.Logistics, BuildZone.Factory, one, 1, 40, 0, 0, "#7a6a2a",
                "진행 중인 세력 계약에 맞는 포션은 계약 납품으로, 나머지 포션은 도매가(70%)로, 마력 결정은 35G에 판매");
            Add("MAC_TRASH", "폐기통", "폐기", MachineKind.Trash, BuildCategory.Logistics, BuildZone.Factory, one, 1, 10, 0, 0, "#3a3a3a",
                "무엇이든 폐기. 슬러지는 1개당 환경 부담금 5G");
            // 가공·연성
            Add("MAC_CRUSHER", "자동 분쇄기", "분쇄", MachineKind.Crusher, BuildCategory.Processing, BuildZone.Factory, new Vector2Int(1, 2), 1, 60, 3, 3f, "#7a4a3a",
                "고체 원재료 → 분말. 주 원소 x1.3, 불순물 +10%p");
            Add("MAC_EXTRACTOR", "증기 압착 추출기", "추출", MachineKind.Extractor, BuildCategory.Processing, BuildZone.Factory, new Vector2Int(2, 2), 1, 100, 5, 4f, "#3a6a4a",
                "약초·분말 → 액체 원액(출력 포트에 배관 연결). 원소와 불순물 유지");
            Add("MAC_FILTER", "원심 불순물 여과기", "여과", MachineKind.Filter, BuildCategory.Processing, BuildZone.Factory, new Vector2Int(2, 2), 2, 180, 8, 3.5f, "#3a5a7a",
                "원액 불순물 -20%p. 왼쪽 측면으로 슬러지 1개 배출", false, 80);
            Add("MAC_DISTILLER", "증류탑", "증류", MachineKind.Distiller, BuildCategory.Processing, BuildZone.Factory, new Vector2Int(1, 2), 2, 160, 6, 4f, "#5a4a7a",
                "보조 원소를 제거하고 가장 높은 원소만 1.5배 농축", false, 120);
            Add("MAC_CAULDRON", "마도 합성 가마", "합성 가마", MachineKind.Cauldron, BuildCategory.Processing, BuildZone.Factory, new Vector2Int(3, 3), 2, 300, 12, 5f, "#7a3a5a",
                "원액 2개를 섞어 원소를 합산(불순물 평균). 15% 확률 대성공(순도 +8)", false, 160, param: 2f);
            Add("MAC_BOTTLER", "자동 병입 및 라벨기", "병입", MachineKind.Bottler, BuildCategory.Processing, BuildZone.Factory, new Vector2Int(2, 2), 2, 220, 6, 2f, "#4a6a7a",
                "인접 배관의 원액을 유리병(1G)에 담아 포션으로 확정. 원소 조건으로 종류가 결정됨");
            Add("MAC_SLIME", "연금 슬라임 배양조", "슬라임", MachineKind.SlimeVat, BuildCategory.Processing, BuildZone.Factory, new Vector2Int(2, 2), 3, 450, 10, 8f, "#4a7a3a",
                "슬러지 2개를 먹여 마력 결정(35G) 1개 생산", false, 250);
            Add("MAC_COOLER", "얼음 정령석", "얼음석", MachineKind.Cooler, BuildCategory.Processing, BuildZone.Factory, one, 2, 60, 0, 0, "#6fc8e8",
                "맞닿은 가공 설비의 열을 초당 6 식힘. 오버클럭·화염 재료 라인의 과열을 막는다", false, 60);
            Add("MAC_OVERCLOCK", "오버클럭 태엽", "", MachineKind.Overclock, BuildCategory.Processing, BuildZone.Factory, one, 2, 40, 0, 0, "#d9a83a",
                "가공 설비를 클릭해 부착: 처리 속도 150%, 발열 2배. 열 100이면 화재(6초 정지·재료 소실·열 전파). 다시 클릭하면 분리", false, 90);
            // 상점
            Add("MAC_SHELF", "나무 진열대", "진열대", MachineKind.Shelf, BuildCategory.Shop, BuildZone.Shop, one, 1, 30, 0, 0, "#8a5a32",
                "포션 4개 진열. 뒤쪽(위)에서 벨트로 재고 자동 보충. 클릭해 가격 배율 변경", param: 4f);
            Add("MAC_COLDSHELF", "냉장 마법 진열대", "냉장", MachineKind.Shelf, BuildCategory.Shop, BuildZone.Shop, one, 2, 90, 0, 0, "#4a7a9a",
                "신선도 유지로 판매가 +15%", false, 100, param: 4f, priceBonus: 1.15f);
            Add("MAC_VIP", "황금 VIP 쇼케이스", "VIP", MachineKind.Shelf, BuildCategory.Shop, BuildZone.Shop, one, 3, 250, 0, 0, "#c9a227",
                "단일 슬롯, 판매가 +30%. 고급 포션 전용 진열에 추천", false, 200, param: 1f, priceBonus: 1.3f);
            Add("MAC_COUNTER", "마도 자동 계산대", "계산대", MachineKind.Counter, BuildCategory.Shop, BuildZone.Shop, new Vector2Int(2, 1), 1, 0, 0, 0, "#5a3a2a",
                "손님이 결제하는 곳", removable: false);
            // 마력
            Add("MAC_PIPE", "마력 배관", "", MachineKind.Pipe, BuildCategory.Pipes, BuildZone.Any, one, 1, 3, 0, 0, "#8a95a8",
                "원액(액체)을 옮기는 천장 배관 레이어. 벨트 위에 겹쳐 교차 설치 가능. 이어진 배관은 하나의 네트워크가 되어 " +
                "출력 포트에 연결된 설비의 원액을 인접한 여과기·증류탑·가마·병입기로 자동 공급. 드래그로 연속 설치");
            Add("MAC_CORE", "마나 코어", "마나 코어", MachineKind.ManaCore, BuildCategory.Pipes, BuildZone.Factory, new Vector2Int(2, 2), 1, 200, 30, 0, "#2a5a8a",
                "마력 전력망에 30 MP/s 공급. 하루 유지비 30G");
            return list;
        }

        // ═══════════════ 포션 레시피 (GDD 4.3) ═══════════════

        public static List<PotionSO> Potions(string root, out PotionSO murky)
        {
            var list = new List<PotionSO>();
            PotionSO Make(string id, string name, string shortName, int tier, Elements req, int price, string color, string note, float maxImp = 100f) =>
                Upsert<PotionSO>($"{root}/Data/Potions/{id}.asset", p =>
                {
                    p.id = id; p.displayName = name; p.shortName = shortName; p.tier = tier; p.requirement = req;
                    p.basePrice = price; p.color = C(color); p.note = note; p.maxImpurity = maxImp;
                });
            list.Add(Make("POT_HEAL", "하급 치유 물약", "치유", 1, new Elements(0, 10, 0, 5, 0), 18, "#ff5a6e", "약초 추출 → 병입 (튜토리얼)"));
            list.Add(Make("POT_FIRE", "화염 활력제", "활력", 1, new Elements(20, 0, 0, 0, 0), 28, "#ff9a2a", "분쇄 → 추출 → 병입"));
            list.Add(Make("POT_STEEL", "강철 피부 포션", "강철", 2, new Elements(10, 0, 0, 35, 0), 60, "#9aa7b8", "마광석 분쇄·증류 + 화염 원액 합성"));
            list.Add(Make("POT_MANA", "고순도 마나 엘릭서", "마나", 2, new Elements(0, 25, 0, 0, 15), 75, "#5a7cff", "버섯 + 얼음 파편 합성"));
            list.Add(Make("POT_STORM", "폭풍 질주 포션", "질주", 2, new Elements(0, 15, 30, 0, 0), 95, "#5af0a0", "민들레 분쇄·증류 + 얼음 파편 합성"));
            list.Add(Make("POT_DRAGON", "드래곤 브레스 영약", "브레스", 3, new Elements(60, 0, 0, 0, 30), 220, "#d0203a", "독샘 다중 농축 + 2차 여과"));
            list.Add(Make("POT_STONE", "액상 현자의 돌", "현자", 4, new Elements(50, 50, 50, 50, 50), 650, "#ffd84a", "5대 원소 각 50 + 불순물 0%", 0f));
            murky = Make("POT_MURKY", "탁한 물약", "탁함", 0, default, 4, "#6b6a4a", "어떤 레시피에도 맞지 않는 실패작. 암시장 브로커가 산다");
            return list;
        }

        // ═══════════════ 손님 (GDD 2.2 / 3.3) ═══════════════

        public static List<CustomerProfileSO> Customers(string root)
        {
            var list = new List<CustomerProfileSO>();
            void Add(string id, string name, string color, float tolerance, float speed, Grade minGrade, int buy, float pay, float weight,
                     DayPhaseMask phases, int minDay = 1, bool defective = false, float suspicion = 0f)
            {
                list.Add(Upsert<CustomerProfileSO>($"{root}/Data/Customers/{id}.asset", c =>
                {
                    c.id = id; c.displayName = name; c.color = C(color); c.priceTolerance = tolerance; c.moveSpeed = speed;
                    c.minGrade = minGrade; c.buyCount = buy; c.payMultiplier = pay; c.spawnWeight = weight; c.phases = phases;
                    c.minDay = minDay; c.wantsDefective = defective; c.suspicionPerVisit = suspicion;
                }));
            }
            var all = DayPhaseMask.Day | DayPhaseMask.Evening;
            Add("CUS_VILLAGER", "마을 주민", "#8cc0f0", 1.15f, 2.0f, Grade.C, 1, 1f, 5f, all);
            Add("CUS_ROOKIE", "초보 모험가", "#9ad880", 1.25f, 2.6f, Grade.C, 2, 1f, 3f, all);
            Add("CUS_ELITE", "정예 모험가 파티", "#f08c4a", 1.4f, 2.4f, Grade.B, 3, 1f, 2f, DayPhaseMask.Evening, 2);
            Add("CUS_SCHOLAR", "마법 학회 감정사", "#b080f0", 1.6f, 1.6f, Grade.S, 1, 1.2f, 0.8f, DayPhaseMask.Evening, 3);
            Add("CUS_BROKER", "수상한 암시장 브로커", "#4a4a58", 9f, 2.2f, Grade.C, 2, 1f, 0.6f, DayPhaseMask.Evening, 2, true, 12f);
            return list;
        }
    }
}
