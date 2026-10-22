using System.Linq;
using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 3인칭 캐릭터 설치: Kenney Mini Characters 모델을 플레이어에 붙이고, 동작 상태 머신(Animator Controller)을 만든다.
    /// 캐릭터를 바꾸려면 CharacterModelPath만 고치고 메뉴를 다시 실행하면 된다 (모든 캐릭터가 같은 뼈대라 동작은 그대로 쓴다).
    /// 상태 머신은 이미 있으면 건드리지 않으므로, Animator 창에서 직접 고친 전이·속도가 유지된다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string CharacterModelPath = "Assets/CookieClicker/Models/Kenney/Characters/character-female-b.glb";
        private const string AvatarAnimationFolder = "Assets/CookieClicker/Animation/Avatar";
        private const string AvatarControllerPath = AvatarAnimationFolder + "/PlayerAvatar.controller";
        /// <summary>원본 키 0.72m → 약 1.65m</summary>
        private const float CharacterScale = 2.3f;

        /// <summary>쓰는 동작과 반복 여부</summary>
        private static readonly (string clip, bool loop)[] AvatarClips =
        {
            ("idle", true), ("walk", true), ("sprint", true), ("jump", false), ("fall", true), ("sit", true),
        };

        [MenuItem("Tools/Cookie Clicker/Install Player Avatar (3인칭 캐릭터 설치)")]
        private static void InstallAvatarMenu()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            var hud = Object.FindAnyObjectByType<ExploreHudView>(FindObjectsInactive.Include);
            var mode = Object.FindAnyObjectByType<ExploreModeController>(FindObjectsInactive.Include);
            if (player == null || hud == null || mode == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "구경하기 월드가 없습니다. 먼저 Build Scene으로 씬을 만들어 주세요.", "확인");
                return;
            }
            InstallAvatar(player, player.GetComponentInChildren<Camera>(true), hud, mode);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "3인칭 캐릭터를 설치했습니다.\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        internal static void InstallAvatar(FirstPersonController player, Camera playerCamera, ExploreHudView hud, ExploreModeController mode)
        {
            var root = player.transform;
            foreach (var old in new[] { root.Find("Avatar"), hud.transform.Find("SeatPrompt") })
                if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (var c in root.GetComponents<CameraModeController>()) Object.DestroyImmediate(c);

            // Avatar(방향 돌리기) → Model(캐릭터 모델, 크기·정면 맞춤)
            var facing = new GameObject("Avatar").transform;
            facing.SetParent(root, false);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, facing);
            model.name = "Model";
            model.transform.localScale = Vector3.one * CharacterScale;

            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = GetOrCreateAvatarController(modelAsset);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var avatar = facing.gameObject.AddComponent<PlayerAvatar>();
            Wire(avatar, ("player", player), ("animator", animator), ("facing", facing));

            var cameraMode = root.gameObject.AddComponent<CameraModeController>();
            Wire(cameraMode, ("player", player), ("cameraTransform", playerCamera.transform), ("avatar", avatar),
                ("viewmodel", playerCamera.transform.Find("Viewmodel").gameObject));

            // 의자를 보면 뜨는 "[F] 앉기" 안내
            var hudRect = (RectTransform)hud.transform;
            var seatRoot = Stretch(CreateRect("SeatPrompt", hudRect), Vector2.zero, Vector2.one);
            var prompt = AddText(Place(CreateRect("Prompt", seatRoot), new Vector2(0.5f, 0.42f), new Vector2(500f, 50f)),
                "[F] 앉기", 28, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(prompt);
            prompt.gameObject.SetActive(false);
            var seats = seatRoot.gameObject.AddComponent<SeatInteractor>();
            Wire(seats, ("player", player), ("playerCamera", playerCamera), ("avatar", avatar), ("cameraMode", cameraMode),
                ("exploreMode", mode), ("cancelAction", FindAction("UI/Cancel")), ("prompt", prompt.gameObject), ("promptText", prompt));
        }

        /// <summary>
        /// 동작 상태 머신: Locomotion(Speed로 서기·걷기·달리기 섞기) ↔ Jump → Fall, 어디서든 Sitting이면 Sit.
        /// 땅에서 잠깐 떨어지는 계단·경사는 AirTime이 짧아서 Fall로 가지 않는다.
        /// </summary>
        private static AnimatorController GetOrCreateAvatarController(GameObject modelAsset)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(AvatarControllerPath);
            if (existing != null) return existing;

            EditorFolders.Ensure(AvatarAnimationFolder);
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(modelAsset)).OfType<AnimationClip>().ToList();
            var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            foreach (var (name, loop) in AvatarClips)
            {
                var source = sourceClips.FirstOrDefault(c => c.name == name);
                if (source == null)
                {
                    Debug.LogError($"[Avatar] {modelAsset.name}에 '{name}' 동작이 없습니다.");
                    continue;
                }
                // 모델 안의 동작은 읽기 전용이라 복사본을 만들어 반복 여부를 정한다 (복사본은 Animation 창에서 고칠 수 있다).
                var copy = Object.Instantiate(source);
                copy.name = name;
                var settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(copy, settings);
                AssetDatabase.CreateAsset(copy, $"{AvatarAnimationFolder}/{name}.anim");
                clips[name] = copy;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(AvatarControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("AirTime", AnimatorControllerParameterType.Float);
            controller.AddParameter("Sitting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;

            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["idle"], 0f);
            tree.AddChild(clips["walk"], 1f);
            tree.AddChild(clips["sprint"], 1.7f); // 달리기 속도 / 걷기 속도
            machine.defaultState = locomotion;

            var jump = machine.AddState("Jump");
            jump.motion = clips["jump"];
            var fall = machine.AddState("Fall");
            fall.motion = clips["fall"];
            var sit = machine.AddState("Sit");
            sit.motion = clips["sit"];

            var toJump = locomotion.AddTransition(jump);
            toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            toJump.hasExitTime = false;
            toJump.duration = 0.08f;

            var jumpToFall = jump.AddTransition(fall);
            jumpToFall.hasExitTime = true;
            jumpToFall.exitTime = 0.85f;
            jumpToFall.duration = 0.15f;

            var jumpLand = jump.AddTransition(locomotion);
            jumpLand.AddCondition(AnimatorConditionMode.Less, 0.01f, "AirTime");
            jumpLand.hasExitTime = true;
            jumpLand.exitTime = 0.4f;
            jumpLand.duration = 0.1f;

            var toFall = locomotion.AddTransition(fall);
            toFall.AddCondition(AnimatorConditionMode.Greater, 0.25f, "AirTime");
            toFall.hasExitTime = false;
            toFall.duration = 0.2f;

            var land = fall.AddTransition(locomotion);
            land.AddCondition(AnimatorConditionMode.Less, 0.01f, "AirTime");
            land.hasExitTime = false;
            land.duration = 0.1f;

            var toSit = machine.AddAnyStateTransition(sit);
            toSit.AddCondition(AnimatorConditionMode.If, 0f, "Sitting");
            toSit.canTransitionToSelf = false;
            toSit.hasExitTime = false;
            toSit.duration = 0.2f;

            var standUp = sit.AddTransition(locomotion);
            standUp.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sitting");
            standUp.hasExitTime = false;
            standUp.duration = 0.2f;

            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
