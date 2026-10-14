using CookieClicker.Core;
using CookieClicker.FX;
using CookieClicker.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 전골냄비(StewPot) 아래에 파티클 시스템 4개(재료 튀기, 반짝임, 거품, 김)를 미리 만들어 둔다.
    /// 씬에 실제 오브젝트로 남으므로 모양·색·수명은 각 Particle System Inspector에서 바로 고치면 된다.
    /// 좌표는 냄비 기준(가로 1) 값이고, Scaling Mode = Hierarchy라 냄비 크기가 바뀌면 같이 커진다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string PotFXName = "PotFX";
        private const string ParticleMaterialFolder = CookieArtFactory.ArtFolder + "/Particles";
        // 기울어진 냄비(ModelRotation)의 국물 표면 가운데 (냄비 기준 좌표)
        private static readonly Vector3 SoupCenter = new Vector3(0f, 0.03f, -0.12f);
        // 거품이 끓는 국물 가장자리(냄비 벽 안쪽 타원)의 중심과 반지름. 재료가 솟은 가운데보다 벽 쪽이 더 보글거린다.
        private static readonly Vector3 SoupRimCenter = new Vector3(0.07f, -0.04f, -0.12f);
        private const float SoupRimRadius = 0.32f;

        [MenuItem("Tools/Cookie Clicker/Install Pot Effects (냄비 연출 설치)")]
        private static void InstallPotFXMenu()
        {
            var pot = Object.FindAnyObjectByType<CookieClickTarget>();
            var game = Object.FindAnyObjectByType<CookieGame>();
            if (pot == null || game == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "열린 씬에 냄비(CookieClickTarget)가 없습니다.\n먼저 Build Scene으로 씬을 만들어 주세요.", "확인");
                return;
            }
            InstallPotFX(pot, game);
            EditorSceneManager.MarkSceneDirty(pot.gameObject.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "냄비 연출(파티클)을 설치했습니다.\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        internal static void InstallPotFX(CookieClickTarget pot, CookieGame game)
        {
            var old = pot.transform.Find(PotFXName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(PotFXName).transform;
            root.SetParent(pot.transform, false);

            var ingredients = CreateIngredientBursts(root);
            var sparkles = CreateSparkleBurst(root);
            var bubbles = CreateBubbles(root);
            var steam = CreateSteam(root);

            Wire(root.gameObject.AddComponent<StewPotFX>(),
                ("game", game), ("pot", pot),
                ("ingredientBursts", ingredients), ("sparkleBurst", sparkles),
                ("bubbles", bubbles), ("steam", steam));
        }

        // ------------------------------------------------------------------ 각 파티클

        /// <summary>튀어 오르는 재료: 모델 파일, 크기 배율(모양마다 눈에 보이는 크기를 맞춤)</summary>
        private static readonly (string name, string modelPath, float sizeScale)[] Ingredients =
        {
            ("Udon", "Assets/CookieClicker/Models/Udon.glb", 1.25f),
            ("Garlic", "Assets/CookieClicker/Models/Garlic.glb", 0.8f),
            ("Octopus", "Assets/CookieClicker/Models/Octopus.glb", 1.4f),
            ("Shrimp", "Assets/CookieClicker/Models/Shrimp.glb", 1f),
        };

        /// <summary>
        /// 재료 모델마다 파티클 시스템을 하나씩 만든다 (모델마다 텍스처가 달라 머티리얼을 나눠야 하기 때문).
        /// 모델 파일이 없는 재료는 건너뛰고, 하나도 없으면 기본 도형으로 대신한다.
        /// </summary>
        private static ParticleSystem[] CreateIngredientBursts(Transform parent)
        {
            var group = new GameObject("Ingredients").transform;
            group.SetParent(parent, false);

            var bursts = new System.Collections.Generic.List<ParticleSystem>();
            foreach (var (name, modelPath, sizeScale) in Ingredients)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var filter = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null) continue;

                var source = filter.GetComponent<Renderer>().sharedMaterial;
                Texture texture = null;
                if (source != null)
                    texture = source.HasProperty("baseColorTexture") ? source.GetTexture("baseColorTexture") : source.mainTexture;

                var ps = CreateIngredientBurst(group, $"Ingredient_{name}", filter.sharedMesh, sizeScale);
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial($"Ingredient_{name}", texture, ParticleBlend.Lit);
                bursts.Add(ps);
            }

            if (bursts.Count == 0)
            {
                var fallback = CreateIngredientBurst(group, "Ingredient_Shapes", BuiltinMesh("Sphere.fbx"), 0.5f);
                fallback.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial("Ingredient", null, ParticleBlend.Lit);
                bursts.Add(fallback);
            }
            return bursts.ToArray();
        }

        /// <summary>클릭하면 재료 하나가 튀어 올랐다가 중력으로 떨어진다. 3D 메시 파티클.</summary>
        private static ParticleSystem CreateIngredientBurst(Transform parent, string name, Mesh mesh, float sizeScale)
        {
            var ps = CreateParticleSystem(name, parent, SoupCenter, 40);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
            // Scaling Mode가 Hierarchy라 속도·크기에도 냄비 크기(약 2.5배)가 곱해진다.
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            // 모델의 가장 긴 변이 냄비 지름의 약 1/6이 되도록 맞춘다.
            float longest = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
            float startSize = 0.075f / Mathf.Max(0.0001f, longest) * sizeScale;
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.8f, startSize * 1.15f);
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white; // 색은 모델 텍스처 그대로

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 콘은 +Z로 쏘므로 위로 세운다

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = rotation.y = rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.1f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            // 모델 중심이 원점이 아니어도 제자리에서 돌도록 회전 중심을 모델 가운데로 옮긴다 (단위: 파티클 크기).
            renderer.pivot = -mesh.bounds.center;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            return ps;
        }

        /// <summary>클릭할 때 같이 터지는 금빛 반짝이 (가산 블렌딩이라 겹칠수록 밝아진다).</summary>
        private static ParticleSystem CreateSparkleBurst(Transform parent)
        {
            var ps = CreateParticleSystem("SparkleBurst", parent, SoupCenter, 80);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.98f, 0.8f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.5f;
            limit.dampen = 0.15f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f)));

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                ParticleMaterial("Sparkle", CookieArtFactory.Sparkle.texture, ParticleBlend.Additive);
            return ps;
        }

        /// <summary>
        /// 국물이 볼록 부풀었다가 톡 터지는 거품. 터질 때(Death) 서브 이미터로 작은 국물 방울이 튄다.
        /// 냄비와 같이 움직이도록 Local 공간에서 돈다.
        /// </summary>
        private static ParticleSystem CreateBubbles(Transform parent)
        {
            var ps = CreateParticleSystem("Bubbles", parent, SoupRimCenter, 150);
            ps.transform.localRotation = Quaternion.Euler(ModelRotation); // 국물 면과 같은 기울기
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.85f, 0.78f, 0.78f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime = 12f;

            // 실제 전골처럼 냄비 벽에 맞닿은 국물 가장자리에서 주로 보글거린다 (가운데는 재료가 솟아 있어 거품이 잘 안 보인다).
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = SoupRimRadius;
            // 바깥쪽 25% 띠에서만 생긴다 (0 = 테두리 선, 1 = 원 전체)
            shape.radiusThickness = 0.25f;
            shape.rotation = new Vector3(90f, 0f, 0f); // Circle은 XY 평면이라 국물 면(XZ)으로 눕힌다
            shape.scale = new Vector3(1f, 1.1f, 1f); // 화면에서 국물 타원에 맞춘 비율

            // 볼록하게 부풀고(ease-out) 조금 출렁이다가, 마지막에 팽창하며 터진다.
            // 국물 면이 기울어져 있어서 세로를 납작하게(0.65) 해야 면 위에 붙은 돔처럼 보인다.
            var grow = new AnimationCurve(new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.3f, 0.9f), new Keyframe(0.6f, 0.85f),
                new Keyframe(0.88f, 1f), new Keyframe(0.96f, 1.2f), new Keyframe(1f, 0f));
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, grow);
            size.y = new ParticleSystem.MinMaxCurve(0.65f, grow);
            size.z = new ParticleSystem.MinMaxCurve(1f, grow);

            var pop = CreatePopSplash(ps.transform);
            var sub = ps.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(pop, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                ParticleMaterial("SoupBubble", CookieArtFactory.SoupBubble.texture, ParticleBlend.Alpha);
            return ps;
        }

        /// <summary>거품이 터질 때 튀는 작은 국물 방울 (Bubbles의 서브 이미터, 자기 Burst 설정대로 터진다).</summary>
        private static ParticleSystem CreatePopSplash(Transform bubbles)
        {
            var ps = CreateParticleSystem("PopSplash", bubbles, Vector3.zero, 150);
            var main = ps.main;
            main.loop = false;
            // playOnAwake는 끄지 않는다. 이 값은 부모(Bubbles)와 공유되는 값(Inspector의 *)이라
            // 여기서 끄면 거품까지 재생되지 않는다. 서브 이미터는 켜 둬도 혼자 재생되지 않는다.
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.35f, 0.18f), new Color(1f, 0.6f, 0.4f));
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 5) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.01f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 국물 면의 위쪽(+Y)으로 튄다

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                ParticleMaterial("Droplet", CookieArtFactory.SoftCircle.texture, ParticleBlend.Alpha);
            return ps;
        }

        /// <summary>냄비 위로 피어오르는 김. World 공간이라 냄비가 흔들려도 김은 제자리에서 퍼진다.</summary>
        private static ParticleSystem CreateSteam(Transform parent)
        {
            var ps = CreateParticleSystem("Steam", parent, SoupCenter + new Vector3(0f, 0.2f, 0f), 100); // 국물 위로 띄워서 거품을 덮지 않게
            ps.transform.localRotation = Quaternion.Euler(ModelRotation);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.28f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;

            var emission = ps.emission;
            emission.rateOverTime = 1.5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.5f, 0f, 0.4f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            // 처음엔 작고 투명했다가, 올라가며 커지고 흐려진다.
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.6f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(Fade(0.25f, 0.5f));

            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                ParticleMaterial("Steam", CookieArtFactory.SoftCircle.texture, ParticleBlend.Alpha);
            return ps;
        }

        // ------------------------------------------------------------------ 공통

        private static ParticleSystem CreateParticleSystem(string name, Transform parent, Vector3 localPosition, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var ps = go.AddComponent<ParticleSystem>();
            // AddComponent 직후에는 재생 중이라 일부 값(Duration 등)을 못 바꾼다.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = maxParticles; // 모바일에서 무한정 늘지 않게 상한을 둔다
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = 0f; // 터지는 효과는 StewPotFX가 Emit으로 직접 쏜다

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private enum ParticleBlend { Alpha, Additive, Lit }

        /// <summary>URP 파티클 머티리얼을 에셋으로 만들어 둔다 (이미 있으면 그대로 사용 → 직접 고친 값이 유지된다).</summary>
        private static Material ParticleMaterial(string name, Texture texture, ParticleBlend blend)
        {
            string path = $"{ParticleMaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EditorFolders.Ensure(ParticleMaterialFolder);
            var shader = Shader.Find(blend == ParticleBlend.Lit
                ? "Universal Render Pipeline/Particles/Simple Lit"
                : "Universal Render Pipeline/Particles/Unlit");
            var material = new Material(shader);
            if (texture != null) material.SetTexture("_BaseMap", texture);

            if (blend != ParticleBlend.Lit)
            {
                // 반투명(Transparent) 설정. URP 머티리얼 Inspector에서 Surface Type을 바꾸는 것과 같다.
                bool additive = blend == ParticleBlend.Additive;
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", additive ? 2f : 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Mesh BuiltinMesh(string name) => Resources.GetBuiltinResource<Mesh>(name);

        /// <summary>RandomColor 모드용: 각 색이 같은 폭을 갖도록 끊어서(Fixed) 만든다.</summary>
        private static Gradient Palette(params Color[] colors)
        {
            var keys = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) keys[i] = new GradientColorKey(colors[i], (i + 1f) / colors.Length);
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        /// <summary>서서히 나타났다가(fadeIn) 사라지는(fadeOut부터) 흰색 알파 그라데이션.</summary>
        private static Gradient Fade(float fadeIn, float fadeOut)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, fadeOut), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
