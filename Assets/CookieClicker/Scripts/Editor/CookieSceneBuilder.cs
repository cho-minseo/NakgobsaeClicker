using System.IO;
using System.Linq;
using CookieClicker.Core;
using CookieClicker.Data;
using CookieClicker.FX;
using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 메뉴 한 번으로 게임 씬·프리팹을 "파일로" 만들어 주는 에디터 도구.
    /// 결과물은 일반 씬/프리팹이라 이후에는 Hierarchy와 Inspector에서 직접 편집하면 된다.
    /// (게임 실행 중에 오브젝트를 만드는 방식이 아니므로 플레이하지 않아도 모든 오브젝트가 보인다)
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string ScenePath = "Assets/CookieClicker/Scenes/CookieClicker.unity";
        private const string PrefabFolder = "Assets/CookieClicker/Prefabs";
        private const string ModelPath = "Assets/CookieClicker/Models/Nakgopsae.glb";
        internal const string FontPath = "Assets/CookieClicker/Fonts/Cafe24Ohsquare.ttf";
        private const string ClickTargetName = "StewPot";
        private const string ModelPivotName = "ModelPivot";
        // 전골냄비는 윗면(+Y)이 위를 보고 있어서, 정면 카메라에서 국물이 보이도록 앞으로 기울인다.
        private static readonly Vector3 ModelRotation = new Vector3(-55f, 0f, 0f);
        private const int UILayer = 5;

        private static readonly Color Cream = new Color(1f, 0.93f, 0.8f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        private static readonly Color SubText = new Color(0.78f, 0.74f, 0.68f);
        private static readonly Color Wood = new Color(0.42f, 0.27f, 0.14f);

        // "동네 평상" 팔레트: 크림 카드 + 갈색 글씨 + 고추장 빨강(살 수 있는 것에만) + 구경하기 월드의 하늘·풀·주황 집
        private static readonly Color CardCream = new Color(0.992f, 0.965f, 0.91f);
        private static readonly Color CreamDeep = new Color(0.953f, 0.902f, 0.8f);
        private static readonly Color Line = new Color(0.9f, 0.83f, 0.7f);
        private static readonly Color Ink = new Color(0.23f, 0.14f, 0.094f);
        private static readonly Color InkSoft = new Color(0.54f, 0.416f, 0.32f);
        private static readonly Color InkFaint = new Color(0.725f, 0.627f, 0.533f);
        private static readonly Color ChiliRed = new Color(0.886f, 0.25f, 0.165f);
        private static readonly Color ChiliDeep = new Color(0.663f, 0.165f, 0.098f);
        private static readonly Color Terracotta = new Color(0.85f, 0.467f, 0.29f);
        private static readonly Color TerracottaLight = new Color(0.88f, 0.585f, 0.415f);
        private static readonly Color TerracottaDeep = new Color(0.66f, 0.32f, 0.17f);
        private static readonly Color Roof = new Color(0.43f, 0.275f, 0.21f);
        private static readonly Color HillFar = new Color(0.66f, 0.79f, 0.6f);
        private static readonly Color HillMid = new Color(0.553f, 0.733f, 0.47f);
        private static readonly Color HillNear = new Color(0.475f, 0.68f, 0.4f);

        private static Font font;

        private struct Prefabs
        {
            public BuildingRowView BuildingRow;
            public UpgradeRowView UpgradeRow;
            public UIParticle FloatingText;
            public UIParticle Crumb;
            public UIParticle RainCookie;
        }

        [MenuItem("Tools/Cookie Clicker/Build Scene (씬 만들기)", priority = 0)]
        public static void BuildScene()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Cookie Clicker",
                    "씬이 이미 있습니다. 다시 만들까요?\n\n데이터 에셋(건물/업그레이드 수치)은 그대로 두고 씬과 프리팹만 새로 만듭니다.",
                    "다시 만들기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BuildSceneWithoutDialogs();
            EditorUtility.DisplayDialog("Cookie Clicker",
                $"씬을 만들었습니다.\n{ScenePath}\n\nPlay 버튼을 눌러 실행해 보세요.", "확인");
        }

        /// <summary>확인 창 없이 씬을 다시 만든다 (배치 모드처럼 창을 띄울 수 없는 곳에서 쓴다).</summary>
        public static void BuildSceneWithoutDialogs()
        {
            font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogWarning($"[CookieSceneBuilder] {FontPath}이 없어 기본 폰트를 사용합니다.");
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            EditorFolders.Ensure(PrefabFolder);
            EditorFolders.Ensure("Assets/CookieClicker/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 새 씬을 열 때 Unity가 쓰지 않는 에셋을 메모리에서 내리므로, 데이터 에셋은 반드시 새 씬을 연 "뒤에"
            // 불러와야 한다. 먼저 불러 두면 이미 내려간 객체를 참조하게 되어 Config 연결이 비어 버린다.
            var config = CookieContentFactory.CreateOrLoadContent();
            AssignShopIcons();
            AssignUpgradeRarities();
            var prefabs = CreatePrefabs();

            var game = new GameObject("GameManager").AddComponent<CookieGame>();
            Wire(game, ("config", config));
            Wire(game.gameObject.AddComponent<DebugCheats>(), ("game", game));

            var camera = CreateCamera();
            CreateLight("Key Light", new Vector3(35f, -25f, 0f), 1.3f, new Color(1f, 0.96f, 0.9f));
            CreateLight("Fill Light", new Vector3(10f, 30f, 0f), 0.45f, new Color(0.85f, 0.9f, 1f));
            CreateEventSystem();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.47f, 0.45f);

            var cookie = CreateCookie(game, camera);
            InstallPotFX(cookie, game);
            var background = CreateBackgroundCanvas(game, camera, prefabs);
            var ui = CreateUICanvas(game, cookie, config, prefabs, background);
            CreateExploreWorld(game, config, camera, cookie, background, ui);
            InstallTitleScreen(ui.MainHud.GetComponent<ResponsiveLayout>(), cookie.GetComponentInChildren<StewPotFX>());
            // 게임 화면의 [설정] 버튼도 타이틀과 같은 설정 창을 연다.
            Wire(ui.Menu, ("settings", Object.FindAnyObjectByType<SettingsView>(FindObjectsInactive.Include)));
            // 영어판 문구 (건물·업그레이드 에셋, 씬의 고정 글자)
            InstallEnglish();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            Selection.activeGameObject = game.gameObject;
        }

        // ------------------------------------------------------------------ 3D

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0f, 0f, -10f);
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.08f, 0.16f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            go.AddComponent<AudioListener>();
            // 3D 쿠키(Collider)도 UI 버튼처럼 EventSystem 클릭을 받게 해 준다.
            go.AddComponent<PhysicsRaycaster>();
            return camera;
        }

        private static void CreateLight(string name, Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        [MenuItem("Tools/Cookie Clicker/Replace Click Model (클릭 모델 교체)", priority = 1)]
        public static void ReplaceClickModel()
        {
            var target = Object.FindAnyObjectByType<CookieClickTarget>();
            if (target == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "열린 씬에 CookieClickTarget이 없습니다.\n먼저 Build Scene으로 씬을 만들어 주세요.", "확인");
                return;
            }
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", $"{ModelPath}을 아직 불러오지 못했습니다.\n임포트가 끝난 뒤 다시 실행해 주세요.", "확인");
                return;
            }

            var oldPivot = target.transform.Find(ModelPivotName);
            if (oldPivot != null) Undo.DestroyObjectImmediate(oldPivot.gameObject);

            var pivot = AttachModel(target.transform, modelAsset);
            Undo.RegisterCreatedObjectUndo(pivot.gameObject, "Replace Click Model");
            Undo.RecordObject(target.gameObject, "Replace Click Model");
            target.gameObject.name = ClickTargetName;

            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Selection.activeGameObject = pivot.gameObject;
            EditorUtility.DisplayDialog("Cookie Clicker",
                "클릭 모델을 교체했습니다.\nCtrl+S로 씬을 저장해 주세요.\n\n각도가 어색하면 ModelPivot의 Rotation을 조절하면 됩니다.", "확인");
        }

        private static CookieClickTarget CreateCookie(CookieGame game, Camera camera)
        {
            var root = new GameObject(ClickTargetName);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"[CookieSceneBuilder] {ModelPath}을 불러오지 못해 임시 모델을 사용합니다. " +
                                 "임포트가 끝난 뒤 Replace Click Model 메뉴로 교체하세요.");
            }
            AttachModel(root.transform, modelAsset);

            root.AddComponent<SphereCollider>().radius = 0.48f;
            var target = root.AddComponent<CookieClickTarget>();
            Wire(target, ("game", game), ("viewCamera", camera));

            // CookieClickTarget.FitToScreen과 같은 계산으로 16:9 기준 위치에 미리 놓아 둔다 (씬 뷰에서도 실제와 같게 보이도록).
            const float distance = 8f;
            float visibleHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * 16f / 9f;
            root.transform.position = camera.transform.position +
                                      new Vector3((0.17f - 0.5f) * visibleWidth, (0.47f - 0.5f) * visibleHeight, distance);
            root.transform.localScale = Vector3.one * Mathf.Min(visibleHeight * 0.42f, visibleWidth * 0.28f);
            return target;
        }

        /// <summary>
        /// root 아래에 ModelPivot을 만들고 모델을 넣는다. 어떤 모델이든 가장 긴 변이 1, 중심이 원점이 되도록 맞추므로
        /// 모델만 바꿔도 화면 크기·클릭 범위(SphereCollider)는 그대로 쓸 수 있다.
        /// </summary>
        private static Transform AttachModel(Transform root, GameObject modelAsset, Vector3? rotation = null)
        {
            // 크기 계산은 월드 기준이라 잠시 root를 원점·기본 크기로 돌려 놓고 계산한 뒤 되돌린다.
            Vector3 position = root.position;
            Quaternion rootRotation = root.rotation;
            Vector3 scale = root.localScale;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;

            var pivot = new GameObject(ModelPivotName).transform;
            pivot.SetParent(root, false);
            pivot.localRotation = Quaternion.Euler(rotation ?? ModelRotation);

            var model = modelAsset != null
                ? PrefabUtility.InstantiatePrefab(modelAsset) as GameObject ?? Object.Instantiate(modelAsset)
                : CreateFallbackCookie();
            model.name = "Model";
            model.transform.SetParent(pivot, false);
            NormalizeToUnitSize(root, pivot);

            root.SetPositionAndRotation(position, rootRotation);
            root.localScale = scale;
            return pivot;
        }

        /// <summary>모델 크기가 제각각이어도 가장 긴 변이 1이 되고 중심이 원점에 오도록 맞춘다.</summary>
        private static void NormalizeToUnitSize(Transform root, Transform pivot)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize <= 0f) return;

            float scale = 1f / maxSize;
            pivot.localScale = Vector3.one * scale;
            pivot.localPosition = -bounds.center * scale;
        }

        private static GameObject CreateFallbackCookie()
        {
            var root = new GameObject("FallbackCookie");
            var dough = CookieArtFactory.GetOrCreateMaterial("FallbackDough", new Color(0.83f, 0.6f, 0.33f));
            var chip = CookieArtFactory.GetOrCreateMaterial("FallbackChip", new Color(0.25f, 0.13f, 0.07f));

            var body = CreatePrimitive(PrimitiveType.Cylinder, "Dough", root.transform, dough);
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.localScale = new Vector3(1f, 0.08f, 1f);

            var random = new System.Random(3);
            for (int i = 0; i < 9; i++)
            {
                var c = CreatePrimitive(PrimitiveType.Sphere, $"Chip{i}", root.transform, chip);
                var p = Random2D(random) * 0.38f;
                c.localPosition = new Vector3(p.x, p.y, 0.07f);
                c.localScale = Vector3.one * 0.12f;
            }
            return root;
        }

        private static Transform CreatePrimitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Vector2 Random2D(System.Random random)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Sqrt((float)random.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        // ------------------------------------------------------------------ 배경 ("동네 평상": 구경하기 월드와 같은 하늘·언덕·집)

        private struct BackgroundParts
        {
            public GameObject Root;
            /// <summary>언덕·집·평상을 담는 칸. 가로는 화면 전체, 세로는 냄비가 있는 위쪽만.</summary>
            public RectTransform Scenery;
            public RectTransform Table;
            /// <summary>냄비 밑 그림자 (냄비 위치를 따라다닌다)</summary>
            public RectTransform PotShadow;
            public RectTransform RainArea;
        }

        private static BackgroundParts CreateBackgroundCanvas(CookieGame game, Camera camera, Prefabs prefabs)
        {
            // Screen Space - Camera 캔버스를 냄비보다 멀리(50) 두어서 3D 냄비 뒤에 그려지게 한다.
            var root = (RectTransform)CreateCanvas("Background Canvas", RenderMode.ScreenSpaceCamera, camera, -100, false).transform;

            AddImage(Stretch(CreateRect("Sky", root), Vector2.zero, Vector2.one), CookieArtFactory.Sky, Color.white);
            AddImage(Place(CreateRect("SunGlow", root), new Vector2(0.48f, 0.5f), new Vector2(520f, 520f)), CookieArtFactory.SoftCircle,
                new Color(1f, 0.93f, 0.78f, 0.9f));
            CreateCloud(root, "Cloud_A", new Vector2(0.12f, 0.8f), new Vector2(210f, 84f), 0f);
            CreateCloud(root, "Cloud_B", new Vector2(0.43f, 0.9f), new Vector2(150f, 60f), 0.4f);
            CreateCloud(root, "Cloud_C", new Vector2(0.86f, 0.84f), new Vector2(180f, 72f), 0.7f);

            // 생산량이 많을수록 하늘에서 낙곱새가 내린다 (원작의 쿠키 비). 언덕·평상보다 뒤에 그린다.
            var rainArea = Stretch(CreateRect("CookieRain", root), Vector2.zero, Vector2.one);
            var rain = rainArea.gameObject.AddComponent<CookieRain>();
            Wire(rain, ("game", game), ("cookiePrefab", prefabs.RainCookie), ("area", rainArea));

            var scenery = Stretch(CreateRect("Scenery", root), Vector2.zero, Vector2.one);
            AddImage(Stretch(CreateRect("Hill_Far", scenery), new Vector2(0f, 0.3f), new Vector2(1f, 0.6f)), CookieArtFactory.HillFar, HillFar);
            CreateHouse(scenery, "House_1", new Vector2(0.1f, 0.47f), new Vector2(54f, 50f), Terracotta);
            CreateHouse(scenery, "House_2", new Vector2(0.17f, 0.465f), new Vector2(38f, 36f), TerracottaLight);
            CreateHouse(scenery, "House_3", new Vector2(0.52f, 0.48f), new Vector2(66f, 60f), TerracottaDeep);
            CreateHouse(scenery, "House_4", new Vector2(0.58f, 0.47f), new Vector2(42f, 40f), Terracotta);
            AddImage(Stretch(CreateRect("Hill_Mid", scenery), new Vector2(0f, 0.27f), new Vector2(1f, 0.47f)), CookieArtFactory.HillMid, HillMid);
            AddImage(Stretch(CreateRect("Hill_Near", scenery), new Vector2(0f, 0.24f), new Vector2(1f, 0.38f)), CookieArtFactory.HillNear, HillNear);

            var table = Stretch(CreateRect("Table", scenery), new Vector2(-0.03f, 0f), new Vector2(1.03f, 0.33f));
            AddImage(table, CookieArtFactory.Table, Color.white);

            var potShadow = Place(CreateRect("PotShadow", root), new Vector2(0.34f, 0.4f), new Vector2(560f, 90f));
            potShadow.pivot = new Vector2(0.5f, 2.4f); // 냄비 중심보다 아래(평상 위)에 깔리도록 기준점을 위로 뺀다
            AddImage(potShadow, CookieArtFactory.SoftCircle, new Color(0.3f, 0.14f, 0.04f, 0.4f));

            return new BackgroundParts { Root = root.gameObject, Scenery = scenery, Table = table, PotShadow = potShadow, RainArea = rainArea };
        }

        private static void CreateCloud(RectTransform parent, string name, Vector2 anchor, Vector2 size, float phase)
        {
            var cloud = Place(CreateRect(name, parent), anchor, size);
            AddImage(cloud, CookieArtFactory.Cloud, new Color(1f, 1f, 1f, 0.92f));
            Wire(cloud.gameObject.AddComponent<UIDrift>(), ("phase", phase), ("distance", 50f + size.x * 0.2f));
        }

        /// <summary>멀리 보이는 집 (구경하기 월드의 주황 박스 건물을 닮게). 아래 가운데가 기준점.</summary>
        private static void CreateHouse(RectTransform parent, string name, Vector2 anchor, Vector2 size, Color color)
        {
            var house = Place(CreateRect(name, parent), anchor, size);
            house.pivot = new Vector2(0.5f, 0f);
            AddImage(house, null, color);
            var roof = Stretch(CreateRect("Roof", house), new Vector2(0f, 1f), Vector2.one,
                new Vector2(-size.x * 0.12f, -2f), new Vector2(size.x * 0.12f, size.y * 0.22f));
            AddImage(roof, null, Roof);
        }

        // ------------------------------------------------------------------ UI

        private struct UiParts
        {
            public RectTransform Canvas;
            /// <summary>클리커 화면 UI 전체. 구경하기 중에는 통째로 꺼진다.</summary>
            public GameObject MainHud;
            public Button ExploreButton;
            public HudMenuView Menu;
        }

        private static UiParts CreateUICanvas(CookieGame game, CookieClickTarget cookie, GameConfig config, Prefabs prefabs,
            BackgroundParts background)
        {
            var canvas = (RectTransform)CreateCanvas("UI Canvas", RenderMode.ScreenSpaceOverlay, null, 0, true).transform;
            var root = Stretch(CreateRect("MainHud", canvas), Vector2.zero, Vector2.one);

            var counter = CreateCounterCard(root, game);
            var news = CreateNewsTicker(root, game);
            var exploreButton = CreateExploreButton(root);
            var stats = CreateStatsWindow(root, game, out var closeStats);
            var store = CreateStorePanel(root, game, config, prefabs);
            var topButtons = CreateTopButtons(root, out var statsButton, out var settingsButton);
            var tabBar = CreateTabBar(root, out var storeTab, out var statsTab);

            // 맨 위: 이펙트 / 황금 낙곱새 / 안내 문구 (배경 이미지가 없어서 클릭을 가로막지 않는다)
            var effectLayer = Stretch(CreateRect("EffectLayer", root), Vector2.zero, Vector2.one);

            var golden = Place(CreateRect("GoldenCookie", effectLayer), new Vector2(0.5f, 0.5f), new Vector2(120f, 120f));
            var goldenImage = AddImage(golden, CookieArtFactory.GoldenCookie, Color.white, true);
            var goldenButton = AddButton(golden, goldenImage);
            var goldenGroup = golden.gameObject.AddComponent<CanvasGroup>();
            var goldenView = golden.gameObject.AddComponent<GoldenCookieView>();
            Wire(goldenView, ("game", game), ("button", goldenButton), ("canvasGroup", goldenGroup), ("spawnArea", effectLayer));

            var popup = Place(CreateRect("Popup", effectLayer), new Vector2(0.34f, 0.8f), new Vector2(700f, 70f));
            var popupText = AddText(popup, "", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddRedOutline(popupText);

            Wire(effectLayer.gameObject.AddComponent<ClickFeedbackView>(),
                ("game", game), ("cookie", cookie), ("effectLayer", effectLayer),
                ("floatingTextPrefab", prefabs.FloatingText), ("crumbPrefab", prefabs.Crumb), ("popupText", popupText));

            var layout = ConfigureResponsiveLayout(root, cookie, goldenView, background,
                counter, stats, store, news, exploreButton, topButtons, tabBar, storeTab, statsTab, popup);

            var menu = root.gameObject.AddComponent<HudMenuView>();
            Wire(menu, ("layout", layout), ("statsButton", statsButton), ("closeStatsButton", closeStats), ("settingsButton", settingsButton));

            return new UiParts
            {
                Canvas = canvas, MainHud = root.gameObject,
                ExploreButton = exploreButton.GetComponent<Button>(), Menu = menu,
            };
        }

        /// <summary>
        /// 가로: 왼쪽 위 숫자 간판, 오른쪽 상점 카드, 아래 속보 띠, 가운데에 평상 위 냄비. 통계는 버튼으로 여는 창.
        /// 세로: 위에 냄비, 아래에 상점/통계 탭.
        /// 값은 ResponsiveLayout 컴포넌트에 데이터로 저장되므로 이후에는 Inspector에서 조정하면 된다.
        /// </summary>
        private static ResponsiveLayout ConfigureResponsiveLayout(RectTransform root, CookieClickTarget cookie, GoldenCookieView golden,
            BackgroundParts background, RectTransform counter, RectTransform stats, RectTransform store, RectTransform news,
            RectTransform explore, RectTransform topButtons, RectTransform tabBar, Button storeTab, Button statsTab, RectTransform popup)
        {
            const float split = 0.56f;   // 세로 화면에서 냄비 영역이 시작되는 높이 비율
            const float tabHeight = 70f;
            const float shopWidth = 624f, margin = 24f;
            const float playRight = shopWidth + margin * 2f; // 가로 화면에서 상점이 차지하는 오른쪽 폭
            var bottom = new RectLayout(Vector2.zero, new Vector2(1f, split), new Vector2(16f, 16f), new Vector2(-16f, -tabHeight - 8f));

            PanelLayout Panel(RectTransform target, RectLayout landscape, RectLayout portrait) =>
                new PanelLayout { target = target, landscape = landscape, portrait = portrait };
            RectLayout Box(Vector2 anchor, Vector2 pivotOffset, Vector2 size) =>
                new RectLayout(anchor, anchor, pivotOffset, pivotOffset + size);

            var panels = new[]
            {
                Panel(counter,
                    Box(new Vector2(0f, 1f), new Vector2(58f, -48f - 236f), new Vector2(470f, 236f)),
                    Box(new Vector2(0.5f, 1f), new Vector2(-235f, -24f - 236f), new Vector2(470f, 236f))),
                Panel(topButtons,
                    Box(new Vector2(1f, 1f), new Vector2(-playRight - 16f - 250f, -48f - 54f), new Vector2(250f, 54f)),
                    Box(new Vector2(1f, 1f), new Vector2(-16f - 250f, -24f - 54f), new Vector2(250f, 54f))),
                Panel(store,
                    new RectLayout(new Vector2(1f, 0f), Vector2.one, new Vector2(-shopWidth - margin, margin), new Vector2(-margin, -margin)),
                    bottom),
                Panel(stats,
                    new RectLayout(Vector2.zero, Vector2.one, new Vector2(80f, 200f), new Vector2(-playRight - 80f, -310f)),
                    bottom),
                Panel(news,
                    new RectLayout(Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(-playRight + margin, 64f)),
                    new RectLayout(new Vector2(0f, split), new Vector2(1f, split), Vector2.zero, new Vector2(0f, 56f))),
                Panel(explore,
                    Box(Vector2.zero, new Vector2(58f, 100f), new Vector2(270f, 76f)),
                    Box(new Vector2(0f, split), new Vector2(16f, 72f), new Vector2(250f, 68f))),
                Panel(tabBar,
                    new RectLayout(new Vector2(0f, split), new Vector2(1f, split), new Vector2(0f, -tabHeight), Vector2.zero),
                    new RectLayout(new Vector2(0f, split), new Vector2(1f, split), new Vector2(0f, -tabHeight), Vector2.zero)),
                Panel(background.Scenery,
                    new RectLayout(Vector2.zero, Vector2.one),
                    new RectLayout(new Vector2(0f, split), Vector2.one)),
                Panel(background.Table,
                    new RectLayout(new Vector2(-0.03f, 0f), new Vector2(1.03f, 0.33f)),
                    new RectLayout(new Vector2(-0.06f, 0f), new Vector2(1.06f, 0.4f))),
                Panel(popup,
                    new RectLayout(new Vector2(0.34f, 0.8f), new Vector2(0.34f, 0.8f), new Vector2(-350f, -35f), new Vector2(350f, 35f)),
                    new RectLayout(new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f), new Vector2(-350f, -35f), new Vector2(350f, 35f))),
            };

            var landscapeCookie = new CookieLayout
            {
                viewportPosition = new Vector2(0.34f, 0.4f),
                heightRatio = 0.5f,
                maxWidthRatio = 0.36f,
                goldenSpawnRect = new Rect(0.04f, 0.15f, 0.58f, 0.6f),
            };
            var portraitCookie = new CookieLayout
            {
                viewportPosition = new Vector2(0.5f, 0.74f),
                heightRatio = 0.3f,
                maxWidthRatio = 0.8f,
                goldenSpawnRect = new Rect(0.06f, 0.6f, 0.88f, 0.28f),
            };

            var layout = root.gameObject.AddComponent<ResponsiveLayout>();
            layout.EditorConfigure(panels, cookie, golden, new[] { background.PotShadow },
                landscapeCookie, portraitCookie, tabBar.gameObject, storeTab, statsTab, store.gameObject, stats.gameObject);
            return layout;
        }

        /// <summary>왼쪽 위 크림색 간판: 가게 이름, 보유량(크게), 초당 생산량. 아래에 버프 문구.</summary>
        private static RectTransform CreateCounterCard(RectTransform root, CookieGame game)
        {
            var card = CreateRect("CounterCard", root);
            AddCard(card, CardCream, CreamDeep, 0.6f, false);
            AddText(TopBand(CreateRect("BakeryName", card), 24f, 38f, 34f, 24f), "야르! 낙곱새 맛집", 28, ChiliRed, TextAnchor.MiddleLeft);
            var countText = AddText(TopBand(CreateRect("CookieCount", card), 64f, 104f, 30f, 24f), "0 낙곱새", 76, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var cpsText = AddText(TopBand(CreateRect("CookiesPerSecond", card), 170f, 40f, 34f, 24f), "초당 0", 26, InkSoft, TextAnchor.MiddleLeft);
            var buffRect = Stretch(CreateRect("Buff", card), Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -64f), new Vector2(240f, -12f));
            var buffText = AddText(buffRect, "", 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            AddRedOutline(buffText);
            Wire(card.gameObject.AddComponent<HudView>(),
                ("game", game), ("cookieCountText", countText), ("cpsText", cpsText), ("buffText", buffText));
            return card;
        }

        /// <summary>오른쪽 위 [통계] [설정] 알약 버튼.</summary>
        private static RectTransform CreateTopButtons(RectTransform root, out Button stats, out Button settings)
        {
            var bar = CreateRect("TopButtons", root);
            stats = CreatePill(bar, "StatsButton", "통계", new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            settings = CreatePill(bar, "SettingsButton", "설정", new Vector2(0.52f, 0f), Vector2.one);
            return bar;
        }

        private static Button CreatePill(RectTransform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = Stretch(CreateRect(name, parent), anchorMin, anchorMax);
            var image = AddImage(rt, CookieArtFactory.RoundedRect, new Color(CardCream.r, CardCream.g, CardCream.b, 0.92f), true);
            image.pixelsPerUnitMultiplier = 0.5f;
            var button = AddButton(rt, image);
            AddText(Stretch(CreateRect("Label", rt), Vector2.zero, Vector2.one), label, 24, Ink, TextAnchor.MiddleCenter);
            return button;
        }

        /// <summary>방송 속보 자막 띠: [● 속보] [동네 소식] | 흘러가는 문장</summary>
        private static RectTransform CreateNewsTicker(RectTransform root, CookieGame game)
        {
            var news = CreateRect("NewsTicker", root);
            AddImage(news, null, Color.white, true);
            AddImage(Stretch(CreateRect("TopLine", news), new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 5f)), null, ChiliRed);

            var tag = Stretch(CreateRect("Tag", news), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(176f, 0f));
            AddImage(tag, CookieArtFactory.SlantTag, ChiliRed);
            var dot = Place(CreateRect("LiveDot", tag), new Vector2(0f, 0.5f), new Vector2(12f, 12f));
            dot.anchoredPosition = new Vector2(34f, 0f);
            AddImage(dot, CookieArtFactory.Dot, Color.white);
            dot.gameObject.AddComponent<Blink>();
            AddText(Stretch(CreateRect("Label", tag), Vector2.zero, Vector2.one, new Vector2(50f, 0f), new Vector2(-24f, 0f)),
                "속보", 30, Color.white, TextAnchor.MiddleLeft);

            AddText(Stretch(CreateRect("Section", news), Vector2.zero, new Vector2(0f, 1f), new Vector2(186f, 0f), new Vector2(316f, 0f)),
                "동네 소식", 22, ChiliDeep, TextAnchor.MiddleCenter);
            AddImage(Stretch(CreateRect("Divider", news), Vector2.zero, new Vector2(0f, 1f), new Vector2(324f, 12f), new Vector2(326f, -12f)), null, Line);

            var clip = Stretch(CreateRect("Clip", news), Vector2.zero, Vector2.one, new Vector2(340f, 0f), Vector2.zero);
            clip.gameObject.AddComponent<RectMask2D>();
            var newsText = AddText(Stretch(CreateRect("Text", clip), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(600f, 0f)),
                "뉴스", 26, Ink, TextAnchor.MiddleLeft);
            newsText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Wire(news.gameObject.AddComponent<NewsTickerView>(), ("game", game), ("newsText", newsText));
            return news;
        }

        private static RectTransform CreateExploreButton(RectTransform root)
        {
            var rt = CreateRect("ExploreButton", root);
            var image = AddCard(rt, Terracotta, TerracottaDeep, 0.7f, true);
            AddButton(rt, image);
            AddText(Stretch(CreateRect("Label", rt), Vector2.zero, Vector2.one), "건물 구경하기 →", 30, Color.white, TextAnchor.MiddleCenter);
            return rt;
        }

        private static RectTransform CreateTabBar(RectTransform root, out Button storeTab, out Button statsTab)
        {
            var bar = CreateRect("TabBar", root);
            AddImage(bar, null, CreamDeep);
            storeTab = CreateTab(bar, "StoreTab", "상점", Vector2.zero, new Vector2(0.5f, 1f), new Vector2(8f, 8f), new Vector2(-4f, -8f));
            statsTab = CreateTab(bar, "StatsTab", "통계", new Vector2(0.5f, 0f), Vector2.one, new Vector2(4f, 8f), new Vector2(-8f, -8f));
            bar.gameObject.SetActive(false);
            return bar;
        }

        private static Button CreateTab(RectTransform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = Stretch(CreateRect(name, parent), anchorMin, anchorMax, offsetMin, offsetMax);
            var image = AddImage(rt, CookieArtFactory.RoundedRect, Color.white, true);
            var button = AddButton(rt, image);
            AddText(Stretch(CreateRect("Label", rt), Vector2.zero, Vector2.one), label, 26, Ink, TextAnchor.MiddleCenter);
            return button;
        }

        /// <summary>[통계] 버튼으로 여는 창: 통계 글, 저장·초기화 버튼, 닫기.</summary>
        private static RectTransform CreateStatsWindow(RectTransform root, CookieGame game, out Button close)
        {
            var window = CreateRect("StatsPanel", root);
            AddCard(window, CardCream, CreamDeep, 0.6f, true);

            AddText(TopBand(CreateRect("StatsTitle", window), 26f, 48f, 36f, 140f), "통계", 38, Ink, TextAnchor.MiddleLeft);
            var closeRect = CreateRect("CloseButton", window);
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-20f, -22f);
            closeRect.sizeDelta = new Vector2(120f, 56f);
            close = AddButton(closeRect, AddImage(closeRect, null, new Color(1f, 1f, 1f, 0f), true));
            AddText(Stretch(CreateRect("Label", closeRect), Vector2.zero, Vector2.one), "닫기", 26, InkSoft, TextAnchor.MiddleCenter);

            // 왼쪽: 기록, 오른쪽: 건물별 생산량
            var statsBody = Stretch(CreateRect("StatsBody", window), Vector2.zero, Vector2.one, new Vector2(36f, 100f), new Vector2(-36f, -92f));
            var statsText = AddText(Stretch(CreateRect("Records", statsBody), Vector2.zero, new Vector2(0.45f, 1f)), "", 22, Ink, TextAnchor.UpperLeft);
            statsText.lineSpacing = 1.2f;
            var productionText = AddText(Stretch(CreateRect("Production", statsBody), new Vector2(0.47f, 0f), Vector2.one), "", 20, Ink, TextAnchor.UpperLeft);
            productionText.lineSpacing = 1.15f;
            Wire(statsBody.gameObject.AddComponent<StatsView>(), ("game", game), ("statsText", statsText), ("productionText", productionText));

            var menu = Stretch(CreateRect("Menu", window), Vector2.zero, new Vector2(1f, 0f), new Vector2(36f, 22f), new Vector2(-36f, 80f));
            var saveButton = CreateTextButton(menu, "SaveButton", "저장", new Vector2(0f, 0f), new Vector2(150f, 56f), out _);
            var resetButton = CreateTextButton(menu, "ResetButton", "초기화", new Vector2(166f, 0f), new Vector2(150f, 56f), out var resetLabel);
            var notice = AddText(Stretch(CreateRect("Notice", menu), Vector2.zero, Vector2.one, new Vector2(336f, 0f), Vector2.zero),
                "", 20, InkSoft, TextAnchor.MiddleLeft);
            Wire(menu.gameObject.AddComponent<MenuView>(),
                ("game", game), ("saveButton", saveButton), ("resetButton", resetButton),
                ("resetButtonLabel", resetLabel), ("noticeText", notice));
            window.gameObject.SetActive(false);
            return window;
        }

        private static RectTransform CreateStorePanel(RectTransform root, CookieGame game, GameConfig config, Prefabs prefabs)
        {
            var store = CreateRect("StorePanel", root);
            AddCard(store, CardCream, CreamDeep, 0.45f, false);
            AddText(TopBand(CreateRect("StoreTitle", store), 26f, 56f, 36f, 36f), "상점", 42, Ink, TextAnchor.MiddleLeft);

            // 업그레이드: 그림 칸 (이름·설명은 마우스를 올리면 말풍선으로)
            AddText(TopBand(CreateRect("UpgradesLabel", store), 92f, 32f, 38f, 36f), "업그레이드", 22, InkSoft, TextAnchor.MiddleLeft);
            var upgradeScroll = TopBand(CreateScroll("UpgradeList", store, out var upgradeContent), 126f, 236f, 26f, 26f);
            UseGrid(upgradeContent, new Vector2(100f, 104f), new Vector2(12f, 14f));
            var noUpgrades = AddText(Stretch(CreateRect("EmptyLabel", upgradeScroll), Vector2.zero, Vector2.one),
                "구매할 수 있는 업그레이드가 없어요", 20, InkFaint, TextAnchor.MiddleCenter);

            var upgradeRows = new UpgradeRowView[config.Upgrades.Count];
            for (int i = 0; i < upgradeRows.Length; i++)
                upgradeRows[i] = InstantiateRow(prefabs.UpgradeRow, upgradeContent, $"UpgradeRow_{config.Upgrades[i].Id}");

            // 건물 목록 + 구매 수량
            var buildingHeader = TopBand(CreateRect("BuildingsHeader", store), 374f, 48f, 38f, 36f);
            AddText(Stretch(CreateRect("Label", buildingHeader), Vector2.zero, Vector2.one), "건물", 22, InkSoft, TextAnchor.MiddleLeft);
            var segment = CreateRect("BuyAmount", buildingHeader);
            segment.anchorMin = segment.anchorMax = segment.pivot = new Vector2(1f, 0.5f);
            segment.sizeDelta = new Vector2(270f, 46f);
            var segmentImage = AddImage(segment, CookieArtFactory.RoundedRect, CreamDeep);
            segmentImage.pixelsPerUnitMultiplier = 0.5f;
            int[] amounts = { 1, 10, 100 };
            var amountButtons = new Button[amounts.Length];
            for (int i = 0; i < amounts.Length; i++)
            {
                var rt = Stretch(CreateRect($"Buy{amounts[i]}", segment), new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f),
                    new Vector2(4f, 4f), new Vector2(-4f, -4f));
                var image = AddImage(rt, CookieArtFactory.RoundedRect, Color.white, true);
                image.pixelsPerUnitMultiplier = 0.5f;
                amountButtons[i] = AddButton(rt, image);
                amountButtons[i].colors = PlainColors();
                AddText(Stretch(CreateRect("Label", rt), Vector2.zero, Vector2.one), $"{amounts[i]}개", 20, Ink, TextAnchor.MiddleCenter);
            }

            var buildingScroll = Stretch(CreateScroll("BuildingList", store, out var buildingContent),
                Vector2.zero, Vector2.one, new Vector2(26f, 22f), new Vector2(-26f, -428f));
            var buildingRows = new BuildingRowView[config.Buildings.Count];
            for (int i = 0; i < buildingRows.Length; i++)
            {
                buildingRows[i] = InstantiateRow(prefabs.BuildingRow, buildingContent, $"BuildingRow_{config.Buildings[i].Id}");
                PreviewText(buildingRows[i], "nameText", config.Buildings[i].DisplayName);
            }

            // 업그레이드 말풍선: 상점 카드 바로 왼쪽에 뜬다.
            var tooltip = CreateRect("UpgradeTooltip", store);
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(0f, 1f);
            tooltip.pivot = new Vector2(1f, 0.5f);
            tooltip.anchoredPosition = new Vector2(-14f, -200f);
            tooltip.sizeDelta = new Vector2(420f, 130f);
            AddCard(tooltip, Color.white, CreamDeep, 0.6f, false);
            var tipName = AddText(TopBand(CreateRect("Name", tooltip), 18f, 38f, 24f, 24f), "", 28, Ink, TextAnchor.MiddleLeft);
            var tipCost = AddText(TopBand(CreateRect("Cost", tooltip), 18f, 38f, 24f, 24f), "", 22, ChiliRed, TextAnchor.MiddleRight);
            var tipDescription = AddText(Stretch(CreateRect("Description", tooltip), Vector2.zero, Vector2.one, new Vector2(24f, 16f), new Vector2(-24f, -62f)),
                "", 20, InkSoft, TextAnchor.UpperLeft);
            tipDescription.lineSpacing = 1.15f;

            Wire(store.gameObject.AddComponent<StoreView>(),
                ("game", game), ("buildingRows", buildingRows), ("upgradeRows", upgradeRows),
                ("noUpgradesLabel", noUpgrades.gameObject), ("buyAmountButtons", amountButtons),
                ("tooltip", tooltip), ("tooltipName", tipName), ("tooltipDescription", tipDescription), ("tooltipCost", tipCost));
            return store;
        }

        private static void UseGrid(RectTransform content, Vector2 cell, Vector2 spacing)
        {
            Object.DestroyImmediate(content.GetComponent<VerticalLayoutGroup>());
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = spacing;
            grid.padding = new RectOffset(6, 6, 6, 12); // 빨간 테두리·두께가 잘리지 않게 여유를 둔다
        }

        // ------------------------------------------------------------------ 프리팹

        private static Prefabs CreatePrefabs()
        {
            return new Prefabs
            {
                BuildingRow = CreateBuildingRowPrefab(),
                UpgradeRow = CreateUpgradeRowPrefab(),
                FloatingText = CreateFloatingTextPrefab(),
                Crumb = CreateImageParticlePrefab("Crumb", 22f, Color.white, 0.5f),
                RainCookie = CreateImageParticlePrefab("RainCookie", 50f, new Color(1f, 1f, 1f, 0.9f), 0.85f),
            };
        }

        private static BuildingRowView CreateBuildingRowPrefab()
        {
            var root = CreateRect("BuildingRow", null);
            root.sizeDelta = new Vector2(560f, 100f);
            var background = AddCard(root, Color.white, CreamDeep, 0.7f, true);
            var button = AddButton(root, background);
            button.colors = PlainColors();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            AddLayoutHeight(root, 100f);
            var highlight = AddHighlight(background);

            var tile = CreateRect("IconTile", root);
            tile.anchorMin = tile.anchorMax = tile.pivot = new Vector2(0f, 0.5f);
            tile.anchoredPosition = new Vector2(12f, 0f);
            tile.sizeDelta = new Vector2(76f, 76f);
            AddImage(tile, CookieArtFactory.RoundedRect, CreamDeep).pixelsPerUnitMultiplier = 0.8f;
            var icon = Stretch(CreateRect("Icon", tile), Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            var iconImage = AddImage(icon, CookieArtFactory.RoundedRect, Color.white);
            var iconLabel = AddText(Stretch(CreateRect("Label", icon), Vector2.zero, Vector2.one), "?", 36, Color.white, TextAnchor.MiddleCenter);

            var nameText = AddText(TopBand(CreateRect("Name", root), 12f, 36f, 104f, 100f), "건물", 28, Ink, TextAnchor.MiddleLeft);
            var costText = AddText(TopBand(CreateRect("Cost", root), 48f, 26f, 104f, 100f), "0", 22, InkSoft, TextAnchor.MiddleLeft, FontStyle.Bold);
            var infoText = AddText(TopBand(CreateRect("Info", root), 73f, 22f, 104f, 100f), "", 17, InkFaint, TextAnchor.MiddleLeft);
            var countText = AddText(Stretch(CreateRect("Count", root), new Vector2(1f, 0f), Vector2.one, new Vector2(-110f, 0f), new Vector2(-20f, 0f)),
                "", 48, InkFaint, TextAnchor.MiddleRight, FontStyle.Bold);

            var view = root.gameObject.AddComponent<BuildingRowView>();
            Wire(view, ("button", button), ("canvasGroup", group), ("background", background), ("highlight", highlight),
                ("depth", background.GetComponent<Shadow>()), ("icon", iconImage), ("fallbackIcon", CookieArtFactory.RoundedRect),
                ("iconLabel", iconLabel), ("nameText", nameText), ("costText", costText), ("infoText", infoText), ("countText", countText));
            AddBuildingRowRarity(root.gameObject); // 등급 색 칸·빛
            return SavePrefab<BuildingRowView>(root.gameObject, "BuildingRow");
        }

        private static UpgradeRowView CreateUpgradeRowPrefab()
        {
            var root = CreateRect("UpgradeRow", null);
            root.sizeDelta = new Vector2(100f, 104f);
            var background = AddCard(root, Color.white, CreamDeep, 0.8f, true);
            var button = AddButton(root, background);
            button.colors = PlainColors();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var highlight = AddHighlight(background);

            var icon = Stretch(CreateRect("Icon", root), Vector2.zero, Vector2.one, new Vector2(14f, 30f), new Vector2(-14f, -8f));
            var iconImage = AddImage(icon, CookieArtFactory.RoundedRect, Color.white);
            var iconLabel = AddText(Stretch(CreateRect("Label", icon), Vector2.zero, Vector2.one), "", 32, Color.white, TextAnchor.MiddleCenter);
            var costText = AddText(Stretch(CreateRect("Cost", root), Vector2.zero, new Vector2(1f, 0f), new Vector2(4f, 4f), new Vector2(-4f, 30f)),
                "0", 17, InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold);

            var view = root.gameObject.AddComponent<UpgradeRowView>();
            Wire(view, ("button", button), ("canvasGroup", group), ("background", background), ("highlight", highlight),
                ("depth", background.GetComponent<Shadow>()), ("icon", iconImage), ("fallbackIcon", CookieArtFactory.RoundedRect),
                ("iconLabel", iconLabel), ("costText", costText));
            AddUpgradeRowRarity(root.gameObject); // 등급 색 칸·빛
            return SavePrefab<UpgradeRowView>(root.gameObject, "UpgradeRow");
        }

        private static UIParticle CreateFloatingTextPrefab()
        {
            var root = CreateRect("FloatingText", null);
            root.sizeDelta = new Vector2(320f, 60f);
            var text = AddText(root, "+1", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddRedOutline(text);
            var particle = root.gameObject.AddComponent<UIParticle>();
            Wire(particle, ("graphic", text));
            return SavePrefab<UIParticle>(root.gameObject, "FloatingText");
        }

        // ------------------------------------------------------------------ "동네 평상" 스타일 헬퍼

        /// <summary>
        /// 둥근 카드 + 아래쪽 두께(Shadow). 두께가 있으면 "눌리는 장난감 버튼"처럼 보인다.
        /// pixelsPerUnitMultiplier가 작을수록 모서리가 더 둥글다.
        /// </summary>
        private static Image AddCard(RectTransform rt, Color face, Color depthColor, float roundness, bool raycastTarget)
        {
            var image = AddImage(rt, CookieArtFactory.RoundedRect, face, raycastTarget);
            image.pixelsPerUnitMultiplier = roundness;
            var depth = rt.gameObject.AddComponent<Shadow>();
            depth.effectColor = depthColor;
            depth.effectDistance = new Vector2(0f, -6f);
            return image;
        }

        /// <summary>살 수 있을 때 켜는 빨간 테두리 (처음엔 꺼 둔다. 상점 줄이 상태에 따라 켠다).</summary>
        private static Outline AddHighlight(Image image)
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = ChiliRed;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;
            return outline;
        }

        /// <summary>흰 글씨 둘레를 고추장 빨강으로 두껍게 (+1 숫자, 버프 문구).</summary>
        private static void AddRedOutline(Graphic graphic)
        {
            foreach (var d in new[] { new Vector2(3f, -3f), new Vector2(-3f, 3f) })
            {
                var outline = graphic.gameObject.AddComponent<Outline>();
                outline.effectColor = ChiliRed;
                outline.effectDistance = d;
            }
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = ChiliDeep;
            shadow.effectDistance = new Vector2(0f, -5f);
        }

        /// <summary>색이 코드(상점 상태)로 정해지는 버튼: 마우스 반응은 밝기만 살짝 바꾼다.</summary>
        private static ColorBlock PlainColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.97f, 0.92f);
            colors.pressedColor = new Color(0.9f, 0.85f, 0.78f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.06f;
            return colors;
        }

        private static UIParticle CreateImageParticlePrefab(string name, float size, Color color, float fadeStart)
        {
            var root = CreateRect(name, null);
            root.sizeDelta = new Vector2(size, size);
            var image = AddImage(root, CookieArtFactory.Cookie, color);
            var particle = root.gameObject.AddComponent<UIParticle>();
            Wire(particle, ("graphic", image), ("fadeStartRatio", fadeStart));
            return SavePrefab<UIParticle>(root.gameObject, name);
        }

        private static T SavePrefab<T>(GameObject go, string name) where T : Component
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<T>();
        }

        private static T InstantiateRow<T>(T prefab, Transform parent, string name) where T : Component
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            go.name = name;
            return go.GetComponent<T>();
        }

        // ------------------------------------------------------------------ 헬퍼

        /// <summary>private [SerializeField] 필드에 인스펙터와 같은 방식으로 값을 넣는다.</summary>
        private static void Wire(Object target, params (string property, object value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach (var (property, value) in values)
            {
                var prop = serialized.FindProperty(property);
                if (prop == null)
                {
                    Debug.LogError($"[CookieSceneBuilder] {target.GetType().Name}에 '{property}' 필드가 없습니다.");
                    continue;
                }
                switch (value)
                {
                    case Object obj:
                        prop.objectReferenceValue = obj;
                        break;
                    case Object[] array:
                        prop.arraySize = array.Length;
                        for (int i = 0; i < array.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                        break;
                    case float f:
                        prop.floatValue = f;
                        break;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>플레이 전에도 씬에서 어떤 줄인지 알아볼 수 있게 미리 글자를 채워 둔다 (실행하면 데이터로 덮어씀).</summary>
        private static void PreviewText(Object view, string property, string text)
        {
            var prop = new SerializedObject(view).FindProperty(property);
            if (prop?.objectReferenceValue is Text label) label.text = text;
        }

        private static Canvas CreateCanvas(string name, RenderMode mode, Camera camera, int sortingOrder, bool raycaster)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = mode;
            canvas.sortingOrder = sortingOrder;
            if (mode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = camera;
                canvas.planeDistance = 50f;
            }

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (raycaster) go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>부모 위쪽에서 top만큼 떨어진 곳에 height 높이의 가로 띠를 만든다.</summary>
        private static RectTransform TopBand(RectTransform rt, float top, float height, float left = 0f, float right = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -(top + height));
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        private static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image AddImage(RectTransform rt, Sprite sprite, Color color, bool raycastTarget = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            return image;
        }

        private static Text AddText(RectTransform rt, string content, int size, Color color, TextAnchor alignment,
            FontStyle style = FontStyle.Normal)
        {
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = UIFontTool.AdjustStyle(font, style);
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void AddOutline(Graphic graphic)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static Button AddButton(RectTransform rt, Graphic target)
        {
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f);
            button.colors = colors;
            return button;
        }

        private static Button CreateTextButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, out Text text)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var image = AddImage(rt, CookieArtFactory.RoundedRect, Wood, true);
            var button = AddButton(rt, image);
            text = AddText(Stretch(CreateRect("Label", rt), Vector2.zero, Vector2.one), label, 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            return button;
        }

        private static RectTransform CreateScroll(string name, RectTransform parent, out RectTransform content)
        {
            var root = CreateRect(name, parent);
            AddImage(root, null, new Color(0f, 0f, 0f, 0f), true); // 투명하지만 휠·드래그를 받는다
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            var viewport = Stretch(CreateRect("Viewport", root), Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return root;
        }

        private static void AddLayoutHeight(RectTransform rt, float height)
        {
            var element = rt.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
