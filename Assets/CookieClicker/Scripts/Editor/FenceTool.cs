using CookieClicker.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 건물 자리(BuildingPlot) 둘레에 나무 울타리를 세운다. 정면(본진 쪽)은 출입구로 비워 둔다.
    /// 울타리는 Built 아래에 들어가므로 건물을 사기 전에는 함께 숨겨진다.
    /// 변마다 BoxCollider를 하나만 둬서 기둥·가로대마다 충돌체를 두는 것보다 가볍다.
    /// </summary>
    public static class FenceTool
    {
        private const string MenuPath = "Tools/Cookie Clicker/Add Fence To Selected Plot (울타리 추가)";
        private const float PostSpacing = 1.6f;
        private const float PostHeight = 1.1f;
        private const float GateWidth = 2.6f;
        private static readonly Color WoodColor = new Color(0.55f, 0.38f, 0.22f);

        [MenuItem(MenuPath, priority = 20)]
        private static void AddToSelected()
        {
            var plot = Selection.activeGameObject.GetComponentInParent<BuildingPlot>(true);
            var fence = AddFence(plot);
            Selection.activeGameObject = fence;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanAddToSelected() =>
            Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<BuildingPlot>(true) != null;

        /// <param name="margin">건물 벽에서 울타리까지 거리</param>
        public static GameObject AddFence(BuildingPlot plot, float margin = 2.8f)
        {
            var built = plot.transform.Find("Built");
            var body = CookieSceneBuilder.FindBuildingBody(built);
            float half = body.localScale.x * 0.5f + margin;

            var old = built.Find("Fence");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var fence = new GameObject("Fence").transform;
            fence.SetParent(built, false);
            Undo.RegisterCreatedObjectUndo(fence.gameObject, "Add Fence");

            var wood = CookieArtFactory.GetOrCreateMaterial("World/Fence_Wood", WoodColor);
            float gateHalf = GateWidth * 0.5f;

            // 플롯의 +Z가 본진 쪽(정면). 정면 가운데를 출입구로 비운다.
            Side(fence, "Front_L", new Vector3(-half, 0f, half), new Vector3(-gateHalf, 0f, half), wood);
            Side(fence, "Front_R", new Vector3(gateHalf, 0f, half), new Vector3(half, 0f, half), wood);
            Side(fence, "Back", new Vector3(-half, 0f, -half), new Vector3(half, 0f, -half), wood);
            Side(fence, "Left", new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), wood);
            Side(fence, "Right", new Vector3(half, 0f, -half), new Vector3(half, 0f, half), wood);

            EditorSceneManager.MarkSceneDirty(plot.gameObject.scene);
            return fence.gameObject;
        }

        private static void Side(Transform parent, string name, Vector3 from, Vector3 to, Material wood)
        {
            float length = Vector3.Distance(from, to);
            var side = new GameObject(name).transform;
            side.SetParent(parent, false);
            side.localPosition = (from + to) * 0.5f;
            side.localRotation = Quaternion.LookRotation(to - from);

            int posts = Mathf.Max(2, Mathf.CeilToInt(length / PostSpacing) + 1);
            for (int i = 0; i < posts; i++)
            {
                float z = Mathf.Lerp(-length * 0.5f, length * 0.5f, i / (float)(posts - 1));
                Piece(side, $"Post{i}", wood, new Vector3(0f, PostHeight * 0.5f, z), new Vector3(0.16f, PostHeight, 0.16f));
            }
            Piece(side, "RailLow", wood, new Vector3(0f, 0.45f, 0f), new Vector3(0.07f, 0.1f, length));
            Piece(side, "RailHigh", wood, new Vector3(0f, 0.85f, 0f), new Vector3(0.07f, 0.1f, length));

            var collider = side.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, PostHeight * 0.55f, 0f);
            collider.size = new Vector3(0.3f, PostHeight * 1.1f, length);
        }

        private static void Piece(Transform parent, string name, Material material, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
        }
    }
}
