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
    public static class CookieSceneBuilder
    {
        private const string ScenePath = "Assets/CookieClicker/Scenes/CookieClicker.unity";
        private const string PrefabFolder = "Assets/CookieClicker/Prefabs";
        private const string ModelPath = "Assets/CookieClicker/Models/Nakgopsae.glb";
        private const string ClickTargetName = "StewPot";
        private const string ModelPivotName = "ModelPivot";
        // 전골냄비는 윗면(+Y)이 위를 보고 있어서, 정면 카메라에서 국물이 보이도록 앞으로 기울인다.
        private static readonly Vector3 ModelRotation = new Vector3(-55f, 0f, 0f);
        private const int UILayer = 5;

        private static readonly Color Cream = new Color(1f, 0.93f, 0.8f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        private static readonly Color SubText = new Color(0.78f, 0.74f, 0.68f);
        private static readonly Color Wood = new Color(0.42f, 0.27f, 0.14f);

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

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EditorFolders.Ensure(PrefabFolder);
            EditorFolders.Ensure("Assets/CookieClicker/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 새 씬을 열 때 Unity가 쓰지 않는 에셋을 메모리에서 내리므로, 데이터 에셋은 반드시 새 씬을 연 "뒤에"
            // 불러와야 한다. 먼저 불러 두면 이미 내려간 객체를 참조하게 되어 Config 연결이 비어 버린다.
            var config = CookieContentFactory.CreateOrLoadContent();
            var prefabs = CreatePrefabs();

            var game = new GameObject("GameManager").AddComponent<CookieGame>();
            Wire(game, ("config", config));

            var camera = CreateCamera();
            CreateLight("Key Light", new Vector3(35f, -25f, 0f), 1.3f, new Color(1f, 0.96f, 0.9f));
            CreateLight("Fill Light", new Vector3(10f, 30f, 0f), 0.45f, new Color(0.85f, 0.9f, 1f));
            CreateEventSystem();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.47f, 0.45f);

            var cookie = CreateCookie(game, camera);
            var background = CreateBackgroundCanvas(game, camera, prefabs);
            CreateUICanvas(game, cookie, config, prefabs, background);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            Selection.activeGameObject = game.gameObject;

            EditorUtility.DisplayDialog("Cookie Clicker",
                $"씬을 만들었습니다.\n{ScenePath}\n\nPlay 버튼을 눌러 실행해 보세요.", "확인");
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
        private static Transform AttachModel(Transform root, GameObject modelAsset)
        {
            // 크기 계산은 월드 기준이라 잠시 root를 원점·기본 크기로 돌려 놓고 계산한 뒤 되돌린다.
            Vector3 position = root.position;
            Quaternion rotation = root.rotation;
            Vector3 scale = root.localScale;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;

            var pivot = new GameObject(ModelPivotName).transform;
            pivot.SetParent(root, false);
            pivot.localRotation = Quaternion.Euler(ModelRotation);

            var model = modelAsset != null
                ? PrefabUtility.InstantiatePrefab(modelAsset) as GameObject ?? Object.Instantiate(modelAsset)
                : CreateFallbackCookie();
            model.name = "Model";
            model.transform.SetParent(pivot, false);
            NormalizeToUnitSize(root, pivot);

            root.SetPositionAndRotation(position, rotation);
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

        // ------------------------------------------------------------------ 배경

        private struct BackgroundParts
        {
            public RectTransform Rays;
            public RectTransform Glow;
            public RectTransform RainArea;
        }

        private static BackgroundParts CreateBackgroundCanvas(CookieGame game, Camera camera, Prefabs prefabs)
        {
            // Screen Space - Camera 캔버스를 쿠키보다 멀리(50) 두어서 3D 쿠키 뒤에 그려지게 한다.
            var root = (RectTransform)CreateCanvas("Background Canvas", RenderMode.ScreenSpaceCamera, camera, -100, false).transform;

            AddImage(Stretch(CreateRect("Gradient", root), Vector2.zero, Vector2.one), CookieArtFactory.Background, Color.white);

            var cookieAnchor = new Vector2(0.17f, 0.47f);
            var rays = Place(CreateRect("Rays", root), cookieAnchor, new Vector2(1100f, 1100f));
            AddImage(rays, CookieArtFactory.Rays, new Color(1f, 0.95f, 0.8f, 0.12f));
            rays.gameObject.AddComponent<Spinner>();

            var glow = Place(CreateRect("Glow", root), cookieAnchor, new Vector2(640f, 640f));
            AddImage(glow, CookieArtFactory.SoftCircle, new Color(1f, 0.85f, 0.55f, 0.35f));

            var rainArea = Stretch(CreateRect("CookieRain", root), Vector2.zero, new Vector2(0.34f, 1f));
            var rain = rainArea.gameObject.AddComponent<CookieRain>();
            Wire(rain, ("game", game), ("cookiePrefab", prefabs.RainCookie), ("area", rainArea));

            return new BackgroundParts { Rays = rays, Glow = glow, RainArea = rainArea };
        }

        // ------------------------------------------------------------------ UI

        private static void CreateUICanvas(CookieGame game, CookieClickTarget cookie, GameConfig config, Prefabs prefabs,
            BackgroundParts background)
        {
            var root = (RectTransform)CreateCanvas("UI Canvas", RenderMode.ScreenSpaceOverlay, null, 0, true).transform;

            // 왼쪽: 쿠키 수 표시
            var left = Stretch(CreateRect("LeftPanel", root), Vector2.zero, new Vector2(0.34f, 1f));
            var header = TopBand(CreateRect("Header", left), 0f, 190f);
            AddImage(header, null, new Color(0f, 0f, 0f, 0.35f));
            AddText(TopBand(CreateRect("BakeryName", header), 14f, 40f), "야르! 낙곱새 맛집", 30, Cream, TextAnchor.MiddleCenter);
            var countText = AddText(TopBand(CreateRect("CookieCount", header), 54f, 72f), "0 낙곱새", 56, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(countText);
            var cpsText = AddText(TopBand(CreateRect("CookiesPerSecond", header), 128f, 40f), "초당 0", 26, Cream, TextAnchor.MiddleCenter);
            var buffText = AddText(TopBand(CreateRect("Buff", left), 200f, 44f), "", 28, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(buffText);
            Wire(left.gameObject.AddComponent<HudView>(),
                ("game", game), ("cookieCountText", countText), ("cpsText", cpsText), ("buffText", buffText));

            var middle = CreateMiddlePanel(root, game);
            var store = CreateStorePanel(root, game, config, prefabs);
            var news = CreateNewsTicker(root, game);
            var tabBar = CreateTabBar(root, out var storeTab, out var statsTab);

            // 맨 위: 이펙트 / 황금 쿠키 / 안내 문구 (배경 이미지가 없어서 클릭을 가로막지 않는다)
            var effectLayer = Stretch(CreateRect("EffectLayer", root), Vector2.zero, Vector2.one);

            var golden = Place(CreateRect("GoldenCookie", effectLayer), new Vector2(0.5f, 0.5f), new Vector2(120f, 120f));
            var goldenImage = AddImage(golden, CookieArtFactory.GoldenCookie, Color.white, true);
            var goldenButton = AddButton(golden, goldenImage);
            var goldenGroup = golden.gameObject.AddComponent<CanvasGroup>();
            var goldenView = golden.gameObject.AddComponent<GoldenCookieView>();
            Wire(goldenView, ("game", game), ("button", goldenButton), ("canvasGroup", goldenGroup), ("spawnArea", effectLayer));

            var popup = Place(CreateRect("Popup", effectLayer), new Vector2(0.17f, 0.8f), new Vector2(700f, 70f));
            var popupText = AddText(popup, "", 40, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(popupText);

            Wire(effectLayer.gameObject.AddComponent<ClickFeedbackView>(),
                ("game", game), ("cookie", cookie), ("effectLayer", effectLayer),
                ("floatingTextPrefab", prefabs.FloatingText), ("crumbPrefab", prefabs.Crumb), ("popupText", popupText));

            ConfigureResponsiveLayout(root, cookie, goldenView, background,
                left, middle, store, news, tabBar, storeTab, statsTab, popup);
        }

        /// <summary>
        /// 가로: [쿠키 | 통계 | 상점] 3단. 세로: 위에 쿠키, 아래에 상점/통계 탭.
        /// 값은 ResponsiveLayout 컴포넌트에 데이터로 저장되므로 이후에는 Inspector에서 조정하면 된다.
        /// </summary>
        private static void ConfigureResponsiveLayout(RectTransform root, CookieClickTarget cookie, GoldenCookieView golden,
            BackgroundParts background, RectTransform left, RectTransform middle, RectTransform store, RectTransform news,
            RectTransform tabBar, Button storeTab, Button statsTab, RectTransform popup)
        {
            const float split = 0.56f;   // 세로 화면에서 쿠키 영역이 시작되는 높이 비율
            const float tabHeight = 70f;
            var bottom = new RectLayout(Vector2.zero, new Vector2(1f, split), Vector2.zero, new Vector2(0f, -tabHeight));

            PanelLayout Panel(RectTransform target, RectLayout landscape, RectLayout portrait) =>
                new PanelLayout { target = target, landscape = landscape, portrait = portrait };

            var panels = new[]
            {
                Panel(left,
                    new RectLayout(Vector2.zero, new Vector2(0.34f, 1f)),
                    new RectLayout(new Vector2(0f, split), Vector2.one)),
                Panel(middle,
                    new RectLayout(new Vector2(0.34f, 0f), new Vector2(0.68f, 1f)),
                    bottom),
                Panel(store,
                    new RectLayout(new Vector2(0.68f, 0f), Vector2.one),
                    bottom),
                Panel(news,
                    new RectLayout(new Vector2(0.34f, 1f), new Vector2(0.68f, 1f), new Vector2(5f, -70f), new Vector2(-5f, 0f)),
                    new RectLayout(new Vector2(0f, split), new Vector2(1f, split), Vector2.zero, new Vector2(0f, 56f))),
                Panel(tabBar,
                    new RectLayout(new Vector2(0f, split), new Vector2(1f, split), new Vector2(0f, -tabHeight), Vector2.zero),
                    new RectLayout(new Vector2(0f, split), new Vector2(1f, split), new Vector2(0f, -tabHeight), Vector2.zero)),
                Panel(background.RainArea,
                    new RectLayout(Vector2.zero, new Vector2(0.34f, 1f)),
                    new RectLayout(new Vector2(0f, split), Vector2.one)),
                Panel(popup,
                    new RectLayout(new Vector2(0.17f, 0.8f), new Vector2(0.17f, 0.8f), new Vector2(-350f, -35f), new Vector2(350f, 35f)),
                    new RectLayout(new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f), new Vector2(-350f, -35f), new Vector2(350f, 35f))),
            };

            var landscapeCookie = new CookieLayout
            {
                viewportPosition = new Vector2(0.17f, 0.47f),
                heightRatio = 0.42f,
                maxWidthRatio = 0.28f,
                goldenSpawnRect = new Rect(0.04f, 0.1f, 0.6f, 0.75f),
            };
            var portraitCookie = new CookieLayout
            {
                viewportPosition = new Vector2(0.5f, 0.745f),
                heightRatio = 0.25f,
                maxWidthRatio = 0.7f,
                goldenSpawnRect = new Rect(0.06f, 0.6f, 0.88f, 0.28f),
            };

            var layout = root.gameObject.AddComponent<ResponsiveLayout>();
            layout.EditorConfigure(panels, cookie, golden, new[] { background.Rays, background.Glow },
                landscapeCookie, portraitCookie, tabBar.gameObject, storeTab, statsTab, store.gameObject, middle.gameObject);
        }

        private static RectTransform CreateNewsTicker(RectTransform root, CookieGame game)
        {
            var news = TopBand(CreateRect("NewsTicker", root), 0f, 70f, 5f, 5f);
            AddImage(news, null, new Color(0f, 0f, 0f, 0.45f));
            var newsText = AddText(Stretch(CreateRect("Text", news), Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f)),
                "뉴스", 22, Cream, TextAnchor.MiddleCenter, FontStyle.Italic);
            Wire(news.gameObject.AddComponent<NewsTickerView>(), ("game", game), ("newsText", newsText));
            return news;
        }

        private static RectTransform CreateTabBar(RectTransform root, out Button storeTab, out Button statsTab)
        {
            var bar = CreateRect("TabBar", root);
            AddImage(bar, null, new Color(0.1f, 0.06f, 0.03f, 0.95f));
            storeTab = CreateTab(bar, "StoreTab", "상점", Vector2.zero, new Vector2(0.5f, 1f), new Vector2(8f, 8f), new Vector2(-4f, -8f));
            statsTab = CreateTab(bar, "StatsTab", "통계", new Vector2(0.5f, 0f), Vector2.one, new Vector2(4f, 8f), new Vector2(-8f, -8f));
            bar.gameObject.SetActive(false);
            return bar;
        }

        private static Button CreateTab(RectTransform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = Stretch(CreateRect(name, parent), anchorMin, anchorMax, offsetMin, offsetMax);
            var image = AddImage(rt, CookieArtFactory.RoundedRect, Wood, true);
            var button = AddButton(rt, image);
            AddText(Stretch(CreateRect("Label", rt), Vector2.zero, Vector2.one), label, 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            return button;
        }

        private static RectTransform CreateMiddlePanel(RectTransform root, CookieGame game)
        {
            var middle = Stretch(CreateRect("MiddlePanel", root), new Vector2(0.34f, 0f), new Vector2(0.68f, 1f));
            AddImage(middle, null, new Color(0.04f, 0.06f, 0.1f, 0.55f));
            AddImage(Stretch(CreateRect("LeftBorder", middle), Vector2.zero, new Vector2(0f, 1f), new Vector2(-5f, 0f), new Vector2(5f, 0f)), null, Wood);
            AddImage(Stretch(CreateRect("RightBorder", middle), new Vector2(1f, 0f), Vector2.one, new Vector2(-5f, 0f), new Vector2(5f, 0f)), null, Wood);

            AddText(TopBand(CreateRect("StatsTitle", middle), 90f, 44f, 30f, 30f), "통계", 32, Cream, TextAnchor.MiddleLeft, FontStyle.Bold);
            var statsBody = Stretch(CreateRect("StatsBody", middle), Vector2.zero, Vector2.one, new Vector2(30f, 100f), new Vector2(-30f, -145f));
            var statsText = AddText(statsBody, "", 21, Color.white, TextAnchor.UpperLeft);
            statsText.lineSpacing = 1.2f;
            Wire(statsBody.gameObject.AddComponent<StatsView>(), ("game", game), ("statsText", statsText));

            var menu = Stretch(CreateRect("Menu", middle), Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 20f), new Vector2(-30f, 80f));
            var saveButton = CreateTextButton(menu, "SaveButton", "저장", new Vector2(0f, 0f), new Vector2(150f, 56f), out _);
            var resetButton = CreateTextButton(menu, "ResetButton", "초기화", new Vector2(166f, 0f), new Vector2(150f, 56f), out var resetLabel);
            var notice = AddText(Stretch(CreateRect("Notice", menu), Vector2.zero, Vector2.one, new Vector2(336f, 0f), Vector2.zero),
                "", 20, SubText, TextAnchor.MiddleLeft);
            Wire(menu.gameObject.AddComponent<MenuView>(),
                ("game", game), ("saveButton", saveButton), ("resetButton", resetButton),
                ("resetButtonLabel", resetLabel), ("noticeText", notice));
            return middle;
        }

        private static RectTransform CreateStorePanel(RectTransform root, CookieGame game, GameConfig config, Prefabs prefabs)
        {
            var store = Stretch(CreateRect("StorePanel", root), new Vector2(0.68f, 0f), Vector2.one);
            AddImage(store, null, new Color(0.17f, 0.1f, 0.05f, 0.88f));
            AddOutline(AddText(TopBand(CreateRect("StoreTitle", store), 8f, 56f), "상점", 38, Cream, TextAnchor.MiddleCenter, FontStyle.Bold));

            // 업그레이드 목록
            AddText(TopBand(CreateRect("UpgradesLabel", store), 68f, 34f, 20f, 20f), "업그레이드", 22, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            var upgradeScroll = TopBand(CreateScroll("UpgradeList", store, out var upgradeContent), 104f, 240f, 10f, 10f);
            var noUpgrades = AddText(Stretch(CreateRect("EmptyLabel", upgradeScroll), Vector2.zero, Vector2.one),
                "구매할 수 있는 업그레이드가 없어요", 18, SubText, TextAnchor.MiddleCenter);

            var upgradeRows = new UpgradeRowView[config.Upgrades.Count];
            for (int i = 0; i < upgradeRows.Length; i++)
            {
                upgradeRows[i] = InstantiateRow(prefabs.UpgradeRow, upgradeContent, $"UpgradeRow_{config.Upgrades[i].Id}");
                PreviewText(upgradeRows[i], "nameText", config.Upgrades[i].DisplayName);
                PreviewText(upgradeRows[i], "descriptionText", config.Upgrades[i].Description);
            }

            // 건물 목록 + 구매 수량
            var buildingHeader = TopBand(CreateRect("BuildingsHeader", store), 354f, 50f, 20f, 20f);
            AddText(Stretch(CreateRect("Label", buildingHeader), Vector2.zero, Vector2.one), "건물", 22, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            int[] amounts = { 1, 10, 100 };
            var amountButtons = new Button[amounts.Length];
            for (int i = 0; i < amounts.Length; i++)
            {
                var rt = CreateRect($"Buy{amounts[i]}", buildingHeader);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-(amounts.Length - 1 - i) * 86f, 0f);
                rt.sizeDelta = new Vector2(80f, 42f);
                var image = AddImage(rt, CookieArtFactory.RoundedRect, Color.white, true);
                amountButtons[i] = AddButton(rt, image);
                AddText(Stretch(CreateRect("Label", rt), Vector2.zero, Vector2.one), $"x{amounts[i]}", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            var buildingScroll = Stretch(CreateScroll("BuildingList", store, out var buildingContent),
                Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -414f));
            var buildingRows = new BuildingRowView[config.Buildings.Count];
            for (int i = 0; i < buildingRows.Length; i++)
            {
                buildingRows[i] = InstantiateRow(prefabs.BuildingRow, buildingContent, $"BuildingRow_{config.Buildings[i].Id}");
                PreviewText(buildingRows[i], "nameText", config.Buildings[i].DisplayName);
            }

            Wire(store.gameObject.AddComponent<StoreView>(),
                ("game", game), ("buildingRows", buildingRows), ("upgradeRows", upgradeRows),
                ("noUpgradesLabel", noUpgrades.gameObject), ("buyAmountButtons", amountButtons));
            return store;
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
            root.sizeDelta = new Vector2(560f, 88f);
            var background = AddImage(root, CookieArtFactory.RoundedRect, new Color(0.36f, 0.24f, 0.14f), true);
            var button = AddButton(root, background);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            AddLayoutHeight(root, 88f);

            var icon = CreateRect("Icon", root);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(10f, 0f);
            icon.sizeDelta = new Vector2(68f, 68f);
            var iconImage = AddImage(icon, CookieArtFactory.RoundedRect, Color.white);
            var iconLabel = AddText(Stretch(CreateRect("Label", icon), Vector2.zero, Vector2.one), "?", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(iconLabel);

            var nameText = AddText(TopBand(CreateRect("Name", root), 6f, 32f, 92f, 100f), "건물", 26, Cream, TextAnchor.MiddleLeft, FontStyle.Bold);
            var costText = AddText(TopBand(CreateRect("Cost", root), 38f, 24f, 92f, 100f), "가격 0", 20, Color.white, TextAnchor.MiddleLeft);
            var infoText = AddText(TopBand(CreateRect("Info", root), 62f, 22f, 92f, 100f), "", 16, SubText, TextAnchor.MiddleLeft);
            var countText = AddText(Stretch(CreateRect("Count", root), new Vector2(1f, 0f), Vector2.one, new Vector2(-110f, 0f), new Vector2(-14f, 0f)),
                "", 46, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleRight, FontStyle.Bold);

            var view = root.gameObject.AddComponent<BuildingRowView>();
            Wire(view, ("button", button), ("canvasGroup", group), ("icon", iconImage), ("iconLabel", iconLabel),
                ("nameText", nameText), ("costText", costText), ("infoText", infoText), ("countText", countText));
            return SavePrefab<BuildingRowView>(root.gameObject, "BuildingRow");
        }

        private static UpgradeRowView CreateUpgradeRowPrefab()
        {
            var root = CreateRect("UpgradeRow", null);
            root.sizeDelta = new Vector2(560f, 66f);
            var background = AddImage(root, CookieArtFactory.RoundedRect, new Color(0.3f, 0.2f, 0.28f), true);
            var button = AddButton(root, background);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            AddLayoutHeight(root, 66f);

            var icon = CreateRect("Icon", root);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(10f, 0f);
            icon.sizeDelta = new Vector2(46f, 46f);
            var iconImage = AddImage(icon, CookieArtFactory.RoundedRect, Color.white);

            var nameText = AddText(TopBand(CreateRect("Name", root), 6f, 30f, 68f, 150f), "업그레이드", 22, Cream, TextAnchor.MiddleLeft, FontStyle.Bold);
            var descriptionText = AddText(TopBand(CreateRect("Description", root), 34f, 26f, 68f, 150f), "", 16, SubText, TextAnchor.MiddleLeft);
            var costText = AddText(Stretch(CreateRect("Cost", root), new Vector2(1f, 0f), Vector2.one, new Vector2(-150f, 0f), new Vector2(-14f, 0f)),
                "0", 20, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);

            var view = root.gameObject.AddComponent<UpgradeRowView>();
            Wire(view, ("button", button), ("canvasGroup", group), ("icon", iconImage),
                ("nameText", nameText), ("descriptionText", descriptionText), ("costText", costText));
            return SavePrefab<UpgradeRowView>(root.gameObject, "UpgradeRow");
        }

        private static UIParticle CreateFloatingTextPrefab()
        {
            var root = CreateRect("FloatingText", null);
            root.sizeDelta = new Vector2(320f, 60f);
            var text = AddText(root, "+1", 36, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(text);
            var particle = root.gameObject.AddComponent<UIParticle>();
            Wire(particle, ("graphic", text));
            return SavePrefab<UIParticle>(root.gameObject, "FloatingText");
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
            text.fontStyle = style;
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
            AddImage(root, null, new Color(0f, 0f, 0f, 0.2f), true);
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
