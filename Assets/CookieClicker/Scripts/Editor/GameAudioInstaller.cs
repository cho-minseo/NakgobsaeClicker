using System.Linq;
using CookieClicker.Audio;
using CookieClicker.Core;
using CookieClicker.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 클리커 화면 효과음(냄비 클릭·구매·버튼)과 배경음악을 설치한다. GameManager 아래 GameAudio 오브젝트에 미리 둔다.
    /// 효과음: Audio/UI (Kenney Interface Sounds, CC0)
    /// 배경음악: Audio/Music 폴더에 이름이 clicker_ / explore_ 로 시작하는 파일을 넣으면 각 화면의 곡으로 쓴다. 없으면 조용하다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string UiAudioFolder = "Assets/CookieClicker/Audio/UI";
        private const string MusicFolder = "Assets/CookieClicker/Audio/Music";
        private const string UiSoundSetFolder = UiAudioFolder + "/Sets";

        [MenuItem("Tools/Cookie Clicker/Install Game Sounds (클릭 소리·배경음악 설치)", priority = 23)]
        private static void InstallGameAudioMenu()
        {
            var game = Object.FindAnyObjectByType<CookieGame>();
            if (game == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "GameManager를 찾지 못했습니다.", "확인");
                return;
            }
            InstallGameAudio(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "클릭 소리·배경음악을 설치했습니다.\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        public static void InstallGameAudio(CookieGame game)
        {
            ConfigureGameAudioImport();

            var pot = UiSoundSet("PotClick", new Vector2(0.75f, 0.95f), new Vector2(0.85f, 1.2f), "drop_");
            var building = UiSoundSet("BuildingBought", new Vector2(0.9f, 1f), new Vector2(0.97f, 1.03f), "confirmation_");
            var upgrade = UiSoundSet("UpgradeBought", new Vector2(0.9f, 1f), new Vector2(0.97f, 1.05f), "maximize_");
            var golden = UiSoundSet("GoldenClaimed", new Vector2(0.9f, 1f), new Vector2(0.95f, 1.1f), "glass_");
            var button = UiSoundSet("ButtonClick", new Vector2(0.8f, 1f), new Vector2(0.95f, 1.08f), "click_");
            var enter = UiSoundSet("ExploreEnter", new Vector2(0.9f, 1f), new Vector2(0.95f, 1.05f), "open_");
            var exit = UiSoundSet("ExploreExit", new Vector2(0.9f, 1f), new Vector2(0.95f, 1.05f), "close_");

            var old = game.transform.Find("GameAudio");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("GameAudio").transform;
            root.SetParent(game.transform, false);

            var voices = new AudioSource[4];
            for (int i = 0; i < voices.Length; i++) voices[i] = CreateAudioSource(root, $"Voice{i}", null, false);
            var clickerMusic = CreateAudioSource(root, "Music_Clicker", MusicClip("clicker_"), true);
            var exploreMusic = CreateAudioSource(root, "Music_Explore", MusicClip("explore_"), true);
            // 구경하기 곡이 없으면 클리커 곡을 계속 튼다.
            if (exploreMusic.clip == null) exploreMusic.clip = clickerMusic.clip;

            Wire(root.gameObject.AddComponent<GameAudio>(),
                ("game", game),
                ("explore", Object.FindAnyObjectByType<ExploreModeController>(FindObjectsInactive.Include)),
                ("voices", voices),
                ("potClick", pot),
                ("buildingBought", building),
                ("upgradeBought", upgrade),
                ("goldenClaimed", golden),
                ("buttonClick", button),
                ("exploreEnter", enter),
                ("exploreExit", exit),
                ("clickerMusic", clickerMusic),
                ("exploreMusic", exploreMusic));
        }

        private static AudioClip MusicClip(string prefix) =>
            !AssetDatabase.IsValidFolder(MusicFolder)
                ? null
                : AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => System.IO.Path.GetFileName(p).StartsWith(prefix))
                    .OrderBy(p => p)
                    .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                    .FirstOrDefault();

        /// <summary>SoundSet 에셋이 없으면 만들고, Audio/UI에서 이름이 prefix로 시작하는 소리를 넣는다. 이미 있으면 그대로 쓴다.</summary>
        private static SoundSet UiSoundSet(string name, Vector2 volume, Vector2 pitch, string filePrefix)
        {
            string path = $"{UiSoundSetFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SoundSet>(path);
            if (existing != null) return existing;

            EditorFolders.Ensure(UiSoundSetFolder);
            var set = ScriptableObject.CreateInstance<SoundSet>();
            AssetDatabase.CreateAsset(set, path);
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { UiAudioFolder })
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

        /// <summary>
        /// 짧은 효과음은 모노로 풀어 두고(재생 지연 없음), 배경음악은 들으면서 풀도록(Streaming) 해서 메모리와 로딩을 아낀다.
        /// </summary>
        private static void ConfigureGameAudioImport()
        {
            var folders = AssetDatabase.IsValidFolder(MusicFolder) ? new[] { UiAudioFolder, MusicFolder } : new[] { UiAudioFolder };
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool isMusic = path.StartsWith(MusicFolder);
                var settings = importer.defaultSampleSettings;
                var loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (importer.forceToMono == !isMusic && settings.loadType == loadType) continue;

                importer.forceToMono = !isMusic;
                settings.loadType = loadType;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = isMusic ? 0.5f : 0.7f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }
    }
}
