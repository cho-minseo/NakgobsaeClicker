using UnityEngine;
using UnityEngine.InputSystem;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 1인칭 이동·시점. 키를 직접 읽지 않고 InputActionReference로 받아서,
    /// 키 배치를 바꾸거나 게임패드를 지원할 때 Input Actions 에셋만 수정하면 된다.
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

        [Header("이동")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8.5f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;

        [Header("시점")]
        [SerializeField] private float lookSensitivity = 0.1f;
        [SerializeField, Range(10f, 89f)] private float maxPitch = 85f;

        /// <summary>현재 수평 속도 / 걷기 속도. 손 흔들림 세기에 쓴다 (달리면 1보다 커짐).</summary>
        public float MoveRatio { get; private set; }
        /// <summary>이번 프레임의 시점 회전량. 손이 시점을 살짝 늦게 따라오는 연출에 쓴다.</summary>
        public Vector2 LookDelta { get; private set; }
        public bool IsGrounded => controller.isGrounded;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float pitch;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void OnEnable()
        {
            moveAction.action.Enable();
            lookAction.action.Enable();
            jumpAction.action.Enable();
            sprintAction.action.Enable();
        }

        private void Update()
        {
            Look();
            Move(Time.deltaTime);
        }

        private void Look()
        {
            LookDelta = lookAction.action.ReadValue<Vector2>() * lookSensitivity;
            transform.Rotate(0f, LookDelta.x, 0f);
            pitch = Mathf.Clamp(pitch - LookDelta.y, -maxPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Move(float dt)
        {
            Vector2 input = Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
            float speed = sprintAction.action.IsPressed() ? sprintSpeed : walkSpeed;
            Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
            // 바로 최고 속도가 되지 않고 부드럽게 가속·감속한다.
            planarVelocity = Vector3.MoveTowards(planarVelocity, target, acceleration * speed * dt);

            if (controller.isGrounded)
            {
                verticalVelocity = -2f;
                if (jumpAction.action.WasPressedThisFrame())
                    verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
            }
            verticalVelocity += gravity * dt;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            MoveRatio = controller.isGrounded ? planarVelocity.magnitude / walkSpeed : 0f;
        }

        /// <summary>구경하기 시작할 때 정해진 위치로 보낸다. CharacterController는 끈 상태에서 옮겨야 순간이동이 적용된다.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // 꺼진 상태에서 호출돼도 안전하도록 필요하면 여기서 찾는다.
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            pitch = 0f;
            cameraPivot.localRotation = Quaternion.identity;
        }
    }
}
