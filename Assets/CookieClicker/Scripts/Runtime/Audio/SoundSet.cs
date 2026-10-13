using System;
using UnityEngine;

namespace CookieClicker.Audio
{
    /// <summary>
    /// 같은 종류의 소리 묶음 (예: 잔디 발소리 5개). 재생할 때마다 하나를 골라 볼륨·음높이를 살짝 흔들어서
    /// 같은 소리가 기계적으로 반복되는 느낌("머신건 효과")을 없앤다.
    /// 에셋이라 Inspector에서 소리를 바꾸거나 추가해도 코드는 그대로다.
    /// </summary>
    [CreateAssetMenu(menuName = "Cookie Clicker/Sound Set", fileName = "SoundSet")]
    public class SoundSet : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
        [SerializeField] private Vector2 volume = new Vector2(0.8f, 1f);
        [SerializeField] private Vector2 pitch = new Vector2(0.92f, 1.08f);

        [NonSerialized] private int lastIndex = -1;

        public bool IsEmpty => clips == null || clips.Length == 0;

        /// <param name="volumeScale">상황에 따른 세기 (예: 높이 떨어질수록 큰 착지음)</param>
        /// <param name="pitchScale">상황에 따른 음높이 (예: 물속에서는 낮게)</param>
        public void Play(AudioSource source, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (IsEmpty || source == null) return;

            // 바로 전에 나온 소리는 연달아 고르지 않는다.
            int index = UnityEngine.Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastIndex) index = (index + 1) % clips.Length;
            lastIndex = index;

            source.clip = clips[index];
            source.volume = UnityEngine.Random.Range(volume.x, volume.y) * volumeScale;
            source.pitch = UnityEngine.Random.Range(pitch.x, pitch.y) * pitchScale;
            source.Play();
        }
    }
}
