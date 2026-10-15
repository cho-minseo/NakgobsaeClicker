using UnityEngine;

namespace CookieClicker.Core
{
    /// <summary>
    /// 플레이어 설정(소리 크기, 마우스 감도). 세이브 데이터와 따로 기기에 저장해서 "새로 시작"해도 유지된다.
    /// 값을 바꾸면 바로 적용되고, 게임을 켤 때 자동으로 다시 적용된다.
    /// </summary>
    public static class GameSettings
    {
        private const string VolumeKey = "settings.volume";
        private const string LookSensitivityKey = "settings.lookSensitivity";

        /// <summary>전체 소리 크기 (0~1)</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 1f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                AudioListener.volume = Volume;
            }
        }

        /// <summary>구경하기 마우스 감도 배율 (1 = 기본)</summary>
        public static float LookSensitivity
        {
            get => PlayerPrefs.GetFloat(LookSensitivityKey, 1f);
            set => PlayerPrefs.SetFloat(LookSensitivityKey, Mathf.Clamp(value, 0.2f, 3f));
        }

        /// <summary>바꾼 설정을 디스크에 기록한다 (설정 창을 닫을 때).</summary>
        public static void Save() => PlayerPrefs.Save();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnLaunch() => AudioListener.volume = Volume;
    }
}
