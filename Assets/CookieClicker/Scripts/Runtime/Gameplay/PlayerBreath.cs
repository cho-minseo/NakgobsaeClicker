using System;
using UnityEngine;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 산소 게이지 (Raft 방식). 머리가 물에 잠기면 줄고, 물 밖에서는 빠르게 다시 찬다.
    /// 바닥나면 잠시 잠수를 막고 수면으로 띄운다. 이동 코드와 분리해 두어서 규칙(시간, 벌칙)만 따로 조정할 수 있다.
    /// </summary>
    public class PlayerBreath : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [Tooltip("물속에서 버틸 수 있는 시간(초)")]
        [SerializeField] private float maxBreathSeconds = 30f;
        [Tooltip("물 밖에서 다시 차는 속도 (물속에서 주는 속도의 몇 배)")]
        [SerializeField] private float refillMultiplier = 4f;
        [Tooltip("숨이 막힌 뒤 다시 잠수할 수 있을 때까지 (산소가 이 비율 이상 찰 때까지)")]
        [SerializeField, Range(0f, 1f)] private float recoverRatio = 0.35f;

        /// <summary>0~1</summary>
        public float Breath01 { get; private set; } = 1f;
        public bool IsOutOfBreath { get; private set; }

        public event Action OutOfBreath;
        public event Action Recovered;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (player.IsHeadUnderwater)
                Breath01 = Mathf.Max(0f, Breath01 - dt / maxBreathSeconds);
            else
                Breath01 = Mathf.Min(1f, Breath01 + dt * refillMultiplier / maxBreathSeconds);

            if (!IsOutOfBreath && Breath01 <= 0f)
            {
                IsOutOfBreath = true;
                player.DiveLocked = true;
                OutOfBreath?.Invoke();
            }
            else if (IsOutOfBreath && !player.IsHeadUnderwater && Breath01 >= recoverRatio)
            {
                IsOutOfBreath = false;
                player.DiveLocked = false;
                Recovered?.Invoke();
            }
        }

        private void OnDisable()
        {
            // 구경하기를 나갔다 오면 숨이 다시 꽉 찬 상태로 시작한다.
            Breath01 = 1f;
            IsOutOfBreath = false;
            if (player != null) player.DiveLocked = false;
        }
    }
}
