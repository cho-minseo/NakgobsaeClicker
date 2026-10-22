using UnityEngine;
using UnityEngine.InputSystem;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 1인칭 ↔ 3인칭 시점 전환 (V). 카메라는 시점 회전축(CameraPivot)의 자식이라, 3인칭에서는 그 축 뒤로 물러나기만 하면
    /// 마우스로 캐릭터 주위를 도는 카메라가 된다. 뒤에 벽이 있으면 그 앞까지 당겨서 벽 속을 비추지 않는다.
    /// 1인칭 손(viewmodel)과 3인칭 캐릭터 중 하나만 보이게 한다.
    /// </summary>
    // 플레이어가 움직이고 시점을 돌린 뒤(Update)에 카메라 위치를 정한다.
    public class CameraModeController : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerAvatar avatar;
        [Tooltip("1인칭에서만 보이는 손")]
        [SerializeField] private GameObject viewmodel;
        [SerializeField] private InputAction toggleAction = new InputAction("ToggleView", InputActionType.Button, "<Keyboard>/v");

        [Header("3인칭")]
        [SerializeField] private float distance = 4f;
        [Tooltip("어깨 너머로 보도록 옆·위로 비켜 서는 정도 (CameraPivot 기준)")]
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.35f, 0.25f, 0f);
        [Tooltip("벽에 닿았는지 볼 때 카메라를 공 모양으로 본 반지름")]
        [SerializeField] private float collisionRadius = 0.25f;
        [Tooltip("물러나고 다가오는 빠르기")]
        [SerializeField] private float followSpeed = 10f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private float currentDistance;
        private bool shownThird;

        public bool IsThirdPerson { get; private set; }
        /// <summary>true면 V와 상관없이 3인칭 (앉아 있는 모습을 보여 줄 때)</summary>
        public bool ForceThirdPerson { get; set; }
        /// <summary>벽 검사에서 뺄 물체 (앉아 있는 의자 등받이에 카메라가 걸리지 않게)</summary>
        public Transform IgnoreRoot { get; set; }

        private bool ThirdActive => IsThirdPerson || ForceThirdPerson;

        private void OnEnable()
        {
            toggleAction.Enable();
            Apply(true);
        }

        private void OnDisable() => toggleAction.Disable();

        private void Update()
        {
            if (toggleAction.WasPressedThisFrame() && player.enabled) IsThirdPerson = !IsThirdPerson;
        }

        private void LateUpdate() => Apply(false);

        private void Apply(bool instant)
        {
            bool third = ThirdActive;
            if (third != shownThird || instant)
            {
                shownThird = third;
                viewmodel.SetActive(!third);
                avatar.SetVisible(third);
                if (third && instant) currentDistance = distance;
            }

            float target = third ? FreeDistance() : 0f;
            currentDistance = instant ? target : Mathf.Lerp(currentDistance, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            // 벽이 가까우면 바로 당긴다 (천천히 따라가면 그동안 벽 속이 보임).
            if (currentDistance > target) currentDistance = target;

            float blend = distance > 0f ? currentDistance / distance : 0f;
            cameraTransform.localPosition = shoulderOffset * blend + Vector3.back * currentDistance;
        }

        /// <summary>카메라를 뒤로 뺄 수 있는 거리 (벽·물건에 막히면 그 앞까지).</summary>
        private float FreeDistance()
        {
            var pivot = player.CameraPivot;
            Vector3 origin = pivot.TransformPoint(shoulderOffset);
            Vector3 direction = -pivot.forward;
            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float free = distance;
            for (int i = 0; i < count; i++)
            {
                var other = hits[i].collider.transform;
                if (other.IsChildOf(player.transform)) continue;
                if (IgnoreRoot != null && other.IsChildOf(IgnoreRoot)) continue;
                // 시작부터 겹쳐 있으면 distance가 0으로 나온다 (그 물체 안에서 시작) — 무시.
                if (hits[i].distance <= 0f) continue;
                free = Mathf.Min(free, hits[i].distance);
            }
            return Mathf.Max(0.3f, free - 0.05f);
        }
    }
}
