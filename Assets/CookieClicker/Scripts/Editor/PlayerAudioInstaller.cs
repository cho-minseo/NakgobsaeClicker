using System.Linq;
using CookieClicker.Audio;
using CookieClicker.Gameplay;
using CookieClicker.Gameplay.Viewmodel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 구경하기 플레이어 효과음(발소리·점프·착지·수영·잠수)을 설치한다.
    /// 소리 묶음은 SoundSet 에셋(Audio/Explore/Sets)으로 만들고, 소리 내는 AudioSource는 Player/Audio 아래에 미리 둔다.
    /// 여러 번 실행해도 Player/Audio를 다시 만들 뿐이고, 이미 있는 SoundSet 에셋은 Inspector에서 고친 값을 그대로 둔다.
    /// 소리 파일 출처: Kenney Impact Sounds, OpenGameArt rubberduck·BMacZero (모두 CC0)
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string ExploreAudioFolder = "Assets/CookieClicker/Audio/Explore";
        private const string SoundSetFolder = ExploreAudioFolder + "/Sets";

        [MenuItem("Tools/Cookie Clicker/Install Player Sounds (플레이어 효과음 설치)", priority = 22)]
        private static void InstallPlayerAudioMenu()
        {
            var world = GameObject.Find("ExploreWorld");
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (world == null || player == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "ExploreWorld / Player를 찾지 못했습니다.", "확인");
                return;
            }
            InstallPlayerAudio(world.transform, player);
            EditorSceneManager.MarkSceneDirty(world.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "플레이어 효과음을 설치했습니다.\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        public static void InstallPlayerAudio(Transform world, FirstPersonController player)
        {
            ConfigureAudioImport();

            var grass = SoundSetAsset("Footsteps_Grass", new Vector2(0.85f, 1f), new Vector2(0.9f, 1.1f), "Footsteps/footstep_grass_");
            var mud = SoundSetAsset("Footsteps_Mudflat", new Vector2(0.55f, 0.75f), new Vector2(0.8f, 0.95f), "Footsteps/mud_slime_");
            var landing = SoundSetAsset("Landing", new Vector2(0.85f, 1f), new Vector2(0.85f, 1f), "Jump/impactSoft_heavy_");
            var stroke = SoundSetAsset("SwimStroke", new Vector2(0.75f, 1f), new Vector2(0.85f, 1.1f), "Swim/stroke_splash_");
            var enter = SoundSetAsset("WaterEnter", new Vector2(0.9f, 1f), new Vector2(0.9f, 1.05f), "Swim/enter_splash_");
            var bubbles = SoundSetAsset("Bubbles", new Vector2(0.7f, 1f), new Vector2(0.8f, 1.15f), "Underwater/bubble");

            // 바닥 영역: 갯벌 판 위에서는 질척한 발소리. 판 크기(scale)가 곧 영역 크기다.
            var mudflat = world.Find("Ground/Zone_Mudflat");
            if (mudflat != null)
            {
                var zone = GetOrAdd<GroundSurfaceZone>(mudflat.gameObject);
                var serialized = new SerializedObject(zone);
                serialized.FindProperty("area").vector2Value = new Vector2(mudflat.lossyScale.x, mudflat.lossyScale.z);
                serialized.FindProperty("footsteps").objectReferenceValue = mud;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var old = player.transform.Find("Audio");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Audio").transform;
            root.SetParent(player.transform, false);

            // 한 번씩 나는 소리: 4개를 돌려 쓴다 (발소리 + 착지음 + 첨벙이 겹쳐도 끊기지 않게).
            var voices = new AudioSource[4];
            for (int i = 0; i < voices.Length; i++)
                voices[i] = CreateAudioSource(root, $"Voice{i}", null, false);

            // 물속 배경: 낮은 웅웅거림 + 보글거림. 평소에는 볼륨 0이었다가 머리가 잠기면 커진다.
            var loops = new[]
            {
                CreateAudioSource(root, "UnderwaterRumble", Clip("Underwater/loop_water_02.ogg"), true),
                CreateAudioSource(root, "UnderwaterBubbles", Clip("Underwater/loop_bubbles_02.ogg"), true),
            };

            // 먹먹함: 귀(AudioListener)에 필터를 걸어야 발소리·물소리·배경음 모두 함께 먹먹해진다.
            var listener = player.GetComponentInChildren<AudioListener>(true);
            var muffle = GetOrAdd<AudioLowPassFilter>(listener.gameObject);
            muffle.cutoffFrequency = 22000f;
            muffle.lowpassResonanceQ = 1f;
            muffle.enabled = false;

            var audio = root.gameObject.AddComponent<PlayerAudio>();
            Wire(audio,
                ("player", player),
                ("hands", player.GetComponentInChildren<ViewmodelAnimator>(true)),
                ("voices", voices),
                ("underwaterLoops", loops),
                ("muffle", muffle),
                ("defaultFootsteps", grass),
                ("landing", landing),
                ("swimStroke", stroke),
                ("waterEnter", enter),
                ("bubbles", bubbles));
        }

        private static AudioSource CreateAudioSource(Transform parent, string name, AudioClip clip, bool loop)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(parent, false);
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 내 몸에서 나는 소리라 방향 없이 2D
            source.volume = loop ? 0f : 1f;
            return source;
        }

        /// <summary>SoundSet 에셋이 없으면 만들고, 이름이 prefix로 시작하는 소리 파일을 넣는다. 이미 있으면 그대로 쓴다.</summary>
        private static SoundSet SoundSetAsset(string name, Vector2 volume, Vector2 pitch, string clipPrefix)
        {
            string path = $"{SoundSetFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SoundSet>(path);
            if (existing != null) return existing;

            EditorFolders.Ensure(SoundSetFolder);
            var set = ScriptableObject.CreateInstance<SoundSet>();
            AssetDatabase.CreateAsset(set, path);

            string folder = $"{ExploreAudioFolder}/{clipPrefix.Substring(0, clipPrefix.LastIndexOf('/'))}";
            string filePrefix = clipPrefix.Substring(clipPrefix.LastIndexOf('/') + 1);
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetFileName(p).StartsWith(filePrefix))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .ToArray();

            var serialized = new SerializedObject(set);
            var clipsProperty = serialized.FindProperty("clips");
            clipsProperty.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) clipsProperty.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            serialized.FindProperty("volume").vector2Value = volume;
            serialized.FindProperty("pitch").vector2Value = pitch;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return set;
        }

        private static AudioClip Clip(string relativePath) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{ExploreAudioFolder}/{relativePath}");

        /// <summary>
        /// 짧은 효과음은 메모리에 풀어 두고(재생 지연 없음), 긴 반복음은 압축된 채로 둔다(메모리 절약).
        /// 내 몸에서 나는 소리라 스테레오가 필요 없어 모노로 줄인다.
        /// </summary>
        private static void ConfigureAudioImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ExploreAudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool isLoop = System.IO.Path.GetFileName(path).StartsWith("loop_");
                var settings = importer.defaultSampleSettings;
                var loadType = isLoop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                if (importer.forceToMono && settings.loadType == loadType) continue;

                importer.forceToMono = true;
                settings.loadType = loadType;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }
    }
}
