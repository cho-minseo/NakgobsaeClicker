using CookieClicker.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 의자를 보고 F를 누르면 앉고, 다시 F(또는 Esc)를 누르면 일어난다.
    /// 앉아 있는 동안은 움직이지 않고 시점만 돌릴 수 있으며, 앉은 모습이 보이도록 3인칭으로 바뀐다.
    /// </summary>
    // 건물 팝업(100)·꾸미기(110)와 같은 F/Esc를 쓰므로 그보다 늦게 읽어서 "일어나기"와 "구경하기 나가기"가 겹치지 않게 한다.
    [DefaultExecutionOrder(120)]
    public class SeatInteractor : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerAvatar avatar;
        [SerializeField] private CameraModeController cameraMode;
        [SerializeField] private ExploreModeController exploreMode;
        [Tooltip("1인칭 눈 위치에서 이 거리 안의 의자만 앉을 수 있다")]
        [SerializeField] private float reach = 3.5f;
        [Tooltip("일어날 때 의자 앞으로 이만큼 떨어져 선다")]
        [SerializeField] private float standDistance = 0.8f;
        [SerializeField] private InputAction interactAction = new InputAction("Sit", InputActionType.Button, "<Keyboard>/f");
        [SerializeField] private InputActionReference cancelAction;

        [Header("UI")]
        [SerializeField] private GameObject prompt;
        [SerializeField] private Text promptText;

        private Seat seat;

        public bool IsSitting => seat != null;

        private void OnEnable()
        {
            interactAction.Enable();
            cancelAction.action.Enable();
            prompt.SetActive(false);
        }

        private void OnDisable()
        {
            interactAction.Disable();
            // 앉은 채로 구경하기를 나가면 일어난 상태로 정리한다.
            if (IsSitting) Stand();
            prompt.SetActive(false);
        }

        private void Update()
        {
            if (IsSitting)
            {
                exploreMode.ExitBlocked = true;
                SetPrompt(Loc.T("[F] 일어나기", "[F] Stand up"));
                if (interactAction.WasPressedThisFrame() || cancelAction.action.WasPressedThisFrame()) Stand();
                return;
            }

            // 팝업이 열려 있거나(플레이어 멈춤) 꾸미기 중(조준이 물건 놓기용)이면 앉기 안내를 띄우지 않는다.
            bool canLook = player.enabled && !player.IsSwimming && !player.MovementLocked && !Decor.DecorBuilder.AnyBuilding;
            var looked = canLook ? FindSeat() : null;
            SetPrompt(looked != null ? Loc.T("[F] 앉기", "[F] Sit") : null);
            if (looked != null && interactAction.WasPressedThisFrame()) Sit(looked);
        }

        private Seat FindSeat()
        {
            var ray = new Ray(player.CameraPivot.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, reach + Vector3.Distance(player.CameraPivot.position, playerCamera.transform.position),
                    ~0, QueryTriggerInteraction.Ignore)) return null;
            var found = hit.collider.GetComponentInParent<Seat>();
            return found != null && !found.Occupied ? found : null;
        }

        private void Sit(Seat target)
        {
            seat = target;
            seat.Occupied = true;
            var point = seat.SitPoint;
            // 앉는 동작에 맞춰 발(루트) 위치를 앉는 면에서 조금 내린다.
            var rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(point.forward, Vector3.up));
            var position = point.position + rotation * avatar.SitOffset;
            player.SitAt(position, rotation);
            avatar.Sitting = true;
            cameraMode.IgnoreRoot = seat.transform;
            cameraMode.ForceThirdPerson = true;
            exploreMode.ExitBlocked = true;
        }

        private void Stand()
        {
            var point = seat.SitPoint;
            var forward = Vector3.ProjectOnPlane(point.forward, Vector3.up).normalized;
            var standAt = point.position + forward * standDistance;
            // 의자가 놓인 바닥 높이에 선다 (평상 위가 아니라 평상 앞 땅).
            standAt.y = seat.transform.position.y + 0.05f;
            seat.Occupied = false;
            seat = null;
            avatar.Sitting = false;
            cameraMode.ForceThirdPerson = false;
            cameraMode.IgnoreRoot = null;
            player.StandUp(standAt);
            exploreMode.ExitBlocked = false;
        }

        private void SetPrompt(string text)
        {
            bool show = text != null;
            if (prompt.activeSelf != show) prompt.SetActive(show);
            if (show && promptText.text != text) promptText.text = text;
        }
    }
}
