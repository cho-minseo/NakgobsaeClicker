using UnityEngine;

namespace CookieClicker.FX
{
    /// <summary>
    /// UFO 빛줄기에 끌려 올라가는 소. 땅에서 천천히 떠올라 빙글빙글 돌며 다리를 버둥거리다가,
    /// 꼭대기에서 잠깐 멈춘 뒤 쿵 떨어지고(찌그러졌다 펴짐) 다시 올라가기를 반복한다.
    /// 높이는 이 오브젝트의 처음 위치(땅)부터 liftHeight까지, 속도는 cycle로 조정한다.
    /// </summary>
    public class BeamLift : MonoBehaviour
    {
        [SerializeField] private float liftHeight = 8f;
        [Tooltip("올라갔다 떨어지는 한 바퀴 시간 (초)")]
        [SerializeField] private float cycle = 9f;
        [Tooltip("올라가는 동안 도는 속도 (도/초)")]
        [SerializeField] private float spinSpeed = 70f;
        [Tooltip("버둥거리는 다리 (축이 다리 윗부분에 있어야 자연스럽다)")]
        [SerializeField] private Transform[] legs;
        [SerializeField] private float kickAngle = 35f;
        [SerializeField] private float kickSpeed = 14f;

        // 한 바퀴 안에서: 땅에서 멍하니(0~0.12) → 떠오름(~0.72) → 꼭대기에서 멈칫(~0.82) → 떨어짐(~0.9) → 착지 찌그러짐
        private const float RiseStart = 0.12f, RiseEnd = 0.72f, HangEnd = 0.82f, FallEnd = 0.9f;

        private Vector3 groundPosition;
        private Quaternion baseRotation;
        private Vector3 baseScale;
        private float spin;

        private void Awake()
        {
            groundPosition = transform.localPosition;
            baseRotation = transform.localRotation;
            baseScale = transform.localScale;
        }

        private void Update()
        {
            float t = Mathf.Repeat(Time.time / cycle, 1f);
            float height;
            bool airborne = t > RiseStart && t < FallEnd;
            if (t < RiseStart) height = 0f;
            else if (t < RiseEnd) height = Mathf.SmoothStep(0f, 1f, (t - RiseStart) / (RiseEnd - RiseStart));
            else if (t < HangEnd) height = 1f + Mathf.Sin((t - RiseEnd) / (HangEnd - RiseEnd) * Mathf.PI) * 0.05f;
            else if (t < FallEnd) { float f = (t - HangEnd) / (FallEnd - HangEnd); height = 1f - f * f; } // 중력처럼 점점 빨라진다
            else height = 0f;

            // 떠 있는 동안에만 돌고, 기울어진 채로 흔들린다.
            if (airborne) spin += spinSpeed * Time.deltaTime;
            float wobble = airborne ? Mathf.Sin(Time.time * 2.3f) * 12f : 0f;
            transform.localPosition = groundPosition + Vector3.up * (height * liftHeight);
            transform.localRotation = baseRotation * Quaternion.Euler(wobble, spin, wobble * 0.6f);

            // 착지 직후 납작하게 찌그러졌다가 튕기며 펴진다 (squash & stretch).
            float squash = 0f;
            if (t >= FallEnd) squash = Mathf.Sin((t - FallEnd) / (1f - FallEnd) * Mathf.PI * 3f) * Mathf.Exp(-(t - FallEnd) * 40f) * 0.35f;
            transform.localScale = new Vector3(baseScale.x * (1f + squash), baseScale.y * (1f - squash), baseScale.z * (1f + squash));

            if (legs == null) return;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float angle = airborne ? Mathf.Sin(Time.time * kickSpeed + i * 1.7f) * kickAngle : 0f;
                legs[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
        }
    }
}
