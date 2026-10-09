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
            new BuildingSeed("cursor", "커서", "자동으로 쿠키를 클릭합니다.", 15, 0.1, new Color(0.85f, 0.85f, 0.9f)),
            new BuildingSeed("grandma", "할머니", "정성껏 쿠키를 구워 주시는 할머니.", 100, 1, new Color(0.9f, 0.6f, 0.7f)),
            new BuildingSeed("farm", "농장", "쿠키 나무를 키웁니다.", 1_100, 8, new Color(0.45f, 0.75f, 0.35f)),
            new BuildingSeed("mine", "광산", "땅속의 쿠키 반죽을 캐냅니다.", 12_000, 47, new Color(0.6f, 0.5f, 0.4f)),
            new BuildingSeed("factory", "공장", "쿠키를 대량 생산합니다.", 130_000, 260, new Color(0.55f, 0.6f, 0.65f)),
            new BuildingSeed("bank", "은행", "쿠키 이자를 받습니다.", 1_400_000, 1_400, new Color(0.95f, 0.8f, 0.3f)),
            new BuildingSeed("temple", "사원", "고대 쿠키 신을 섬깁니다.", 20_000_000, 7_800, new Color(0.85f, 0.55f, 0.3f)),
            new BuildingSeed("wizard_tower", "마법사 탑", "마법으로 쿠키를 소환합니다.", 330_000_000, 44_000, new Color(0.6f, 0.45f, 0.9f)),
            new BuildingSeed("shipment", "우주 화물선", "쿠키 행성에서 쿠키를 실어 옵니다.", 5_100_000_000, 260_000, new Color(0.4f, 0.6f, 0.95f)),
            new BuildingSeed("alchemy_lab", "연금술 실험실", "금을 쿠키로 바꿉니다.", 75_000_000_000, 1_600_000, new Color(1f, 0.85f, 0.45f)),
            new BuildingSeed("portal", "차원 포털", "쿠키 차원으로 가는 문을 엽니다.", 1_000_000_000_000, 10_000_000, new Color(0.75f, 0.35f, 0.75f)),
            new BuildingSeed("time_machine", "타임머신", "과거에서 쿠키를 가져옵니다.", 14_000_000_000_000, 65_000_000, new Color(0.35f, 0.85f, 0.85f)),
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
            var mouseColor = new Color(0.75f, 0.75f, 0.8f);

            void AddCursor(string id, string name, double cost, int required) =>
                list.Add(Upgrade(id, name, "마우스와 커서의 효율이 2배가 됩니다.", cost, b["cursor"].IconColor,
                    UpgradeEffectType.BuildingMultiplier, b["cursor"], true, 0f, b["cursor"], required, 0));

            void AddMouse(string id, string name, double cost, double handmade) =>
                list.Add(Upgrade(id, name, "클릭할 때마다 초당 생산량의 1%를 추가로 얻습니다.", cost, mouseColor,
                    UpgradeEffectType.ClickCpsPercent, null, false, 0.01f, null, 0, handmade));

            void AddBuilding(string id, string name, string target, double cost, int required) =>
                list.Add(Upgrade(id, name, $"{b[target].DisplayName}의 효율이 2배가 됩니다.", cost, b[target].IconColor,
                    UpgradeEffectType.BuildingMultiplier, b[target], false, 0f, b[target], required, 0));

            AddCursor("reinforced_index_finger", "강화 검지", 100, 1);
            AddCursor("carpal_tunnel_cream", "손목 보호 크림", 500, 1);
            AddCursor("ambidextrous", "양손잡이", 10_000, 10);

            AddMouse("plastic_mouse", "플라스틱 마우스", 50_000, 1_000);
            AddMouse("iron_mouse", "철 마우스", 5_000_000, 100_000);
            AddMouse("titanium_mouse", "티타늄 마우스", 500_000_000, 10_000_000);

            AddBuilding("forwards_from_grandma", "할머니의 안부 인사", "grandma", 1_000, 1);
            AddBuilding("steel_rolling_pins", "강철 밀대", "grandma", 5_000, 5);
            AddBuilding("lubricated_dentures", "윤활 틀니", "grandma", 50_000, 25);
            AddBuilding("cheap_hoes", "저렴한 괭이", "farm", 11_000, 1);
            AddBuilding("fertilizer", "쿠키 비료", "farm", 55_000, 5);
            AddBuilding("cookie_trees", "쿠키 나무", "farm", 550_000, 25);
            AddBuilding("sugar_gas", "설탕 가스", "mine", 120_000, 1);
            AddBuilding("megadrill", "메가드릴", "mine", 600_000, 5);
            AddBuilding("ultradrill", "울트라드릴", "mine", 6_000_000, 25);
            AddBuilding("sturdier_belts", "튼튼한 컨베이어", "factory", 1_300_000, 1);
            AddBuilding("night_shifts", "야간 근무", "factory", 6_500_000, 5);
            AddBuilding("taller_tellers", "키 큰 은행원", "bank", 14_000_000, 1);
            AddBuilding("scissor_vaults", "가위 방지 금고", "bank", 70_000_000, 5);
            AddBuilding("golden_idols", "황금 우상", "temple", 200_000_000, 1);
            AddBuilding("pointier_hats", "더 뾰족한 모자", "wizard_tower", 3_300_000_000, 1);
            AddBuilding("vanilla_nebulae", "바닐라 성운", "shipment", 51_000_000_000, 1);
            AddBuilding("antimony", "안티몬", "alchemy_lab", 750_000_000_000, 1);
            AddBuilding("ancient_tablet", "고대 석판", "portal", 10_000_000_000_000, 1);
            AddBuilding("flux_capacitors", "플럭스 축전기", "time_machine", 140_000_000_000_000, 1);

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
