using UnityEngine;
using UnityEngine.InputSystem;

namespace CookieClicker.Gameplay.Viewmodel
{
    /// <summary>
    /// 1인칭 손의 움직임을 키프레임 애니메이션 없이 코드로 만든다.
    /// - 걸을 때: 양손이 반대 박자로 앞뒤로 휘적거림
    /// - 시점 회전: 손이 살짝 늦게 따라옴 (무게감)
    /// - 가만히 있을 때: 숨 쉬듯 미세하게 위아래
    /// - 클릭: 오른손 주먹 쥐고 앞으로 툭
    /// - 달리기: 양손 주먹 쥐고 뛰기
    /// 손 모델을 바꿔도 움직임은 소켓(leftHand/rightHand)에 걸려 있어서 그대로 유지된다.
    /// </summary>
    public class ViewmodelAnimator : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private HandPoseSwitcher leftPose;
        [SerializeField] private HandPoseSwitcher rightPose;
        [SerializeField] private InputActionReference attackAction;

        [Header("걷기 휘적임")]
        [SerializeField] private float swingDistance = 0.07f;
        [SerializeField] private float swingBob = 0.025f;
        [SerializeField] private float swingRoll = 12f;
        [SerializeField] private float swingFrequency = 8f;

        [Header("시점 따라오기")]
        [SerializeField] private float swayAmount = 0.004f;
        [SerializeField] private float swayLimit = 0.05f;
        [SerializeField] private float swaySmooth = 10f;

        [Header("숨쉬기 / 주먹")]
        [SerializeField] private float breathAmount = 0.006f;
        [SerializeField] private float punchDistance = 0.12f;
        [SerializeField] private float punchRecover = 10f;
        [Tooltip("MoveRatio가 이 값을 넘으면 달리는 중으로 보고 양손 주먹을 쥔다")]
        [SerializeField] private float runFistThreshold = 1.2f;

        private Vector3 leftBasePosition;
        private Vector3 rightBasePosition;
        private Quaternion leftBaseRotation;
        private Quaternion rightBaseRotation;
        private float phase;
        private float swingWeight;
        private Vector3 sway;
        private float punch;

        private void Awake()
        {
            leftBasePosition = leftHand.localPosition;
            rightBasePosition = rightHand.localPosition;
            leftBaseRotation = leftHand.localRotation;
            rightBaseRotation = rightHand.localRotation;
        }

        private void OnEnable() => attackAction.action.Enable();

        private void Update()
        {
            float dt = Time.deltaTime;

            // 주먹: 누르는 동안 쥐고, 누른 순간 앞으로 툭 내민다.
            float moveRatio = player.MoveRatio;
            bool running = moveRatio > runFistThreshold;
            if (attackAction.action.WasPressedThisFrame()) punch = 1f;
            rightPose.SetPose(running || attackAction.action.IsPressed() ? HandPose.Fist : HandPose.Open);
            leftPose.SetPose(running ? HandPose.Fist : HandPose.Open);
            punch = Mathf.Lerp(punch, 0f, punchRecover * dt);

            // 걷는 속도만큼 박자가 빨라지고, 멈추면 휘적임이 서서히 줄어든다.
            swingWeight = Mathf.Lerp(swingWeight, Mathf.Clamp01(moveRatio), 8f * dt);
            phase += dt * swingFrequency * Mathf.Max(0.6f, moveRatio);

            Vector2 look = player.LookDelta;
            Vector3 swayTarget = Vector3.ClampMagnitude(new Vector3(-look.x, -look.y, 0f) * swayAmount, swayLimit);
            sway = Vector3.Lerp(sway, swayTarget, swaySmooth * dt);

            float breath = Mathf.Sin(Time.time * 1.6f) * breathAmount * (1f - swingWeight);
            Apply(leftHand, leftBasePosition, leftBaseRotation, 1f, breath, 0f);
            Apply(rightHand, rightBasePosition, rightBaseRotation, -1f, breath, punch);
        }

        /// <param name="side">왼손 1, 오른손 -1. 서로 반대 박자로 흔들리게 한다.</param>
        private void Apply(Transform hand, Vector3 basePosition, Quaternion baseRotation, float side, float breath, float punchAmount)
        {
            float s = Mathf.Sin(phase) * side;
            float bob = Mathf.Abs(Mathf.Cos(phase)) * swingBob;

            Vector3 swing = new Vector3(0f, bob, s * swingDistance) * swingWeight;
            Vector3 offset = swing + sway + Vector3.up * breath + Vector3.forward * (punchAmount * punchDistance);
            hand.localPosition = basePosition + offset;
            hand.localRotation = baseRotation * Quaternion.Euler(-s * swingRoll * swingWeight, 0f, s * swingRoll * 0.5f * swingWeight);
        }
    }
}
