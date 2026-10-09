using System.Collections.Generic;
using CookieClicker.Core;
using CookieClicker.Data;
using CookieClicker.Gameplay;
using CookieClicker.Gameplay.Viewmodel;
using CookieClicker.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 1인칭 "건물 구경하기" 월드를 씬에 배치한다. (CookieSceneBuilder의 나머지 절반)
    /// 건물은 회색 박스(블록아웃) 단계라 전부 큐브이고, 나중에 모델로 바꿀 때는 각 Plot의 Built 아래만 교체하면 된다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        // 클리커 카메라(시야 100m)에 절대 보이지 않도록 아래쪽 멀리 둔다.
        private static readonly Vector3 WorldOrigin = new Vector3(0f, -500f, 0f);
        private static readonly int[] TierThresholds = { 5, 10, 25, 50, 100 };

        /// <summary>건물 ID → 월드 위치(x, z). 본진이 (0,0). 동쪽 = 대창, 서쪽 = 낙지, 남쪽 = 새우.</summary>
        private static readonly Dictionary<string, Vector2> PlotPositions = new Dictionary<string, Vector2>
        {
            { "ladle", new Vector2(-8f, 7f) },
            { "part_timer", new Vector2(8f, 7f) },
            { "cow_ranch", new Vector2(30f, 18f) },
            { "mudflat", new Vector2(-30f, 18f) },
            { "shrimp_farm", new Vector2(-14f, -32f) },
            { "gopchang_alley", new Vector2(30f, -12f) },
            { "octopus_trap_fleet", new Vector2(-50f, -4f) },
            { "shrimp_trawler", new Vector2(14f, -55f) },
            { "franchise", new Vector2(36f, -40f) },
            { "giant_octopus_shrine", new Vector2(-54f, 44f) },
            { "space_ranch", new Vector2(54f, 48f) },
            { "parallel_kitchen", new Vector2(0f, 64f) },
        };

        private static void CreateExploreWorld(CookieGame game, GameConfig config, Camera mainCamera,
            CookieClickTarget cookie, BackgroundParts background, UiParts ui)
        {
            var world = new GameObject("ExploreWorld").transform;
            world.position = WorldOrigin;

            CreateGround(world);
            CreateHomeBase(world);

            var plots = new GameObject("Plots").transform;
            plots.SetParent(world, false);
            for (int i = 0; i < config.Buildings.Count; i++)
            {
                var building = config.Buildings[i];
                if (!PlotPositions.TryGetValue(building.Id, out var position))
                {
                    // 위치표에 없는 새 건물은 바깥 원 위에 임시로 놓는다. 위치는 씬에서 옮기면 된다.
                    float angle = i * Mathf.PI * 2f / config.Buildings.Count;
                    position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 75f;
                }
                CreatePlot(plots, game, building, i, position);
            }

            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetParent(world, false);
            spawn.localPosition = new Vector3(0f, 0.1f, -14f);

            var player = CreatePlayer(world, spawn, out var playerCamera);
            var hud = CreateExploreHud(ui.Canvas, game, playerCamera);

            var mode = new GameObject("ExploreMode").AddComponent<ExploreModeController>();
            Wire(mode,
                ("clickerOnly", new[] { mainCamera.gameObject, ui.MainHud, background.Root, cookie.gameObject }),
                ("exploreOnly", new[] { player.gameObject, hud }),
                ("enterButton", ui.ExploreButton),
                ("exitAction", FindAction("UI/Cancel")),
                ("player", player),
                ("spawnPoint", spawn));
        }

        // ------------------------------------------------------------------ 지형

        private static void CreateGround(Transform world)
        {
            var ground = new GameObject("Ground").transform;
            ground.SetParent(world, false);

            Block(PrimitiveType.Cube, "Base", ground, Mat("Ground_Base", new Color(0.62f, 0.66f, 0.5f)),
                new Vector3(0f, -0.5f, 0f), new Vector3(180f, 1f, 180f), collider: true);
            // 구역은 바닥 위에 얇은 판을 깔아 색으로만 구분한다 (충돌은 Base가 담당).
            Block(PrimitiveType.Cube, "Zone_Ranch", ground, Mat("Ground_Ranch", new Color(0.45f, 0.7f, 0.35f)),
                new Vector3(40f, 0.01f, 22f), new Vector3(55f, 0.02f, 70f));
            Block(PrimitiveType.Cube, "Zone_Mudflat", ground, Mat("Ground_Mudflat", new Color(0.5f, 0.4f, 0.3f)),
                new Vector3(-40f, 0.01f, 22f), new Vector3(55f, 0.02f, 70f));
            Block(PrimitiveType.Cube, "Zone_Sea", ground, Mat("Ground_Sea", new Color(0.3f, 0.55f, 0.8f)),
                new Vector3(0f, 0.01f, -60f), new Vector3(180f, 0.02f, 60f));

            CreateWorldLabel(ground, "Sign_Ranch", "대창 지대", new Vector3(18f, 4f, 2f), Quaternion.Euler(0f, 90f, 0f), 90, Color.white);
            CreateWorldLabel(ground, "Sign_Mudflat", "낙지 지대", new Vector3(-18f, 4f, 2f), Quaternion.Euler(0f, -90f, 0f), 90, Color.white);
            CreateWorldLabel(ground, "Sign_Sea", "새우 지대", new Vector3(0f, 4f, -24f), Quaternion.Euler(0f, 180f, 0f), 90, Color.white);

            // 맵 밖으로 떨어지지 않게 보이지 않는 벽
            var walls = new GameObject("Walls").transform;
            walls.SetParent(ground, false);
            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2;
                float sign = i % 2 == 0 ? 1f : -1f;
                var wall = Block(PrimitiveType.Cube, $"Wall{i}", walls, null,
                    alongX ? new Vector3(0f, 5f, 90f * sign) : new Vector3(90f * sign, 5f, 0f),
                    alongX ? new Vector3(180f, 10f, 1f) : new Vector3(1f, 10f, 180f), collider: true);
                Object.DestroyImmediate(wall.GetComponent<MeshRenderer>());
            }
        }

        private static void CreateHomeBase(Transform world)
        {
            var home = new GameObject("Home").transform;
            home.SetParent(world, false);

            Block(PrimitiveType.Cube, "Platform", home, Mat("Home_Platform", new Color(0.85f, 0.75f, 0.6f)),
                new Vector3(0f, 0.2f, 0f), new Vector3(10f, 0.4f, 10f), collider: true);
            Block(PrimitiveType.Cube, "Table", home, Mat("Home_Table", new Color(0.55f, 0.35f, 0.2f)),
                new Vector3(0f, 0.9f, 0f), new Vector3(2.4f, 1f, 2.4f), collider: true);

            // 클리커 화면과 같은 냄비 모델을 본진 테이블 위에 올려 둔다.
            var pot = new GameObject("StewPot").transform;
            pot.SetParent(home, false);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            // 위에서 내려다보는 1인칭 시점이라 클리커 화면처럼 기울이지 않고 똑바로 놓는다.
            AttachModel(pot, modelAsset, Vector3.zero);
            pot.localPosition = new Vector3(0f, 1.55f, 0f);
            pot.localScale = Vector3.one * 1.8f;

            CreateWorldLabel(home, "Sign_Home", "본진: 낙곱새 냄비", new Vector3(0f, 3.6f, 0f), Quaternion.identity, 70, new Color(1f, 0.85f, 0.4f));
        }

        // ------------------------------------------------------------------ 건물 자리

        private static void CreatePlot(Transform parent, CookieGame game, BuildingDefinition building, int index, Vector2 position)
        {
            var plot = new GameObject($"Plot_{building.Id}").transform;
            plot.SetParent(parent, false);
            plot.localPosition = new Vector3(position.x, 0f, position.y);
            // 건물 정면이 본진을 보게 돌린다.
            plot.localRotation = Quaternion.LookRotation(new Vector3(-position.x, 0f, -position.y));

            var built = new GameObject("Built").transform;
            built.SetParent(plot, false);

            float size = 2.5f + index * 0.7f;
            float height = size * 1.1f;
            var bodyMaterial = Mat($"Plot_{building.Id}", building.IconColor);
            var roofMaterial = Mat($"Plot_{building.Id}_Roof", building.IconColor * 0.6f + new Color(0f, 0f, 0f, 0.4f));

            Block(PrimitiveType.Cube, "Body", built, bodyMaterial, new Vector3(0f, height * 0.5f, 0f), new Vector3(size, height, size), collider: true);
            Block(PrimitiveType.Cube, "Roof", built, roofMaterial, new Vector3(0f, height + size * 0.1f, 0f), new Vector3(size * 1.15f, size * 0.2f, size * 1.15f), collider: true);
            Block(PrimitiveType.Cube, "Door", built, roofMaterial, new Vector3(0f, size * 0.3f, size * 0.5f + 0.01f), new Vector3(size * 0.3f, size * 0.6f, 0.05f));

            // 보유 개수가 늘수록 켜지는 상자 더미. 간판 위로 숫자가 올라가는 대신 눈으로 성장이 보이게 한다.
            var tierObjects = new GameObject[TierThresholds.Length];
            for (int t = 0; t < TierThresholds.Length; t++)
            {
                var tier = new GameObject($"Tier_{TierThresholds[t]}").transform;
                tier.SetParent(built, false);
                float radius = size * 0.75f + 1.2f;
                for (int k = 0; k < 2; k++)
                {
                    float angle = (t * 2 + k) * 36f * Mathf.Deg2Rad + Mathf.PI * 0.6f;
                    float crate = 0.6f + t * 0.15f;
                    Block(PrimitiveType.Cube, $"Crate{k}", tier, bodyMaterial,
                        new Vector3(Mathf.Cos(angle) * radius, crate * 0.5f, Mathf.Sin(angle) * radius - size * 0.2f),
                        Vector3.one * crate, rotation: Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f));
                }
                tierObjects[t] = tier.gameObject;
            }

            // 간판은 본진 쪽(건물 정면)에서 읽히도록 180도 돌린다.
            var sign = CreateWorldLabel(built, "Sign", building.DisplayName, new Vector3(0f, height + size * 0.2f + 1.4f, 0f),
                Quaternion.Euler(0f, 180f, 0f), 60, Color.white);

            var view = plot.gameObject.AddComponent<BuildingPlot>();
            Wire(view, ("game", game), ("building", building), ("builtRoot", built.gameObject), ("signText", sign));
            var serialized = new SerializedObject(view);
            var tiers = serialized.FindProperty("tiers");
            tiers.arraySize = TierThresholds.Length;
            for (int t = 0; t < TierThresholds.Length; t++)
            {
                var element = tiers.GetArrayElementAtIndex(t);
                element.FindPropertyRelative("minCount").intValue = TierThresholds[t];
                element.FindPropertyRelative("visual").objectReferenceValue = tierObjects[t];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 아직 안 산 건물은 빈 땅. 실행하면 BuildingPlot이 보유 개수에 맞춰 켜 준다.
            built.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 플레이어

        private static FirstPersonController CreatePlayer(Transform world, Transform spawn, out Camera playerCamera)
        {
            var root = new GameObject("Player");
            root.transform.SetParent(world, false);
            root.transform.localPosition = spawn.localPosition;

            var body = root.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.45f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);

            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(pivot, false);
            playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.fieldOfView = 70f;
            playerCamera.nearClipPlane = 0.03f;
            playerCamera.farClipPlane = 400f;
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
            cameraObject.AddComponent<AudioListener>();

            var controller = root.AddComponent<FirstPersonController>();
            Wire(controller, ("cameraPivot", pivot),
                ("moveAction", FindAction("Player/Move")), ("lookAction", FindAction("Player/Look")),
                ("jumpAction", FindAction("Player/Jump")), ("sprintAction", FindAction("Player/Sprint")));

            CreateViewmodel(cameraObject.transform, controller);

            root.SetActive(false);
            return controller;
        }

        /// <summary>카메라에 붙는 손. 소켓(움직임) → 포즈별 모델(모양) 2단 구조라 모델만 갈아 끼울 수 있다.</summary>
        private static void CreateViewmodel(Transform cameraTransform, FirstPersonController controller)
        {
            var viewmodel = new GameObject("Viewmodel").transform;
            viewmodel.SetParent(cameraTransform, false);

            var left = CreateHandSocket(viewmodel, "LeftHandSocket", new Vector3(-0.24f, -0.2f, 0.45f), 1f);
            var right = CreateHandSocket(viewmodel, "RightHandSocket", new Vector3(0.24f, -0.2f, 0.45f), -1f);

            var animator = viewmodel.gameObject.AddComponent<ViewmodelAnimator>();
            Wire(animator, ("player", controller), ("leftHand", left.transform), ("rightHand", right.transform),
                ("leftPose", left), ("rightPose", right), ("attackAction", FindAction("Player/Attack")));
        }

        /// <param name="side">왼손 1, 오른손 -1 (엄지 방향)</param>
        private static HandPoseSwitcher CreateHandSocket(Transform parent, string name, Vector3 position, float side)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            socket.localPosition = position;
            socket.localRotation = Quaternion.Euler(10f, side * 12f, side * 15f);

            // 임시 손: 만화 흰 장갑. AI로 만든 손이 생기면 Pose_Open / Pose_Fist 아래 모델만 바꾸면 된다.
            var glove = Mat("Hand_Glove", new Color(0.97f, 0.97f, 0.97f));
            var cuff = Mat("Hand_Cuff", new Color(0.95f, 0.75f, 0.3f));

            var open = new GameObject("Pose_Open").transform;
            open.SetParent(socket, false);
            HandPart("Palm", open, glove, new Vector3(0f, 0f, 0f), new Vector3(0.09f, 0.03f, 0.09f));
            for (int f = 0; f < 4; f++)
                HandPart($"Finger{f}", open, glove, new Vector3(-0.03f + f * 0.02f, 0f, 0.08f), new Vector3(0.017f, 0.022f, 0.07f));
            HandPart("Thumb", open, glove, new Vector3(side * 0.055f, 0f, 0.02f), new Vector3(0.022f, 0.022f, 0.05f), Quaternion.Euler(0f, side * -35f, 0f));
            HandPart("Cuff", open, cuff, new Vector3(0f, 0f, -0.06f), new Vector3(0.075f, 0.05f, 0.035f));

            var fist = new GameObject("Pose_Fist").transform;
            fist.SetParent(socket, false);
            HandPart("Palm", fist, glove, new Vector3(0f, 0f, 0f), new Vector3(0.09f, 0.035f, 0.08f));
            HandPart("Knuckles", fist, glove, new Vector3(0f, -0.012f, 0.055f), new Vector3(0.088f, 0.05f, 0.045f));
            HandPart("Thumb", fist, glove, new Vector3(side * 0.025f, 0.02f, 0.05f), new Vector3(0.05f, 0.02f, 0.022f), Quaternion.Euler(0f, side * 20f, 0f));
            HandPart("Cuff", fist, cuff, new Vector3(0f, 0f, -0.06f), new Vector3(0.075f, 0.05f, 0.035f));

            var switcher = socket.gameObject.AddComponent<HandPoseSwitcher>();
            var serialized = new SerializedObject(switcher);
            var poses = serialized.FindProperty("poses");
            poses.arraySize = 2;
            SetPose(poses.GetArrayElementAtIndex(0), HandPose.Open, open.gameObject);
            SetPose(poses.GetArrayElementAtIndex(1), HandPose.Fist, fist.gameObject);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fist.gameObject.SetActive(false);
            return switcher;
        }

        private static void SetPose(SerializedProperty element, HandPose pose, GameObject visual)
        {
            element.FindPropertyRelative("pose").enumValueIndex = (int)pose;
            element.FindPropertyRelative("visual").objectReferenceValue = visual;
        }

        private static void HandPart(string name, Transform parent, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            var part = Block(PrimitiveType.Cube, name, parent, material, position, scale, rotation: rotation);
            // 손이 바닥에 그림자를 드리우면 어색하므로 끈다.
            part.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ 구경하기 UI

        private static GameObject CreateExploreHud(RectTransform canvas, CookieGame game, Camera playerCamera)
        {
            var hud = Stretch(CreateRect("ExploreHud", canvas), Vector2.zero, Vector2.one);

            AddImage(Place(CreateRect("Crosshair", hud), new Vector2(0.5f, 0.5f), new Vector2(12f, 12f)),
                CookieArtFactory.SoftCircle, new Color(1f, 1f, 1f, 0.9f));

            var currency = AddText(TopBand(CreateRect("Currency", hud), 20f, 50f, 30f, 30f), "", 30, Color.white,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            AddOutline(currency);

            var lookInfo = AddText(Place(CreateRect("LookInfo", hud), new Vector2(0.5f, 0.38f), new Vector2(900f, 120f)), "", 32, Cream,
                TextAnchor.UpperCenter, FontStyle.Bold);
            AddOutline(lookInfo);

            AddOutline(AddText(Stretch(CreateRect("Hint", hud), Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 20f), new Vector2(-30f, 70f)),
                "WASD 이동 · Shift 달리기(주먹 쥐고 뛰기) · Space 점프 · 클릭 주먹 · Esc 돌아가기", 22, Color.white, TextAnchor.MiddleCenter));

            Wire(hud.gameObject.AddComponent<ExploreHudView>(),
                ("game", game), ("playerCamera", playerCamera), ("currencyText", currency), ("lookInfoText", lookInfo));

            hud.gameObject.SetActive(false);
            return hud.gameObject;
        }

        // ------------------------------------------------------------------ 헬퍼

        private static InputActionReference FindAction(string mapAndAction)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(InputActionsPath))
            {
                if (asset is InputActionReference reference && reference.action != null &&
                    $"{reference.action.actionMap.name}/{reference.action.name}" == mapAndAction)
                    return reference;
            }
            Debug.LogError($"[CookieSceneBuilder] {InputActionsPath}에서 '{mapAndAction}' 액션을 찾지 못했습니다.");
            return null;
        }

        private static Material Mat(string name, Color color) =>
            CookieArtFactory.GetOrCreateMaterial($"World/{name}", color);

        private static Transform Block(PrimitiveType type, string name, Transform parent, Material material,
            Vector3 position, Vector3 scale, bool collider = false, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;
            return go.transform;
        }

        /// <summary>월드 공간에 떠 있는 글자 (간판). 1 유닛 = 100 픽셀 비율.</summary>
        private static Text CreateWorldLabel(Transform parent, string name, string content, Vector3 position, Quaternion rotation,
            int fontSize, Color color)
        {
            var rt = CreateRect(name, parent);
            rt.gameObject.layer = 0;
            rt.localPosition = position;
            rt.localRotation = rotation;
            rt.localScale = Vector3.one * 0.01f;
            rt.sizeDelta = new Vector2(1200f, 300f);
            rt.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var text = AddText(Stretch(CreateRect("Text", rt), Vector2.zero, Vector2.one), content, fontSize, color,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            text.gameObject.layer = 0;
            AddOutline(text);
            return text;
        }
    }
}
