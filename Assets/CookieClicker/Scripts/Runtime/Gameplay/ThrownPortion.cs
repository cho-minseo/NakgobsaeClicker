using UnityEngine;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 손에서 놓은 1인분 낙곱새. 바닥(무엇이든)에 처음 닿은 뒤 정해진 시간이 지나면 작아지며 사라진다.
    /// 물에 빠져 계속 가라앉는 등 끝내 닿지 않으면 maxAirTime 뒤에 사라지기 시작한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ThrownPortion : MonoBehaviour
    {
        [Tooltip("작아지며 사라지는 시간(초)")]
        [SerializeField] private float shrinkDuration = 0.35f;
        [Tooltip("이 시간 동안 아무 데도 안 닿으면 그냥 사라지기 시작한다(초)")]
        [SerializeField] private float maxAirTime = 8f;

        private bool released;
        private bool landed;
        private float groundLifetime = 3f;
        private float timer;
        private float shrinkTimer = -1f;
        private Vector3 baseScale;

        public bool IsGone { get; private set; }

        /// <summary>손에서 놓을 때 부른다. 바닥에 닿고 groundLifetime초 뒤 사라진다.</summary>
        public void Release(float lifetimeOnGround)
        {
            released = true;
            groundLifetime = lifetimeOnGround;
            timer = 0f;
            baseScale = transform.localScale;
        }

        /// <summary>바닥에 떨어진 개수가 너무 많을 때 오래된 것부터 바로 치운다.</summary>
        public void Vanish()
        {
            if (shrinkTimer < 0f) shrinkTimer = 0f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!released || landed) return;
            landed = true;
            timer = 0f;
        }

        private void Update()
        {
            if (!released) return;
            timer += Time.deltaTime;
            if (!landed && timer >= maxAirTime)
            {
                landed = true;
                timer = groundLifetime;
            }
            if (landed && shrinkTimer < 0f && timer >= groundLifetime) shrinkTimer = 0f;
            if (shrinkTimer < 0f) return;

            shrinkTimer += Time.deltaTime;
            float t = shrinkTimer / shrinkDuration;
            transform.localScale = baseScale * Mathf.Clamp01(1f - t * t);
            if (t >= 1f)
            {
                IsGone = true;
                Destroy(gameObject);
            }
        }
    }
}
