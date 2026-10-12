using System;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.FX
{
    /// <summary>
    /// UI 위에서 날아가다 사라지는 단발성 이펙트 (+1 글자, 쿠키 부스러기, 쿠키 비 등).
    /// 프리팹에 붙여 두고 Launch로 움직임만 다르게 주면 하나의 스크립트로 여러 연출을 만든다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIParticle : MonoBehaviour
    {
        [SerializeField] private Graphic graphic;
        [Tooltip("수명 중 이 비율이 지나면 투명해지기 시작")]
        [SerializeField, Range(0f, 1f)] private float fadeStartRatio = 0.5f;

        public Graphic Graphic => graphic;

        /// <summary>
        /// 풀(UIParticlePool)에서 꺼낸 경우, 수명이 끝나면 Destroy 대신 이 콜백으로 반납된다.
        /// 클릭할 때마다 새로 만들고 지우면 GC가 쌓여 모바일에서 끊김이 생기므로 재사용한다.
        /// </summary>
        public Action<UIParticle> Finished { get; set; }

        private RectTransform rectTransform;
        private Vector2 velocity;
        private float gravity;
        private float angularVelocity;
        private float lifetime = 1f;
        private float age;
        private Color baseColor;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            if (graphic == null) graphic = GetComponent<Graphic>();
            baseColor = graphic != null ? graphic.color : Color.white;
        }

        public void Launch(Vector2 startPosition, Vector2 startVelocity, float gravityForce, float spin, float life)
        {
            rectTransform.anchoredPosition = startPosition;
            velocity = startVelocity;
            gravity = gravityForce;
            angularVelocity = spin;
            lifetime = Mathf.Max(0.01f, life);
            age = 0f;
            rectTransform.localRotation = Quaternion.identity;
            if (graphic != null) graphic.color = baseColor;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            velocity.y -= gravity * dt;
            rectTransform.anchoredPosition += velocity * dt;
            rectTransform.Rotate(0f, 0f, angularVelocity * dt);

            if (graphic != null)
            {
                float t = Mathf.InverseLerp(lifetime * fadeStartRatio, lifetime, age);
                var c = baseColor;
                c.a *= 1f - t;
                graphic.color = c;
            }

            if (age < lifetime) return;
            if (Finished != null) Finished(this);
            else Destroy(gameObject);
        }
    }
}
