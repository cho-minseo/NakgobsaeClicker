using System.Collections.Generic;
using CookieClicker.Data;
using UnityEditor;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 상점 그림 연결: 우리 3D 모델을 Blender로 렌더한 썸네일(Art/Thumbnails)을 건물·업그레이드 에셋에 넣는다.
    /// 그림이 없는 건물은 색 칸 + 이름 첫 글자로 보이고, 업그레이드는 대상 건물의 그림을 따라간다.
    /// 새 썸네일을 만들면 아래 표에 추가하고 메뉴를 다시 실행하면 된다 (여러 번 실행해도 안전).
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string ThumbnailFolder = "Assets/CookieClicker/Art/Thumbnails";

        private static readonly Dictionary<string, string> BuildingThumbnails = new()
        {
            ["ladle"] = "Ladle",
            ["part_timer"] = "Staff",
            ["mudflat"] = "Octopus",
            ["shrimp_farm"] = "Shrimp",
        };

        /// <summary>대상 건물이 없는 업그레이드(냄비 젓기 강화)에 쓸 그림</summary>
        private const string ClickUpgradeThumbnail = "Ladle";

        [MenuItem("Tools/Cookie Clicker/Assign Shop Icons (상점 그림 연결)", priority = 26)]
        private static void AssignShopIconsMenu()
        {
            AssignShopIcons();
            EditorUtility.DisplayDialog("Cookie Clicker", "상점 그림을 연결했습니다.", "확인");
        }

        internal static void AssignShopIcons()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:BuildingDefinition"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                asset.EditorSetIcon(BuildingThumbnails.TryGetValue(asset.Id, out var file) ? LoadThumbnail(file) : null);
                EditorUtility.SetDirty(asset);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UpgradeDefinition"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                // 대상 건물이 있으면 비워 둬서 건물 그림을 따라가게 한다.
                asset.EditorSetIcon(asset.TargetBuilding == null ? LoadThumbnail(ClickUpgradeThumbnail) : null);
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>썸네일 PNG를 UI용 스프라이트로 불러온다 (처음이면 스프라이트로 임포트 설정).</summary>
        private static Sprite LoadThumbnail(string file)
        {
            string path = $"{ThumbnailFolder}/{file}.png";
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[ShopIcons] {path} 썸네일이 없습니다.");
            return sprite;
        }
    }
}
