using System;
using CookieClicker.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 씬에 놓인 3D 쿠키. 클릭을 CookieGame에 전달하고, 눌림·흔들림 연출을 담당한다.
    /// 카메라의 PhysicsRaycaster가 이 오브젝트의 Collider를 맞히면 EventSystem이 OnPointerDown을 호출한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CookieClickTarget : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Camera viewCamera;

        [Header("화면 배치 (끄면 씬에 놓인 위치·크기를 그대로 사용)")]
        [SerializeField] private bool fitToScreen = true;
        [Tooltip("화면 비율 좌표 (0,0 = 왼쪽 아래, 1,1 = 오른쪽 위)")]
        [SerializeField] private Vector2 viewportPosition = new Vector2(0.17f, 0.47f);
        [SerializeField] private float distanceFromCamera = 8f;
        [SerializeField, Range(0.1f, 1f)] private float heightRatio = 0.42f;
        [SerializeField, Range(0.1f, 1f)] private float maxWidthRatio = 0.28f;

        [Header("연출")]
        [SerializeField] private float hoverScale = 1.06f;
        [Tooltip("클릭하면 납작하게 눌렸다가(스쿼시) 위로 늘어나며(스트레치) 튕겨 돌아온다. 눌리는 정도")]
        [SerializeField, Range(0f, 0.5f)] private float squashAmount = 0.2f;
        [Tooltip("스프링 세기. 클수록 빨리 튕긴다")]
        [SerializeField] private float squashStiffness = 260f;
        [Tooltip("스프링 감쇠. 작을수록 여러 번 출렁인다")]
        [SerializeField] private float squashDamping = 11f;
        [SerializeField] private float scaleSmoothTime = 0.08f;
        [SerializeField] private Vector2 wobbleAngles = new Vector2(6f, 14f);
        [SerializeField] private float wobbleSpeed = 0.5f;
        [SerializeField] private float pointerTilt = 10f;

        /// <summary>클릭으로 얻은 쿠키 수와 화면 좌표. 이펙트 쪽에서 구독한다.</summary>
        public event Action<double, Vector2> Clicked;

        /// <summary>true면 대체 키로 누르지 않는다 (타이틀·설정 창이 떠 있을 때 TitleScreenView가 켠다).</summary>
        public static bool KeyClickBlocked { get; set; }

        // 설정의 "클릭 대체 키". 마우스 왼쪽 클릭과 똑같이 냄비를 누른다.
        private InputAction clickKeyAction;

        private Quaternion baseRotation;
        private Vector3 baseScale;
        private float scaleFactor = 1f;
        private float scaleVelocity;
        private bool hovered;
        private float squash;
        private float squashVelocity;

        private void Awake()
        {
            baseRotation = transform.localRotation;
            baseScale = transform.localScale;
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void OnEnable()
        {
            GameSettings.Changed += RebindClickKey;
            RebindClickKey();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= RebindClickKey;
            clickKeyAction?.Dispose();
            clickKeyAction = null;
        }

        /// <summary>설정에서 키가 바뀌면 액션을 새 키로 다시 만든다 (같은 키면 그대로 둔다).</summary>
        private void RebindClickKey()
        {
            string path = GameSettings.ClickKey;
            if (clickKeyAction != null && clickKeyAction.bindings[0].path == path) return;
            clickKeyAction?.Dispose();
            clickKeyAction = new InputAction("ClickPot", InputActionType.Button, path);
            clickKeyAction.Enable();
        }

        private void Update()
        {
            if (clickKeyAction == null || KeyClickBlocked || !clickKeyAction.WasPressedThisFrame()) return;
            // 키로 누를 때는 마우스 위치 대신 냄비 위치(조금씩 흩어지게)에서 연출이 나온다.
            Vector2 screen = viewCamera.WorldToScreenPoint(transform.position);
            screen += UnityEngine.Random.insideUnitCircle * (Screen.height * 0.06f);
            Press(screen);
        }

        private void LateUpdate()
        {
            Vector3 size = fitToScreen ? FitToScreen() : baseScale;

            float target = hovered ? hoverScale : 1f;
            scaleFactor = Mathf.SmoothDamp(scaleFactor, target, ref scaleVelocity, scaleSmoothTime);
            // 감쇠 스프링: 눌린 값(squash)이 0으로 돌아가며 반대로 한 번 넘쳤다가(stretch) 멈춘다.
            float dt = Time.deltaTime;
            squashVelocity += (-squashStiffness * squash - squashDamping * squashVelocity) * dt;
            squash += squashVelocity * dt;
            // 부피가 대충 유지되도록 세로로 줄면 가로·깊이는 늘어난다.
            var squashScale = new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f);
            transform.localScale = Vector3.Scale(size * scaleFactor, squashScale);

            transform.localRotation = baseRotation * CalculateWobble();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press(eventData.position);
        }

        private void Press(Vector2 screenPosition)
        {
            squash = -squashAmount;
            squashVelocity = 0f;
            double amount = game.ClickCookie();
            Clicked?.Invoke(amount, screenPosition);
        }

        /// <summary>가로/세로 전환 시 ResponsiveLayout이 호출한다.</summary>
        public void SetScreenLayout(Vector2 viewport, float newHeightRatio, float newMaxWidthRatio)
        {
            viewportPosition = viewport;
            heightRatio = newHeightRatio;
            maxWidthRatio = newMaxWidthRatio;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        /// <summary>해상도가 바뀌어도 쿠키가 화면 왼쪽 영역에 같은 비율로 보이도록 위치와 크기를 맞춘다.</summary>
        private Vector3 FitToScreen()
        {
            float visibleHeight = 2f * distanceFromCamera * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * viewCamera.aspect;
            float size = Mathf.Min(visibleHeight * heightRatio, visibleWidth * maxWidthRatio);

            transform.position = viewCamera.ViewportToWorldPoint(
                new Vector3(viewportPosition.x, viewportPosition.y, distanceFromCamera));
            return Vector3.one * size;
        }

        private Quaternion CalculateWobble()
        {
            float t = Time.time * wobbleSpeed;
            float pitch = Mathf.Sin(t * 1.3f) * wobbleAngles.x;
            float yaw = Mathf.Sin(t) * wobbleAngles.y;

            var pointer = Pointer.current;
            if (pointer != null && viewCamera != null)
            {
                Vector2 cookieOnScreen = viewCamera.WorldToScreenPoint(transform.position);
                Vector2 offset = (pointer.position.ReadValue() - cookieOnScreen) / Mathf.Max(1, Screen.height);
                pitch += Mathf.Clamp(offset.y, -1f, 1f) * pointerTilt;
                yaw -= Mathf.Clamp(offset.x, -1f, 1f) * pointerTilt;
            }
            return Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
