using CookieClicker.Core;
using CookieClicker.Gameplay;
using UnityEngine;

namespace CookieClicker.FX
{
    /// <summary>
    /// 전골냄비 파티클 연출. 파티클 시스템은 씬(StewPot/PotFX 아래)에 미리 만들어 두고, 이 스크립트는 "언제·얼마나"만 정한다.
    /// - 클릭: 재료(낙지·곱창·새우·대파)가 튀어 오르고 반짝임이 터진다.
    /// - 상시: 국물이 보글보글 끓고 김이 오른다. 초당 생산량이 높을수록 더 세게 끓는다.
    /// 모양·색·수명 같은 겉모습은 각 Particle System의 Inspector에서 바로 고치면 된다.
    /// </summary>
    public class StewPotFX : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private CookieClickTarget pot;

        [Header("클릭")]
        [SerializeField] private ParticleSystem ingredientBurst;
        [SerializeField] private ParticleSystem sparkleBurst;
        [SerializeField, Min(0)] private int ingredientsPerClick = 6;
        [SerializeField, Min(0)] private int sparklesPerClick = 8;

        [Header("끓기 (초당 생산량에 따라)")]
        [SerializeField] private ParticleSystem bubbles;
        [SerializeField] private ParticleSystem steam;
        [Tooltip("초당 생산량이 0일 때 / 최대일 때 초당 거품 수")]
        [SerializeField] private Vector2 bubbleRate = new Vector2(12f, 36f);
        [SerializeField] private Vector2 steamRate = new Vector2(1.5f, 12f);
        [Tooltip("초당 생산량이 10^이 값에 도달하면 최대로 끓는다")]
        [SerializeField] private float maxBoilLog10 = 6f;
        [Tooltip("갑자기 생산량이 바뀌어도 부드럽게 따라가는 시간(초)")]
        [SerializeField] private float boilSmoothTime = 0.6f;

        private float boil;
        private float boilVelocity;

        /// <summary>0 = 약불, 1 = 최대로 끓음. 다른 연출(소리 등)이 같이 쓸 수 있게 공개한다.</summary>
        public float Boil => boil;

        private void OnEnable()
        {
            pot.Clicked += OnPotClicked;
            // Inspector에서 Play On Awake가 꺼져도 상시 연출(거품·김)은 항상 돌게 한다.
            if (bubbles != null && !bubbles.isPlaying) bubbles.Play();
            if (steam != null && !steam.isPlaying) steam.Play();
        }

        private void OnDisable() => pot.Clicked -= OnPotClicked;

        private void Update()
        {
            // 생산량은 몇 자리씩 커지므로 로그로 0~1에 맞춘다.
            float target = Mathf.Clamp01(Mathf.Log10((float)game.CookiesPerSecond + 1f) / maxBoilLog10);
            boil = Mathf.SmoothDamp(boil, target, ref boilVelocity, boilSmoothTime);

            SetRate(bubbles, Mathf.Lerp(bubbleRate.x, bubbleRate.y, boil));
            SetRate(steam, Mathf.Lerp(steamRate.x, steamRate.y, boil));
        }

        private void OnPotClicked(double amount, Vector2 screenPosition)
        {
            // Emit(n)은 이미 만들어 둔 파티클 버퍼를 쓰므로 클릭마다 오브젝트가 생기지 않는다.
            if (ingredientBurst != null) ingredientBurst.Emit(ingredientsPerClick);
            if (sparkleBurst != null) sparkleBurst.Emit(sparklesPerClick);
        }

        private static void SetRate(ParticleSystem system, float rate)
        {
            if (system == null) return;
            var emission = system.emission;
            emission.rateOverTime = rate;
        }
    }
}
