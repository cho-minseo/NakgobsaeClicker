using System.Collections.Generic;
using System.Text.RegularExpressions;
using CookieClicker.Data;
using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 영어판 설치: 건물·업그레이드 에셋에 영어 이름·설명을 채우고, 씬에 고정된 글자에 LocalizedText를 붙인다.
    /// 코드가 매번 새로 쓰는 글자(보유량, 통계, 건물 줄 등)는 코드에서 Loc.T로 고르므로 여기서는 건드리지 않는다.
    /// 문구를 고치려면 아래 표를 고치고 메뉴를 다시 실행하면 된다 (여러 번 실행해도 안전).
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private static readonly Dictionary<string, (string name, string description)> BuildingEnglish = new()
        {
            ["ladle"] = ("Auto Ladle", "Stirs the pot all by itself. Saves your wrists, too."),
            ["part_timer"] = ("Part-Timer", "Stir-frying hard while dodging the boss's glare."),
            ["cow_ranch"] = ("Cow Ranch", "Poker-faced cow 'Moomeeba' is in charge of the daechang."),
            ["mudflat"] = ("Mudflat Experience", "Reach in and an octopus comes out with your hand. Sometimes it bites."),
            ["shrimp_farm"] = ("Shrimp Farm", "Hunched shrimp grow up fast. They were always hunched."),
            ["gopchang_alley"] = ("Gopchang Alley", "An alley that makes you hungry just by walking through."),
            ["octopus_trap_fleet"] = ("Octopus Trap Fleet", "100 traps in the water. Chairman Octopus tries to escape every time."),
            ["shrimp_trawler"] = ("Deep-Sea Shrimp Trawler", "Sails far out for shrimp. The captain gets terribly seasick."),
            ["franchise"] = ("Nakgopsae Franchise", "Same taste everywhere. Franchise inquiries are flooding in."),
            ["giant_octopus_shrine"] = ("Giant Octopus Shrine", "The giant octopus of the deep cooks with all eight arms at once."),
            ["space_ranch"] = ("Space Cow Pasture", "Rumor has it zero-g cows make chewier daechang."),
            ["parallel_kitchen"] = ("Parallel-World Kitchen", "Brings in nakgopsae cooked by you from another worldline."),
        };

        private static readonly Dictionary<string, (string name, string description)> UpgradeEnglish = new()
        {
            ["stainless_ladle"] = ("Stainless Ladle", "Stirring & Auto Ladle x2 · Never rusts. Neither does your will."),
            ["wrist_guard"] = ("Wrist Guard", "Stirring & Auto Ladle x2 · Because wrists are precious."),
            ["dual_ladle"] = ("Two-Handed Ladle", "Stirring & Auto Ladle x2 · Two hands, twice as much. Mathematically perfect."),
            ["snap_stir"] = ("Annoying Snap", "Each stir +1% of production per second · Your wrist makes an annoying sound."),
            ["letsgo_wrist"] = ("Let's-Go Wrist", "Each stir +1% of production per second · Let's go!"),
            ["legendary_wok"] = ("Legendary Wok Toss", "Each stir +1% of production per second · Free fire show included."),
            ["weekly_bonus"] = ("Weekly Holiday Pay", "Part-Timer x2 · Morale skyrockets."),
            ["new_apron"] = ("New Apron", "Part-Timer x2 · Oil splashes? No problem."),
            ["manager_promotion"] = ("Promoted to Manager", "Part-Timer x2 · More responsibility, more sighs."),
            ["cow_bed"] = ("Moomeeba's Own Bed", "Cow Ranch x2 · Moomeeba smiled. (Maybe.)"),
            ["classical_music"] = ("Classical Music for Cows", "Cow Ranch x2 · Happy cow, happy daechang."),
            ["cow_massage"] = ("Cow Massage", "Cow Ranch x2 · Moomeeba: \"...moo.\""),
            ["new_boots"] = ("New Rain Boots", "Mudflat x2 · The mud won't steal your shoes anymore."),
            ["mudflat_map"] = ("Mudflat Treasure Map", "Mudflat x2 · Octopus under the X. Roughly."),
            ["octopus_dog"] = ("Octopus Sniffer Dog", "Mudflat x2 · Great nose. Keeps getting bitten, though."),
            ["oxygen_pump"] = ("New Oxygen Pump", "Shrimp Farm x2 · The shrimp breathe deeply."),
            ["shrimp_stretching"] = ("Shrimp Back Stretches", "Shrimp Farm x2 · Failed to straighten up. Always been bent."),
            ["briquette_fire"] = ("Briquette Fire Boost", "Gopchang Alley x2 · Smoky flavor +100."),
            ["neon_sign"] = ("Neon Sign", "Gopchang Alley x2 · Visible from far away. Blinding."),
            ["golden_trap"] = ("Golden Trap", "Octopus Trap Fleet x2 · Even Chairman Octopus gives up escaping."),
            ["seasick_pills"] = ("Captain's Seasick Pills", "Deep-Sea Shrimp Trawler x2 · The captain finally enjoys the sea."),
            ["secret_sauce"] = ("Secret Sauce Recipe", "Franchise x2 · The safe code is 1234."),
            ["octopus_prayer"] = ("Giant Octopus Prayer", "Giant Octopus Shrine x2 · Praying with all eight arms at once."),
            ["zero_g_feed"] = ("Zero-G Fodder", "Space Cow Pasture x2 · The fodder floats. So do the cows."),
            ["worldline_device"] = ("Worldline Converger", "Parallel-World Kitchen x2 · Nakgopsae is delicious in every worldline."),
        };

        /// <summary>씬에 고정된 글자 (한국어 → 영어). 코드가 덮어쓰는 글자는 넣지 않는다.</summary>
        private static readonly Dictionary<string, string> SceneTextEnglish = new()
        {
            ["야르! 낙곱새 맛집"] = "Yarr! Nakgopsae House",
            ["상점"] = "Store",
            ["통계"] = "Stats",
            ["업그레이드"] = "Upgrades",
            ["건물"] = "Buildings",
            ["저장"] = "Save",
            ["구매할 수 있는 업그레이드가 없어요"] = "No upgrades available yet",
            ["건물 구경하기"] = "Explore",
            ["보글보글"] = "Bubble Bubble",
            ["낙곱새"] = "Nakgopsae",
            ["시작하기"] = "Start",
            ["설정"] = "Settings",
            ["종료"] = "Quit",
            ["닫기"] = "Close",
            ["[F] 건물 보기"] = "[F] View building",
            ["닫기 (F / Esc)"] = "Close (F / Esc)",
            ["본진: 낙곱새 냄비"] = "Home: The Nakgopsae Pot",
            ["산소"] = "Air",
        };

        private static readonly Dictionary<string, string> ZoneEnglish = new()
        {
            ["대창 지대"] = "Daechang Zone",
            ["낙지 지대"] = "Octopus Zone",
            ["새우 지대"] = "Shrimp Zone",
        };

        [MenuItem("Tools/Cookie Clicker/Install English (영어 설치)", priority = 25)]
        private static void InstallEnglishMenu()
        {
            var report = InstallEnglish();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Cookie Clicker", report + "\n\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        internal static string InstallEnglish()
        {
            int buildings = 0, upgrades = 0, texts = 0, zones = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:BuildingDefinition"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || !BuildingEnglish.TryGetValue(asset.Id, out var en)) continue;
                asset.EditorSetEnglish(en.name, en.description);
                EditorUtility.SetDirty(asset);
                buildings++;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UpgradeDefinition"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || !UpgradeEnglish.TryGetValue(asset.Id, out var en)) continue;
                asset.EditorSetEnglish(en.name, en.description);
                EditorUtility.SetDirty(asset);
                upgrades++;
            }
            AssetDatabase.SaveAssets();

            var missing = new SortedSet<string>();
            var korean = new Regex("[가-힣]");
            foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // 프리팹 줄(건물·업그레이드 줄)의 글자는 코드가 채우므로 건드리지 않는다.
                if (PrefabUtility.IsPartOfPrefabInstance(text)) continue;
                if (text.GetComponent<LocalizedText>() != null) continue;
                if (SceneTextEnglish.TryGetValue(text.text, out var english))
                {
                    Localize(text, english);
                    texts++;
                }
                else if (korean.IsMatch(text.text))
                {
                    missing.Add(text.text.Replace("\n", " "));
                }
            }

            foreach (var sign in Object.FindObjectsByType<ZoneSign>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(sign);
                string name = serialized.FindProperty("zoneName").stringValue;
                if (!ZoneEnglish.TryGetValue(name, out var english)) continue;
                serialized.FindProperty("zoneNameEn").stringValue = english;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                zones++;
            }

            // 표에 없는 한국어 글자 = 코드가 덮어쓰는 글자(정상)이거나 표에 추가해야 할 글자. 로그로 남겨 확인할 수 있게 한다.
            if (missing.Count > 0) Debug.Log("[InstallEnglish] 표에 없는 한국어 글자 (코드가 채우는 글자면 정상): " + string.Join(" | ", missing));
            return $"영어 문구를 설치했습니다.\n건물 {buildings}개, 업그레이드 {upgrades}개, 화면 글자 {texts}개, 지대 간판 {zones}개";
        }
    }
}
