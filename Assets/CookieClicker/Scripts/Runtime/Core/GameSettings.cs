using System;
using UnityEngine;

namespace CookieClicker.Core
{
    public enum Language
    {
        Korean,
        English,
    }

    /// <summary>
    /// 플레이어 설정(소리 크기, 마우스 감도, 언어, 클릭 대체 키). 세이브 데이터와 따로 기기에 저장해서 "새로 시작"해도 유지된다.
    /// 값을 바꾸면 바로 적용(미리보기)되지만 기기에는 Save()를 불러야 남는다. Load()를 부르면 마지막 저장값으로 되돌아간다.
    /// </summary>
    public static class GameSettings
    {
        private const string VolumeKey = "settings.volume";
        private const string LookSensitivityKey = "settings.lookSensitivity";
        private const string LanguageKey = "settings.language";
        private const string ClickKeyKey = "settings.clickKey";

        public const float DefaultVolume = 1f;
        public const float DefaultLookSensitivity = 1f;
        public const string DefaultClickKey = "<Keyboard>/space";
        /// <summary>처음 켤 때는 기기 언어를 따른다 (한국어 기기 → 한국어, 그 밖 → 영어).</summary>
        public static Language DefaultLanguage =>
            Application.systemLanguage == SystemLanguage.Korean ? Language.Korean : Language.English;

        /// <summary>어떤 설정이든 바뀌면 (미리보기·되돌리기 포함) 알린다. 언어·키를 쓰는 화면이 구독한다.</summary>
        public static event Action Changed;

        private static bool loaded;
        private static float volume;
        private static float lookSensitivity;
        private static Language language;
        private static string clickKey;

        /// <summary>전체 소리 크기 (0~1)</summary>
        public static float Volume
        {
            get { EnsureLoaded(); return volume; }
            set { EnsureLoaded(); volume = Mathf.Clamp01(value); Apply(); }
        }

        /// <summary>구경하기 마우스 감도 배율 (1 = 기본)</summary>
        public static float LookSensitivity
        {
            get { EnsureLoaded(); return lookSensitivity; }
            set { EnsureLoaded(); lookSensitivity = Mathf.Clamp(value, 0.2f, 3f); Apply(); }
        }

        public static Language Language
        {
            get { EnsureLoaded(); return language; }
            set { EnsureLoaded(); language = value; Apply(); }
        }

        /// <summary>냄비를 누르는 키 (Input System 경로, 예: "&lt;Keyboard&gt;/space"). 마우스 왼쪽 클릭과 같은 효과.</summary>
        public static string ClickKey
        {
            get { EnsureLoaded(); return clickKey; }
            set { EnsureLoaded(); clickKey = string.IsNullOrEmpty(value) ? DefaultClickKey : value; Apply(); }
        }

        /// <summary>기기에 저장된 값으로 되돌린다 (설정 창에서 "취소").</summary>
        public static void Load()
        {
            volume = PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
            lookSensitivity = PlayerPrefs.GetFloat(LookSensitivityKey, DefaultLookSensitivity);
            language = (Language)PlayerPrefs.GetInt(LanguageKey, (int)DefaultLanguage);
            clickKey = PlayerPrefs.GetString(ClickKeyKey, DefaultClickKey);
            loaded = true;
            Apply();
        }

        /// <summary>지금 값을 기기에 기록한다 (설정 창에서 "저장").</summary>
        public static void Save()
        {
            EnsureLoaded();
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.SetFloat(LookSensitivityKey, lookSensitivity);
            PlayerPrefs.SetInt(LanguageKey, (int)language);
            PlayerPrefs.SetString(ClickKeyKey, clickKey);
            PlayerPrefs.Save();
        }

        /// <summary>처음 값으로 되돌린다. Save()를 불러야 기기에 남는다.</summary>
        public static void ResetToDefaults()
        {
            volume = DefaultVolume;
            lookSensitivity = DefaultLookSensitivity;
            language = DefaultLanguage;
            clickKey = DefaultClickKey;
            loaded = true;
            Apply();
        }

        private static void EnsureLoaded()
        {
            if (!loaded) Load();
        }

        private static void Apply()
        {
            AudioListener.volume = volume;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnLaunch() => Load();
    }

    /// <summary>
    /// 한국어/영어 문구 고르기. 코드의 문구는 Loc.T("한국어", "English")처럼 두 언어를 나란히 적는다.
    /// 키 표 대신 나란히 적는 방식이라, 문구를 고칠 때 한 곳만 보면 된다.
    /// </summary>
    public static class Loc
    {
        public static bool English => GameSettings.Language == Language.English;

        public static string T(string korean, string english) =>
            English && !string.IsNullOrEmpty(english) ? english : korean;
    }
}
