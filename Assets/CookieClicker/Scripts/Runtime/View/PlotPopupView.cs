using CookieClicker.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 구경하기에서 건물을 보고 F를 누르면 뜨는 건물 팝업. 이름·개수·설명을 보여 주고,
    /// 건물에 조절할 수 있는 것(지금은 국자 공전 속도)이 있으면 그 설정 칸도 같이 보여 준다.
    /// 열려 있는 동안은 마우스를 풀고 플레이어 이동·시점을 멈춘다. F·Esc·닫기 버튼으로 닫는다.
    /// </summary>
    // Esc를 ExploreModeController보다 늦게 읽어야 "팝업 닫기"와 "구경하기 나가기"가 한 번에 일어나지 않는다.
    [DefaultExecutionOrder(100)]
    public class PlotPopupView : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private ExploreModeController exploreMode;
        [SerializeField] private float interactDistance = 15f;
        [SerializeField] private InputAction interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/f");
        [SerializeField] private InputActionReference closeAction;

        [Header("UI")]
        [Tooltip("건물을 보고 있을 때 뜨는 \"[F] 건물 보기\" 안내")]
        [SerializeField] private GameObject prompt;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private GameObject speedSection;
        [SerializeField] private Text speedText;
        [SerializeField] private Button baseSpeedButton;
        [SerializeField] private Text baseSpeedLabel;
        [SerializeField] private Button countSpeedButton;
        [SerializeField] private Text countSpeedLabel;
        [SerializeField] private Button closeButton;

        private BuildingPlot openPlot;
        private OrbitSpeedByCount openSpeed;

        public bool IsOpen => openPlot != null;

        private void Awake()
        {
            baseSpeedButton.onClick.AddListener(() => openSpeed?.SetUseBaseSpeed(true));
            countSpeedButton.onClick.AddListener(() => openSpeed?.SetUseBaseSpeed(false));
            closeButton.onClick.AddListener(() => Close(true));
        }

        private void OnEnable()
        {
            interactAction.Enable();
            closeAction.action.Enable();
            panel.SetActive(false);
            prompt.SetActive(false);
        }

        private void OnDisable()
        {
            interactAction.Disable();
            // 구경하기를 나가면서 꺼지는 경우: 커서는 ExploreModeController가 정리한다.
            if (IsOpen) Close(false);
        }

        private void Update()
        {
            if (IsOpen)
            {
                if (interactAction.WasPressedThisFrame() || closeAction.action.WasPressedThisFrame()) Close(true);
                else Refresh();
                return;
            }

            bool looking = TryGetLookedPlot(out var plot);
            if (prompt.activeSelf != looking) prompt.SetActive(looking);
            if (looking && interactAction.WasPressedThisFrame()) Open(plot);
        }

        private bool TryGetLookedPlot(out BuildingPlot plot)
        {
            plot = null;
            if (player.IsHeadUnderwater) return false;
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, interactDistance, ~0, QueryTriggerInteraction.Ignore)) return false;
            plot = hit.collider.GetComponentInParent<BuildingPlot>();
            return plot != null && plot.Count > 0;
        }

        private void Open(BuildingPlot plot)
        {
            openPlot = plot;
            openSpeed = plot.GetComponentInChildren<OrbitSpeedByCount>();
            panel.SetActive(true);
            prompt.SetActive(false);
            speedSection.SetActive(openSpeed != null);

            // 팝업을 누를 수 있게 마우스를 풀고, 그동안 걷기·시점 회전을 멈춘다.
            player.enabled = false;
            exploreMode.ExitBlocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
        }

        private void Close(bool relockCursor)
        {
            openPlot = null;
            openSpeed = null;
            panel.SetActive(false);
            player.enabled = true;
            exploreMode.ExitBlocked = false;
            if (relockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Refresh()
        {
            titleText.text = $"{openPlot.Building.DisplayName} x{openPlot.Count}";
            bodyText.text = openPlot.Building.Description;
            if (openSpeed == null) return;

            string next = openSpeed.IsMaxed
                ? "최고 속도 도달!"
                : $"다음 단계까지 {openSpeed.NextStepCount - openPlot.Count}개 ({openSpeed.NextStepCount}개 보유 시)";
            speedText.text =
                $"공전 속도 <color=#FFD15A>x{openSpeed.CurrentMultiplier:0.##}</color>   " +
                $"<size=20>({openSpeed.CountPerStep}개마다 +{openSpeed.BonusPerStep * 100f:0}%)</size>\n<size=20>{next}</size>";

            // 지금 고른 쪽은 눌린 상태로 보이게 비활성화한다.
            baseSpeedButton.interactable = !openSpeed.UseBaseSpeed;
            countSpeedButton.interactable = openSpeed.UseBaseSpeed;
            baseSpeedLabel.text = openSpeed.UseBaseSpeed ? "처음 속도 (사용 중)" : "처음 속도로";
            countSpeedLabel.text = openSpeed.UseBaseSpeed ? "원래대로" : $"원래대로 (사용 중, x{openSpeed.CountMultiplier:0.##})";
        }
    }
}
