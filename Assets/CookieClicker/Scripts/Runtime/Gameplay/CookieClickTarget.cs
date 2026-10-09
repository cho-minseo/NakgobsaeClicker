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
        [SerializeField] private float pressScale = 0.9f;
        [SerializeField] private float scaleSmoothTime = 0.08f;
        [SerializeField] private Vector2 wobbleAngles = new Vector2(6f, 14f);
        [SerializeField] private float wobbleSpeed = 0.5f;
        [SerializeField] private float pointerTilt = 10f;

        /// <summary>클릭으로 얻은 쿠키 수와 화면 좌표. 이펙트 쪽에서 구독한다.</summary>
        public event Action<double, Vector2> Clicked;

        private Quaternion baseRotation;
        private Vector3 baseScale;
        private float scaleFactor = 1f;
        private float scaleVelocity;
        private bool hovered;

        private void Awake()
        {
            baseRotation = transform.localRotation;
            baseScale = transform.localScale;
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void LateUpdate()
        {
            Vector3 size = fitToScreen ? FitToScreen() : baseScale;

            float target = hovered ? hoverScale : 1f;
            scaleFactor = Mathf.SmoothDamp(scaleFactor, target, ref scaleVelocity, scaleSmoothTime);
            transform.localScale = size * scaleFactor;

            transform.localRotation = baseRotation * CalculateWobble();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            scaleFactor = pressScale;
            double amount = game.ClickCookie();
            Clicked?.Invoke(amount, eventData.position);
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
