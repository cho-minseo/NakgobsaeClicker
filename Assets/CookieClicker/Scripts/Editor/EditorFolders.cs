using UnityEditor;

namespace CookieClicker.EditorTools
{
    internal static class EditorFolders
    {
        /// <summary>"Assets/A/B/C"처럼 중첩된 폴더를 AssetDatabase에 등록된 상태로 만든다.</summary>
        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
