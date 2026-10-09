using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 가끔 화면에 나타나는 황금 쿠키. 오브젝트는 씬에 항상 있고 CanvasGroup으로 보였다 숨겼다만 한다.
    /// (매번 생성·삭제하지 않으니 인스펙터에서 모양을 바로 편집할 수 있다)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GoldenCookieView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform spawnArea;
        [Tooltip("spawnArea 안에서 나타날 수 있는 범위 (0~1 비율)")]
        [SerializeField] private Rect normalizedSpawnRect = new Rect(0.04f, 0.1f, 0.6f, 0.75f);
        [SerializeField] private float fadeDuration = 1f;

        public Rect SpawnRect
        {
            get => normalizedSpawnRect;
            set => normalizedSpawnRect = value;
        }

        private RectTransform rectTransform;
        private float waitTimer;
        private float visibleTimer;
        private bool visible;

        private void Awake() => rectTransform = (RectTransform)transform;

        private void Start()
        {
            button.onClick.AddListener(OnClicked);
            Hide();
        }

        private void Update()
        {
            if (!visible)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f) Show();
                return;
            }

            visibleTimer += Time.deltaTime;
            float lifetime = game.Config.GoldenLifetime;
            if (visibleTimer >= lifetime)
            {
                Hide();
                return;
            }

            float fadeIn = Mathf.Clamp01(visibleTimer / fadeDuration);
            float fadeOut = Mathf.Clamp01((lifetime - visibleTimer) / fadeDuration);
            canvasGroup.alpha = Mathf.Min(fadeIn, fadeOut);
            rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(visibleTimer * 4f) * 0.08f);
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(visibleTimer * 1.5f) * 12f);
        }

        private void Show()
        {
            Rect area = spawnArea.rect;
            float x = Mathf.Lerp(area.xMin, area.xMax, Random.Range(normalizedSpawnRect.xMin, normalizedSpawnRect.xMax));
            float y = Mathf.Lerp(area.yMin, area.yMax, Random.Range(normalizedSpawnRect.yMin, normalizedSpawnRect.yMax));
            rectTransform.anchoredPosition = new Vector2(x, y);

            visible = true;
            visibleTimer = 0f;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        private void Hide()
        {
            visible = false;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Vector2 interval = game.Config.GoldenSpawnInterval;
            waitTimer = Random.Range(interval.x, interval.y);
        }

        private void OnClicked()
        {
            if (!visible) return;
            game.ClaimGoldenCookie();
            Hide();
        }
    }
}
