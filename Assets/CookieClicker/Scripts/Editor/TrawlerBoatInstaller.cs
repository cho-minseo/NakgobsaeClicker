using CookieClicker.FX;
using CookieClicker.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 원양 새우잡이배 자리의 임시 박스 건물을 배 모델로 바꾼다. 단계(보유 개수)가 오를수록 배가 커진다.
    /// 배는 BoatBob으로 출렁이고, 굴뚝에서 연기가 나고, 배 둘레 물에는 하얀 물거품이 인다.
    /// 여러 번 실행해도 이전 배를 지우고 다시 만들기 때문에 결과가 같다. 크기·흔들림은 씬에서 Boat 오브젝트를 고치면 된다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string TrawlerBoatModelPath = "Assets/CookieClicker/Models/TrawlerBoat.glb";
        // 단계별 배 길이(m). StageThresholds(1, 10, 25, 50, 100)와 짝이다.
        private static readonly float[] TrawlerBoatLengths = { 11f, 12.2f, 13.4f, 14.8f, 16.2f };
        // 배 높이 중 물에 잠기는 비율
        private const float TrawlerDraft = 0.22f;
        // 원양어선 터 높이 (SeaInstaller가 물보다 조금 아래로 옮긴다). 씬을 처음 만들 때는 아직 옮기기 전이라 상수로 둔다.
        private const float TrawlerPlotY = -0.7f;

        [MenuItem("Tools/Cookie Clicker/Install Trawler Boat (원양어선 배 모델)")]
        private static void InstallTrawlerBoatMenu()
        {
            foreach (var plot in Object.FindObjectsByType<BuildingPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (plot.Building == null || plot.Building.Id != "shrimp_trawler") continue;
                bool ok = InstallTrawlerBoat(plot);
                if (ok) EditorSceneManager.MarkSceneDirty(plot.gameObject.scene);
                EditorUtility.DisplayDialog("Cookie Clicker",
                    ok ? "원양어선을 배 모델로 바꿨습니다.\nCtrl+S로 씬을 저장해 주세요."
                       : $"바꾸지 못했습니다.\n{TrawlerBoatModelPath}가 있는지, 건물 단계가 설치돼 있는지 확인해 주세요.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("Cookie Clicker", "씬에서 Plot_shrimp_trawler를 찾지 못했습니다.", "확인");
        }

        internal static bool InstallTrawlerBoat(BuildingPlot plot)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TrawlerBoatModelPath);
            var serialized = new SerializedObject(plot);
            var built = serialized.FindProperty("builtRoot").objectReferenceValue as GameObject;
            var stages = serialized.FindProperty("stages");
            if (modelAsset == null || built == null || stages.arraySize == 0) return false;

            // 꺼진 오브젝트는 Renderer.bounds가 비어서 크기를 못 재므로, 재는 동안만 켠다.
            bool wasActive = built.activeSelf;
            built.SetActive(true);

            // 물 높이를 건물 터 기준 좌표로 (원양어선 터는 물보다 조금 아래에 있다)
            float waterline = WaterLevel - TrawlerPlotY;
            for (int i = 0; i < stages.arraySize; i++)
            {
                var element = stages.GetArrayElementAtIndex(i);
                var visual = element.FindPropertyRelative("visual").objectReferenceValue as GameObject;
                if (visual == null) continue;
                bool stageActive = visual.activeSelf;
                visual.SetActive(true);

                foreach (var oldName in new[] { "Body", "Roof", "Door", "Boat", "BoatCollision", "Foam", "Gangway" })
                {
                    var old = visual.transform.Find(oldName);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }

                float length = TrawlerBoatLengths[Mathf.Min(i, TrawlerBoatLengths.Length - 1)];
                float top = BuildBoat(visual.transform, modelAsset, length, waterline);
                element.FindPropertyRelative("signHeight").floatValue = top + 1.4f;
                visual.SetActive(stageActive);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            built.SetActive(wasActive);
            return true;
        }

        /// <summary>배 한 척(출렁이는 Boat + 물에 고정된 Foam)을 만들고, 배 꼭대기 높이를 돌려준다.</summary>
        private static float BuildBoat(Transform stage, GameObject modelAsset, float length, float waterline)
        {
            var boat = new GameObject("Boat").transform;
            boat.SetParent(stage, false);
            AttachModel(boat, modelAsset, Vector3.zero); // 가장 긴 변 1, 중심이 원점 (배 길이 방향 = X)
            boat.localScale = Vector3.one * length;

            var bounds = ModelBounds(boat);
            Vector3 size = stage.InverseTransformVector(bounds.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            // 바닥이 물 아래로 TrawlerDraft만큼 잠기게 띄운다.
            boat.localPosition = new Vector3(0f, waterline - size.y * TrawlerDraft + size.y * 0.5f, 0f);
            boat.gameObject.AddComponent<BoatBob>();

            // 배 위를 걸을 수 있게 모델 모양 그대로 충돌을 준다. 충돌판까지 출렁이면 올라오는 갑판이 캐릭터를 파고들어
            // 배 밑으로 빠지므로, 충돌은 멈춰 있는 배 자세 그대로 따로 두고(BoatCollision) 보이는 모델만 살짝 출렁인다.
            var collision = new GameObject("BoatCollision").transform;
            collision.SetParent(stage, false);
            foreach (var filter in boat.GetComponentsInChildren<MeshFilter>())
            {
                var part = new GameObject(filter.name).transform;
                part.SetParent(collision, false);
                part.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                part.localScale = filter.transform.lossyScale;
                part.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }

            CreateBoatSmoke(boat, FindHighestPoint(boat), length);
            CreateBoatFoam(stage, new Vector3(0f, waterline + 0.04f, 0f), size);
            var gangwayTop = BuildGangway(stage, collision, length);
            BuildBulwark(stage, collision, gangwayTop);
            return boat.localPosition.y + size.y * 0.5f;
        }

        /// <summary>
        /// 선착장 위에서 배 난간 위로 걸친 경사로. 배 옆구리 끝(난간)을 실제로 재서 그 위에 걸치고,
        /// 너무 가파르지 않게(24도) 아래쪽 끝은 필요하면 선착장 판자 위로 더 길게 내려놓는다.
        /// </summary>
        private static Vector3 BuildGangway(Transform stage, Transform collision, float length)
        {
            float x = DockX * length / TrawlerBoatLengths[2]; // 배가 커지면 갑판 빈 자리도 같이 바깥으로
            float dockTop = DockTopY - TrawlerPlotY;           // 선착장 높이를 건물 터 기준 좌표로
            Physics.SyncTransforms();
            var colliders = collision.GetComponentsInChildren<Collider>();

            // 갑판 가운데에서 해안 쪽(+z)으로 걸어 나가며 배가 끝나는 곳(난간)과 그 높이를 찾는다.
            float edgeZ = 0f, railY = 0f;
            for (float z = 0f; z < length; z += 0.1f)
            {
                if (!BoatHeight(colliders, stage, new Vector3(x, 0f, z), out _)) break;
                edgeZ = z;
            }
            for (float z = edgeZ - 0.8f; z <= edgeZ; z += 0.1f)
                if (BoatHeight(colliders, stage, new Vector3(x, 0f, z), out float y)) railY = Mathf.Max(railY, y);

            // 위쪽 끝은 난간보다 조금 높게 갑판 쪽으로 넘겨 걸치고(배가 출렁여도 난간에 걸리지 않게), 아래쪽 끝은 선착장 위에
            var top = new Vector3(x, railY + 0.3f, edgeZ - 0.5f);
            float run = Mathf.Max((top.y - dockTop) / Mathf.Tan(24f * Mathf.Deg2Rad), DockEndZ + 0.8f - top.z);
            var bottom = new Vector3(DockX, dockTop, top.z + run);

            var gangway = new GameObject("Gangway").transform;
            gangway.SetParent(stage, false);
            var along = bottom - top;
            gangway.localPosition = (top + bottom) * 0.5f;
            gangway.localRotation = Quaternion.LookRotation(along);

            var wood = Mat("Dock_Plank", new Color(0.72f, 0.52f, 0.33f));
            var rope = Mat("Dock_Beam", new Color(0.42f, 0.29f, 0.19f));
            const float width = 1.3f;
            // 판 윗면이 두 끝점을 지나도록 두께 절반만큼 내린다.
            Block(PrimitiveType.Cube, "Board", gangway, wood, new Vector3(0f, -0.05f, 0f), new Vector3(width, 0.1f, along.magnitude), collider: true);
            // 미끄럼 막는 가로 턱 (보기만, 걷는 판정은 판 하나)
            for (float d = -along.magnitude * 0.5f + 0.3f; d < along.magnitude * 0.5f - 0.2f; d += 0.4f)
                Block(PrimitiveType.Cube, "Cleat", gangway, rope, new Vector3(0f, 0.02f, d), new Vector3(width - 0.1f, 0.04f, 0.06f));
            // 양옆 손잡이 줄과 기둥
            foreach (float side in new[] { -1f, 1f })
            {
                float sx = side * (width * 0.5f - 0.04f);
                Block(PrimitiveType.Cube, side < 0 ? "RailL" : "RailR", gangway, rope, new Vector3(sx, 0.75f, 0f), new Vector3(0.06f, 0.06f, along.magnitude));
                foreach (float end in new[] { -1f, 1f })
                    Block(PrimitiveType.Cube, "Post", gangway, rope, new Vector3(sx, 0.38f, end * (along.magnitude * 0.5f - 0.1f)), new Vector3(0.06f, 0.76f, 0.06f));
            }
            return top;
        }

        /// <summary>
        /// 난간이 낮아서 걷다가 바다로 떨어지지 않게, 갑판 둘레에 보이지 않는 벽을 세운다 (경사로 자리만 비운다).
        /// 난간을 밟고 넘어가지 못하게 벽은 난간보다 조금 안쪽에, 갑판 위 1.3m(점프 1.2m보다 높게)까지 세운다.
        /// 배에서 내릴 때는 경사로로 내려간다. 배 충돌판과 같이 BoatCollision 아래에 둔다.
        /// </summary>
        private static void BuildBulwark(Transform stage, Transform collision, Vector3 gap)
        {
            var colliders = collision.GetComponentsInChildren<Collider>();
            if (!BoatHeight(colliders, stage, new Vector3(gap.x, 0f, 0f), out float deckY)) return;

            // 갑판 가운데에서 사방으로 걸어 나가며 배 가장자리(가장 멀리 배가 있는 곳)를 찾는다. 모델 틈새에서 멈추지 않게 끝까지 본다.
            const int rays = 28;
            var edge = new Vector3[rays];
            for (int i = 0; i < rays; i++)
            {
                float a = i * Mathf.PI * 2f / rays;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float last = 0f;
                for (float d = 0f; d < 20f; d += 0.1f)
                {
                    if (BoatHeight(colliders, stage, dir * d, out _)) last = d;
                }
                edge[i] = dir * Mathf.Max(0f, last - 0.3f);
            }

            var bulwark = new GameObject("Bulwark").transform;
            bulwark.SetParent(collision, false);
            bulwark.position = stage.TransformPoint(new Vector3(0f, deckY, 0f));
            const float wallHeight = 1.3f;
            for (int i = 0; i < rays; i++)
            {
                Vector3 a = edge[i], b = edge[(i + 1) % rays];
                var middle = (a + b) * 0.5f;
                if (Vector2.Distance(new Vector2(middle.x, middle.z), new Vector2(gap.x, gap.z)) < 1.1f) continue; // 경사로 자리
                var wall = new GameObject($"Wall{i}").AddComponent<BoxCollider>();
                wall.transform.SetParent(bulwark, false);
                wall.transform.position = stage.TransformPoint(middle + Vector3.up * (deckY + wallHeight * 0.5f));
                wall.transform.rotation = stage.rotation * Quaternion.LookRotation(b - a);
                wall.size = new Vector3(0.2f, wallHeight, (b - a).magnitude + 0.2f);
            }
        }

        /// <summary>배 충돌체를 위에서 아래로 쏴서 (stage 기준) 그 자리의 배 높이를 잰다.</summary>
        private static bool BoatHeight(Collider[] colliders, Transform stage, Vector3 local, out float height)
        {
            height = float.MinValue;
            var origin = stage.TransformPoint(new Vector3(local.x, 50f, local.z));
            foreach (var c in colliders)
                if (c.Raycast(new Ray(origin, Vector3.down), out var hit, 100f))
                    height = Mathf.Max(height, stage.InverseTransformPoint(hit.point).y);
            return height > float.MinValue;
        }

        private static Bounds ModelBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        /// <summary>모델에서 가장 높은 꼭짓점(= 굴뚝 꼭대기)을 Boat 기준 좌표로 찾는다.</summary>
        private static Vector3 FindHighestPoint(Transform boat)
        {
            Vector3 best = Vector3.zero;
            float bestY = float.MinValue;
            foreach (var filter in boat.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                foreach (var v in mesh.vertices)
                {
                    var world = filter.transform.TransformPoint(v);
                    if (world.y > bestY) { bestY = world.y; best = world; }
                }
            }
            if (bestY == float.MinValue)
            {
                var b = ModelBounds(boat);
                best = new Vector3(b.center.x, b.max.y, b.center.z);
            }
            return boat.InverseTransformPoint(best);
        }

        /// <summary>굴뚝에서 뭉게뭉게 올라가는 연기 (월드 기준이라 배가 흔들려도 꼬리를 남긴다).</summary>
        private static void CreateBoatSmoke(Transform boat, Vector3 chimneyTop, float length)
        {
            var ps = CreateParticleSystem("Smoke", boat, chimneyTop, 60);
            ps.transform.localScale = Vector3.one / length; // 배 크기와 상관없이 연기 크기를 m 단위로 정한다
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.53f, 0.52f, 0.7f), new Color(0.75f, 0.73f, 0.72f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 3f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // 위로 올라가면서 바닷바람에 한쪽으로 흘러간다.
            velocity.x = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.6f));
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(0.1f, 0.4f));
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                ParticleMaterial("Steam", CookieArtFactory.SoftCircle.texture, ParticleBlend.Alpha);
        }

        /// <summary>배 둘레 물 위에 하얀 물거품이 생겼다 퍼지며 사라진다. 물에 붙어 있도록 출렁이는 배가 아니라 단계 아래에 둔다.</summary>
        private static void CreateBoatFoam(Transform stage, Vector3 position, Vector3 boatSize)
        {
            var ps = CreateParticleSystem("Foam", stage, position, 120);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 28f;
            // 배 모양에 맞춘 납작한 타원 테두리에서 바깥으로 퍼진다.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.scale = new Vector3(boatSize.x * 0.5f, boatSize.z * 0.55f, 1f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(0.15f, 0.45f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = ParticleMaterial("Foam", CookieArtFactory.SoftCircle.texture, ParticleBlend.Alpha);
        }
    }
}
