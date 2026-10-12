using CookieClicker.Core;
using UnityEngine;

namespace CookieClicker.FX
{
    /// <summary>초당 생산량이 많을수록 배경에 쿠키가 더 많이 떨어진다 (원작의 쿠키 비 연출).</summary>
    public class CookieRain : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private UIParticle cookiePrefab;
        [SerializeField] private RectTransform area;
        [SerializeField] private float maxPerSecond = 12f;
        [SerializeField] private Vector2 fallSpeed = new Vector2(160f, 260f);
        [SerializeField] private Vector2 sizeRange = new Vector2(36f, 64f);

        private float spawnBudget;
        private UIParticlePool pool;

        private void Awake() => pool = new UIParticlePool(cookiePrefab, area, 16, 64);

        private void Update()
        {
            double cps = game.CookiesPerSecond;
            if (cps <= 0) return;

            float rate = Mathf.Min(maxPerSecond, Mathf.Log10((float)cps + 1f) * 2.5f);
            spawnBudget += rate * Time.deltaTime;
            while (spawnBudget >= 1f)
            {
                spawnBudget -= 1f;
                Spawn();
            }
        }

        private void Spawn()
        {
            Rect rect = area.rect;
            float speed = Random.Range(fallSpeed.x, fallSpeed.y);
            float size = Random.Range(sizeRange.x, sizeRange.y);

            var particle = pool.Get();
            ((RectTransform)particle.transform).sizeDelta = new Vector2(size, size);
            particle.Launch(
                new Vector2(Random.Range(rect.xMin, rect.xMax), rect.yMax + size),
                new Vector2(0f, -speed),
                0f,
                Random.Range(-90f, 90f),
                (rect.height + size * 2f) / speed);
        }
    }
}
