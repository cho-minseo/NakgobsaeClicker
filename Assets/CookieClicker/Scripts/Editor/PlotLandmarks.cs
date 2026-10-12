using CookieClicker.FX;
using CookieClicker.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 건물 자리(Built) 옆에 그 건물을 상징하는 3D 모델(랜드마크)을 세운다.
    /// Built 아래에 넣기 때문에 건물을 하나라도 사야 같이 보인다. 위치·크기는 씬에서 Landmark_* 오브젝트를 옮겨 조정하면 된다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string LadleModelPath = "Assets/CookieClicker/Models/Ladle.glb";

        [MenuItem("Tools/Cookie Clicker/Install Building Models (건물 모델 설치)")]
        private static void InstallBuildingModelsMenu()
        {
            int count = 0;
            foreach (var plot in Object.FindObjectsByType<BuildingPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (plot.Building != null && plot.Building.Id == "ladle" && AddLadleLandmark(plot, true)) count++;
            }
            EditorUtility.DisplayDialog("Cookie Clicker",
                count > 0 ? "자동 국자 건물 둘레를 도는 국자를 설치했습니다.\nCtrl+S로 씬을 저장해 주세요."
                          : $"설치하지 못했습니다.\n씬에 Plot_ladle이 있는지, {LadleModelPath}이 임포트됐는지 확인해 주세요.", "확인");
        }

        /// <summary>
        /// 자동 국자 건물 몸통 둘레를 큰 국자가 빙빙 돌게 한다. 이미 있으면 새로 만든다.
        /// LadleOrbit(건물 중심, Orbiter가 회전) → Landmark_Ladle(반지름만큼 떨어진 국자) 구조라
        /// 속도는 LadleOrbit의 Orbiter에서, 반지름·높이·기울기는 Landmark_Ladle에서 조정한다.
        /// </summary>
        internal static bool AddLadleLandmark(BuildingPlot plot, bool undo = false)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(LadleModelPath);
            var built = new SerializedObject(plot).FindProperty("builtRoot").objectReferenceValue as GameObject;
            if (modelAsset == null || built == null) return false;

            foreach (var oldName in new[] { "LadleOrbit", "Landmark_Ladle" })
            {
                var old = built.transform.Find(oldName);
                if (old == null) continue;
                if (undo) Undo.DestroyObjectImmediate(old.gameObject);
                else Object.DestroyImmediate(old.gameObject);
            }

            var orbit = new GameObject("LadleOrbit").transform;
            orbit.SetParent(built.transform, false);
            var orbiter = orbit.gameObject.AddComponent<Orbiter>();
            // 국자 50개마다 조금씩 빨라진다. 팝업(F)에서 "처음 속도"로 되돌릴 수 있다.
            Wire(orbit.gameObject.AddComponent<OrbitSpeedByCount>(), ("plot", plot), ("orbiter", orbiter));

            var root = new GameObject("Landmark_Ladle").transform;
            root.SetParent(orbit, false);
            AttachModel(root, modelAsset, Vector3.zero);

            // 모델은 가장 긴 변이 1로 맞춰지므로 scale이 곧 국자 길이(m)다.
            const float length = 5f;
            root.localScale = Vector3.one * length;
            // 모델 그대로의 방향(회전 0). 국자 끝이 상자 더미 위로 지나가도록 띄운다.
            root.localPosition = new Vector3(2.8f, 1.4f + length * 0.5f, 0f);
            root.localRotation = Quaternion.identity;

            if (undo)
            {
                Undo.RegisterCreatedObjectUndo(orbit.gameObject, "Install Building Models");
                EditorSceneManager.MarkSceneDirty(plot.gameObject.scene);
            }
            return true;
        }
    }
}
