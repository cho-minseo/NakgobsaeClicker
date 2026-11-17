#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;
using UnityEngine.InputSystem;

namespace CookieClicker.Core
{
    /// <summary>
    /// 웹 브라우저의 마우스 잠금 상태 (Plugins/WebGL/WebPointerLock.jslib). 웹이 아니면 Unity의 상태를 그대로 쓴다.
    /// </summary>
    public static class WebPointerLock
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int CookieWeb_IsPointerLocked();
        [DllImport("__Internal")] private static extern int CookieWeb_ConsumeEscUnlock();
        public static bool IsLocked => CookieWeb_IsPointerLocked() != 0;
        private static bool ReadEscUnlock() => CookieWeb_ConsumeEscUnlock() != 0;
#else
        public static bool IsLocked => Cursor.lockState == CursorLockMode.Locked;
        private static bool ReadEscUnlock() => false;
#endif

        private static int escFrame = -1;
        private static bool escThisFrame;

        /// <summary>
        /// 이번 프레임에 사용자가 Esc로 브라우저의 마우스 잠금을 풀었는지. 그 Esc는 게임에 키 입력으로 오지 않는다.
        /// 한 프레임 동안은 누가 몇 번 물어봐도 같은 값이다.
        /// </summary>
        public static bool EscUnlockedThisFrame
        {
            get
            {
                if (escFrame != Time.frameCount)
                {
                    escFrame = Time.frameCount;
                    escThisFrame = ReadEscUnlock();
                }
                return escThisFrame;
            }
        }

        /// <summary>
        /// Esc가 묶인 "뒤로/취소" 액션이 눌렸는지. 웹에서 마우스가 잠긴 동안 누른 Esc도 똑같이 친다.
        /// </summary>
        public static bool BackPressed(InputAction action) => action.WasPressedThisFrame() || EscUnlockedThisFrame;
    }
}
