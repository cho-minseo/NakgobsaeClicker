using CookieClicker.Core;
using CookieClicker.FX;
using CookieClicker.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 클릭·황금 쿠키 같은 "결과"를 이벤트로 받아 +N 글자, 부스러기, 안내 문구를 띄운다.
    /// 게임 로직은 이 클래스의 존재를 모르므로 연출을 빼거나 바꿔도 로직에 영향이 없다.
    /// </summary>
    public class ClickFeedbackView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private CookieClickTarget cookie;
        [SerializeField] private RectTransform effectLayer;
        [SerializeField] private UIParticle floatingTextPrefab;
        [SerializeField] private UIParticle crumbPrefab;
        [SerializeField, Min(0)] private int crumbsPerClick = 4;

        [Header("안내 문구")]
        [SerializeField] private Text popupText;
        [SerializeField] private float popupDuration = 2.5f;

        private float popupTimer;

        private void OnEnable()
        {
            cookie.Clicked += OnCookieClicked;
            game.GoldenCookieClaimed += OnGoldenCookieClaimed;
        }

        private void OnDisable()
        {
            cookie.Clicked -= OnCookieClicked;
            game.GoldenCookieClaimed -= OnGoldenCookieClaimed;
        }

        private void Start() => popupText.text = string.Empty;

        private void Update()
        {
            if (popupTimer <= 0f) return;
            popupTimer -= Time.deltaTime;

            var c = popupText.color;
            c.a = Mathf.Clamp01(popupTimer / 0.5f);
            popupText.color = c;
            popupText.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, popupTimer - popupDuration + 0.2f));
        }

        private void OnCookieClicked(double amount, Vector2 screenPosition)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(effectLayer, screenPosition, null, out var local))
                return;

            var label = Instantiate(floatingTextPrefab, effectLayer);
            if (label.Graphic is Text text) text.text = "+" + NumberFormatter.Format(amount, true);
            label.Launch(local + new Vector2(0f, 20f), new Vector2(Random.Range(-25f, 25f), 140f), 0f, 0f, 1f);

            for (int i = 0; i < crumbsPerClick; i++)
            {
                var crumb = Instantiate(crumbPrefab, effectLayer);
                Vector2 direction = Quaternion.Euler(0f, 0f, Random.Range(-60f, 60f)) * Vector2.up;
                crumb.Launch(local, direction * Random.Range(250f, 450f), 1400f, Random.Range(-360f, 360f), 0.8f);
            }
        }

        private void OnGoldenCookieClaimed(GoldenCookieResult result)
        {
            popupText.text = result.Effect == GoldenCookieEffect.Lucky
                ? $"럭키! 쿠키 +{NumberFormatter.Format(result.Cookies)}"
                : $"프렌지! {result.Duration:0}초 동안 생산량 x{game.Config.FrenzyMultiplier:0}";
            popupTimer = popupDuration;
        }
    }
}
