using System;
using CookieClicker.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 클리커 화면 ↔ 1인칭 구경하기 전환. 두 모드에서 켤 오브젝트 목록만 들고 있다가 통째로 켜고 끈다.
    /// 오브젝트는 모두 씬에 미리 있고 활성 상태만 바뀌므로, 어느 모드든 Hierarchy에서 바로 편집할 수 있다.
    /// </summary>
    public class ExploreModeController : MonoBehaviour
    {
        [Tooltip("클리커 화면에서만 켜지는 것 (메인 카메라, 메인 UI, 배경 등)")]
        [SerializeField] private GameObject[] clickerOnly = Array.Empty<GameObject>();
        [Tooltip("구경하기에서만 켜지는 것 (플레이어, 구경하기 UI)")]
        [SerializeField] private GameObject[] exploreOnly = Array.Empty<GameObject>();

        [SerializeField] private Button enterButton;
        [SerializeField] private InputActionReference exitAction;

        [Header("시작 위치")]
        [SerializeField] private FirstPersonController player;
        [SerializeField] private Transform spawnPoint;
        [Tooltip("끄면 마지막으로 구경하던 위치에서 다시 시작")]
        [SerializeField] private bool returnToSpawnOnEnter = true;

        public bool IsExploring { get; private set; }
        /// <summary>true면 Esc로 구경하기를 나가지 않는다 (팝업이 열려 있어서 Esc가 팝업 닫기로 쓰일 때).</summary>
        public bool ExitBlocked { get; set; }
        public event Action<bool> ModeChanged;

        private void Start()
        {
            enterButton.onClick.AddListener(Enter);
            SetMode(false);
        }

        private void OnEnable() => exitAction.action.Enable();

        private void Update()
        {
            // 매 프레임 물어봐서, 웹에서 Esc로 마우스 잠금이 풀린 일을 다음 프레임까지 남겨 두지 않는다.
            bool back = WebPointerLock.BackPressed(exitAction.action);
            if (IsExploring && !ExitBlocked && back) Exit();
            KeepCursorLocked();
        }

        /// <summary>
        /// 웹에서는 창 밖을 누르거나 Alt+Tab·Esc를 하면 브라우저가 마우스 잠금을 풀어 버리는데, Unity는 계속 잠긴 줄 안다.
        /// 그러면 숨은 커서가 게임 밖으로 빠져나가 시점이 멈춘다. 잠금이 풀렸으면 커서를 보여 주고, 화면을 한 번 누르면 다시 잠근다.
        /// 팝업·꾸미기처럼 일부러 커서를 풀어 둔 동안(Cursor.lockState가 None)에는 건드리지 않는다.
        /// </summary>
        private void KeepCursorLocked()
        {
            if (!IsExploring || Cursor.lockState != CursorLockMode.Locked || WebPointerLock.IsLocked) return;
            Cursor.visible = true;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            // 한 번 풀었다가 다시 걸어야 Unity가 브라우저에 잠금을 새로 요청한다 (같은 값이면 무시함).
            Cursor.lockState = CursorLockMode.None;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Enter()
        {
            // 플레이어는 구경하기 전까지 꺼져 있어서 아직 초기화(Awake)가 안 된 상태다. 먼저 켠 뒤에 옮긴다.
            SetMode(true);
            if (returnToSpawnOnEnter) player.Teleport(spawnPoint.position, spawnPoint.rotation);
        }

        public void Exit() => SetMode(false);

        private void SetMode(bool exploring)
        {
            IsExploring = exploring;
            foreach (var go in clickerOnly) if (go != null) go.SetActive(!exploring);
            foreach (var go in exploreOnly) if (go != null) go.SetActive(exploring);

            // 구경할 때는 마우스를 화면 가운데 고정해서 시점 회전에 쓴다.
            Cursor.lockState = exploring ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !exploring;
            ModeChanged?.Invoke(exploring);
        }
    }
}
