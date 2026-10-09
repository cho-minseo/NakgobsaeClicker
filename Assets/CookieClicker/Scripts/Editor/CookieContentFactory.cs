using System.Collections.Generic;
using System.Linq;
using CookieClicker.Data;
using UnityEditor;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 기본 건물·업그레이드 데이터 에셋을 만든다. 이미 있는 에셋은 덮어쓰지 않으므로
    /// 인스펙터에서 바꾼 밸런스 수치는 씬을 다시 만들어도 유지된다.
    /// </summary>
    public static class CookieContentFactory
    {
        public const string DataRoot = "Assets/CookieClicker/Data";
        private const string BuildingFolder = DataRoot + "/Buildings";
        private const string UpgradeFolder = DataRoot + "/Upgrades";
        private const string ConfigPath = DataRoot + "/GameConfig.asset";

        private readonly struct BuildingSeed
        {
            public readonly string Id, Name, Description;
            public readonly double Cost, Cps;
            public readonly Color Color;

            public BuildingSeed(string id, string name, string description, double cost, double cps, Color color)
            {
                Id = id; Name = name; Description = description; Cost = cost; Cps = cps; Color = color;
            }
        }

        private static readonly BuildingSeed[] Buildings =
        {
            new BuildingSeed("ladle", "자동 국자", "알아서 냄비를 저어 줍니다. 손목 보호는 덤.", 15, 0.1, new Color(0.80f, 0.82f, 0.86f)),
            new BuildingSeed("part_timer", "알바생", "사장님 눈치 보면서 열심히 볶는 중.", 100, 1, new Color(0.95f, 0.60f, 0.35f)),
            new BuildingSeed("cow_ranch", "소 목장", "무표정한 소 '음메에바'가 대창을 책임집니다.", 1_100, 8, new Color(0.55f, 0.80f, 0.40f)),
            new BuildingSeed("mudflat", "갯벌 체험장", "손을 넣으면 낙지가 딸려 나옵니다. 가끔 손가락도 물림.", 12_000, 47, new Color(0.60f, 0.48f, 0.38f)),
            new BuildingSeed("shrimp_farm", "새우 양식장", "허리 굽은 새우들이 쑥쑥 자랍니다. 허리는 원래 굽어 있음.", 130_000, 260, new Color(1.00f, 0.55f, 0.45f)),
            new BuildingSeed("gopchang_alley", "곱창 골목", "지나가기만 해도 배고파지는 골목.", 1_400_000, 1_400, new Color(0.85f, 0.35f, 0.30f)),
            new BuildingSeed("octopus_trap_fleet", "낙지 통발 선단", "통발 100개 투척 완료. 낙지 회장님은 매번 탈출 시도.", 20_000_000, 7_800, new Color(0.70f, 0.35f, 0.55f)),
            new BuildingSeed("shrimp_trawler", "원양 새우잡이배", "먼바다까지 나가서 새우를 잡아 옵니다. 선장님 뱃멀미 심함.", 330_000_000, 44_000, new Color(0.35f, 0.55f, 0.90f)),
            new BuildingSeed("franchise", "낙곱새 프랜차이즈", "전국 어디서나 같은 맛. 가맹 문의 폭주.", 5_100_000_000, 260_000, new Color(1.00f, 0.75f, 0.25f)),
            new BuildingSeed("giant_octopus_shrine", "대왕낙지 신전", "심해의 대왕낙지가 다리 8개로 동시에 요리합니다.", 75_000_000_000, 1_600_000, new Color(0.55f, 0.30f, 0.75f)),
            new BuildingSeed("space_ranch", "우주 소 방목장", "무중력에서 키운 소는 대창이 더 쫄깃하다는 소문.", 1_000_000_000_000, 10_000_000, new Color(0.40f, 0.85f, 0.90f)),
            new BuildingSeed("parallel_kitchen", "평행세계 낙곱새집", "다른 세계선의 내가 끓인 낙곱새를 가져옵니다.", 14_000_000_000_000, 65_000_000, new Color(0.75f, 0.95f, 0.50f)),
        };

        public static GameConfig CreateOrLoadContent()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (existing != null) return existing;

            EditorFolders.Ensure(BuildingFolder);
            EditorFolders.Ensure(UpgradeFolder);

            var buildings = new Dictionary<string, BuildingDefinition>();
            foreach (var seed in Buildings)
            {
                var building = LoadOrCreate<BuildingDefinition>($"{BuildingFolder}/Building_{seed.Id}.asset", b =>
                    b.EditorInitialize(seed.Id, seed.Name, seed.Description, seed.Cost, seed.Cps, seed.Color));
                buildings[seed.Id] = building;
            }

            var upgrades = CreateUpgrades(buildings);

            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.EditorSetContent(
                Buildings.Select(s => buildings[s.Id]).ToArray(),
                upgrades.OrderBy(u => u.Cost).ToArray());
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static List<UpgradeDefinition> CreateUpgrades(Dictionary<string, BuildingDefinition> b)
        {
            var list = new List<UpgradeDefinition>();
            var stirColor = new Color(1f, 0.55f, 0.25f);

            void AddLadle(string id, string name, string description, double cost, int required) =>
                list.Add(Upgrade(id, name, description, cost, b["ladle"].IconColor,
                    UpgradeEffectType.BuildingMultiplier, b["ladle"], true, 0f, b["ladle"], required, 0));

            void AddStir(string id, string name, string description, double cost, double handmade) =>
                list.Add(Upgrade(id, name, description, cost, stirColor,
                    UpgradeEffectType.ClickCpsPercent, null, false, 0.01f, null, 0, handmade));

            void AddBuilding(string id, string name, string description, string target, double cost, int required) =>
                list.Add(Upgrade(id, name, description, cost, b[target].IconColor,
                    UpgradeEffectType.BuildingMultiplier, b[target], false, 0f, b[target], required, 0));

            AddLadle("stainless_ladle", "스테인리스 국자", "젓기·국자 x2 · 녹슬지 않는다. 의지도.", 100, 1);
            AddLadle("wrist_guard", "손목 보호대", "젓기·국자 x2 · 손목은 소중하니까.", 500, 1);
            AddLadle("dual_ladle", "양손 국자 스킬", "젓기·국자 x2 · 양손이면 두 배. 수학적으로 완벽.", 10_000, 10);

            AddStir("snap_stir", "킹받는 스냅", "저을 때 초당 생산량 +1% · 손목에서 킹받는 소리가 난다.", 50_000, 1_000);
            AddStir("letsgo_wrist", "가보자고 손목", "저을 때 초당 생산량 +1% · 가보자고!", 5_000_000, 100_000);
            AddStir("legendary_wok", "전설의 웍질", "저을 때 초당 생산량 +1% · 불 쇼는 덤.", 500_000_000, 10_000_000);

            AddBuilding("weekly_bonus", "주휴수당 지급", "알바생 x2 · 사기 급상승.", "part_timer", 1_000, 1);
            AddBuilding("new_apron", "새 앞치마", "알바생 x2 · 기름 튀어도 괜찮아.", "part_timer", 5_000, 5);
            AddBuilding("manager_promotion", "알바생 점장 승진", "알바생 x2 · 책임감과 한숨이 같이 늘었다.", "part_timer", 50_000, 25);
            AddBuilding("cow_bed", "음메에바 전용 침대", "소 목장 x2 · 음메에바가 웃었다. (착각일 수도)", "cow_ranch", 11_000, 1);
            AddBuilding("classical_music", "클래식 음악 틀어주기", "소 목장 x2 · 소가 행복하면 대창도 행복하다.", "cow_ranch", 55_000, 5);
            AddBuilding("cow_massage", "소 전용 마사지", "소 목장 x2 · 음메에바: \"...음메.\"", "cow_ranch", 550_000, 25);
            AddBuilding("new_boots", "새 장화", "갯벌 x2 · 더 이상 갯벌에 신발을 뺏기지 않는다.", "mudflat", 120_000, 1);
            AddBuilding("mudflat_map", "갯벌 보물지도", "갯벌 x2 · X 표시 아래 낙지가 있다. 대충.", "mudflat", 600_000, 5);
            AddBuilding("octopus_dog", "낙지 탐지견", "갯벌 x2 · 코가 좋다. 근데 자꾸 물린다.", "mudflat", 6_000_000, 25);
            AddBuilding("oxygen_pump", "산소 펌프 교체", "새우 양식장 x2 · 새우들이 깊게 숨 쉰다.", "shrimp_farm", 1_300_000, 1);
            AddBuilding("shrimp_stretching", "새우 허리 스트레칭", "새우 양식장 x2 · 허리 펴기 실패. 원래 굽었음.", "shrimp_farm", 6_500_000, 5);
            AddBuilding("briquette_fire", "연탄불 화력 강화", "곱창 골목 x2 · 불맛 +100.", "gopchang_alley", 14_000_000, 1);
            AddBuilding("neon_sign", "네온사인 간판", "곱창 골목 x2 · 멀리서도 보인다. 눈이 부시다.", "gopchang_alley", 70_000_000, 5);
            AddBuilding("golden_trap", "황금 통발", "낙지 통발 선단 x2 · 낙지 회장님도 탈출 포기.", "octopus_trap_fleet", 200_000_000, 1);
            AddBuilding("seasick_pills", "선장님 멀미약", "원양 새우잡이배 x2 · 선장님이 드디어 바다를 즐긴다.", "shrimp_trawler", 3_300_000_000, 1);
            AddBuilding("secret_sauce", "비밀 양념 레시피", "프랜차이즈 x2 · 금고 비번은 1234.", "franchise", 51_000_000_000, 1);
            AddBuilding("octopus_prayer", "대왕낙지 기도문", "대왕낙지 신전 x2 · 다리 8개로 동시에 합장.", "giant_octopus_shrine", 750_000_000_000, 1);
            AddBuilding("zero_g_feed", "무중력 여물", "우주 소 방목장 x2 · 여물도 소도 떠다닌다.", "space_ranch", 10_000_000_000_000, 1);
            AddBuilding("worldline_device", "세계선 수렴 장치", "평행세계 낙곱새집 x2 · 모든 세계선에서 낙곱새는 맛있다.", "parallel_kitchen", 140_000_000_000_000, 1);

            return list;
        }

        private static UpgradeDefinition Upgrade(string id, string name, string description, double cost, Color color,
            UpgradeEffectType type, BuildingDefinition target, bool affectsClick, float cpsPercent,
            BuildingDefinition required, int requiredCount, double handmade)
        {
            return LoadOrCreate<UpgradeDefinition>($"{UpgradeFolder}/Upgrade_{id}.asset", u =>
                u.EditorInitialize(id, name, description, cost, color, type, target, 2f, affectsClick,
                    cpsPercent, required, requiredCount, handmade));
        }

        private static T LoadOrCreate<T>(string path, System.Action<T> initialize) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            initialize(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
