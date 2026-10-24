using UnityEngine;

namespace CookieClicker.FX
{
    /// <summary>
    /// 물에 뜬 배처럼 위아래로 출렁이고(들썩임) 앞뒤·좌우로 기우뚱거린다.
    /// 세 움직임의 속도를 서로 어긋나게 해서 같은 동작이 반복돼 보이지 않게 한다. 배 길이 방향은 로컬 X축.
    /// </summary>
    public class BoatBob : MonoBehaviour
    {
        [Tooltip("위아래로 들썩이는 높이 (m)")]
        [SerializeField] private float heave = 0.12f;
        [SerializeField] private float heaveSpeed = 1.1f;
        [Tooltip("앞뒤로 기우는 각도 (도). 뱃머리가 들렸다 내려갔다 한다")]
        [SerializeField] private float pitch = 1.2f;
        [SerializeField] private float pitchSpeed = 0.8f;
        [Tooltip("좌우로 흔들리는 각도 (도)")]
        [SerializeField] private float roll = 1.5f;
        [SerializeField] private float rollSpeed = 0.6f;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float phase;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            // 배가 여러 척이어도 똑같이 움직이지 않게 위치로 시작 박자를 다르게 한다.
            phase = transform.position.x * 0.37f + transform.position.z * 0.61f;
        }

        private void Update()
        {
            float t = Time.time + phase;
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(t * heaveSpeed) * heave);
            transform.localRotation = baseRotation * Quaternion.Euler(
                Mathf.Sin(t * rollSpeed + 1.3f) * roll, 0f, Mathf.Sin(t * pitchSpeed + 0.4f) * pitch);
        }
    }
}
