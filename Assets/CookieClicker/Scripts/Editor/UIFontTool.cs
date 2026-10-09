using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 열린 씬과 프리팹의 모든 Text에 게임 폰트를 적용한다. 씬을 다시 만들지 않고 폰트만 바꾸고 싶을 때 쓴다.
    /// 폰트를 바꾸려면 CookieSceneBuilder.FontPath만 고치고 이 메뉴를 실행하면 된다.
    /// </summary>
    public static class UIFontTool
    {
        [MenuItem("Tools/Cookie Clicker/Apply UI Font (폰트 적용)", priority = 2)]
        public static void ApplyFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(CookieSceneBuilder.FontPath);
            if (font == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", $"{CookieSceneBuilder.FontPath}을 찾지 못했습니다.", "확인");
                return;
            }

            // 프리팹 원본을 먼저 바꾼다. 씬의 프리팹 인스턴스는 원본을 따라가므로 따로 건드리지 않는다 (불필요한 오버라이드 방지).
            int prefabCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/CookieClicker" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                var texts = root.GetComponentsInChildren<Text>(true);
                foreach (var text in texts) Apply(text, font);
                if (texts.Length > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                prefabCount += texts.Length;
            }

            int sceneCount = 0;
            foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(text)) continue;
                Undo.RecordObject(text, "Apply UI Font");
                Apply(text, font);
                sceneCount++;
            }
            EditorSceneManager.MarkAllScenesDirty();

            EditorUtility.DisplayDialog("Cookie Clicker",
                $"폰트를 적용했습니다.\n씬 {sceneCount}개, 프리팹 {prefabCount}개\n\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        private static void Apply(Text text, Font font)
        {
            text.font = font;
            text.fontStyle = AdjustStyle(font, text.fontStyle);
        }

        /// <summary>
        /// 원래 굵은 글꼴(Cafe24 Ohsquare 등)에 Bold를 또 걸면 Unity가 가짜 굵게를 덧씌워 글자가 뭉개진다.
        /// 기본 폰트가 아니면 Bold만 빼고 Italic은 유지한다.
        /// </summary>
        internal static FontStyle AdjustStyle(Font font, FontStyle style)
        {
            if (font == null || font.name == "LegacyRuntime") return style;
            return style switch
            {
                FontStyle.Bold => FontStyle.Normal,
                FontStyle.BoldAndItalic => FontStyle.Italic,
                _ => style,
            };
        }
    }
}
