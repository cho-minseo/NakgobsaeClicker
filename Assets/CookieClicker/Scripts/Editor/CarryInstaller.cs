using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 본진 냄비에서 1인분 낙곱새를 꺼내 들고·내려놓고·던지는 기능을 씬에 설치한다.
    /// - Prefabs/NakgopsaePortion: 가벼운 냄비 모델(LOD2) + 충돌 상자 + Rigidbody + ThrownPortion
    /// - PlayerCamera/CarryPoint(1인칭), Avatar/CarryPoint(3인칭): 드는 위치. 씬에서 옮기면 바로 반영된다
    /// - ExploreHud/CarryPrompt: "[클릭] 낙곱새 들기" 안내 + PotCarrier
    /// 다시 실행하면 위 오브젝트만 새로 만든다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string PortionPrefabName = "NakgopsaePortion";
        /// <summary>1인분 냄비의 가장 긴 변(m)</summary>
        private const float PortionSize = 0.5f;

        [MenuItem("Tools/Cookie Clicker/Install Pot Carrying (낙곱새 들기 설치)")]
        private static void InstallCarryMenu()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            var hud = Object.FindAnyObjectByType<ExploreHudView>(FindObjectsInactive.Include);
            var homePot = FindHomePot();
            if (player == null || hud == null || homePot == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "구경하기 월드(플레이어·본진 냄비)가 없습니다. 먼저 Build Scene으로 씬을 만들어 주세요.", "확인");
                return;
            }
            InstallCarry(player, player.GetComponentInChildren<Camera>(true), hud, homePot);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "낙곱새 들기를 설치했습니다.\n본진 냄비를 보고 클릭: 들기 / 클릭: 던지기 / 우클릭: 내려놓기\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        /// <summary>구경하기 월드 본진 테이블 위의 냄비 (ExploreWorld/Home/StewPot).</summary>
        private static Transform FindHomePot()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "StewPot" && t.parent != null && t.parent.name == "Home") return t;
            }
            return null;
        }

        internal static void InstallCarry(FirstPersonController player, Camera playerCamera, ExploreHudView hud, Transform homePot)
        {
            var avatar = player.GetComponentInChildren<PlayerAvatar>(true);
            var cameraMode = player.GetComponent<CameraModeController>();
            var seats = hud.GetComponentInChildren<SeatInteractor>(true);
            var world = homePot.root;

            foreach (var old in new[]
                     {
                         hud.transform.Find("CarryPrompt"), playerCamera.transform.Find("CarryPoint"),
                         playerCamera.transform.Find("Viewmodel/RightHandSocket/CarryPoint"),
                         avatar != null ? avatar.transform.Find("CarryPoint") : null, FindDeep(avatar != null ? avatar.transform : null, "CarryPoint"),
                         world.Find("DroppedPortions"),
                     })
            {
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var prefab = CreatePortionPrefab();

            // 1인칭: 오른손(RightHandSocket)이 냄비 오른쪽 손잡이를 쥔다. 손 소켓의 자식이라 걷기 휘적임·숨쉬기를 같이 따라간다.
            // 손 소켓은 조금 기울어져 있어서 그 기울기는 되돌리고, 안이 보이게 앞으로 살짝 기울인다.
            var viewmodel = playerCamera.transform.Find("Viewmodel");
            var rightHand = viewmodel != null ? viewmodel.Find("RightHandSocket") : null;
            var firstPerson = new GameObject("CarryPoint").transform;
            if (rightHand != null)
            {
                firstPerson.SetParent(rightHand, false);
                var level = Quaternion.Inverse(rightHand.localRotation) * Quaternion.Euler(26f, 0f, 0f);
                firstPerson.localRotation = level;
                // 냄비 모델은 손잡이가 좌우(±X) 끝에 있다. 오른쪽 손잡이가 주먹 안에 오도록 냄비를 왼쪽으로 밀고,
                // 화면 아래에 잘리지 않게 위·앞으로 올린다 (손이 냄비를 받쳐 든 모양).
                firstPerson.localPosition = level * new Vector3(-0.46f * PortionSize, 0.09f, 0.1f);
            }
            else
            {
                firstPerson.SetParent(playerCamera.transform, false);
                firstPerson.SetLocalPositionAndRotation(new Vector3(0.05f, -0.3f, 0.55f), Quaternion.Euler(20f, 0f, 0f));
            }

            // 3인칭: 캐릭터 오른손이 손잡이를 쥐고, 냄비는 몸 앞쪽으로 나온다. 팔 뼈의 자식이라 걷고 뛸 때 팔과 같이 흔들린다.
            Transform thirdPerson = avatar != null ? CreateThirdPersonCarryPoint(avatar.transform) : null;

            var droppedParent = new GameObject("DroppedPortions").transform;
            droppedParent.SetParent(world, false);

            var hudRect = (RectTransform)hud.transform;
            var root = Stretch(CreateRect("CarryPrompt", hudRect), Vector2.zero, Vector2.one);
            // 앉기(0.42)·건물 보기(0.45) 안내와 겹치지 않게 조금 아래
            var prompt = AddText(Place(CreateRect("Prompt", root), new Vector2(0.5f, 0.36f), new Vector2(700f, 50f)),
                "[클릭] 낙곱새 들기", 28, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(prompt);
            prompt.gameObject.SetActive(false);

            var carrier = root.gameObject.AddComponent<PotCarrier>();
            Wire(carrier, ("player", player), ("playerCamera", playerCamera), ("cameraMode", cameraMode), ("seats", seats),
                ("hands", viewmodel != null ? viewmodel.GetComponent<CookieClicker.Gameplay.Viewmodel.ViewmodelAnimator>() : null),
                ("homePot", homePot), ("portionPrefab", prefab), ("droppedParent", droppedParent),
                ("firstPersonHold", firstPerson), ("thirdPersonHold", thirdPerson),
                ("throwAction", FindAction("Player/Attack")), ("prompt", prompt.gameObject), ("promptText", prompt));
        }

        /// <summary>3인칭에서 든 냄비의 크기(m)</summary>
        private const float ThirdPersonPortionSize = 0.5f;

        /// <summary>
        /// 3인칭: 캐릭터 배 앞에 양손으로 받쳐 든 위치 (캐릭터 방향 기준점의 자식이라 몸이 도는 대로 따라 돈다).
        /// 팔 뼈에 붙이면 팔 동작에 따라 냄비가 세로로 기울어져서, 몸 기준으로 수평을 유지하게 했다.
        /// 위치·크기는 씬의 Avatar/CarryPoint를 옮겨 바로 조정할 수 있다.
        /// </summary>
        private static Transform CreateThirdPersonCarryPoint(Transform avatar)
        {
            var point = new GameObject("CarryPoint").transform;
            point.SetParent(avatar, false);
            point.SetLocalPositionAndRotation(new Vector3(0f, 0.62f, 0.42f), Quaternion.Euler(10f, 0f, 0f));
            float lossy = avatar.lossyScale.x;
            point.localScale = Vector3.one * (ThirdPersonPortionSize / PortionSize / Mathf.Max(0.0001f, lossy));
            return point;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name && t != root) return t;
            return null;
        }

        /// <summary>
        /// 1인분 낙곱새 프리팹. 손에 들고 던지는 물건이라 가벼운 LOD2(약 1.2만 삼각형) 모델을 쓰고,
        /// 충돌은 모델 크기에 맞춘 상자 하나로 단순하게 한다.
        /// </summary>
        private static GameObject CreatePortionPrefab()
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{LodFolder}/Nakgopsae_LOD2.glb")
                             ?? AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            var root = new GameObject(PortionPrefabName);
            AttachModel(root.transform, modelAsset, Vector3.zero); // 가장 긴 변 1, 중심이 원점

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
            root.transform.localScale = Vector3.one * PortionSize;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1.2f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            root.AddComponent<ThrownPortion>();

            EditorFolders.Ensure(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{PortionPrefabName}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
