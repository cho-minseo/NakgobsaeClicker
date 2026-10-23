using System.Linq;
using CookieClicker.Data;
using CookieClicker.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 상점 칸 등급 색: 건물 카드·업그레이드 칸 프리팹에 "그림 뒤 등급 칸"과 "영웅·전설 빛"을 넣고,
    /// 업그레이드 에셋에 가격 순으로 등급을 채운다. 프리팹을 고치므로 씬의 모든 칸에 한 번에 반영된다.
    /// 여러 번 실행해도 안전하다 (이미 있는 칸은 다시 만들지 않음). 단, 업그레이드 등급은 가격 순으로 다시 채운다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string RarityGlowName = "RarityGlow";
        private const string RarityTileName = "RarityTile";

        [MenuItem("Tools/Cookie Clicker/Install Rarity Colors (등급 색 설치)", priority = 26)]
        private static void InstallRarityMenu()
        {
            EditPrefab($"{PrefabFolder}/BuildingRow.prefab", AddBuildingRowRarity);
            EditPrefab($"{PrefabFolder}/UpgradeRow.prefab", AddUpgradeRowRarity);
            int upgrades = AssignUpgradeRarities();
            EditorUtility.DisplayDialog("Cookie Clicker",
                $"상점 등급 색을 설치했습니다.\n건물 카드·업그레이드 칸 프리팹, 업그레이드 {upgrades}개 등급 (가격 순)", "확인");
        }

        private static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>건물 카드: 그림 뒤 칸(IconTile)을 등급 색으로 칠하고, 그 뒤에 빛을 깐다.</summary>
        internal static void AddBuildingRowRarity(GameObject root)
        {
            var view = root.GetComponent<BuildingRowView>();
            var tile = root.transform.Find("IconTile") as RectTransform;
            if (view == null || tile == null) return;

            var glow = root.transform.Find(RarityGlowName) as RectTransform;
            if (glow == null)
            {
                glow = CreateRect(RarityGlowName, root.transform);
                glow.anchorMin = glow.anchorMax = tile.anchorMin;
                glow.pivot = new Vector2(0.5f, 0.5f);
                // 칸 가운데에 맞춰 칸보다 크게 (칸 바깥으로 빛이 번지게)
                glow.anchoredPosition = tile.anchoredPosition + new Vector2(tile.sizeDelta.x * (0.5f - tile.pivot.x), 0f);
                glow.sizeDelta = tile.sizeDelta * 1.6f;
                AddGlow(glow);
            }
            glow.SetSiblingIndex(tile.GetSiblingIndex()); // 칸 뒤에 그린다

            Wire(view, ("rarityTile", tile.GetComponent<Image>()), ("rarityGlow", glow.GetComponent<RarityShine>()));
        }

        /// <summary>업그레이드 칸: 그림(Icon) 뒤에 등급 칸을 새로 깔고, 그 뒤에 빛을 깐다.</summary>
        internal static void AddUpgradeRowRarity(GameObject root)
        {
            var view = root.GetComponent<UpgradeRowView>();
            var icon = root.transform.Find("Icon") as RectTransform;
            if (view == null || icon == null) return;

            var tile = root.transform.Find(RarityTileName) as RectTransform;
            if (tile == null)
            {
                // 그림보다 사방 4px 큰 둥근 칸
                tile = Stretch(CreateRect(RarityTileName, root.transform), icon.anchorMin, icon.anchorMax,
                    icon.offsetMin - new Vector2(4f, 4f), icon.offsetMax + new Vector2(4f, 4f));
                AddImage(tile, CookieArtFactory.RoundedRect, Color.white).pixelsPerUnitMultiplier = 0.8f;
            }
            tile.SetSiblingIndex(icon.GetSiblingIndex());

            var glow = root.transform.Find(RarityGlowName) as RectTransform;
            if (glow == null)
            {
                glow = Stretch(CreateRect(RarityGlowName, root.transform), icon.anchorMin, icon.anchorMax,
                    icon.offsetMin - new Vector2(22f, 22f), icon.offsetMax + new Vector2(22f, 22f));
                AddGlow(glow);
            }
            glow.SetSiblingIndex(tile.GetSiblingIndex());

            Wire(view, ("rarityTile", tile.GetComponent<Image>()), ("rarityGlow", glow.GetComponent<RarityShine>()));
        }

        private static void AddGlow(RectTransform glow)
        {
            AddImage(glow, CookieArtFactory.SoftCircle, Color.white);
            glow.gameObject.AddComponent<RarityShine>();
            glow.gameObject.SetActive(false); // 영웅·전설일 때만 상점이 켠다
        }

        /// <summary>업그레이드를 가격 순으로 5등분해 일반 → 전설 등급을 채운다. 채운 개수를 돌려준다.</summary>
        internal static int AssignUpgradeRarities()
        {
            var upgrades = AssetDatabase.FindAssets("t:UpgradeDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(u => u != null)
                .OrderBy(u => u.Cost)
                .ToList();
            int tiers = (int)Rarity.Legendary + 1;
            for (int i = 0; i < upgrades.Count; i++)
            {
                upgrades[i].EditorSetRarity((Rarity)Mathf.Min(tiers - 1, i * tiers / Mathf.Max(1, upgrades.Count)));
                EditorUtility.SetDirty(upgrades[i]);
            }
            AssetDatabase.SaveAssets();
            return upgrades.Count;
        }
    }
}
