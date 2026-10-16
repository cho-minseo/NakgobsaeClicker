using CookieClicker.Gameplay;
using System.Collections;
using CookieClicker.FX;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 타이틀 화면. 게임 씬 위에 덮어서, 진짜 냄비가 화면 가운데에서 보글보글 끓는 모습을 배경으로 쓴다.
    /// - 로고·버튼 움직임은 Animator(TitleIntro → TitleIdle → TitleOutro 클립)가 맡는다. 이 스크립트는 "언제"만 정한다.
    /// - 시작하기: Animator에 Start 트리거 → 로고가 날아가는 동안 냄비가 게임 자리로 미끄러지고 게임 UI가 나타난다.
    /// - 끝나는 시점은 Outro 클립 끝의 애니메이션 이벤트(OnOutroFinished)로 받는다.
    /// </summary>
    public class TitleScreenView : MonoBehaviour
    {
        private static readonly int StartTrigger = Animator.StringToHash("Start");

        [SerializeField] private Animator animator;
        [Tooltip("타이틀이 떠 있는 동안 숨길 게임 UI")]
        [SerializeField] private CanvasGroup gameHud;
        [Tooltip("타이틀이 떠 있는 동안 꺼 둘 것 (예: 게임 화면 왼쪽에만 내리는 낙곱새 비)")]
        [SerializeField] private GameObject[] hideDuringTitle = System.Array.Empty<GameObject>();
        [SerializeField] private ResponsiveLayout layout;
        [SerializeField] private StewPotFX potFX;

        [Header("버튼")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private SettingsView settings;

        [Header("연출")]
        [Tooltip("끄면 타이틀 없이 바로 게임 화면 (개발 중 빠르게 테스트할 때)")]
        [SerializeField] private bool showOnLaunch = true;
        [Tooltip("타이틀에서 냄비가 놓일 화면 비율 좌표와 크기")]
        [SerializeField] private Vector2 potViewport = new Vector2(0.5f, 0.47f);
        [SerializeField, Range(0.1f, 1f)] private float potHeightRatio = 0.42f;
        [SerializeField, Range(0.1f, 1f)] private float potMaxWidthRatio = 0.6f;
        [Tooltip("타이틀에서는 생산량과 상관없이 이만큼 끓는다 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float titleBoil = 0.7f;
        [Tooltip("재료가 저절로 튀어 오르는 간격(초) 최소·최대")]
        [SerializeField] private Vector2 splashInterval = new Vector2(1.2f, 2.6f);
        [Tooltip("냄비가 게임 자리로 옮겨 가고 게임 UI가 나타나는 시간(초)")]
        [SerializeField] private float transitionDuration = 0.9f;

        private bool leaving;
        private bool outroFinished;
        private bool transitionFinished;
        private float splashTimer;

        private void Awake()
        {
            startButton.onClick.AddListener(StartGame);
            settingsButton.onClick.AddListener(settings.Open);
            quitButton.onClick.AddListener(Quit);
#if !(UNITY_STANDALONE || UNITY_EDITOR)
            // 휴대폰·웹에서는 앱을 직접 끄지 않는 게 관례라 종료 버튼을 숨긴다.
            quitButton.gameObject.SetActive(false);
#endif
        }

        private void Start()
        {
            if (!showOnLaunch)
            {
                CookieClickTarget.KeyClickBlocked = false;
                gameObject.SetActive(false);
                return;
            }

            SetHud(0f, false);
            SetHidden(false);
            CookieClickTarget.KeyClickBlocked = true; // 타이틀에서는 스페이스 등으로 냄비를 누르지 않는다
            layout.CookieLayoutOverridden = true;
            layout.PlaceCookie(potViewport, potHeightRatio, potMaxWidthRatio);
            potFX.MinBoil = titleBoil;
            splashTimer = 0.8f; // 로고가 떨어지고 나서 첫 재료가 튄다
        }

        private void Update()
        {
            if (leaving) return;

            // 화면 크기가 바뀌어도 냄비가 가운데에 있게 매 프레임 맞춘다.
            layout.PlaceCookie(potViewport, potHeightRatio, potMaxWidthRatio);

            splashTimer -= Time.deltaTime;
            if (splashTimer <= 0f)
            {
                splashTimer = Random.Range(splashInterval.x, splashInterval.y);
                potFX.Splash(Random.Range(2, 4), 4);
            }
        }

        private void StartGame()
        {
            if (leaving || settings.IsOpen) return;
            leaving = true;
            animator.SetTrigger(StartTrigger);
            StartCoroutine(MovePotToGame());
        }

        /// <summary>냄비를 타이틀 자리 → 게임 자리로 부드럽게 옮기면서 게임 UI를 서서히 보여 준다.</summary>
        private IEnumerator MovePotToGame()
        {
            potFX.Splash(6, 12); // 출발 신호처럼 한 번 크게
            for (float t = 0f; t < transitionDuration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / transitionDuration);
                var target = layout.CurrentCookieLayout;
                layout.PlaceCookie(
                    Vector2.Lerp(potViewport, target.viewportPosition, k),
                    Mathf.Lerp(potHeightRatio, target.heightRatio, k),
                    Mathf.Lerp(potMaxWidthRatio, target.maxWidthRatio, k));
                SetHud(k, false);
                potFX.MinBoil = Mathf.Lerp(titleBoil, 0f, k);
                yield return null;
            }

            layout.CookieLayoutOverridden = false;
            layout.Apply(layout.IsPortrait);
            potFX.MinBoil = 0f;
            SetHud(1f, true);
            SetHidden(true);
            CookieClickTarget.KeyClickBlocked = false;
            transitionFinished = true;
            TryClose();
        }

        /// <summary>TitleOutro 클립 마지막 프레임의 애니메이션 이벤트가 부른다.</summary>
        public void OnOutroFinished()
        {
            outroFinished = true;
            TryClose();
        }

        private void TryClose()
        {
            // 로고 퇴장 애니메이션과 냄비 이동이 둘 다 끝나야 타이틀을 끈다 (먼저 끄면 남은 코루틴이 멈춘다).
            if (outroFinished && transitionFinished) gameObject.SetActive(false);
        }

        private void SetHud(float alpha, bool interactable)
        {
            if (gameHud == null) return;
            gameHud.alpha = alpha;
            gameHud.interactable = interactable;
            gameHud.blocksRaycasts = interactable;
        }

        private void SetHidden(bool visible)
        {
            foreach (var go in hideDuringTitle) if (go != null) go.SetActive(visible);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
