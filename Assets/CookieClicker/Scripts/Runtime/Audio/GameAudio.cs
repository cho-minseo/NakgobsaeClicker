using System.Collections.Generic;
using CookieClicker.Core;
using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.Audio
{
    /// <summary>
    /// 클리커 화면의 효과음과 배경음악. 게임 코드는 소리를 모르고, 이 스크립트가 CookieGame 이벤트를 듣고 소리를 낸다.
    /// - 냄비 클릭: 퐁 (너무 빨리 누르면 몇 번은 건너뛰어서 시끄럽지 않게)
    /// - 건물·업그레이드 구매, 황금 낙곱새: 각자 다른 소리
    /// - 씬의 모든 버튼: 딸깍 (구매 줄·황금 낙곱새처럼 자기 소리가 있는 버튼은 뺀다)
    /// - 배경음악: 클리커 화면과 구경하기에서 다른 곡을 천천히 바꿔 튼다. 곡이 비어 있으면 그냥 조용하다.
    /// 소리 파일·세기는 SoundSet 에셋과 이 Inspector에서 바꾼다.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private ExploreModeController explore;

        [Header("소리 내는 곳")]
        [Tooltip("한 번씩 나는 소리용. 여러 개를 돌려 써서 겹쳐도 앞 소리가 끊기지 않는다")]
        [SerializeField] private AudioSource[] voices;

        [Header("효과음")]
        [SerializeField] private SoundSet potClick;
        [SerializeField] private SoundSet buildingBought;
        [SerializeField] private SoundSet upgradeBought;
        [SerializeField] private SoundSet goldenClaimed;
        [SerializeField] private SoundSet buttonClick;
        [SerializeField] private SoundSet exploreEnter;
        [SerializeField] private SoundSet exploreExit;
        [SerializeField, Range(0f, 1f)] private float potClickVolume = 0.5f;
        [Tooltip("냄비 소리 사이 최소 간격(초). 빠르게 연타해도 소리가 겹겹이 쌓이지 않는다")]
        [SerializeField] private float potClickMinInterval = 0.06f;
        [SerializeField, Range(0f, 1f)] private float buttonVolume = 0.6f;

        [Header("배경음악")]
        [SerializeField] private AudioSource clickerMusic;
        [SerializeField] private AudioSource exploreMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
        [Tooltip("곡이 바뀔 때 몇 초에 걸쳐 바꾸는지")]
        [SerializeField] private float musicFadeTime = 1.2f;

        private int nextVoice;
        private float lastPotClick = -1f;
        private bool exploring;
        private readonly List<Button> hookedButtons = new List<Button>();

        private void OnEnable()
        {
            game.CookieClicked += OnCookieClicked;
            game.BuildingPurchased += OnBuildingPurchased;
            game.UpgradePurchased += OnUpgradePurchased;
            game.GoldenCookieClaimed += OnGoldenClaimed;
            if (explore != null) explore.ModeChanged += OnModeChanged;
        }

        private void OnDisable()
        {
            game.CookieClicked -= OnCookieClicked;
            game.BuildingPurchased -= OnBuildingPurchased;
            game.UpgradePurchased -= OnUpgradePurchased;
            game.GoldenCookieClaimed -= OnGoldenClaimed;
            if (explore != null) explore.ModeChanged -= OnModeChanged;
        }

        private void Start()
        {
            HookButtons();
            exploring = explore != null && explore.IsExploring;
            StartMusic(clickerMusic, exploring ? 0f : musicVolume);
            StartMusic(exploreMusic, exploring ? musicVolume : 0f);
        }

        private void OnDestroy()
        {
            foreach (var button in hookedButtons)
                if (button != null) button.onClick.RemoveListener(OnButtonClicked);
        }

        private void Update()
        {
            // 두 곡을 서로 엇갈려 키우고 줄인다 (크로스페이드).
            float step = musicFadeTime > 0f ? musicVolume * Time.unscaledDeltaTime / musicFadeTime : musicVolume;
            FadeMusic(clickerMusic, exploring ? 0f : musicVolume, step);
            FadeMusic(exploreMusic, exploring ? musicVolume : 0f, step);
        }

        // ------------------------------------------------------------------ 효과음

        private void OnCookieClicked(double _)
        {
            float now = Time.unscaledTime;
            if (now - lastPotClick < potClickMinInterval) return;
            lastPotClick = now;
            Play(potClick, potClickVolume);
        }

        private void OnBuildingPurchased(int _) => Play(buildingBought, 0.5f);
        private void OnUpgradePurchased(int _) => Play(upgradeBought, 0.5f);
        private void OnGoldenClaimed(GoldenCookieResult _) => Play(goldenClaimed, 1f);
        private void OnButtonClicked() => Play(buttonClick, buttonVolume);

        private void OnModeChanged(bool nowExploring)
        {
            if (nowExploring == exploring) return;
            exploring = nowExploring;
            Play(nowExploring ? exploreEnter : exploreExit, 0.4f);
        }

        /// <summary>
        /// 씬의 버튼에 딸깍 소리를 단다 (꺼져 있는 창의 버튼 포함).
        /// 구매 줄과 황금 낙곱새는 구매 성공 소리가 따로 나므로 뺀다.
        /// </summary>
        private void HookButtons()
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.GetComponentInParent<BuildingRowView>(true) != null) continue;
                if (button.GetComponentInParent<UpgradeRowView>(true) != null) continue;
                if (button.GetComponentInParent<GoldenCookieView>(true) != null) continue;
                button.onClick.AddListener(OnButtonClicked);
                hookedButtons.Add(button);
            }
        }

        private void Play(SoundSet set, float volumeScale)
        {
            if (set == null || voices == null || voices.Length == 0) return;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            set.Play(voice, volumeScale);
        }

        // ------------------------------------------------------------------ 배경음악

        private static void StartMusic(AudioSource source, float volume)
        {
            if (source == null || source.clip == null) return;
            source.loop = true;
            source.volume = volume;
            source.Play();
        }

        private static void FadeMusic(AudioSource source, float target, float step)
        {
            if (source == null || source.clip == null) return;
            source.volume = Mathf.MoveTowards(source.volume, target, step);
        }
    }
}
