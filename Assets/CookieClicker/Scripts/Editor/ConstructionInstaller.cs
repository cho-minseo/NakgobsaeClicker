using System;
using System.IO;
using CookieClicker.Core;
using CookieClicker.FX;
using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Unity.Cinemachine;
using Object = UnityEngine.Object;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 건물 공사 장면과 "LIVE 공사 현장" 중계 창을 설치한다.
    /// - 공사 장비(Construction/Rig): 펜스, 비계(네 면 × 층), 먼지·불꽃·색종이 파티클, Cinemachine 샷 3개. 크기 1(바닥 1×1) 기준으로 만들고
    ///   ConstructionDirector가 건물 자리로 옮겨 키운다.
    /// - 장면: Animation/Construction/Construction.playable (Timeline). 펜스·비계·카메라 이동 애니메이션 트랙, Cinemachine 샷 트랙,
    ///   파티클 Control 트랙, 효과음 Audio 트랙 4개, 무너짐(Collapse)·공개(Reveal)·끝(Done) Signal 트랙.
    ///   키프레임은 .anim 에셋이라 Animation 창에서 바로 고칠 수 있다.
    /// - 중계 창: 클리커 화면 왼쪽 아래. TV처럼 켜지고 꺼지는 모습은 Animator(LiveBroadcast.controller)가 맡는다.
    /// 장면·클립 에셋은 이미 있으면 그대로 쓴다(직접 고친 키프레임 보호). 처음부터 다시 만들려면 Animation/Construction 폴더를 지우고 실행.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string ConstructionFolder = "Assets/CookieClicker/Animation/Construction";
        private const string BroadcastFolder = "Assets/CookieClicker/Animation/LiveBroadcast";
        private const string BroadcastTexturePath = CookieArtFactory.ArtFolder + "/LiveBroadcast.renderTexture";
        // Kenney Impact Sounds · Interface Sounds (CC0)에서 고른 효과음
        private const string ConstructionAudioFolder = "Assets/CookieClicker/Audio/Construction";

        // 장면 시간표 (초)
        private const float ConstructionLength = 4.2f;
        private const float RevealTime = 2.9f;
        private const float DoneTime = 3.9f;
        private const float CollapseTime = 2.6f;

        // 비계: 층 높이(장비 크기 1 기준)와 최대 층 수. 건물 높이만큼만 켜진다.
        private const float ScaffoldLevelHeight = 0.35f;
        private const int ScaffoldLevelCount = 6;
        private static readonly string[] SideNames = { "Side_N", "Side_E", "Side_S", "Side_W" };

        [MenuItem("Tools/Cookie Clicker/Install Construction Scene (공사 장면·중계 창)")]
        private static void InstallConstructionMenu()
        {
            var world = GameObject.Find("ExploreWorld");
            var game = Object.FindFirstObjectByType<CookieGame>();
            var hud = GameObject.Find("UI Canvas")?.transform.Find("MainHud");
            if (world == null || game == null || hud == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "ExploreWorld / GameManager / UI Canvas/MainHud를 찾지 못했습니다.", "확인");
                return;
            }
            InstallConstruction(world.transform, game, (RectTransform)hud);
            EditorSceneManager.MarkSceneDirty(world.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "공사 장면과 중계 창을 설치했습니다.\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        internal static void InstallConstruction(Transform world, CookieGame game, RectTransform mainHud)
        {
            var old = world.Find("Construction");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var oldView = mainHud.Find("LiveBroadcast");
            if (oldView != null) Object.DestroyImmediate(oldView.gameObject);
            EditorFolders.Ensure(ConstructionFolder);
            EditorFolders.Ensure(BroadcastFolder);

            var root = new GameObject("Construction").transform;
            root.SetParent(world, false);
            var rig = new GameObject("Rig").transform;
            rig.SetParent(root, false);

            var fence = BuildFence(rig);
            var scaffold = BuildScaffold(rig, out var sides);
            var cameraPivot = new GameObject("CameraPivot").transform;
            cameraPivot.SetParent(rig, false);
            cameraPivot.localPosition = new Vector3(0f, 0.5f, 0f);
            var camera = BuildBroadcastCamera(root, world);
            var shots = BuildCameraShots(cameraPivot, out var cameraRig);
            var effects = BuildConstructionEffects(rig, cameraPivot);
            var sounds = BuildSoundSources(root);
            // 비계가 바닥에 부딪힐 때 샷들을 흔드는 Impulse (아래로 쿵)
            var impulse = rig.gameObject.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.ImpulseDefinition.ImpulseDuration = 0.35f;
            impulse.DefaultVelocity = Vector3.down;

            var view = BuildLiveBroadcast(mainHud);

            var director = root.gameObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;
            var receiver = root.gameObject.AddComponent<SignalReceiver>();
            var reveal = GetOrCreateSignal("Reveal");
            var done = GetOrCreateSignal("Done");
            var collapse = GetOrCreateSignal("Collapse");
            BuildConstructionTimeline(director, receiver, fence, scaffold, cameraRig, camera.GetComponent<CinemachineBrain>(), shots,
                effects, sounds, reveal, done, collapse);

            var plots = world.GetComponentsInChildren<BuildingPlot>(true);
            var construction = root.gameObject.AddComponent<ConstructionDirector>();
            Wire(construction, ("game", game), ("plots", plots), ("timeline", director), ("rig", rig),
                ("scaffoldSides", sides), ("cameraPivot", cameraPivot), ("broadcastCamera", camera), ("broadcast", view),
                ("revealSignal", reveal), ("doneSignal", done), ("collapseSignal", collapse), ("impulse", impulse),
                ("levelHeight", ScaffoldLevelHeight));
            rig.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 장비 (크기 1 기준)

        /// <summary>주황·흰 줄무늬 공사 펜스 네 장. 각 장은 바닥에 축이 있어서 Y 크기로 솟아오른다.</summary>
        private static Animator BuildFence(Transform rig)
        {
            var fence = new GameObject("Fence").transform;
            fence.SetParent(rig, false);
            var orange = Mat("Construction_Orange", new Color(1f, 0.55f, 0.12f));
            var white = Mat("Construction_White", new Color(0.97f, 0.95f, 0.9f));
            var post = Mat("Construction_Post", new Color(0.3f, 0.3f, 0.33f));
            const float distance = 0.62f, width = 1.3f;
            for (int s = 0; s < 4; s++)
            {
                var panel = new GameObject($"Fence_{SideNames[s].Substring(5)}").transform;
                panel.SetParent(fence, false);
                float yaw = s * 90f;
                panel.localRotation = Quaternion.Euler(0f, yaw, 0f);
                panel.localPosition = panel.localRotation * new Vector3(0f, 0f, distance);
                for (int b = 0; b < 3; b++)
                {
                    Block(PrimitiveType.Cube, $"Board{b}", panel, b % 2 == 0 ? orange : white,
                        new Vector3(0f, 0.03f + b * 0.032f, 0f), new Vector3(width, 0.028f, 0.008f));
                }
                foreach (float x in new[] { -width * 0.5f, 0f, width * 0.5f })
                    Block(PrimitiveType.Cube, "Post", panel, post, new Vector3(x, 0.06f, 0f), new Vector3(0.012f, 0.12f, 0.012f));
            }
            return fence.gameObject.AddComponent<Animator>();
        }

        /// <summary>
        /// 노란 파이프 비계. Shaker(키우기·흔들기) 아래 네 면(Side_*)이 있고, 면마다 바깥쪽 아래 모서리에 축이 있어서
        /// X축으로 90도 돌리면 바깥으로 넘어진다. 면마다 Level0~5 층이 있고 건물 높이만큼만 켜진다.
        /// </summary>
        private static Animator BuildScaffold(Transform rig, out Transform[] sides)
        {
            var scaffold = new GameObject("Scaffold").transform;
            scaffold.SetParent(rig, false);
            var shaker = new GameObject("Shaker").transform;
            shaker.SetParent(scaffold, false);

            var pipe = Mat("Construction_Pipe", new Color(1f, 0.8f, 0.2f));
            var plank = Mat("Construction_Plank", new Color(0.72f, 0.52f, 0.33f));
            // 파란 방수포 (초록 건물과 섞이지 않게)
            var net = Mat("Construction_Tarp", new Color(0.25f, 0.5f, 0.85f));
            const float half = 0.5f, t = 0.016f;
            sides = new Transform[4];
            for (int s = 0; s < 4; s++)
            {
                var side = new GameObject(SideNames[s]).transform;
                side.SetParent(shaker, false);
                side.localRotation = Quaternion.Euler(0f, s * 90f, 0f);
                side.localPosition = side.localRotation * new Vector3(0f, 0f, half);
                for (int l = 0; l < ScaffoldLevelCount; l++)
                {
                    var level = new GameObject($"Level{l}").transform;
                    level.SetParent(side, false);
                    float y0 = l * ScaffoldLevelHeight, h = ScaffoldLevelHeight;
                    level.localPosition = new Vector3(0f, y0, 0f);
                    foreach (float x in new[] { -half, half })
                        Block(PrimitiveType.Cube, "Pole", level, pipe, new Vector3(x, h * 0.5f, 0f), new Vector3(t, h, t));
                    Block(PrimitiveType.Cube, "Ledger", level, pipe, new Vector3(0f, h - t * 0.5f, 0f), new Vector3(half * 2f, t, t));
                    // 바깥쪽 발판과, 층마다 방향을 바꾸는 대각선 버팀대
                    Block(PrimitiveType.Cube, "Plank", level, plank, new Vector3(0f, h - t, 0.045f), new Vector3(half * 2f, t * 0.6f, 0.07f));
                    float diagonal = Mathf.Sqrt(half * 2f * half * 2f + h * h);
                    float angle = Mathf.Atan2(h, half * 2f) * Mathf.Rad2Deg * (l % 2 == 0 ? 1f : -1f);
                    Block(PrimitiveType.Cube, "Brace", level, pipe, new Vector3(0f, h * 0.5f, 0.01f), new Vector3(diagonal, t * 0.7f, t * 0.7f),
                        rotation: Quaternion.Euler(0f, 0f, angle));
                    // 아래 두 층에는 파란 방수포를 반쯤 친다.
                    if (l < 2) Block(PrimitiveType.Cube, "Net", level, net, new Vector3(0f, h * 0.45f, -0.006f), new Vector3(half * 2f - t, h * 0.6f, 0.002f));
                }
                sides[s] = side;
            }
            return scaffold.gameObject.AddComponent<Animator>();
        }

        /// <summary>
        /// 중계 카메라: 실제 Camera + Cinemachine Brain. 어디서 찍을지는 CameraPivot 아래 가상 카메라(샷) 세 개가 정하고,
        /// Timeline의 Cinemachine 트랙이 샷을 차례로 바꾼다(겹친 구간은 부드럽게 넘어감). 그림은 Render Texture로 나가 중계 창에 뜬다.
        /// </summary>
        private static Camera BuildBroadcastCamera(Transform root, Transform world)
        {
            var go = new GameObject("BroadcastCamera");
            go.transform.SetParent(root, false);
            var camera = go.AddComponent<Camera>();
            // 하늘·안개는 구경하기 카메라와 똑같이 맞춘다.
            var playerCamera = world.Find("Player")?.GetComponentInChildren<Camera>(true);
            if (playerCamera != null)
            {
                camera.clearFlags = playerCamera.clearFlags;
                camera.backgroundColor = playerCamera.backgroundColor;
                camera.farClipPlane = playerCamera.farClipPlane;
            }
            camera.cullingMask = ~(1 << UILayer);
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.1f;
            camera.depth = -10f;
            camera.targetTexture = GetOrCreateBroadcastTexture();
            camera.enabled = false;
            go.AddComponent<CinemachineBrain>();
            return camera;
        }

        private struct CameraShots
        {
            public CinemachineCamera Wide, Work, Reveal;
        }

        /// <summary>
        /// 샷 세 개 (장비 크기 1 기준 위치, 모두 CameraPivot = 건물 가운데를 바라봄):
        /// Wide = 앞 왼쪽 높은 곳에서 전체, Work = 오른쪽 낮은 곳에서 가까이(손으로 든 듯 흔들림), Reveal = 정면 낮은 곳에서 올려다보며 미끄러짐.
        /// Reveal은 CameraRig/Dolly 아래에 있어서 Timeline 애니메이션 트랙이 Dolly를 움직인다.
        /// </summary>
        private static CameraShots BuildCameraShots(Transform pivot, out Animator cameraRig)
        {
            var rig = new GameObject("CameraRig").transform;
            rig.SetParent(pivot, false);
            var dolly = new GameObject("Dolly").transform;
            dolly.SetParent(rig, false);
            dolly.localPosition = new Vector3(0.8f, 0.05f, 2.1f);
            cameraRig = rig.gameObject.AddComponent<Animator>();

            var noise = AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset");
            CinemachineCamera Shot(string name, Transform parent, Vector3 position, float shake)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                var shot = go.AddComponent<CinemachineCamera>();
                var lens = LensSettings.Default;
                lens.FieldOfView = 45f;
                lens.NearClipPlane = 0.1f;
                lens.FarClipPlane = 1000f;
                shot.Lens = lens;
                shot.Target = new CameraTarget { TrackingTarget = pivot };
                go.AddComponent<CinemachineRotationComposer>();
                if (shake > 0f && noise != null)
                {
                    var perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
                    perlin.NoiseProfile = noise;
                    perlin.AmplitudeGain = shake;
                }
                // 비계가 바닥에 부딪힐 때의 Impulse를 받아 흔들린다.
                go.AddComponent<CinemachineImpulseListener>();
                return shot;
            }

            return new CameraShots
            {
                Wide = Shot("Shot_Wide", rig, new Vector3(-1.5f, 0.75f, 2.6f), 0.4f),
                Work = Shot("Shot_Work", rig, new Vector3(1.3f, 0.05f, 2.1f), 1f),
                Reveal = Shot("Shot_Reveal", dolly, Vector3.zero, 0.3f),
            };
        }

        private struct ConstructionSounds
        {
            public AudioSource Build, Hammer, Collapse, Reveal;
        }

        /// <summary>효과음 트랙마다 하나씩 쓰는 소리 통. 화면 구석 중계라 거리와 상관없이 들리게(2D) 한다.</summary>
        private static ConstructionSounds BuildSoundSources(Transform root)
        {
            AudioSource Source(string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 0.7f;
                return source;
            }
            return new ConstructionSounds
            {
                Build = Source("Sfx_Build"), Hammer = Source("Sfx_Hammer"), Collapse = Source("Sfx_Collapse"), Reveal = Source("Sfx_Reveal"),
            };
        }

        private static AudioClip ConstructionSound(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{ConstructionAudioFolder}/{name}.ogg");

        private static RenderTexture GetOrCreateBroadcastTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(BroadcastTexturePath);
            if (texture != null) return texture;
            texture = new RenderTexture(768, 432, 24) { name = "LiveBroadcast", antiAliasing = 2 };
            AssetDatabase.CreateAsset(texture, BroadcastTexturePath);
            return texture;
        }

        private struct ConstructionEffects
        {
            public GameObject Dust, Sparks, CollapseDust, Confetti, Sparkle;
        }

        /// <summary>
        /// 장면에 나오는 파티클. 크기는 장비 크기 1 기준이고 Hierarchy 스케일이라 건물이 클수록 같이 커진다.
        /// 켜고 끄는 시점은 Timeline Control 트랙이 정하므로 처음엔 모두 꺼 둔다.
        /// </summary>
        private static ConstructionEffects BuildConstructionEffects(Transform rig, Transform pivot)
        {
            var fx = new GameObject("Effects").transform;
            fx.SetParent(rig, false);
            var dustColor = new Color(0.86f, 0.76f, 0.6f, 0.75f);
            var steam = ParticleMaterial("Steam", CookieArtFactory.SoftCircle.texture, ParticleBlend.Alpha);
            var sparkle = ParticleMaterial("Sparkle", CookieArtFactory.Sparkle.texture, ParticleBlend.Additive);

            // 1) 공사 중 바닥에서 풀풀 일어나는 먼지
            var dust = CreateParticleSystem("Dust", fx, new Vector3(0f, 0.05f, 0f), 120);
            {
                var main = dust.main;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = dustColor;
                dust.emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
                var emission = dust.emission;
                emission.rateOverTime = 30f;
                var shape = dust.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(1.1f, 0.05f, 1.1f);
                var size = dust.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
                var color = dust.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(Fade(0.15f, 0.5f));
                dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = steam;
            }

            // 2) 망치질 불꽃: 비계 여기저기서 짧게 튀는 노란 줄
            var sparks = CreateParticleSystem("Sparks", fx, new Vector3(0f, 0.5f, 0f), 120);
            {
                var main = sparks.main;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.55f, 0.15f));
                main.gravityModifier = 0.8f;
                var emission = sparks.emission;
                emission.rateOverTime = 0f;
                // 망치를 두드리듯 0.15초마다 한 줌씩
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 10, 20, 0.15f) });
                var shape = sparks.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(1f, 0.8f, 1f);
                var renderer = sparks.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.08f;
                renderer.lengthScale = 2f;
                renderer.sharedMaterial = sparkle;
            }

            // 3) 비계가 넘어질 때 사방으로 퍼지는 큰 먼지
            var collapse = CreateParticleSystem("CollapseDust", fx, new Vector3(0f, 0.03f, 0f), 80);
            {
                var main = collapse.main;
                main.playOnAwake = false;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = dustColor;
                var emission = collapse.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0.25f, 50) });
                var shape = collapse.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.55f;
                shape.radiusThickness = 0.2f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                var velocity = collapse.limitVelocityOverLifetime;
                velocity.enabled = true;
                velocity.dampen = 0.15f;
                velocity.limit = 0.05f;
                var size = collapse.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.6f));
                var color = collapse.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(Fade(0.05f, 0.4f));
                collapse.GetComponent<ParticleSystemRenderer>().sharedMaterial = steam;
            }

            // 4) 공개 순간 건물 가운데에서 터지는 알록달록 색종이
            var confetti = CreateParticleSystem("Confetti", pivot, Vector3.zero, 200);
            {
                var main = confetti.main;
                main.playOnAwake = false;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
                main.startSizeY = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
                main.startSizeZ = 1f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var palette = new Gradient();
                palette.SetKeys(new[]
                {
                    new GradientColorKey(new Color(0.95f, 0.3f, 0.25f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.25f),
                    new GradientColorKey(new Color(0.35f, 0.75f, 0.4f), 0.5f), new GradientColorKey(new Color(0.3f, 0.6f, 0.95f), 0.75f),
                    new GradientColorKey(new Color(0.85f, 0.45f, 0.85f), 1f),
                }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                main.startColor = new ParticleSystem.MinMaxGradient(palette) { mode = ParticleSystemGradientMode.RandomColor };
                main.gravityModifier = 0.35f;
                var emission = confetti.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 150) });
                var shape = confetti.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f;
                var rotation = confetti.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
                var noise = confetti.noise;
                noise.enabled = true;
                noise.strength = 0.15f;
                noise.frequency = 1.5f;
                var drag = confetti.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.dampen = 0.08f;
                drag.limit = 0.2f;
                var color = confetti.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(Fade(0.01f, 0.8f));
                confetti.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                    ParticleMaterial("Confetti", null, ParticleBlend.Alpha);
            }

            // 5) 같이 터지는 금빛 반짝이
            var shine = CreateParticleSystem("Sparkle", pivot, Vector3.zero, 80);
            {
                var main = shine.main;
                main.playOnAwake = false;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.35f), new Color(1f, 0.97f, 0.75f));
                main.gravityModifier = 0.2f;
                var emission = shine.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
                var shape = shine.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f;
                var size = shine.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f)));
                shine.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkle;
            }

            var effects = new ConstructionEffects
            {
                Dust = dust.gameObject, Sparks = sparks.gameObject, CollapseDust = collapse.gameObject,
                Confetti = confetti.gameObject, Sparkle = shine.gameObject,
            };
            foreach (var go in new[] { effects.Dust, effects.Sparks, effects.CollapseDust, effects.Confetti, effects.Sparkle })
                go.SetActive(false);
            return effects;
        }

        // ------------------------------------------------------------------ Timeline

        private static SignalAsset GetOrCreateSignal(string name)
        {
            string path = $"{ConstructionFolder}/{name}.signal";
            var signal = AssetDatabase.LoadAssetAtPath<SignalAsset>(path);
            if (signal != null) return signal;
            signal = ScriptableObject.CreateInstance<SignalAsset>();
            AssetDatabase.CreateAsset(signal, path);
            return signal;
        }

        private static void BuildConstructionTimeline(PlayableDirector director, SignalReceiver receiver,
            Animator fence, Animator scaffold, Animator cameraRig, CinemachineBrain brain, CameraShots shots,
            ConstructionEffects effects, ConstructionSounds sounds, SignalAsset reveal, SignalAsset done, SignalAsset collapse)
        {
            string path = $"{ConstructionFolder}/Construction.playable";
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, path);

                AddAnimationClip(timeline, "Fence", GetOrCreateClip("Construction_Fence", FillFenceClip));
                AddAnimationClip(timeline, "Scaffold", GetOrCreateClip("Construction_Scaffold", FillScaffoldClip));
                AddAnimationClip(timeline, "Camera Dolly", GetOrCreateClip("Construction_Camera", FillCameraClip));

                // 샷 바꾸기: 겹친 구간(0.2초)은 Cinemachine이 부드럽게 옮겨 간다.
                var cameras = timeline.CreateTrack<CinemachineTrack>(null, "Shots");
                AddShot(cameras, "Shot_Wide", 0f, 1.35f);
                AddShot(cameras, "Shot_Work", 1.15f, 1.6f);
                AddShot(cameras, "Shot_Reveal", 2.55f, ConstructionLength - 2.55f);

                var control = timeline.CreateTrack<ControlTrack>(null, "Effects");
                AddControlClip(control, "Dust", 0.9f, 2.1f);
                AddControlClip(control, "Sparks", 1.2f, 1.45f);
                AddControlClip(control, "CollapseDust", CollapseTime, 1.6f);
                AddControlClip(control, "Confetti", RevealTime, 1.3f);
                AddControlClip(control, "Sparkle", RevealTime, 1.3f);

                // 효과음: 펜스·비계 세우기 / 망치질 / 무너짐 / 완성
                AddSounds(timeline, "SFX Build", ("impactTin_medium_000", 0.05f), ("impactTin_medium_002", 0.3f),
                    ("impactPlank_medium_000", 0.55f), ("impactPlank_medium_001", 0.9f));
                AddSounds(timeline, "SFX Hammer", ("impactMetal_light_000", 1.25f), ("impactMetal_light_001", 1.5f), ("impactMetal_light_002", 1.75f),
                    ("impactMetal_light_003", 2f), ("impactMetal_light_004", 2.25f), ("impactMetal_light_001", 2.45f));
                AddSounds(timeline, "SFX Collapse", ("impactPlank_medium_002", 2.75f), ("impactWood_heavy_000", 2.98f), ("impactWood_heavy_001", 3.15f));
                AddSounds(timeline, "SFX Reveal", ("maximize_006", RevealTime), ("confirmation_002", RevealTime + 0.35f));

                var signals = timeline.CreateTrack<SignalTrack>(null, "Signals");
                signals.CreateMarker<SignalEmitter>(3.0f).asset = collapse;
                signals.CreateMarker<SignalEmitter>(RevealTime).asset = reveal;
                signals.CreateMarker<SignalEmitter>(DoneTime).asset = done;
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
            }

            director.playableAsset = timeline;
            // 트랙 이름으로 이 씬의 오브젝트를 연결한다 (Control·샷 클립은 이름이 같은 오브젝트를 가리킨다).
            foreach (var track in timeline.GetOutputTracks())
            {
                switch (track)
                {
                    case AnimationTrack when track.name == "Fence": director.SetGenericBinding(track, fence); break;
                    case AnimationTrack when track.name == "Scaffold": director.SetGenericBinding(track, scaffold); break;
                    case AnimationTrack when track.name == "Camera Dolly": director.SetGenericBinding(track, cameraRig); break;
                    case SignalTrack: director.SetGenericBinding(track, receiver); break;
                    case AudioTrack when track.name == "SFX Build": director.SetGenericBinding(track, sounds.Build); break;
                    case AudioTrack when track.name == "SFX Hammer": director.SetGenericBinding(track, sounds.Hammer); break;
                    case AudioTrack when track.name == "SFX Collapse": director.SetGenericBinding(track, sounds.Collapse); break;
                    case AudioTrack when track.name == "SFX Reveal": director.SetGenericBinding(track, sounds.Reveal); break;
                    case CinemachineTrack:
                        director.SetGenericBinding(track, brain);
                        foreach (var clip in track.GetClips())
                        {
                            var shot = clip.displayName switch
                            {
                                "Shot_Wide" => shots.Wide,
                                "Shot_Work" => shots.Work,
                                "Shot_Reveal" => shots.Reveal,
                                _ => null,
                            };
                            if (shot != null) director.SetReferenceValue(((CinemachineShot)clip.asset).VirtualCamera.exposedName, shot);
                        }
                        break;
                    case ControlTrack:
                        foreach (var clip in track.GetClips())
                        {
                            var asset = (ControlPlayableAsset)clip.asset;
                            var target = clip.displayName switch
                            {
                                "Dust" => effects.Dust,
                                "Sparks" => effects.Sparks,
                                "CollapseDust" => effects.CollapseDust,
                                "Confetti" => effects.Confetti,
                                "Sparkle" => effects.Sparkle,
                                _ => null,
                            };
                            if (target != null) director.SetReferenceValue(asset.sourceGameObject.exposedName, target);
                        }
                        break;
                }
            }
        }

        private static void AddShot(CinemachineTrack track, string name, float start, float duration)
        {
            var clip = track.CreateClip<CinemachineShot>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;
            var shot = (CinemachineShot)clip.asset;
            shot.VirtualCamera.exposedName = GUID.Generate().ToString();
            EditorUtility.SetDirty(shot);
        }

        /// <summary>한 트랙에 효과음을 시간 순서대로 놓는다. 다음 소리와 겹치지 않게 길이를 자른다.</summary>
        private static void AddSounds(TimelineAsset timeline, string trackName, params (string clip, float time)[] sounds)
        {
            var track = timeline.CreateTrack<AudioTrack>(null, trackName);
            for (int i = 0; i < sounds.Length; i++)
            {
                var audio = ConstructionSound(sounds[i].clip);
                if (audio == null)
                {
                    Debug.LogWarning($"[Construction] 효과음이 없습니다: {sounds[i].clip}");
                    continue;
                }
                var clip = track.CreateClip(audio);
                clip.start = sounds[i].time;
                float gap = i + 1 < sounds.Length ? sounds[i + 1].time - sounds[i].time : audio.length;
                clip.duration = Mathf.Min(audio.length, gap);
            }
        }

        private static void AddAnimationClip(TimelineAsset timeline, string trackName, AnimationClip clip)
        {
            var track = timeline.CreateTrack<AnimationTrack>(null, trackName);
            var timelineClip = track.CreateClip(clip);
            timelineClip.start = 0;
            timelineClip.duration = ConstructionLength;
        }

        private static void AddControlClip(ControlTrack track, string name, float start, float duration)
        {
            var clip = track.CreateClip<ControlPlayableAsset>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;
            var asset = (ControlPlayableAsset)clip.asset;
            asset.sourceGameObject.exposedName = GUID.Generate().ToString();
            asset.updateParticle = true;
            asset.updateDirector = false;
            asset.active = true;
            asset.postPlayback = ActivationControlPlayable.PostPlaybackState.Inactive;
            EditorUtility.SetDirty(asset);
        }

        private static AnimationClip GetOrCreateClip(string name, Action<AnimationClip> fill)
        {
            string path = $"{ConstructionFolder}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            clip = new AnimationClip { name = name, frameRate = 60f };
            fill(clip);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        /// <summary>(시간, 값) 키들을 부드러운(Clamped Auto) 곡선으로 만들어 path의 Transform 속성에 넣는다.</summary>
        private static void RigCurve(AnimationClip clip, string path, string property, params (float t, float v)[] keys) =>
            ClipCurve(clip, path, typeof(Transform), property, keys);

        private static void ClipCurve(AnimationClip clip, string path, Type type, string property, params (float t, float v)[] keys)
        {
            var curve = new AnimationCurve();
            foreach (var (t, v) in keys) curve.AddKey(t, v);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
        }

        /// <summary>펜스 네 장이 차례로 툭툭 솟아올랐다가(살짝 넘치게), 공개 뒤 차례로 땅속으로 내려간다.</summary>
        private static void FillFenceClip(AnimationClip clip)
        {
            for (int s = 0; s < 4; s++)
            {
                string path = $"Fence_{SideNames[s].Substring(5)}";
                float up = s * 0.1f, down = RevealTime + 0.5f + s * 0.06f;
                RigCurve(clip, path, "m_LocalScale.y", (0f, 0f), (up, 0f), (up + 0.22f, 1.2f), (up + 0.36f, 0.92f), (up + 0.46f, 1f),
                    (down, 1f), (down + 0.3f, 0f), (ConstructionLength, 0f));
            }
        }

        /// <summary>
        /// 비계: 땅에서 쑥 자라고(넘치게 늘었다 제자리) → 망치질 동안 덜덜 흔들리고 →
        /// 네 면이 조금씩 어긋나며 바깥으로 쿵 넘어져 튕기고 → 작아지며 사라진다.
        /// </summary>
        private static void FillScaffoldClip(AnimationClip clip)
        {
            RigCurve(clip, "Shaker", "m_LocalScale.y", (0f, 0f), (0.3f, 0f), (0.85f, 1.08f), (1.0f, 0.97f), (1.1f, 1f), (ConstructionLength, 1f));

            // 망치질 흔들림: 1.2초~2.6초 동안 좌우로 잘게
            var shake = new System.Collections.Generic.List<(float, float)> { (0f, 0f), (1.2f, 0f) };
            for (float t = 1.28f; t < CollapseTime; t += 0.08f) shake.Add((t, (shake.Count % 2 == 0 ? 1f : -1f) * 0.01f));
            shake.Add((CollapseTime, 0f));
            RigCurve(clip, "Shaker", "m_LocalPosition.x", shake.ToArray());

            for (int s = 0; s < 4; s++)
            {
                string path = $"Shaker/{SideNames[s]}";
                float yaw = s * 90f, start = CollapseTime + s * 0.05f;
                // 처음엔 천천히, 점점 빨리(중력처럼) 넘어져서 바닥에 부딪혀 살짝 튄다.
                RigCurve(clip, path, "localEulerAnglesRaw.x", (0f, 0f), (start, 0f), (start + 0.18f, 12f), (start + 0.32f, 45f),
                    (start + 0.42f, 90f), (start + 0.5f, 80f), (start + 0.58f, 90f), (ConstructionLength, 90f));
                RigCurve(clip, path, "localEulerAnglesRaw.y", (0f, yaw), (ConstructionLength, yaw));
                RigCurve(clip, path, "localEulerAnglesRaw.z", (0f, 0f), (ConstructionLength, 0f));
                float gone = RevealTime + 0.55f;
                foreach (var axis in new[] { "x", "y", "z" })
                    RigCurve(clip, path, $"m_LocalScale.{axis}", (0f, 1f), (gone, 1f), (gone + 0.3f, 0f), (ConstructionLength, 0f));
            }
        }

        /// <summary>완성 샷 카메라(Dolly): 정면 오른쪽 낮은 곳에서 왼쪽으로 미끄러지며 살짝 떠올라 건물을 올려다본다.</summary>
        private static void FillCameraClip(AnimationClip clip)
        {
            RigCurve(clip, "Dolly", "m_LocalPosition.x", (0f, 0.8f), (2.55f, 0.8f), (ConstructionLength, -0.7f));
            RigCurve(clip, "Dolly", "m_LocalPosition.y", (0f, 0.05f), (2.55f, 0.05f), (ConstructionLength, 0.35f));
            RigCurve(clip, "Dolly", "m_LocalPosition.z", (0f, 2.1f), (2.55f, 2.1f), (ConstructionLength, 2.5f));
        }

        // ------------------------------------------------------------------ 중계 창 (클리커 화면)

        /// <summary>
        /// 왼쪽 아래(구경하기 버튼 위) TV 모양 창: [● LIVE] 공사 현장 / 카메라 화면 / 건물 이름 자막.
        /// Panel의 크기와 Flash(하얀 번쩍임)를 Animator가 움직여 TV처럼 켜지고 꺼진다.
        /// </summary>
        private static LiveBroadcastView BuildLiveBroadcast(RectTransform hud)
        {
            var root = CreateRect("LiveBroadcast", hud);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(40f, 250f);
            const float width = 440f, inset = 12f, header = 58f;
            float screenHeight = (width - inset * 2f) * 9f / 16f;
            root.sizeDelta = new Vector2(width, screenHeight + inset + header);
            // 이펙트 레이어(황금 낙곱새 등)보다는 아래에 둔다.
            var effectLayer = hud.Find("EffectLayer");
            if (effectLayer != null) root.SetSiblingIndex(effectLayer.GetSiblingIndex());

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var panel = Stretch(CreateRect("Panel", root), Vector2.zero, Vector2.one);
            AddCard(panel, CardCream, new Color(0.66f, 0.16f, 0.1f), 0.7f, true);
            var rim = panel.gameObject.AddComponent<Outline>();
            rim.effectColor = ChiliRed;
            rim.effectDistance = new Vector2(3f, -3f);

            var head = TopBand(CreateRect("Header", panel), 10f, 44f, inset, inset);
            var tag = Stretch(CreateRect("Tag", head), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(120f, 0f));
            AddImage(tag, CookieArtFactory.SlantTag, ChiliRed);
            var dot = Place(CreateRect("LiveDot", tag), new Vector2(0f, 0.5f), new Vector2(12f, 12f));
            dot.anchoredPosition = new Vector2(22f, 0f);
            AddImage(dot, CookieArtFactory.Dot, Color.white);
            dot.gameObject.AddComponent<Blink>();
            AddText(Stretch(CreateRect("Label", tag), Vector2.zero, Vector2.one, new Vector2(36f, 0f), new Vector2(-14f, 0f)),
                "LIVE", 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            AddText(Stretch(CreateRect("Title", head), Vector2.zero, Vector2.one, new Vector2(134f, 0f), Vector2.zero),
                "공사 현장", 26, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);

            var screen = Stretch(CreateRect("Screen", panel), Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -header));
            screen.gameObject.AddComponent<RawImage>().texture = GetOrCreateBroadcastTexture();
            var band = Stretch(CreateRect("SubtitleBand", screen), Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 42f));
            AddImage(band, null, new Color(0f, 0f, 0f, 0.55f));
            var subtitle = AddText(Stretch(CreateRect("Subtitle", band), Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, 0f)),
                "원양 새우잡이배", 24, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            FitOneLine(subtitle, 14);
            var flash = AddImage(Stretch(CreateRect("Flash", screen), Vector2.zero, Vector2.one), null, new Color(1f, 1f, 1f, 0f));

            var animator = root.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = GetOrCreateBroadcastController();
            var view = root.gameObject.AddComponent<LiveBroadcastView>();
            var audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.volume = 0.6f;
            Wire(view, ("animator", animator), ("subtitle", subtitle), ("canvasGroup", group), ("audioSource", audio),
                ("openSound", ConstructionSound("glitch_002")), ("closeSound", ConstructionSound("minimize_003")));
            return view;
        }

        /// <summary>Closed → (Open) → TvOn → Idle → (Open 꺼짐) → TvOff → Closed. 클립은 Animation/LiveBroadcast에 있다.</summary>
        private static AnimatorController GetOrCreateBroadcastController()
        {
            string path = $"{BroadcastFolder}/LiveBroadcast.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;

            var closedClip = GetOrCreateBroadcastClip("TvClosed", clip =>
            {
                ClipCurve(clip, "", typeof(CanvasGroup), "m_Alpha", (0f, 0f));
                ClipCurve(clip, "Panel", typeof(RectTransform), "m_LocalScale.x", (0f, 0f));
                ClipCurve(clip, "Panel", typeof(RectTransform), "m_LocalScale.y", (0f, 0.02f));
                ClipCurve(clip, "Panel/Screen/Flash", typeof(Image), "m_Color.a", (0f, 0f));
            });
            // TV를 켤 때처럼: 가운데 점 → 가로줄로 펴짐 → 위아래로 펼쳐지며 살짝 넘침 → 하얗게 번쩍였다 화면이 나온다.
            var onClip = GetOrCreateBroadcastClip("TvOn", clip =>
            {
                ClipCurve(clip, "", typeof(CanvasGroup), "m_Alpha", (0f, 1f), (0.4f, 1f));
                ClipCurve(clip, "Panel", typeof(RectTransform), "m_LocalScale.x", (0f, 0.04f), (0.12f, 1.03f), (0.18f, 1f), (0.4f, 1f));
                ClipCurve(clip, "Panel", typeof(RectTransform), "m_LocalScale.y", (0f, 0.02f), (0.12f, 0.02f), (0.28f, 1.08f), (0.36f, 0.97f), (0.4f, 1f));
                ClipCurve(clip, "Panel/Screen/Flash", typeof(Image), "m_Color.a", (0f, 1f), (0.2f, 1f), (0.4f, 0f));
            });
            var idleClip = GetOrCreateBroadcastClip("TvIdle", clip =>
            {
                ClipCurve(clip, "", typeof(CanvasGroup), "m_Alpha", (0f, 1f));
                ClipCurve(clip, "Panel", typeof(RectTransform), "m_LocalScale.x", (0f, 1f));
                ClipCurve(clip, "Panel", typeof(RectTransform), "m_LocalScale.y", (0f, 1f));
                ClipCurve(clip, "Panel/Screen/Flash", typeof(Image), "m_Color.a", (0f, 0f));
            });
            // TV를 끌 때처럼: 하얗게 번쩍 → 위아래로 납작해져 가로줄 → 가운데 점으로 사라짐
            var offClip = GetOrCreateBroadcastClip("TvOff", clip =>
            {
                ClipCurve(clip, "", typeof(CanvasGroup), "m_Alpha", (0f, 1f), (0.3f, 1f), (0.36f, 0f));
                ClipCurve(clip, "Panel", typeof(RectTransform), "m_LocalScale.x", (0f, 1f), (0.16f, 1f), (0.32f, 0.03f), (0.36f, 0f));
                ClipCurve(clip, "Panel", typeof(RectTransform), "m_LocalScale.y", (0f, 1f), (0.06f, 1.04f), (0.16f, 0.02f), (0.36f, 0.02f));
                ClipCurve(clip, "Panel/Screen/Flash", typeof(Image), "m_Color.a", (0f, 0f), (0.06f, 1f), (0.36f, 1f));
            });

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Open", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var closed = machine.AddState("Closed");
            closed.motion = closedClip;
            var opening = machine.AddState("Opening");
            opening.motion = onClip;
            var open = machine.AddState("Open");
            open.motion = idleClip;
            var closing = machine.AddState("Closing");
            closing.motion = offClip;
            machine.defaultState = closed;

            void Link(AnimatorState from, AnimatorState to, bool? whenOpen)
            {
                var transition = from.AddTransition(to);
                transition.duration = 0f;
                if (whenOpen.HasValue)
                {
                    transition.hasExitTime = false;
                    transition.AddCondition(whenOpen.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "Open");
                }
                else
                {
                    transition.hasExitTime = true;
                    transition.exitTime = 1f;
                }
            }
            Link(closed, opening, true);
            Link(opening, open, null);
            Link(opening, closing, false);
            Link(open, closing, false);
            Link(closing, closed, null);
            Link(closing, opening, true);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip GetOrCreateBroadcastClip(string name, Action<AnimationClip> fill)
        {
            string path = $"{BroadcastFolder}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            clip = new AnimationClip { name = name, frameRate = 60f };
            fill(clip);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
    }
}
