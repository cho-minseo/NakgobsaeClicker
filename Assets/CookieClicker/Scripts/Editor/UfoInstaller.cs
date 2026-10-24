using System.IO;
using CookieClicker.FX;
using CookieClicker.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 우주 소 방목장 옆 하늘에 떠서 빙글빙글 도는 UFO. 테두리 불빛이 차례로 돌고, 아래로 빛줄기를 쏘아
    /// 소를 끌어 올렸다가 떨어뜨린다. 모델 없이 기본 도형으로 만들어서 위치·색·크기를 씬에서 바로 고칠 수 있다.
    /// Built 아래에 넣기 때문에 방목장을 하나라도 사야 보인다. 여러 번 실행해도 이전 UFO를 지우고 다시 만든다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string BeamMeshPath = "Assets/CookieClicker/Art/Generated/World/BeamCone.asset";
        private const float UfoHoverHeight = 16f;
        private const float UfoDiameter = 7f;
        private const int UfoLightCount = 16;
        private static readonly Color UfoGreen = new Color(0.55f, 1f, 0.7f);

        [MenuItem("Tools/Cookie Clicker/Install UFO (우주 소 방목장 UFO)")]
        private static void InstallUfoMenu()
        {
            foreach (var plot in Object.FindObjectsByType<BuildingPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (plot.Building == null || plot.Building.Id != "space_ranch") continue;
                bool ok = InstallUfo(plot);
                if (ok) EditorSceneManager.MarkSceneDirty(plot.gameObject.scene);
                EditorUtility.DisplayDialog("Cookie Clicker", ok ? "UFO를 설치했습니다.\nCtrl+S로 씬을 저장해 주세요." : "설치하지 못했습니다.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("Cookie Clicker", "씬에서 Plot_space_ranch를 찾지 못했습니다.", "확인");
        }

        internal static bool InstallUfo(BuildingPlot plot)
        {
            var built = new SerializedObject(plot).FindProperty("builtRoot").objectReferenceValue as GameObject;
            if (built == null) return false;
            var old = built.transform.Find("UFO");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // 건물(상자 더미 포함) 옆 풀밭 위
            var body = FindBuildingBody(built.transform);
            float buildingSize = body != null ? body.localScale.x : 8f;
            var ufo = new GameObject("UFO").transform;
            ufo.SetParent(built.transform, false);
            ufo.localPosition = new Vector3(-(buildingSize * 0.75f + 8f), 0f, 2f);

            var hover = BuildSaucer(ufo);
            BuildBeam(ufo);
            BuildCow(ufo);
            return hover != null;
        }

        /// <summary>접시 몸통·유리 돔(안에 외계인)·테두리 전구. Hover가 돌면서 둥실거린다.</summary>
        private static Transform BuildSaucer(Transform ufo)
        {
            var hover = new GameObject("Hover").transform;
            hover.SetParent(ufo, false);
            hover.localPosition = new Vector3(0f, UfoHoverHeight, 0f);
            // 살짝 기운 채로 제자리를 돌아서 팽이처럼 흔들려 보인다.
            hover.localRotation = Quaternion.Euler(6f, 0f, 0f);
            var orbiter = hover.gameObject.AddComponent<Orbiter>();
            var so = new SerializedObject(orbiter);
            so.FindProperty("degreesPerSecond").floatValue = 30f;
            so.FindProperty("bobHeight").floatValue = 0.45f;
            so.FindProperty("bobSpeed").floatValue = 1.3f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var hull = Mat("Ufo_Hull", new Color(0.74f, 0.77f, 0.82f));
            hull.SetFloat("_Metallic", 0.65f);
            hull.SetFloat("_Smoothness", 0.75f);
            var dark = Mat("Ufo_Rim", new Color(0.32f, 0.35f, 0.42f));
            dark.SetFloat("_Metallic", 0.5f);
            dark.SetFloat("_Smoothness", 0.6f);

            Block(PrimitiveType.Sphere, "Hull", hover, hull, Vector3.zero, new Vector3(UfoDiameter, 1.2f, UfoDiameter), collider: true);
            Block(PrimitiveType.Cylinder, "Rim", hover, dark, Vector3.zero, new Vector3(UfoDiameter + 0.5f, 0.08f, UfoDiameter + 0.5f));
            Block(PrimitiveType.Sphere, "Belly", hover, dark, new Vector3(0f, -0.4f, 0f), new Vector3(3.6f, 1f, 3.6f));
            Block(PrimitiveType.Cylinder, "BellyGlow", hover, GlowMat("Ufo_Glow", UfoGreen, 3f), new Vector3(0f, -0.88f, 0f), new Vector3(2.2f, 0.04f, 2.2f));

            // 돔 안의 작은 외계인 (초록 머리 + 까만 큰 눈 + 더듬이)
            var alien = new GameObject("Alien").transform;
            alien.SetParent(hover, false);
            alien.localPosition = new Vector3(0f, 0.75f, 0f);
            var skin = Mat("Ufo_Alien", new Color(0.5f, 0.85f, 0.4f));
            var black = Mat("Ufo_AlienEye", new Color(0.05f, 0.05f, 0.08f));
            Block(PrimitiveType.Sphere, "Head", alien, skin, new Vector3(0f, 0.25f, 0f), new Vector3(0.75f, 0.85f, 0.7f));
            foreach (float side in new[] { -1f, 1f })
            {
                Block(PrimitiveType.Sphere, "Eye", alien, black, new Vector3(side * 0.17f, 0.33f, 0.3f), new Vector3(0.2f, 0.28f, 0.1f),
                    rotation: Quaternion.Euler(0f, 0f, side * -20f));
                Block(PrimitiveType.Cylinder, "Antenna", alien, skin, new Vector3(side * 0.15f, 0.78f, 0f), new Vector3(0.04f, 0.18f, 0.04f),
                    rotation: Quaternion.Euler(0f, 0f, side * -20f));
                Block(PrimitiveType.Sphere, "AntennaTip", alien, GlowMat("Ufo_AntennaTip", new Color(1f, 0.45f, 0.6f), 2f),
                    new Vector3(side * 0.22f, 0.95f, 0f), Vector3.one * 0.12f);
            }
            // 유리 돔: 반투명(파티클 셰이더)이라 안의 외계인이 비쳐 보인다.
            var glass = ParticleMaterial("UfoDome", null, ParticleBlend.Alpha);
            glass.SetColor("_BaseColor", new Color(0.65f, 0.95f, 1f, 0.3f));
            EditorUtility.SetDirty(glass);
            var dome = Block(PrimitiveType.Sphere, "Dome", hover, glass, new Vector3(0f, 0.55f, 0f), new Vector3(2.6f, 2.4f, 2.6f));
            dome.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // 테두리 전구: ChaseLights가 차례로 밝혀서 불빛이 빙글빙글 도는 것처럼 보인다.
            var lightRoot = new GameObject("Lights").transform;
            lightRoot.SetParent(hover, false);
            var bulb = GlowMat("Ufo_Light", UfoGreen, 1f);
            var renderers = new Renderer[UfoLightCount];
            for (int i = 0; i < UfoLightCount; i++)
            {
                float a = i * Mathf.PI * 2f / UfoLightCount;
                var light = Block(PrimitiveType.Sphere, $"Light{i}", lightRoot, bulb,
                    new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (UfoDiameter * 0.5f + 0.2f) + Vector3.up * 0.08f, Vector3.one * 0.32f);
                renderers[i] = light.GetComponent<Renderer>();
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }
            Wire(hover.gameObject.AddComponent<ChaseLights>(), ("lights", renderers));
            return hover;
        }

        /// <summary>UFO 배에서 땅까지 퍼지는 반투명 빛줄기 + 위로 빨려 올라가는 반짝이 + 땅의 빛 동그라미.</summary>
        private static void BuildBeam(Transform ufo)
        {
            float top = UfoHoverHeight - 0.6f;
            var beam = new GameObject("Beam").transform;
            beam.SetParent(ufo, false);
            beam.localPosition = new Vector3(0f, top, 0f);
            beam.localScale = new Vector3(1f, top, 1f);
            beam.gameObject.AddComponent<MeshFilter>().sharedMesh = GetOrCreateBeamMesh();
            var beamMat = ParticleMaterial("UfoBeam", null, ParticleBlend.Additive);
            beamMat.SetFloat("_Cull", 0f); // 안쪽 면도 보이게 (빛줄기 안에 들어가도 보인다)
            // 더해지는(Additive) 빛이라 색을 어둡게 줄수록 투명해 보인다.
            beamMat.SetColor("_BaseColor", new Color(UfoGreen.r * 0.22f, UfoGreen.g * 0.22f, UfoGreen.b * 0.22f, 1f));
            EditorUtility.SetDirty(beamMat);
            var beamRenderer = beam.gameObject.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = beamMat;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var glow = Block(PrimitiveType.Quad, "GroundGlow", ufo, ParticleMaterial("UfoGroundGlow", CookieArtFactory.SoftCircle.texture, ParticleBlend.Additive),
                new Vector3(0f, 0.05f, 0f), Vector3.one * 8f, rotation: Quaternion.Euler(90f, 0f, 0f));
            glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var ps = CreateParticleSystem("BeamSparkles", ufo, new Vector3(0f, 0.2f, 0f), 120);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(UfoGreen, Color.white);
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 20f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 3f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            // 위로 올라가면서 빛줄기 가운데로 빨려 들고, 빙글 돈다.
            velocity.y = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.35f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.2f);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(0.15f, 0.7f));
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                ParticleMaterial("Sparkle", CookieArtFactory.Sparkle.texture, ParticleBlend.Additive);

            // 땅을 초록빛으로 물들이는 조명 (그림자 없음)
            var spot = new GameObject("BeamLight").AddComponent<Light>();
            spot.transform.SetParent(ufo, false);
            spot.transform.localPosition = new Vector3(0f, top, 0f);
            spot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            spot.type = LightType.Spot;
            spot.color = UfoGreen;
            spot.intensity = 30f;
            spot.range = top + 4f;
            spot.spotAngle = 32f;
            spot.shadows = LightShadows.None;
        }

        /// <summary>위(반지름 1.5)에서 아래(반지름 3.4)로 퍼지는 열린 원뿔, 높이 1 (y 0 → -1). 아래로 갈수록 투명해진다.</summary>
        private static Mesh GetOrCreateBeamMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BeamMeshPath);
            if (mesh != null) return mesh;

            const int sides = 32;
            var vertices = new Vector3[(sides + 1) * 2];
            var colors = new Color[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[sides * 6];
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i * 2] = dir * 1.5f;
                vertices[i * 2 + 1] = dir * 3.4f + Vector3.down;
                colors[i * 2] = new Color(1f, 1f, 1f, 0.45f);
                colors[i * 2 + 1] = new Color(1f, 1f, 1f, 0.06f);
                uvs[i * 2] = new Vector2(i / (float)sides, 1f);
                uvs[i * 2 + 1] = new Vector2(i / (float)sides, 0f);
            }
            for (int i = 0; i < sides; i++)
            {
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "BeamCone", vertices = vertices, colors = colors, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorFolders.Ensure(Path.GetDirectoryName(BeamMeshPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(mesh, BeamMeshPath);
            return mesh;
        }

        /// <summary>빛줄기에 끌려가는 네모난 젖소. 다리는 윗부분에 축이 있어서 BeamLift가 버둥거리게 돌린다.</summary>
        private static void BuildCow(Transform ufo)
        {
            var cow = new GameObject("Cow").transform;
            cow.SetParent(ufo, false);
            cow.localScale = Vector3.one * 1.5f;

            var white = Mat("Cow_White", new Color(0.96f, 0.95f, 0.92f));
            var spot = Mat("Cow_Spot", new Color(0.12f, 0.1f, 0.1f));
            var pink = Mat("Cow_Pink", new Color(0.95f, 0.65f, 0.65f));
            var horn = Mat("Cow_Horn", new Color(0.93f, 0.86f, 0.7f));

            Block(PrimitiveType.Cube, "Body", cow, white, new Vector3(0f, 1.05f, 0f), new Vector3(0.9f, 0.8f, 1.6f));
            Block(PrimitiveType.Cube, "SpotL", cow, spot, new Vector3(-0.455f, 1.1f, 0.15f), new Vector3(0.02f, 0.4f, 0.5f));
            Block(PrimitiveType.Cube, "SpotR", cow, spot, new Vector3(0.455f, 0.95f, -0.35f), new Vector3(0.02f, 0.35f, 0.45f));
            Block(PrimitiveType.Cube, "SpotTop", cow, spot, new Vector3(0.1f, 1.455f, -0.2f), new Vector3(0.45f, 0.02f, 0.4f));
            Block(PrimitiveType.Cube, "Udder", cow, pink, new Vector3(0f, 0.6f, -0.3f), new Vector3(0.3f, 0.12f, 0.3f));
            Block(PrimitiveType.Cube, "Head", cow, white, new Vector3(0f, 1.35f, 0.95f), new Vector3(0.6f, 0.55f, 0.55f));
            Block(PrimitiveType.Cube, "Snout", cow, pink, new Vector3(0f, 1.2f, 1.25f), new Vector3(0.5f, 0.28f, 0.12f));
            Block(PrimitiveType.Cube, "Tail", cow, white, new Vector3(0f, 1.2f, -0.83f), new Vector3(0.06f, 0.5f, 0.06f), rotation: Quaternion.Euler(-20f, 0f, 0f));
            foreach (float side in new[] { -1f, 1f })
            {
                Block(PrimitiveType.Cube, "Eye", cow, spot, new Vector3(side * 0.17f, 1.46f, 1.225f), Vector3.one * 0.09f);
                Block(PrimitiveType.Cube, "Horn", cow, horn, new Vector3(side * 0.22f, 1.7f, 0.9f), new Vector3(0.08f, 0.2f, 0.08f));
                Block(PrimitiveType.Cube, "Ear", cow, white, new Vector3(side * 0.4f, 1.48f, 0.88f), new Vector3(0.22f, 0.1f, 0.14f));
            }

            var legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.3f : 0.3f, z = i < 2 ? 0.6f : -0.6f;
                var pivot = new GameObject($"Leg{i}").transform;
                pivot.SetParent(cow, false);
                pivot.localPosition = new Vector3(x, 0.7f, z);
                Block(PrimitiveType.Cube, "Leg", pivot, white, new Vector3(0f, -0.3f, 0f), new Vector3(0.22f, 0.6f, 0.22f));
                Block(PrimitiveType.Cube, "Hoof", pivot, spot, new Vector3(0f, -0.65f, 0f), new Vector3(0.24f, 0.1f, 0.24f));
                legs[i] = pivot;
            }

            var lift = cow.gameObject.AddComponent<BeamLift>();
            Wire(lift, ("legs", legs));
            var so = new SerializedObject(lift);
            so.FindProperty("liftHeight").floatValue = UfoHoverHeight - 5.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
