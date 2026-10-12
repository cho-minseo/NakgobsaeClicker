using UnityEngine;

namespace CookieClicker.FX
{
    /// <summary>
    /// 자기 Y축을 기준으로 계속 돌면서 위아래로 둥실거린다. 자식으로 넣은 오브젝트는 이 점을 중심으로 공전한다.
    /// 공전 반지름은 자식의 위치로 정하므로 씬에서 자식을 끌어 옮기기만 하면 된다.
    /// </summary>
    public class Orbiter : MonoBehaviour
    {
        [Tooltip("초당 회전 각도 (음수면 반대 방향)")]
        [SerializeField] private float degreesPerSecond = 60f;
        [SerializeField] private float bobHeight = 0.3f;
        [SerializeField] private float bobSpeed = 2f;

        /// <summary>기본 회전 속도에 곱해지는 배율. 다른 컴포넌트(보유 개수 등)가 바꾼다.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public float DegreesPerSecond => degreesPerSecond;

        private Vector3 basePosition;

        private void Awake() => basePosition = transform.localPosition;

        private void Update()
        {
            transform.Rotate(0f, degreesPerSecond * SpeedMultiplier * Time.deltaTime, 0f, Space.Self);
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        }
    }
}
