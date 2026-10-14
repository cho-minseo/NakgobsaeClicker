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
                if (plot.Building == null) continue;
                if (plot.Building.Id == "ladle" && AddLadleLandmark(plot, true)) count++;
                if (AddModelLandmark(plot, true)) count++;
            }
            EditorUtility.DisplayDialog("Cookie Clicker",
                count > 0 ? $"건물 모델 {count}개를 설치했습니다.\nCtrl+S로 씬을 저장해 주세요."
                          : "설치하지 못했습니다.\n씬에 Plot_* 건물이 있는지, Models 폴더의 모델이 임포트됐는지 확인해 주세요.", "확인");
        }

        /// <summary>
        /// 건물 옆에 세우는 모델: 건물 ID, 오브젝트 이름, 모델 파일, 크기(가장 긴 변, m), 바라보는 방향(도, 0 = 본진 쪽).
        /// 모델을 추가하려면 여기에 한 줄만 넣으면 된다.
        /// </summary>
        private static readonly (string buildingId, string name, string modelPath, float length, float yaw)[] ModelLandmarks =
        {
            ("part_timer", "Landmark_Staff", "Assets/CookieClicker/Models/Staff.glb", 1.7f, -20f),
            ("mudflat", "Landmark_Octopus", "Assets/CookieClicker/Models/Octopus.glb", 3.2f, 35f),
            ("shrimp_farm", "Landmark_Shrimp", "Assets/CookieClicker/Models/Shrimp.glb", 2.4f, 60f),
        };

        /// <summary>
        /// 건물 앞쪽 비스듬한 자리(상자 더미 바깥)에 모델을 땅에 딛고 서게 놓는다. 이미 있으면 새로 만든다.
        /// 위치·크기·방향은 씬에서 Landmark_* 오브젝트를 옮겨 조정하면 된다.
        /// </summary>
        internal static bool AddModelLandmark(BuildingPlot plot, bool undo = false)
        {
            if (plot.Building == null) return false;
            int index = System.Array.FindIndex(ModelLandmarks, l => l.buildingId == plot.Building.Id);
            if (index < 0) return false;
            var (_, name, modelPath, length, yaw) = ModelLandmarks[index];

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var built = new SerializedObject(plot).FindProperty("builtRoot").objectReferenceValue as GameObject;
            if (modelAsset == null || built == null) return false;

            var old = built.transform.Find(name);
            if (old != null)
            {
                if (undo) Undo.DestroyObjectImmediate(old.gameObject);
                else Object.DestroyImmediate(old.gameObject);
            }

            // 꺼진 오브젝트는 Renderer.bounds가 비어서 크기를 못 재므로, 재는 동안만 켠다.
            bool wasActive = built.activeSelf;
            built.SetActive(true);

            var root = new GameObject(name).transform;
            root.SetParent(built.transform, false);
            AttachModel(root, modelAsset, Vector3.zero);
            root.localScale = Vector3.one * length;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // 상자 더미(반지름 = 건물 크기 * 0.75 + 1.2)보다 바깥, 정면에서 54도 옆 (상자 사이 빈 각도)
            var body = built.transform.Find("Body");
            float buildingSize = body != null ? body.localScale.x : 3f;
            float distance = buildingSize * 0.75f + 1.2f + length * 0.5f + 0.6f;
            float angle = 54f * Mathf.Deg2Rad;
            root.localPosition = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

            // 모델 바닥이 땅(건물 터 높이)에 닿게 올린다.
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                root.position += Vector3.up * (built.transform.position.y - bounds.min.y);
            }
            built.SetActive(wasActive);

            if (undo)
            {
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Install Building Models");
                EditorSceneManager.MarkSceneDirty(plot.gameObject.scene);
            }
            return true;
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
