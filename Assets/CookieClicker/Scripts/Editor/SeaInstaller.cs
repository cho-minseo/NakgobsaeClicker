using System.IO;
using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 바다(점점 깊어지는 해저 + 물 표면)와 수영·잠수에 필요한 컴포넌트·UI를 설치한다.
    /// 여러 번 실행해도 기존 바다를 지우고 다시 만들기 때문에 결과가 같다 (씬 전체를 다시 만들 필요 없음).
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string SeabedMeshPath = "Assets/CookieClicker/Art/Generated/World/Seabed.asset";
        private const string WaterNormalPath = "Assets/CookieClicker/Art/Generated/World/WaterNormal.png";
        private const string WaterMaterialPath = "Assets/CookieClicker/Art/Generated/World/Water.mat";

        // 지형 수치 (월드 기준: 본진이 원점, 해안선은 z = -30, 맵 끝은 z = -90)
        private const float ShoreZ = -30f;
        private const float SeaLength = 60f;
        private const float MapHalf = 90f;
        private const float WaterLevel = -0.4f;
        private const float WadingDistance = 10f;   // 해안에서 이 거리까지는 걸어 들어갈 수 있는 얕은 물
        private const float WadingDepth = 1.2f;
        private const float MaxSeaDepth = 22f;

        [MenuItem("Tools/Cookie Clicker/Install Sea & Swimming (바다·수영 설치)", priority = 21)]
        private static void InstallSeaMenu()
        {
            var world = GameObject.Find("ExploreWorld");
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            var hud = Object.FindAnyObjectByType<ExploreHudView>(FindObjectsInactive.Include);
            if (world == null || player == null || hud == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "ExploreWorld / Player / ExploreHud를 찾지 못했습니다.", "확인");
                return;
            }
            InstallSea(world.transform, player, hud);
            EditorSceneManager.MarkSceneDirty(world.scene);
        }

        public static void InstallSea(Transform world, FirstPersonController player, ExploreHudView hud)
        {
            if (font == null) font = AssetDatabase.LoadAssetAtPath<Font>(FontPath) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var ground = world.Find("Ground");

            // 1) 땅은 해안선까지만. 그 너머는 해저 메시가 맡는다.
            var land = ground.Find("Base");
            land.localPosition = new Vector3(0f, -0.5f, (ShoreZ + MapHalf) * 0.5f);
            land.localScale = new Vector3(MapHalf * 2f, 1f, MapHalf - ShoreZ);

            foreach (var old in new[] { "Zone_Sea", "Sea" })
            {
                var t = ground.Find(old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            // 2) 바다 루트 = 수면 위치. WaterVolume이 여기서부터 아래를 물로 본다.
            var sea = new GameObject("Sea").transform;
            sea.SetParent(ground, false);
            sea.localPosition = new Vector3(0f, WaterLevel, ShoreZ - SeaLength * 0.5f);
            var volume = sea.gameObject.AddComponent<WaterVolume>();
            var so = new SerializedObject(volume);
            so.FindProperty("area").vector2Value = new Vector2(MapHalf * 2f, SeaLength);
            so.FindProperty("depth").floatValue = MaxSeaDepth + 4f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 3) 해저: 해안에서 멀어질수록 깊어지는 메시 (+ 걸을 수 있게 MeshCollider)
            var seabed = new GameObject("Seabed").transform;
            seabed.SetParent(sea, false);
            seabed.localPosition = new Vector3(0f, -WaterLevel, SeaLength * 0.5f - ShoreZ); // 메시 좌표 = Ground 기준 좌표
            var mesh = BuildSeabedMesh();
            seabed.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            seabed.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mat("Seabed_Sand", new Color(0.86f, 0.78f, 0.58f));
            seabed.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;

            // 바다 가장자리 아래쪽이 허공으로 보이지 않게 깊은 바다색 벽을 세운다 (충돌은 Walls가 담당).
            var edgeMat = Mat("Sea_Edge", new Color(0.05f, 0.2f, 0.32f));
            float edgeHeight = MaxSeaDepth + 3f, edgeY = -edgeHeight * 0.5f - 0.1f;
            Block(PrimitiveType.Cube, "Edge_Left", sea, edgeMat, new Vector3(-MapHalf, edgeY, 0f), new Vector3(0.5f, edgeHeight, SeaLength));
            Block(PrimitiveType.Cube, "Edge_Right", sea, edgeMat, new Vector3(MapHalf, edgeY, 0f), new Vector3(0.5f, edgeHeight, SeaLength));
            Block(PrimitiveType.Cube, "Edge_Far", sea, edgeMat, new Vector3(0f, edgeY, -SeaLength * 0.5f), new Vector3(MapHalf * 2f, edgeHeight, 0.5f));

            // 4) 수면: 반투명 + 잔물결 노멀맵. 충돌 없음 (그대로 들어갈 수 있게).
            var surface = Block(PrimitiveType.Cube, "Surface", sea, GetOrCreateWaterMaterial(), Vector3.zero,
                new Vector3(MapHalf * 2f, 0.02f, SeaLength));
            surface.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // 5) 맵 끝 보이지 않는 벽을 해저 아래까지 내려서 물속으로 빠져나가지 못하게 한다.
            foreach (Transform wall in ground.Find("Walls"))
            {
                wall.localPosition = new Vector3(wall.localPosition.x, -8f, wall.localPosition.z);
                wall.localScale = new Vector3(wall.localScale.x, 36f, wall.localScale.z);
            }

            // 6) 바다 위치에 있던 건물은 땅으로, 원양어선은 배라서 물에 띄운다.
            MovePlot(world, "shrimp_farm", new Vector3(-14f, 0f, -25f));
            MovePlot(world, "franchise", new Vector3(48f, 0f, -20f));
            MovePlot(world, "shrimp_trawler", new Vector3(14f, -0.7f, -55f));

            InstallSwimming(player, hud);
        }

        private static Mesh BuildSeabedMesh()
        {
            const float cell = 2f;
            int cols = Mathf.RoundToInt(MapHalf * 2f / cell) + 1, rows = Mathf.RoundToInt(SeaLength / cell) + 1;
            var vertices = new Vector3[cols * rows];
            var uvs = new Vector2[cols * rows];
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                float x = -MapHalf + c * cell;
                float distance = r * cell;                 // 해안에서 떨어진 거리
                float z = ShoreZ - distance;
                float depth = distance < WadingDistance
                    ? distance / WadingDistance * WadingDepth
                    : WadingDepth + (MaxSeaDepth - WadingDepth) * Mathf.Pow((distance - WadingDistance) / (SeaLength - WadingDistance), 1.4f);
                // 해안 근처는 매끈하게, 멀어질수록 모래 언덕처럼 울퉁불퉁하게
                float bumps = (Mathf.PerlinNoise(x * 0.06f + 10f, z * 0.06f + 3f) - 0.5f) * 1.6f * Mathf.Clamp01(distance / 12f);
                vertices[r * cols + c] = new Vector3(x, -depth + bumps, z);
                uvs[r * cols + c] = new Vector2(x / 4f, z / 4f);
            }

            var triangles = new int[(cols - 1) * (rows - 1) * 6];
            int i = 0;
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols - 1; c++)
            {
                int a = r * cols + c, b = a + 1, d = a + cols, e = d + 1;
                // 위에서 봤을 때 시계 방향 (z가 줄어드는 방향으로 행이 늘어남)
                triangles[i++] = a; triangles[i++] = b; triangles[i++] = d;
                triangles[i++] = b; triangles[i++] = e; triangles[i++] = d;
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SeabedMeshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "Seabed";
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew)
            {
                EditorFolders.Ensure(Path.GetDirectoryName(SeabedMeshPath).Replace('\\', '/'));
                AssetDatabase.CreateAsset(mesh, SeabedMeshPath);
            }
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material GetOrCreateWaterMaterial()
        {
            // 잔물결 노멀맵을 코드로 생성 (다운로드·라이선스 불필요)
            if (!File.Exists(WaterNormalPath))
            {
                const int n = 256;
                var heights = new float[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * Mathf.PI * 2f, v = y / (float)n * Mathf.PI * 2f;
                    // 정수 주기 사인파를 겹쳐 이음새 없이 반복되는 물결
                    heights[y * n + x] = Mathf.Sin(u * 3f + Mathf.Sin(v * 2f)) * 0.5f + Mathf.Sin(v * 5f + Mathf.Cos(u * 3f) * 1.5f) * 0.35f
                                         + Mathf.Sin((u + v) * 7f) * 0.15f;
                }
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = heights[y * n + (x + 1) % n] - heights[y * n + (x - 1 + n) % n];
                    float dy = heights[((y + 1) % n) * n + x] - heights[((y - 1 + n) % n) * n + x];
                    var normal = new Vector3(-dx * 2f, -dy * 2f, 1f).normalized;
                    tex.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f));
                }
                EditorFolders.Ensure(Path.GetDirectoryName(WaterNormalPath).Replace('\\', '/'));
                File.WriteAllBytes(WaterNormalPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(WaterNormalPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(WaterNormalPath);
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, WaterMaterialPath);
            }
            // URP Lit을 반투명(알파 블렌드)으로
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(0.2f, 0.58f, 0.82f, 0.55f));
            mat.SetFloat("_Smoothness", 0.92f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalPath));
            mat.SetFloat("_BumpScale", 0.5f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTextureScale("_BaseMap", new Vector2(MapHalf * 2f / 8f, SeaLength / 8f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void MovePlot(Transform world, string buildingId, Vector3 position)
        {
            foreach (var plot in world.GetComponentsInChildren<BuildingPlot>(true))
            {
                if (plot.Building == null || plot.Building.Id != buildingId) continue;
                plot.transform.localPosition = position;
                plot.transform.localRotation = Quaternion.LookRotation(new Vector3(-position.x, 0f, -position.z));
            }
        }

        /// <summary>
        /// 에디터에서 없는 컴포넌트를 GetComponent하면 "가짜 null"이 돌아와 ?? 연산자를 통과해 버린다.
        /// Unity의 == null 비교로 확인한 뒤 추가한다.
        /// </summary>
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        /// <summary>플레이어에 잠수 키·산소·물속 효과를 붙이고, 구경하기 UI에 산소 게이지와 안내를 추가한다.</summary>
        private static void InstallSwimming(FirstPersonController player, ExploreHudView hud)
        {
            Wire(player, ("diveAction", FindAction("Player/Crouch")));

            var breath = GetOrAdd<PlayerBreath>(player.gameObject);
            Wire(breath, ("player", player));

            var hudRect = (RectTransform)hud.transform;
            foreach (var old in new[] { "UnderwaterOverlay", "Breath", "Alert" })
            {
                var t = hudRect.Find(old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            // 물속 푸른 화면 (다른 UI보다 뒤에 깔리도록 맨 앞 자식)
            var overlayRect = Stretch(CreateRect("UnderwaterOverlay", hudRect), Vector2.zero, Vector2.one);
            overlayRect.SetSiblingIndex(0);
            var overlay = AddImage(overlayRect, null, new Color(0.2f, 0.5f, 0.7f, 0.3f));
            overlay.enabled = false;

            var effect = GetOrAdd<UnderwaterEffect>(player.gameObject);
            Wire(effect, ("player", player), ("overlay", overlay));

            // 산소 게이지: 화면 아래 가운데
            var breathRoot = Place(CreateRect("Breath", hudRect), new Vector2(0.5f, 0.13f), new Vector2(420f, 30f));
            AddImage(breathRoot, CookieArtFactory.RoundedRect, new Color(0f, 0f, 0f, 0.45f));
            var fillRect = Stretch(CreateRect("Fill", breathRoot), Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var fill = AddImage(fillRect, CookieArtFactory.RoundedRect, new Color(0.55f, 0.85f, 1f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            AddOutline(AddText(Stretch(CreateRect("Label", breathRoot), Vector2.zero, Vector2.one), "산소", 20, Color.white, TextAnchor.MiddleCenter));
            breathRoot.gameObject.SetActive(false);

            var alert = AddText(Place(CreateRect("Alert", hudRect), new Vector2(0.5f, 0.62f), new Vector2(900f, 80f)), "", 44,
                new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleCenter);
            AddOutline(alert);

            var hintTransform = hudRect.Find("Hint");
            var hint = hintTransform != null ? hintTransform.GetComponent<Text>() : null;
            Wire(hud, ("player", player), ("breath", breath), ("breathRoot", breathRoot.gameObject), ("breathFill", fill),
                ("alertText", alert), ("hintText", hint));
        }
    }
}
