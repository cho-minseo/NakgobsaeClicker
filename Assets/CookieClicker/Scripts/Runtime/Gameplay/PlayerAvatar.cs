using UnityEngine;
using UnityEngine.Rendering;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 3인칭에서 보이는 캐릭터. 이동 상태를 Animator 파라미터로 넘기기만 하고, 어떤 동작을 언제 틀지는
    /// Animator Controller(상태 머신)가 정한다. 캐릭터는 움직이는 방향을 부드럽게 바라본다.
    /// 1인칭에서는 몸을 그리지 않고 그림자만 남겨서, 발밑에 내 그림자가 보인다.
    /// </summary>
    public class PlayerAvatar : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AirTimeId = Animator.StringToHash("AirTime");
        private static readonly int SittingId = Animator.StringToHash("Sitting");
        private static readonly int JumpId = Animator.StringToHash("Jump");

        [SerializeField] private FirstPersonController player;
        [SerializeField] private Animator animator;
        [Tooltip("방향을 돌릴 캐릭터 기준점 (플레이어 루트의 자식)")]
        [SerializeField] private Transform facing;
        [Tooltip("가는 방향으로 몸을 돌리는 빠르기")]
        [SerializeField] private float turnSpeed = 12f;
        [Tooltip("Speed 파라미터를 부드럽게 바꾸는 시간 (초)")]
        [SerializeField] private float speedDamping = 0.1f;

        [Header("앉기")]
        [Tooltip("앉았을 때 플레이어 루트를 의자 앉는 면에서 얼마나 옮길지. Kenney 앉기 동작은 엉덩이가 루트 높이에 오므로 높이는 0")]
        [SerializeField] private Vector3 sitOffset = new Vector3(0f, 0f, 0.05f);

        private Renderer[] renderers;
        private float airTime;

        public bool Sitting { get; set; }
        public Vector3 SitOffset => sitOffset;

        private void Awake() => renderers = GetComponentsInChildren<Renderer>(true);

        private void OnEnable() => player.Jumped += OnJumped;
        private void OnDisable() => player.Jumped -= OnJumped;

        private void OnJumped() => animator.SetTrigger(JumpId);

        /// <summary>true면 몸이 보이고, false면 그림자만 드리운다 (1인칭).</summary>
        public void SetVisible(bool visible)
        {
            // 카메라 쪽(부모)의 OnEnable이 이 오브젝트의 Awake보다 먼저 불릴 수 있다.
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
            var mode = visible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            foreach (var r in renderers) r.shadowCastingMode = mode;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool grounded = player.IsGrounded || player.IsSwimming || player.MovementLocked;
            airTime = grounded ? 0f : airTime + dt;

            animator.SetFloat(SpeedId, player.MoveRatio, speedDamping, dt);
            // 계단·경사에서 땅 판정이 잠깐 끊기는 건 무시하도록, 공중에 있던 시간을 넘긴다 (Controller가 기준 시간을 정함).
            animator.SetFloat(AirTimeId, airTime);
            animator.SetBool(SittingId, Sitting);

            // 몸은 가는 방향을 본다. 서 있을 때는 시점을 돌려도 몸은 그대로 (월드 기준으로 유지), 앉으면 의자 정면.
            var velocity = player.PlanarVelocity;
            velocity.y = 0f;
            Quaternion target = facing.rotation;
            if (Sitting) target = player.transform.rotation;
            else if (velocity.sqrMagnitude > 0.04f) target = Quaternion.LookRotation(velocity);
            facing.rotation = Quaternion.Slerp(facing.rotation, target, 1f - Mathf.Exp(-turnSpeed * dt));
        }
    }
}
