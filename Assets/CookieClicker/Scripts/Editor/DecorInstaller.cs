using System;
using System.Collections.Generic;
using System.IO;
using CookieClicker.Core;
using CookieClicker.Decor;
using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 꾸미기 모드 설치: 물건 프리팹·데이터·아이콘을 만들고, 구경하기 월드에 "내 평상 부지"와 꾸미기 UI를 배치한다.
    /// 프리팹과 데이터 에셋은 이미 있으면 건드리지 않으므로(직접 고친 모양·가격 유지) 여러 번 실행해도 안전하다.
    /// 프리팹을 처음 모양으로 되돌리고 싶을 때만 "Rebuild Decor Prefabs" 메뉴를 쓴다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string DecorDataFolder = "Assets/CookieClicker/Data/Decor";
        private const string DecorCatalogPath = "Assets/CookieClicker/Data/DecorCatalog.asset";
        private const string DecorPrefabFolder = PrefabFolder + "/Decor";
        private const string DecorIconFolder = "Assets/CookieClicker/Art/Thumbnails/Decor";
        private const string DecorGhostMaterialPath = CookieArtFactory.ArtFolder + "/World/DecorGhost.mat";

        /// <summary>부지 왼쪽 아래 모서리 (월드 기준). 본진 냄비 북쪽, 국자·알바생 건물 뒤의 빈 들판.</summary>
        private static readonly Vector3 DecorLotOrigin = new Vector3(-12f, 0f, 16f);
        private static readonly Vector2Int DecorLotCells = new Vector2Int(12, 10);
        private const float DecorCell = 2f;
        private const float DecorStorey = 2.5f;
        /// <summary>가구 전체 크기 배율. 캐릭터가 머리 큰 2등신이라 실제 치수대로 만들면 가구가 작아 보여서 키운다.</summary>
        private const float FurnitureScale = 1.3f;

        private static readonly Color DecorWood = new Color(0.78f, 0.58f, 0.38f);
        private static readonly Color DecorWoodDark = new Color(0.52f, 0.34f, 0.2f);
        private static readonly Color DecorJangpan = new Color(0.93f, 0.74f, 0.42f);

        private struct DecorSpec
        {
            public string Id, Name, NameEn, Group;
            public DecorSnap Snap;
            public DecorCategory Category;
            public double Cost;
            /// <summary>앉을 수 있는 물건이면 true. SeatZ는 앉는 자리의 앞뒤 위치 (평상은 앞쪽 끝에 걸터앉는다).</summary>
            public bool Seat;
            public float SeatZ;
            /// <summary>러그처럼 바닥에 까는 물건 (겹쳐 놓을 수 있음)</summary>
            public bool Flat;
            /// <summary>색 바꾸기로 칠할 재질 이름 (비우면 가장 많이 쓰인 재질)</summary>
            public string Paint;
            public Action<Transform> Build;
        }

        /// <summary>기본 물건 목록 (핫바 순서). 가격·이름은 만들어진 에셋에서 고치면 되고, 새 물건은 여기에 한 줄 추가하면 된다.</summary>
        private static readonly DecorSpec[] DecorSpecs =
        {
            // 구조물: 직접 만든 블록 모델. Paint는 색 바꾸기(C)로 칠할 재질 이름.
            new DecorSpec { Id = "floor_wood", Name = "나무 바닥", NameEn = "Wood Floor", Snap = DecorSnap.Floor, Cost = 50, Paint = "Decor_Wood", Build = BuildWoodFloor },
            new DecorSpec { Id = "floor_jangpan", Name = "장판 바닥", NameEn = "Vinyl Floor", Snap = DecorSnap.Floor, Cost = 60, Paint = "Decor_Jangpan", Build = BuildJangpanFloor },
            new DecorSpec { Id = "pillar", Name = "기둥", NameEn = "Pillar", Snap = DecorSnap.Pillar, Cost = 30, Paint = "Decor_Wood", Build = BuildPillar },
            new DecorSpec { Id = "wall", Name = "나무 벽", NameEn = "Wood Wall", Snap = DecorSnap.Wall, Cost = 80, Paint = "Decor_Wood", Build = BuildWall },
            new DecorSpec { Id = "wall_window", Name = "창문 벽", NameEn = "Window Wall", Snap = DecorSnap.Wall, Cost = 120, Paint = "Decor_Wood", Build = BuildWindowWall },
            new DecorSpec { Id = "wall_door", Name = "문 벽", NameEn = "Door Wall", Snap = DecorSnap.Wall, Cost = 120, Paint = "Decor_Wood", Build = BuildDoorWall },
            new DecorSpec { Id = "stairs", Name = "계단", NameEn = "Stairs", Snap = DecorSnap.Floor, Group = "Stair", Cost = 200, Paint = "Decor_Wood", Build = BuildStairs },
            new DecorSpec { Id = "roof_tent", Name = "천막 지붕", NameEn = "Tent Roof", Snap = DecorSnap.Floor, Cost = 100, Paint = "Decor_TentOrange", Build = BuildTentRoof },

            // 의자·테이블: 포장마차 느낌이 나는 평상은 직접 만들고, 나머지는 Kenney Furniture Kit (CC0)
            new DecorSpec { Id = "chair_red", Name = "빨간 의자", NameEn = "Red Chair", Snap = DecorSnap.Free, Category = DecorCategory.Furniture, Cost = 150, Seat = true,
                Paint = "Decor_PlasticRed", Build = Kenney("chair", 0.9f, ("wood", "PlasticRed", new Color(0.88f, 0.2f, 0.16f))) },
            new DecorSpec { Id = "table_round", Name = "원형 테이블", NameEn = "Round Table", Snap = DecorSnap.Free, Category = DecorCategory.Furniture, Cost = 300,
                Paint = "Decor_Steel", Build = Kenney("tableRound", 0.75f, ("wood", "Steel", new Color(0.78f, 0.8f, 0.83f))) },
            new DecorSpec { Id = "pyeongsang", Name = "평상", NameEn = "Pyeongsang", Snap = DecorSnap.Free, Category = DecorCategory.Furniture, Cost = 500, Seat = true, SeatZ = 0.3f,
                Paint = "Decor_Jangpan", Build = BuildPyeongsang },
            new DecorSpec { Id = "stool", Name = "바 의자", NameEn = "Bar Stool", Snap = DecorSnap.Free, Category = DecorCategory.Furniture, Cost = 120, Seat = true,
                Paint = "carpet", Build = Kenney("stoolBar", 0.8f) },
            new DecorSpec { Id = "bench", Name = "나무 벤치", NameEn = "Bench", Snap = DecorSnap.Free, Category = DecorCategory.Furniture, Cost = 250, Seat = true,
                Paint = "wood", Build = Kenney("bench", 0.9f) },

            // 가전
            new DecorSpec { Id = "burner_pot", Name = "버너와 냄비", NameEn = "Burner & Pot", Snap = DecorSnap.Free, Category = DecorCategory.Appliance, Cost = 1000,
                Paint = "Decor_BurnerBody", Build = BuildBurnerPot },
            new DecorSpec { Id = "tv_vintage", Name = "옛날 TV", NameEn = "Retro TV", Snap = DecorSnap.Free, Category = DecorCategory.Appliance, Cost = 800,
                Paint = "wood", Build = Kenney("televisionVintage", 0.55f) },
            new DecorSpec { Id = "radio", Name = "라디오", NameEn = "Radio", Snap = DecorSnap.Free, Category = DecorCategory.Appliance, Cost = 300,
                Paint = "wood", Build = Kenney("radio", 0.3f) },
            new DecorSpec { Id = "lamp", Name = "스탠드 조명", NameEn = "Floor Lamp", Snap = DecorSnap.Free, Category = DecorCategory.Appliance, Cost = 350,
                Paint = "lamp", Build = Kenney("lampRoundFloor", 1.6f) },
            new DecorSpec { Id = "fridge", Name = "냉장고", NameEn = "Fridge", Snap = DecorSnap.Free, Category = DecorCategory.Appliance, Cost = 600,
                Build = Kenney("kitchenFridgeLarge", 1.4f) },

            // 소품
            new DecorSpec { Id = "parasol", Name = "파라솔", NameEn = "Parasol", Snap = DecorSnap.Free, Category = DecorCategory.Prop, Cost = 400,
                Paint = "Decor_PlasticRed", Build = BuildParasol },
            new DecorSpec { Id = "icebox", Name = "아이스박스", NameEn = "Ice Box", Snap = DecorSnap.Free, Category = DecorCategory.Prop, Cost = 200,
                Paint = "Decor_IceBlue", Build = BuildIcebox },
            new DecorSpec { Id = "flower_pot", Name = "화분", NameEn = "Plant Pot", Snap = DecorSnap.Free, Category = DecorCategory.Prop, Cost = 100,
                Paint = "wood", Build = Kenney("pottedPlant", 1f) },
            new DecorSpec { Id = "rug", Name = "둥근 러그", NameEn = "Round Rug", Snap = DecorSnap.Free, Category = DecorCategory.Prop, Cost = 200, Flat = true,
                Paint = "carpet", Build = Kenney("rugRound", 0f, 1.8f) },
            new DecorSpec { Id = "box", Name = "택배 상자", NameEn = "Parcel Box", Snap = DecorSnap.Free, Category = DecorCategory.Prop, Cost = 50,
                Build = Kenney("cardboardBoxClosed", 0.45f) },
            new DecorSpec { Id = "trashcan", Name = "쓰레기통", NameEn = "Trash Can", Snap = DecorSnap.Free, Category = DecorCategory.Prop, Cost = 80,
                Paint = "metal", Build = Kenney("trashcan", 0.55f) },
        };

        [MenuItem("Tools/Cookie Clicker/Install Decor Mode (꾸미기 모드 설치)")]
        private static void InstallDecorMenu()
        {
            if (!FindDecorTargets(out var world, out var game, out var hud, out var player, out var mode)) return;
            InstallDecor(world, game, hud, player, player.GetComponentInChildren<Camera>(true), mode);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "꾸미기 모드를 설치했습니다.\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        [MenuItem("Tools/Cookie Clicker/Rebuild Decor Prefabs (꾸미기 프리팹 다시 만들기)")]
        private static void RebuildDecorPrefabsMenu()
        {
            if (!EditorUtility.DisplayDialog("Cookie Clicker",
                    "꾸미기 물건 프리팹과 아이콘을 처음 모양으로 다시 만듭니다.\n직접 고친 프리팹 모양은 사라집니다. (가격·이름 에셋은 유지)", "다시 만들기", "취소"))
                return;
            RebuildDecorPrefabsWithoutDialogs();
            EditorUtility.DisplayDialog("Cookie Clicker", "꾸미기 프리팹을 다시 만들었습니다.", "확인");
        }

        /// <summary>확인 창 없이 프리팹·아이콘을 다시 만든다 (배치 모드 -executeMethod용). 같은 경로에 덮어써서 씬·데이터의 연결은 유지된다.</summary>
        public static void RebuildDecorPrefabsWithoutDialogs() => EnsureDecorAssets(true);

        private static bool FindDecorTargets(out Transform world, out CookieGame game, out ExploreHudView hud,
            out FirstPersonController player, out ExploreModeController mode)
        {
            world = GameObject.Find("ExploreWorld")?.transform;
            game = Object.FindAnyObjectByType<CookieGame>(FindObjectsInactive.Include);
            hud = Object.FindAnyObjectByType<ExploreHudView>(FindObjectsInactive.Include);
            player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            mode = Object.FindAnyObjectByType<ExploreModeController>(FindObjectsInactive.Include);
            if (world != null && game != null && hud != null && player != null && mode != null) return true;
            EditorUtility.DisplayDialog("Cookie Clicker", "구경하기 월드가 없습니다. 먼저 Build Scene으로 씬을 만들어 주세요.", "확인");
            return false;
        }

        internal static void InstallDecor(Transform world, CookieGame game, ExploreHudView hud,
            FirstPersonController player, Camera playerCamera, ExploreModeController mode)
        {
            var catalog = EnsureDecorAssets(false);

            var oldLot = world.Find("DecorLot");
            if (oldLot != null) Object.DestroyImmediate(oldLot.gameObject);
            var oldHud = hud.transform.Find("DecorHud");
            if (oldHud != null) Object.DestroyImmediate(oldHud.gameObject);

            var area = CreateDecorLot(world, game, catalog);
            var decorHud = CreateDecorHud((RectTransform)hud.transform, game, catalog);

            var builder = decorHud.gameObject.AddComponent<DecorBuilder>();
            Wire(builder, ("game", game), ("area", area), ("hud", decorHud), ("playerCamera", playerCamera),
                ("player", player), ("exploreMode", mode), ("ghostMaterial", GetOrCreateGhostMaterial()),
                ("cancelAction", FindAction("UI/Cancel")));
            Wire(hud, ("decor", builder));
        }

        // ------------------------------------------------------------------ 부지

        private static DecorArea CreateDecorLot(Transform world, CookieGame game, DecorCatalog catalog)
        {
            var lot = new GameObject("DecorLot").transform;
            lot.SetParent(world, false);
            lot.localPosition = DecorLotOrigin;
            float width = DecorLotCells.x * DecorCell, depth = DecorLotCells.y * DecorCell;

            // 부지 표시: 다져진 흙 바닥 + 낮은 나무 테두리 + 간판. 충돌은 원래 땅이 담당하므로 모두 충돌 없음.
            Block(PrimitiveType.Cube, "LotGround", lot, Mat("Decor_LotGround", new Color(0.8f, 0.7f, 0.52f)),
                new Vector3(width * 0.5f, 0.01f, depth * 0.5f), new Vector3(width, 0.02f, depth));
            var curb = Mat("Decor_Curb", DecorWoodDark);
            Block(PrimitiveType.Cube, "Curb_South", lot, curb, new Vector3(width * 0.5f, 0.06f, 0f), new Vector3(width + 0.15f, 0.12f, 0.15f));
            Block(PrimitiveType.Cube, "Curb_North", lot, curb, new Vector3(width * 0.5f, 0.06f, depth), new Vector3(width + 0.15f, 0.12f, 0.15f));
            Block(PrimitiveType.Cube, "Curb_West", lot, curb, new Vector3(0f, 0.06f, depth * 0.5f), new Vector3(0.15f, 0.12f, depth));
            Block(PrimitiveType.Cube, "Curb_East", lot, curb, new Vector3(width, 0.06f, depth * 0.5f), new Vector3(0.15f, 0.12f, depth));
            // 부지 앞 왼쪽 모서리에 작게 (가운데에 두면 지은 집을 가린다)
            CreateWorldLabel(lot, "Sign_Lot", "내 평상 부지", new Vector3(2.5f, 1.6f, -0.4f), Quaternion.identity, 28, Cream);

            // 꾸미기 중에만 보이는 격자 선
            var grid = new GameObject("Grid").transform;
            grid.SetParent(lot, false);
            var line = Mat("Decor_GridLine", new Color(1f, 0.98f, 0.9f));
            for (int j = 0; j <= DecorLotCells.y; j++)
                Block(PrimitiveType.Cube, $"Line_Z{j}", grid, line, new Vector3(width * 0.5f, 0.03f, j * DecorCell), new Vector3(width, 0.01f, 0.05f));
            for (int i = 0; i <= DecorLotCells.x; i++)
                Block(PrimitiveType.Cube, $"Line_X{i}", grid, line, new Vector3(i * DecorCell, 0.03f, depth * 0.5f), new Vector3(0.05f, 0.01f, depth));
            foreach (var r in grid.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            grid.gameObject.SetActive(false);

            var placed = new GameObject("Placed").transform;
            placed.SetParent(lot, false);

            var dust = CreateDecorDust(lot);

            var area = lot.gameObject.AddComponent<DecorArea>();
            Wire(area, ("game", game), ("catalog", catalog), ("placedRoot", placed), ("gridVisual", grid.gameObject), ("dust", dust),
                ("cellSize", DecorCell), ("storeyHeight", DecorStorey));
            var serialized = new SerializedObject(area);
            serialized.FindProperty("cells").vector2IntValue = DecorLotCells;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return area;
        }

        /// <summary>놓을 때·회수할 때 바닥에서 퍼지는 흙먼지. DecorArea가 위치를 옮겨 가며 Emit으로 쏜다.</summary>
        private static ParticleSystem CreateDecorDust(Transform parent)
        {
            var ps = CreateParticleSystem("PlaceDust", parent, Vector3.zero, 120);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.93f, 0.85f, 0.7f, 0.8f), new Color(1f, 0.96f, 0.88f, 0.8f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 바닥을 따라 옆으로 둥글게 퍼진다.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.3f;
            limit.dampen = 0.15f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.5f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(0.05f, 0.4f));

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                ParticleMaterial("DecorDust", CookieArtFactory.SoftCircle.texture, ParticleBlend.Alpha);
            return ps;
        }

        // ------------------------------------------------------------------ UI

        private static DecorHudView CreateDecorHud(RectTransform hudRect, CookieGame game, DecorCatalog catalog)
        {
            var root = Stretch(CreateRect("DecorHud", hudRect), Vector2.zero, Vector2.one);

            var prompt = AddText(Place(CreateRect("Prompt", root), new Vector2(0.5f, 0.3f), new Vector2(600f, 50f)),
                "[B] 꾸미기", 28, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(prompt);
            prompt.gameObject.SetActive(false);

            var build = Stretch(CreateRect("Build", root), Vector2.zero, Vector2.one);

            // 위쪽 가운데: 빨간 "꾸미기 모드" 꼬리표
            var tag = CreateRect("Tag", build);
            tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 1f);
            tag.pivot = new Vector2(0.5f, 1f);
            tag.anchoredPosition = new Vector2(0f, -24f);
            tag.sizeDelta = new Vector2(260f, 54f);
            AddImage(tag, CookieArtFactory.SlantTag, ChiliRed);
            AddText(Stretch(CreateRect("Label", tag), Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-24f, 0f)),
                "꾸미기 모드", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            // 꼬리표 아래: 분류 탭 (Tab / Shift+Tab으로 바꾼다. 색은 DecorHudView가 칠한다). DecorCategory 순서와 같다.
            var tabs = CreateRect("Tabs", build);
            tabs.anchorMin = tabs.anchorMax = new Vector2(0.5f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.anchoredPosition = new Vector2(0f, -90f);
            string[] tabNames = { "구조물", "의자·테이블", "가전", "소품" };
            tabs.sizeDelta = new Vector2(tabNames.Length * 160f, 46f);
            var tabBackgrounds = new Image[tabNames.Length];
            var tabLabels = new Text[tabNames.Length];
            for (int t = 0; t < tabNames.Length; t++)
            {
                float w = 1f / tabNames.Length;
                var tab = Stretch(CreateRect($"Tab_{(DecorCategory)t}", tabs), new Vector2(t * w, 0f), new Vector2((t + 1) * w, 1f),
                    new Vector2(4f, 0f), new Vector2(-4f, 0f));
                tabBackgrounds[t] = AddImage(tab, CookieArtFactory.RoundedRect, Color.white);
                tabBackgrounds[t].pixelsPerUnitMultiplier = 0.8f;
                tabLabels[t] = AddText(Stretch(CreateRect("Label", tab), Vector2.zero, Vector2.one), tabNames[t], 22, ChiliRed,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            // 핫바 바로 위: 지금 고른 물건·가격, 못 놓는 이유
            var statusCard = BottomCenter(CreateRect("Status", build), 236f, new Vector2(980f, 50f));
            AddCard(statusCard, CardCream, CreamDeep, 0.5f, false);
            var status = AddText(Stretch(CreateRect("Text", statusCard), Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f)),
                "", 22, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);

            // 상태 문구 위: 칠할 색 팔레트 (C로 고르기). 첫 칸은 원래 색.
            var palette = BottomCenter(CreateRect("Palette", build), 296f, new Vector2(700f, 40f));
            var paletteLayout = palette.gameObject.AddComponent<HorizontalLayoutGroup>();
            paletteLayout.spacing = 10f;
            paletteLayout.childAlignment = TextAnchor.MiddleCenter;
            paletteLayout.childControlWidth = paletteLayout.childControlHeight = false;
            paletteLayout.childForceExpandWidth = paletteLayout.childForceExpandHeight = false;
            var paletteLabel = AddText(CreateRect("Label", palette), "[C] 색", 20, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            paletteLabel.rectTransform.sizeDelta = new Vector2(70f, 36f);
            AddOutline(paletteLabel);
            int swatchCount = catalog.PaintColors.Count + 1;
            var swatches = new Image[swatchCount];
            var swatchHighlights = new Outline[swatchCount];
            for (int s = 0; s < swatchCount; s++)
            {
                var swatch = CreateRect(s == 0 ? "Swatch_Original" : $"Swatch_{s - 1}", palette);
                swatch.sizeDelta = new Vector2(30f, 30f);
                swatches[s] = AddImage(swatch, CookieArtFactory.Dot, s == 0 ? CardCream : catalog.PaintColors[s - 1]);
                var rim = swatches[s].gameObject.AddComponent<Outline>();
                rim.effectColor = Ink;
                rim.effectDistance = new Vector2(3f, -3f);
                rim.enabled = false;
                swatchHighlights[s] = rim;
                if (s == 0)
                    AddText(Stretch(CreateRect("Label", swatch), Vector2.zero, Vector2.one), "원래", 11, InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            // 맨 아래: 물건 줄. 창(Viewport) 안에서 줄(Strip)이 옆으로 미끄러져 고른 칸이 가운데 온다.
            const float slotWidth = 104f, slotHeight = 132f, spacing = 8f, visibleSlots = 9f;
            int count = catalog.Pieces.Count;
            var viewport = BottomCenter(CreateRect("Hotbar", build), 80f, new Vector2(visibleSlots * (slotWidth + spacing) + 16f, slotHeight + 16f));
            AddImage(viewport, CookieArtFactory.RoundedRect, new Color(1f, 1f, 1f, 0.22f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var bar = CreateRect("Strip", viewport);
            bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.sizeDelta = new Vector2(count * (slotWidth + spacing), slotHeight);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)(spacing * 0.5f), 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var slots = new DecorSlotView[count];
            for (int i = 0; i < count; i++)
            {
                var slot = CreateRect($"Slot_{catalog.Pieces[i].Id}", bar);
                slot.sizeDelta = new Vector2(slotWidth, slotHeight);
                var card = AddCard(slot, CardCream, CreamDeep, 0.6f, false);
                var highlight = AddHighlight(card);
                var group = slot.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;

                var icon = AddImage(TopBand(CreateRect("Icon", slot), 10f, 72f, 16f, 16f), catalog.Pieces[i].Icon, Color.white);
                icon.preserveAspect = true;
                var name = AddText(TopBand(CreateRect("Name", slot), 84f, 22f, 4f, 4f), catalog.Pieces[i].DisplayName, 15, Ink,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 10;
                name.resizeTextMaxSize = 15;
                var cost = AddText(TopBand(CreateRect("Cost", slot), 106f, 20f, 4f, 4f), NumberFormatter.Format(catalog.Pieces[i].Cost), 15, InkSoft,
                    TextAnchor.MiddleCenter);
                var key = AddText(TopBand(CreateRect("Key", slot), 4f, 18f, 8f, 8f), i < 9 ? (i + 1).ToString() : "", 14, InkFaint,
                    TextAnchor.UpperLeft, FontStyle.Bold);

                slots[i] = slot.gameObject.AddComponent<DecorSlotView>();
                Wire(slots[i], ("background", card), ("highlight", highlight), ("icon", icon), ("nameText", name),
                    ("costText", cost), ("keyText", key), ("group", group));
            }

            build.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<DecorHudView>();
            Wire(view, ("game", game), ("buildRoot", build.gameObject), ("slots", slots), ("statusText", status), ("prompt", prompt.gameObject),
                ("tabBackgrounds", tabBackgrounds), ("tabLabels", tabLabels), ("slotStrip", bar), ("slotViewport", viewport),
                ("slotPitch", slotWidth + spacing), ("paintSwatches", swatches), ("paintHighlights", swatchHighlights));
            return view;
        }

        /// <summary>화면 아래 가운데에서 bottom만큼 띄운 곳에 놓는다.</summary>
        private static RectTransform BottomCenter(RectTransform rt, float bottom, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, bottom);
            rt.sizeDelta = size;
            return rt;
        }

        // ------------------------------------------------------------------ 에셋 (데이터·프리팹·아이콘)

        /// <summary>물건 데이터·프리팹·아이콘·목록을 준비한다. force면 프리팹과 아이콘을 처음 모양으로 다시 만든다.</summary>
        internal static DecorCatalog EnsureDecorAssets(bool force)
        {
            EditorFolders.Ensure(DecorDataFolder);
            EditorFolders.Ensure(DecorPrefabFolder);
            EditorFolders.Ensure(DecorIconFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<DecorCatalog>(DecorCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DecorCatalog>();
                AssetDatabase.CreateAsset(catalog, DecorCatalogPath);
            }
            var pieces = new List<DecorPiece>(catalog.Pieces);
            pieces.RemoveAll(p => p == null);

            foreach (var spec in DecorSpecs)
            {
                string prefabPath = $"{DecorPrefabFolder}/Decor_{spec.Id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                bool newPrefab = prefab == null || force;
                if (newPrefab) prefab = SaveDecorPrefab(spec, prefabPath);

                string piecePath = $"{DecorDataFolder}/Decor_{spec.Id}.asset";
                var piece = AssetDatabase.LoadAssetAtPath<DecorPiece>(piecePath);
                if (piece == null)
                {
                    piece = ScriptableObject.CreateInstance<DecorPiece>();
                    piece.EditorSetup(spec.Id, spec.Name, spec.NameEn, spec.Snap, spec.Category, spec.Group, spec.Cost, prefab);
                    AssetDatabase.CreateAsset(piece, piecePath);
                }
                else if (piece.Prefab == null)
                {
                    piece.EditorSetup(piece.Id, spec.Name, spec.NameEn, piece.Snap, piece.Category, spec.Group, piece.Cost, prefab);
                }

                string iconPath = $"{DecorIconFolder}/{spec.Id}.png";
                if (newPrefab || !File.Exists(iconPath)) RenderDecorIcon(prefab, iconPath);
                if (piece.Icon == null || newPrefab) piece.EditorSetIcon(AssetDatabase.LoadAssetAtPath<Sprite>(iconPath));
                piece.EditorSetKind(spec.Category, spec.Flat, spec.Paint);
                EditorUtility.SetDirty(piece);

                // 목록에 없는 기본 물건만 뒤에 붙인다 (직접 추가·정렬한 순서는 유지).
                if (!pieces.Contains(piece)) pieces.Add(piece);
            }

            catalog.EditorSetPieces(pieces);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static GameObject SaveDecorPrefab(DecorSpec spec, string path)
        {
            // 겉모습은 Model 아래에 만들고, 충돌 상자는 모두 루트로 옮긴다 (놓을 때 Model만 통통 튀고 충돌은 처음부터 제 크기).
            var root = new GameObject($"Decor_{spec.Id}");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            spec.Build(model);
            if (spec.Category == DecorCategory.Furniture) model.localScale = Vector3.one * FurnitureScale;
            foreach (var part in model.GetComponentsInChildren<BoxCollider>())
            {
                var t = part.transform;
                var box = root.AddComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(t.TransformPoint(part.center));
                box.size = Vector3.Scale(part.size, t.lossyScale);
                Object.DestroyImmediate(part);
            }
            Wire(root.AddComponent<PlacedDecor>(), ("model", model));
            if (spec.Seat) AddSeat(root.transform, model, spec.SeatZ);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// 앉는 자리 표시를 단다. 앉는 면 높이는 모델 메시에 위에서 광선을 쏴서 잰다
        /// (충돌 상자는 등받이까지 감싸서 높이가 맞지 않으므로 잠깐 메시 충돌을 붙였다 뗀다).
        /// </summary>
        private static void AddSeat(Transform root, Transform model, float seatZ)
        {
            var probes = new System.Collections.Generic.List<MeshCollider>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var probe = filter.gameObject.AddComponent<MeshCollider>();
                probe.sharedMesh = filter.sharedMesh;
                probes.Add(probe);
            }
            Physics.SyncTransforms();
            float height = 0.45f;
            var ray = new Ray(root.TransformPoint(new Vector3(0f, 3f, seatZ)), Vector3.down);
            float best = float.MinValue;
            foreach (var probe in probes)
                if (probe.Raycast(ray, out var hit, 5f)) best = Mathf.Max(best, root.InverseTransformPoint(hit.point).y);
            if (best > float.MinValue) height = best;
            foreach (var probe in probes) Object.DestroyImmediate(probe);

            var point = new GameObject("SitPoint").transform;
            point.SetParent(root, false);
            point.localPosition = new Vector3(0f, height, seatZ);
            Wire(root.gameObject.AddComponent<Seat>(), ("sitPoint", point));
        }

        private static Material GetOrCreateGhostMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(DecorGhostMaterialPath);
            if (material != null) return material;

            // 조명 영향 없는 반투명 단색 (URP Unlit을 Transparent로)
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(0.45f, 1f, 0.55f, 0.45f));
            EditorFolders.Ensure(DecorGhostMaterialPath.Substring(0, DecorGhostMaterialPath.LastIndexOf('/')));
            AssetDatabase.CreateAsset(material, DecorGhostMaterialPath);
            return material;
        }

        /// <summary>
        /// 프리팹을 비스듬히 내려다본 아이콘을 PNG로 찍는다. 검은 배경·흰 배경으로 두 번 찍어 차이로 투명도를 계산하므로
        /// 카메라가 알파를 지원하지 않아도 배경이 투명하게 나온다.
        /// </summary>
        private static void RenderDecorIcon(GameObject prefab, string path)
        {
            const int size = 256;
            var stage = new GameObject("DecorIconStage");
            stage.transform.position = new Vector3(0f, -2000f, 0f);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.transform.SetParent(stage.transform, false);

            var bounds = new Bounds(model.transform.position, Vector3.zero);
            // 김 같은 파티클은 크기 계산에서 뺀다 (넣으면 카메라가 너무 멀리 물러나 물건이 점처럼 찍힌다).
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer)) bounds.Encapsulate(r.bounds);

            var camera = new GameObject("DecorIconCamera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // 정면(+z)을 오른쪽 앞 위에서 내려다본다.
            var direction = Quaternion.Euler(28f, 145f, 0f) * Vector3.forward;
            camera.transform.rotation = Quaternion.LookRotation(direction);
            camera.transform.position = bounds.center - direction * 20f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 40f;
            // 화면에 들어오는 크기에 맞춰 줌
            float extent = 0f;
            for (int c = 0; c < 8; c++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                var local = camera.transform.InverseTransformPoint(corner);
                extent = Mathf.Max(extent, Mathf.Abs(local.x), Mathf.Abs(local.y));
            }
            camera.orthographicSize = extent * 1.08f;

            // 씬 조명 방향과 상관없이 정면이 밝게 나오도록, 찍는 동안만 씬 조명을 끄고 아이콘용 조명을 켠다.
            var sceneLights = new List<Light>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.enabled) { sceneLights.Add(l); l.enabled = false; }
            var key = new GameObject("DecorIconKey").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.25f;
            key.color = new Color(1f, 0.97f, 0.92f);
            key.transform.rotation = Quaternion.Euler(45f, 120f, 0f);
            var fill = new GameObject("DecorIconFill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;
            fill.transform.rotation = Quaternion.Euler(20f, 230f, 0f);

            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            var black = Capture(camera, rt, Color.black);
            var white = Capture(camera, rt, Color.white);
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(key.gameObject);
            Object.DestroyImmediate(fill.gameObject);
            foreach (var l in sceneLights) l.enabled = true;

            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                var b = black[i];
                var w = white[i];
                float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                pixels[i] = alpha > 0.001f ? new Color(b.r / alpha, b.g / alpha, b.b / alpha, alpha) : Color.clear;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static Color[] Capture(Camera camera, RenderTexture rt, Color background)
        {
            camera.backgroundColor = background;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            var pixels = texture.GetPixels();
            Object.DestroyImmediate(texture);
            return pixels;
        }

        // ------------------------------------------------------------------ 물건 모양 (블록아웃)
        // 모든 프리팹은 기준점이 바닥 가운데, 정면이 +z. 충돌은 BoxCollider만 쓴다 (DecorArea의 겹침 검사가 상자로 계산).

        private static Material DecorMat(string name, Color color) => Mat($"Decor_{name}", color);

        /// <summary>물건 조각 하나. collide면 BoxCollider를 붙인다 (원기둥·구도 상자로 근사).</summary>
        private static Transform Part(Transform parent, string name, PrimitiveType type, Material material,
            Vector3 position, Vector3 scale, bool collide = false)
        {
            var part = Block(type, name, parent, material, position, scale);
            if (collide)
            {
                var box = part.gameObject.AddComponent<BoxCollider>();
                // 원기둥 메시는 높이가 2라서 상자도 맞춰 늘린다.
                if (type == PrimitiveType.Cylinder) box.size = new Vector3(1f, 2f, 1f);
            }
            return part;
        }

        private static void AddBoxCollider(Transform root, Vector3 center, Vector3 size)
        {
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        private static void BuildWoodFloor(Transform root)
        {
            var light = DecorMat("Wood", DecorWood);
            var dark = DecorMat("WoodDark", DecorWoodDark);
            for (int k = 0; k < 4; k++)
                Part(root, $"Plank{k}", PrimitiveType.Cube, k % 2 == 0 ? light : DecorMat("WoodMid", Color.Lerp(DecorWood, DecorWoodDark, 0.3f)),
                    new Vector3(-0.75f + k * 0.5f, 0.1f, 0f), new Vector3(0.48f, 0.2f, 2f));
            Part(root, "Joist", PrimitiveType.Cube, dark, new Vector3(0f, 0.04f, 0f), new Vector3(1.98f, 0.08f, 1.98f));
            AddBoxCollider(root, new Vector3(0f, 0.1f, 0f), new Vector3(2f, 0.2f, 2f));
        }

        private static void BuildJangpanFloor(Transform root)
        {
            Part(root, "Base", PrimitiveType.Cube, DecorMat("WoodDark", DecorWoodDark), new Vector3(0f, 0.075f, 0f), new Vector3(2f, 0.15f, 2f));
            Part(root, "Jangpan", PrimitiveType.Cube, DecorMat("Jangpan", DecorJangpan), new Vector3(0f, 0.17f, 0f), new Vector3(1.94f, 0.06f, 1.94f));
            AddBoxCollider(root, new Vector3(0f, 0.1f, 0f), new Vector3(2f, 0.2f, 2f));
        }

        private static void BuildPillar(Transform root)
        {
            var dark = DecorMat("WoodDark", DecorWoodDark);
            Part(root, "Post", PrimitiveType.Cube, DecorMat("Wood", DecorWood), new Vector3(0f, DecorStorey * 0.5f, 0f), new Vector3(0.28f, DecorStorey, 0.28f));
            Part(root, "Base", PrimitiveType.Cube, dark, new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.16f, 0.36f));
            Part(root, "Cap", PrimitiveType.Cube, dark, new Vector3(0f, DecorStorey - 0.08f, 0f), new Vector3(0.36f, 0.16f, 0.36f));
            AddBoxCollider(root, new Vector3(0f, DecorStorey * 0.5f, 0f), new Vector3(0.3f, DecorStorey, 0.3f));
        }

        private static void WallTrims(Transform root)
        {
            var dark = DecorMat("WoodDark", DecorWoodDark);
            Part(root, "Skirting", PrimitiveType.Cube, dark, new Vector3(0f, 0.08f, 0f), new Vector3(2f, 0.16f, 0.2f));
            Part(root, "TopRail", PrimitiveType.Cube, dark, new Vector3(0f, DecorStorey - 0.06f, 0f), new Vector3(2f, 0.12f, 0.2f));
        }

        private static void BuildWall(Transform root)
        {
            Part(root, "Panel", PrimitiveType.Cube, DecorMat("Wood", DecorWood), new Vector3(0f, DecorStorey * 0.5f, 0f), new Vector3(2f, DecorStorey, 0.14f));
            var dark = DecorMat("WoodDark", DecorWoodDark);
            Part(root, "Batten1", PrimitiveType.Cube, dark, new Vector3(0f, 0.9f, 0f), new Vector3(2f, 0.04f, 0.17f));
            Part(root, "Batten2", PrimitiveType.Cube, dark, new Vector3(0f, 1.7f, 0f), new Vector3(2f, 0.04f, 0.17f));
            WallTrims(root);
            AddBoxCollider(root, new Vector3(0f, DecorStorey * 0.5f, 0f), new Vector3(2f, DecorStorey, 0.14f));
        }

        /// <summary>가운데에 구멍(holeBottom~holeTop, 폭 1m)이 뚫린 벽. 구멍 둘레 조각마다 충돌이 있어 구멍으로는 지나갈 수 있다.</summary>
        private static void HoledWall(Transform root, float holeBottom, float holeTop)
        {
            var wood = DecorMat("Wood", DecorWood);
            var dark = DecorMat("WoodDark", DecorWoodDark);
            const float t = 0.14f;
            Part(root, "Left", PrimitiveType.Cube, wood, new Vector3(-0.75f, DecorStorey * 0.5f, 0f), new Vector3(0.5f, DecorStorey, t), true);
            Part(root, "Right", PrimitiveType.Cube, wood, new Vector3(0.75f, DecorStorey * 0.5f, 0f), new Vector3(0.5f, DecorStorey, t), true);
            Part(root, "Top", PrimitiveType.Cube, wood, new Vector3(0f, (holeTop + DecorStorey) * 0.5f, 0f), new Vector3(1f, DecorStorey - holeTop, t), true);
            if (holeBottom > 0f)
                Part(root, "Bottom", PrimitiveType.Cube, wood, new Vector3(0f, holeBottom * 0.5f, 0f), new Vector3(1f, holeBottom, t), true);

            // 구멍 둘레 문틀
            Part(root, "FrameL", PrimitiveType.Cube, dark, new Vector3(-0.52f, (holeBottom + holeTop) * 0.5f, 0f), new Vector3(0.06f, holeTop - holeBottom + 0.06f, 0.2f));
            Part(root, "FrameR", PrimitiveType.Cube, dark, new Vector3(0.52f, (holeBottom + holeTop) * 0.5f, 0f), new Vector3(0.06f, holeTop - holeBottom + 0.06f, 0.2f));
            Part(root, "FrameT", PrimitiveType.Cube, dark, new Vector3(0f, holeTop + 0.03f, 0f), new Vector3(1.1f, 0.06f, 0.2f));
            if (holeBottom > 0f)
                Part(root, "FrameB", PrimitiveType.Cube, dark, new Vector3(0f, holeBottom - 0.03f, 0f), new Vector3(1.1f, 0.06f, 0.24f));
            WallTrims(root);
        }

        private static void BuildWindowWall(Transform root)
        {
            HoledWall(root, 0.95f, 1.85f);
            Part(root, "Glass", PrimitiveType.Cube, DecorMat("Glass", new Color(0.7f, 0.88f, 0.96f)), new Vector3(0f, 1.4f, 0f), new Vector3(1f, 0.9f, 0.03f), true);
            Part(root, "Mullion", PrimitiveType.Cube, DecorMat("WoodDark", DecorWoodDark), new Vector3(0f, 1.4f, 0f), new Vector3(0.04f, 0.9f, 0.06f));
        }

        private static void BuildDoorWall(Transform root) => HoledWall(root, 0f, 2.05f);

        private static void BuildStairs(Transform root)
        {
            // 정면(+z)에서 뒤(-z)로 올라가며, 한 칸(2m) 안에서 한 층(2.5m)을 올라간다. 단 높이 0.31m는 플레이어가 그냥 걸어 오를 수 있는 높이(stepOffset 0.45) 안쪽.
            // 보이는 모양은 디딤판(밟는 판) + 챌판(세운 판) + 양옆 옆판(사선 보)이고, 걷는 판정은 보이지 않는 계단 상자가 맡는다.
            const int steps = 8;
            const float width = 1.8f;
            float rise = DecorStorey / steps, run = DecorCell / steps;
            var tread = DecorMat("Wood", DecorWood);
            var riser = DecorMat("WoodMid", Color.Lerp(DecorWood, DecorWoodDark, 0.3f));
            var side = DecorMat("WoodDark", DecorWoodDark);
            for (int k = 0; k < steps; k++)
            {
                float top = (k + 1) * rise;
                float z = DecorCell * 0.5f - run * (k + 0.5f);
                Part(root, $"Tread{k}", PrimitiveType.Cube, tread, new Vector3(0f, top - 0.035f, z - 0.015f), new Vector3(width - 0.1f, 0.07f, run + 0.03f));
                Part(root, $"Riser{k}", PrimitiveType.Cube, riser, new Vector3(0f, top - rise * 0.5f - 0.035f, z + run * 0.5f - 0.015f), new Vector3(width - 0.12f, rise - 0.07f, 0.03f));

                var step = new GameObject($"StepCollider{k}").AddComponent<BoxCollider>();
                step.transform.SetParent(root, false);
                step.center = new Vector3(0f, top * 0.5f, z);
                step.size = new Vector3(width, top, run);
            }

            // 옆판: 아래 앞에서 위 뒤로 비스듬히 이어지는 두꺼운 보
            var slope = new Vector3(0f, DecorStorey, -DecorCell);
            foreach (float x in new[] { -width * 0.5f + 0.04f, width * 0.5f - 0.04f })
            {
                var beam = Part(root, x < 0f ? "StringerL" : "StringerR", PrimitiveType.Cube, side,
                    new Vector3(x, DecorStorey * 0.5f - 0.12f, 0f), new Vector3(0.08f, 0.32f, slope.magnitude + 0.1f));
                beam.localRotation = Quaternion.LookRotation(slope);
            }
        }

        private static void BuildTentRoof(Transform root)
        {
            // 포장마차 천막: 주황·파랑 줄무늬
            var orange = DecorMat("TentOrange", new Color(0.96f, 0.55f, 0.24f));
            var blue = DecorMat("TentBlue", new Color(0.27f, 0.5f, 0.8f));
            for (int k = 0; k < 4; k++)
                Part(root, $"Stripe{k}", PrimitiveType.Cube, k % 2 == 0 ? orange : blue,
                    new Vector3(-0.75f + k * 0.5f, 0.05f, 0f), new Vector3(0.5f, 0.1f, 2.1f));
            // 앞쪽으로 늘어진 천 자락
            Part(root, "Valance", PrimitiveType.Cube, orange, new Vector3(0f, -0.12f, 1.04f), new Vector3(2f, 0.3f, 0.03f));
            AddBoxCollider(root, new Vector3(0f, 0.05f, 0f), new Vector3(2f, 0.1f, 2f));
        }

        private const string KenneyFurnitureFolder = "Assets/CookieClicker/Models/Kenney/Furniture";

        /// <summary>
        /// Kenney 가구 모델을 넣는다. 원본은 크기가 작고 기준점이 모서리라서, height(m)에 맞게 키우고 바닥 가운데로 옮긴다.
        /// width를 주면 높이 대신 가로 폭에 맞춘다 (러그처럼 납작한 것). remap은 원본 재질 이름 → 우리 색 재질 (예: 나무 의자 → 빨간 플라스틱).
        /// 충돌은 모델 전체를 감싸는 상자 하나.
        /// </summary>
        private static Action<Transform> Kenney(string model, float height, params (string from, string material, Color color)[] remap) =>
            Kenney(model, height, 0f, remap);

        private static Action<Transform> Kenney(string model, float height, float width, params (string from, string material, Color color)[] remap) => root =>
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{KenneyFurnitureFolder}/{model}.glb");
            if (asset == null)
            {
                Debug.LogError($"[Decor] Kenney 모델이 없습니다: {model}.glb");
                return;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            instance.name = model;

            var bounds = RendererBounds(instance, root);
            float scale = width > 0f ? width / Mathf.Max(bounds.size.x, bounds.size.z) : height / bounds.size.y;
            // 모델 자체에 크기가 들어 있는 것도 있어서(쓰레기통 0.47배, 화분 2배) 덮어쓰지 않고 곱한다.
            instance.transform.localScale *= scale;
            bounds = RendererBounds(instance, root);
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = RendererBounds(instance, root);

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                    foreach (var (from, material, color) in remap)
                        if (materials[m] != null && materials[m].name == from) materials[m] = DecorMat(material, color);
                renderer.sharedMaterials = materials;
            }

            // 러그처럼 아주 얇은 것은 땅과 붙어 있지 않도록 살짝 띄운 상자로 판정한다.
            var size = bounds.size;
            var center = bounds.center;
            if (size.y < 0.06f)
            {
                size.y = 0.06f;
                center.y = 0.04f;
            }
            AddBoxCollider(root, center, size);
        };

        /// <summary>모델의 렌더러를 모두 감싸는 상자 (space 기준 좌표)</summary>
        private static Bounds RendererBounds(GameObject model, Transform space)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(space.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (var r in renderers)
            {
                var b = r.bounds;
                for (int c = 0; c < 8; c++)
                    bounds.Encapsulate(space.InverseTransformPoint(b.center + Vector3.Scale(b.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            }
            return bounds;
        }

        private static void BuildPyeongsang(Transform root)
        {
            var wood = DecorMat("Wood", DecorWood);
            var dark = DecorMat("WoodDark", DecorWoodDark);
            Part(root, "Deck", PrimitiveType.Cube, wood, new Vector3(0f, 0.4f, 0f), new Vector3(1.6f, 0.12f, 1.1f));
            Part(root, "Mat", PrimitiveType.Cube, DecorMat("Jangpan", DecorJangpan), new Vector3(0f, 0.47f, 0f), new Vector3(1.5f, 0.02f, 1f));
            for (int k = 0; k < 4; k++)
                Part(root, $"Leg{k}", PrimitiveType.Cube, dark,
                    new Vector3((k % 2 == 0 ? -1 : 1) * 0.72f, 0.17f, (k < 2 ? -1 : 1) * 0.47f), new Vector3(0.1f, 0.34f, 0.1f));
            AddBoxCollider(root, new Vector3(0f, 0.24f, 0f), new Vector3(1.6f, 0.48f, 1.1f));
        }

        /// <summary>
        /// 휴대용 가스버너 위에 본진과 같은 낙곱새 냄비(가벼운 LOD2 모델)를 올리고, 김이 피어오르게 한다.
        /// 김 파티클은 프리팹 안에 있어서 놓으면 바로 피어오르고, 미리보기(유령)에서는 꺼진다.
        /// </summary>
        private static void BuildBurnerPot(Transform root)
        {
            // 버너: 진회색 몸통 + 은색 윗판 + 불꽃 고리 + 냄비 받침 4개 + 빨간 손잡이
            var body = DecorMat("BurnerBody", new Color(0.24f, 0.25f, 0.28f));
            var steel = DecorMat("Steel", new Color(0.78f, 0.8f, 0.83f));
            var dark = DecorMat("Burner", new Color(0.22f, 0.22f, 0.24f));
            Part(root, "Body", PrimitiveType.Cube, body, new Vector3(0f, 0.05f, 0f), new Vector3(0.46f, 0.1f, 0.36f));
            Part(root, "TopPlate", PrimitiveType.Cube, steel, new Vector3(0f, 0.105f, -0.01f), new Vector3(0.44f, 0.012f, 0.32f));
            Part(root, "Ring", PrimitiveType.Cylinder, dark, new Vector3(0f, 0.115f, -0.01f), new Vector3(0.16f, 0.008f, 0.16f));
            for (int k = 0; k < 4; k++)
            {
                float angle = k * 90f + 45f;
                var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var prong = Part(root, $"Prong{k}", PrimitiveType.Cube, dark, new Vector3(0f, 0.125f, -0.01f) + dir * 0.1f, new Vector3(0.02f, 0.02f, 0.08f));
                prong.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
            Part(root, "FrontPanel", PrimitiveType.Cube, DecorMat("TentOrange", new Color(0.96f, 0.55f, 0.24f)), new Vector3(0f, 0.05f, 0.181f), new Vector3(0.3f, 0.05f, 0.004f));
            Part(root, "Knob", PrimitiveType.Cylinder, DecorMat("PlasticRed", new Color(0.88f, 0.2f, 0.16f)), new Vector3(0.17f, 0.05f, 0.19f), new Vector3(0.05f, 0.012f, 0.05f))
                .localRotation = Quaternion.Euler(90f, 0f, 0f);

            // 냄비: 본진 냄비와 같은 모델의 가벼운 버전
            var potAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{LodFolder}/Nakgopsae_LOD2.glb");
            if (potAsset != null)
            {
                var pot = (GameObject)PrefabUtility.InstantiatePrefab(potAsset, root);
                pot.name = "Pot";
                var bounds = RendererBounds(pot, root);
                pot.transform.localScale *= 0.44f / Mathf.Max(bounds.size.x, bounds.size.z);
                bounds = RendererBounds(pot, root);
                pot.transform.localPosition += new Vector3(-bounds.center.x, 0.13f - bounds.min.y, -0.01f - bounds.center.z);
            }

            var steam = CreateParticleSystem("Steam", root, new Vector3(0f, 0.3f, -0.01f), 40);
            var main = steam.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = steam.emission;
            emission.rateOverTime = 4f;
            var shape = steam.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var velocity = steam.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            var size = steam.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f));
            var color = steam.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(0.2f, 0.5f));
            steam.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                ParticleMaterial("Steam", CookieArtFactory.SoftCircle.texture, ParticleBlend.Alpha);

            AddBoxCollider(root, new Vector3(0f, 0.17f, 0f), new Vector3(0.46f, 0.34f, 0.44f));
        }

        private static void BuildParasol(Transform root)
        {
            var white = DecorMat("White", new Color(0.97f, 0.95f, 0.9f));
            Part(root, "Foot", PrimitiveType.Cylinder, DecorMat("Burner", new Color(0.22f, 0.22f, 0.24f)), new Vector3(0f, 0.04f, 0f), new Vector3(0.45f, 0.04f, 0.45f));
            Part(root, "Pole", PrimitiveType.Cylinder, white, new Vector3(0f, 1.2f, 0f), new Vector3(0.05f, 1.2f, 0.05f));
            // 천은 충돌이 없어서 밑에 의자·테이블을 놓을 수 있다.
            Part(root, "Canopy", PrimitiveType.Cylinder, DecorMat("PlasticRed", new Color(0.88f, 0.2f, 0.16f)), new Vector3(0f, 2.3f, 0f), new Vector3(2.2f, 0.04f, 2.2f));
            Part(root, "CanopyTop", PrimitiveType.Cylinder, white, new Vector3(0f, 2.36f, 0f), new Vector3(1.1f, 0.04f, 1.1f));
            AddBoxCollider(root, new Vector3(0f, 0.04f, 0f), new Vector3(0.45f, 0.08f, 0.45f));
            AddBoxCollider(root, new Vector3(0f, 1.2f, 0f), new Vector3(0.1f, 2.4f, 0.1f));
        }

        private static void BuildIcebox(Transform root)
        {
            Part(root, "Box", PrimitiveType.Cube, DecorMat("IceBlue", new Color(0.25f, 0.55f, 0.85f)), new Vector3(0f, 0.2f, 0f), new Vector3(0.6f, 0.4f, 0.4f));
            var white = DecorMat("White", new Color(0.97f, 0.95f, 0.9f));
            Part(root, "Lid", PrimitiveType.Cube, white, new Vector3(0f, 0.44f, 0f), new Vector3(0.62f, 0.08f, 0.42f));
            Part(root, "Handle", PrimitiveType.Cube, white, new Vector3(0f, 0.5f, 0f), new Vector3(0.3f, 0.04f, 0.06f));
            AddBoxCollider(root, new Vector3(0f, 0.24f, 0f), new Vector3(0.62f, 0.48f, 0.42f));
        }
    }
}
