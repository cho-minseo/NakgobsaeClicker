using System;
using CookieClicker.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 1인칭 이동·시점·수영. 키를 직접 읽지 않고 InputActionReference로 받아서,
    /// 키 배치를 바꾸거나 게임패드를 지원할 때 Input Actions 에셋만 수정하면 된다.
    /// 물 판정은 WaterVolume에 위치를 물어보는 방식이라 물을 어디에 두든 코드 수정이 필요 없다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;

        [Header("입력")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference sprintAction;
        [Tooltip("물속에서 아래로 잠수")]
        [SerializeField] private InputActionReference diveAction;

        [Header("이동")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8.5f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        [Tooltip("이 속도(m/s)보다 빨리 떨어져 땅에 닿으면 착지로 본다 (착지 소리 등)")]
        [SerializeField] private float minLandingSpeed = 3f;

        [Header("시점")]
        [SerializeField] private float lookSensitivity = 0.1f;
        [SerializeField, Range(10f, 89f)] private float maxPitch = 85f;

        [Header("수영")]
        [SerializeField] private float swimSpeed = 3.2f;
        [SerializeField] private float swimSprintSpeed = 5f;
        [SerializeField] private float swimVerticalSpeed = 2.8f;
        [Tooltip("발이 수면보다 이만큼 깊으면 수영 시작")]
        [SerializeField] private float swimStartDepth = 1.15f;
        [Tooltip("떠 있을 때 발이 수면 아래 이만큼에 머문다 (눈이 수면 위로 살짝 나옴)")]
        [SerializeField] private float floatDepth = 1.4f;
        [Tooltip("수면으로 떠오르려는 힘")]
        [SerializeField] private float buoyancy = 2.5f;
        [Tooltip("클수록 물속에서 빨리 멈춘다")]
        [SerializeField] private float waterDrag = 4f;

        /// <summary>현재 수평 속도 / 걷기 속도. 손 흔들림 세기에 쓴다 (달리면 1보다 커짐).</summary>
        public float MoveRatio { get; private set; }
        /// <summary>이번 프레임의 시점 회전량. 손이 시점을 살짝 늦게 따라오는 연출에 쓴다.</summary>
        public Vector2 LookDelta { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public bool IsSwimming { get; private set; }
        public bool IsHeadUnderwater { get; private set; }
        /// <summary>머리(카메라)가 수면 아래로 몇 m 들어가 있는지. 물 밖이면 0.</summary>
        public float HeadDepth { get; private set; }
        /// <summary>true면 잠수할 수 없고 수면으로 떠오른다 (산소 부족 등).</summary>
        public bool DiveLocked { get; set; }
        /// <summary>true면 시점만 돌릴 수 있고 움직이지 않는다 (의자에 앉아 있을 때).</summary>
        public bool MovementLocked { get; private set; }
        /// <summary>지금 움직이는 수평 속도 (월드 기준). 3인칭 캐릭터가 가는 방향을 바라보는 데 쓴다.</summary>
        public Vector3 PlanarVelocity => IsSwimming ? new Vector3(swimVelocity.x, 0f, swimVelocity.z) : planarVelocity;
        public Transform CameraPivot => cameraPivot;

        /// <summary>땅에서 점프한 순간. 소리·연출은 이 이벤트를 구독해서 이동 코드와 분리한다.</summary>
        public event Action Jumped;
        /// <summary>공중에서 땅에 닿은 순간. 인자는 닿을 때 떨어지던 속도(m/s).</summary>
        public event Action<float> Landed;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private Vector3 swimVelocity;
        private float pitch;
        /// <summary>앉아 있는 동안 몸 대신 카메라만 좌우로 돈 각도</summary>
        private float seatedYaw;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void OnEnable()
        {
            moveAction.action.Enable();
            lookAction.action.Enable();
            jumpAction.action.Enable();
            sprintAction.action.Enable();
            if (diveAction != null) diveAction.action.Enable();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Look();
            if (MovementLocked)
            {
                MoveRatio = 0f;
                return;
            }

            bool deepEnough = WaterVolume.TryGetSurface(transform.position, out float surface)
                              && surface - transform.position.y > swimStartDepth;
            if (deepEnough) Swim(dt, surface);
            else Move(dt);

            Vector3 head = cameraPivot.position;
            IsHeadUnderwater = WaterVolume.TryGetSurface(head, out float headSurface) && head.y < headSurface;
            HeadDepth = IsHeadUnderwater ? headSurface - head.y : 0f;
        }

        private void Look()
        {
            LookDelta = lookAction.action.ReadValue<Vector2>() * (lookSensitivity * GameSettings.LookSensitivity);
            // 앉아 있으면 몸은 의자 방향 그대로 두고 카메라만 돈다.
            if (MovementLocked) seatedYaw += LookDelta.x;
            else transform.Rotate(0f, LookDelta.x, 0f);
            pitch = Mathf.Clamp(pitch - LookDelta.y, -maxPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, seatedYaw, 0f);
        }

        private void Move(float dt)
        {
            if (IsSwimming)
            {
                // 물에서 나올 때 헤엄치던 수평 속도를 이어받는다.
                IsSwimming = false;
                planarVelocity = new Vector3(swimVelocity.x, 0f, swimVelocity.z);
                verticalVelocity = 0f;
            }

            Vector2 input = Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
            float speed = sprintAction.action.IsPressed() ? sprintSpeed : walkSpeed;
            Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
            // 바로 최고 속도가 되지 않고 부드럽게 가속·감속한다.
            planarVelocity = Vector3.MoveTowards(planarVelocity, target, acceleration * speed * dt);

            bool wasGrounded = controller.isGrounded;
            if (wasGrounded)
            {
                verticalVelocity = -2f;
                if (jumpAction.action.WasPressedThisFrame())
                {
                    verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                    Jumped?.Invoke();
                }
            }
            verticalVelocity += gravity * dt;

            float fallSpeed = -verticalVelocity;
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            MoveRatio = controller.isGrounded ? planarVelocity.magnitude / walkSpeed : 0f;
            // 경사를 내려갈 때 잠깐 떠도 착지로 치지 않도록 일정 속도 이상으로 떨어졌을 때만 알린다.
            if (!wasGrounded && controller.isGrounded && fallSpeed > minLandingSpeed) Landed?.Invoke(fallSpeed);
        }

        /// <summary>
        /// 바라보는 방향으로 헤엄친다 (아래를 보고 앞으로 가면 잠수). Space = 위, 잠수 키 = 아래.
        /// 아무것도 안 누르면 부력으로 수면에 떠오른다.
        /// </summary>
        private void Swim(float dt, float surface)
        {
            if (!IsSwimming)
            {
                IsSwimming = true;
                swimVelocity = planarVelocity + Vector3.up * Mathf.Max(verticalVelocity, -6f);
            }

            Vector2 input = Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
            float speed = sprintAction.action.IsPressed() ? swimSprintSpeed : swimSpeed;
            Vector3 direction = Vector3.ClampMagnitude(cameraPivot.forward * input.y + transform.right * input.x, 1f);
            Vector3 target = direction * speed;

            float vertical = 0f;
            if (jumpAction.action.IsPressed()) vertical += 1f;
            if (!DiveLocked && diveAction != null && diveAction.action.IsPressed()) vertical -= 1f;
            target.y += vertical * swimVerticalSpeed;

            float floatY = surface - floatDepth;
            bool diving = vertical < 0f || target.y < -0.5f;
            if (DiveLocked)
            {
                // 산소가 없으면 무조건 수면으로 빠르게 올라간다.
                target.y = Mathf.Max(target.y, 3.5f);
            }
            else if (!diving)
            {
                // 잠수 중이 아니면 수면 쪽으로 천천히 떠오른다.
                target.y += Mathf.Clamp((floatY - transform.position.y) * buoyancy, -1.5f, 2.5f);
            }

            // 물 밖으로 날아오르지 않도록 수면 높이에서 막는다 (해안에서는 바닥을 밟고 걸어 나간다).
            if (transform.position.y >= floatY && target.y > 0f) target.y = 0f;

            swimVelocity = Vector3.Lerp(swimVelocity, target, 1f - Mathf.Exp(-waterDrag * dt));
            controller.Move(swimVelocity * dt);
            MoveRatio = new Vector2(swimVelocity.x, swimVelocity.z).magnitude / walkSpeed;
        }

        /// <summary>
        /// 의자에 앉는다: 몸 충돌을 끄고 그 자리에 고정한다 (시점은 계속 돌릴 수 있다).
        /// 몸을 의자 쪽으로 돌리므로 시점도 정면을 보게 된다.
        /// </summary>
        public void SitAt(Vector3 position, Quaternion rotation)
        {
            MovementLocked = true;
            controller.enabled = false;
            // 앉는 순간 카메라가 휙 돌지 않도록, 보던 방향을 카메라 쪽 각도로 넘긴다.
            seatedYaw = Mathf.DeltaAngle(rotation.eulerAngles.y, transform.eulerAngles.y + seatedYaw);
            transform.SetPositionAndRotation(position, rotation);
            planarVelocity = Vector3.zero;
            swimVelocity = Vector3.zero;
            verticalVelocity = 0f;
            MoveRatio = 0f;
        }

        /// <summary>일어나서 position에 선다.</summary>
        public void StandUp(Vector3 position)
        {
            // 앉아서 보던 방향으로 몸을 돌려 선다.
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, transform.eulerAngles.y + seatedYaw, 0f));
            seatedYaw = 0f;
            controller.enabled = true;
            MovementLocked = false;
        }

        /// <summary>구경하기 시작할 때 정해진 위치로 보낸다. CharacterController는 끈 상태에서 옮겨야 순간이동이 적용된다.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // 꺼진 상태에서 호출돼도 안전하도록 필요하면 여기서 찾는다.
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            MovementLocked = false;
            seatedYaw = 0f;
            planarVelocity = Vector3.zero;
            swimVelocity = Vector3.zero;
            verticalVelocity = 0f;
            pitch = 0f;
            cameraPivot.localRotation = Quaternion.identity;
        }
    }
}
