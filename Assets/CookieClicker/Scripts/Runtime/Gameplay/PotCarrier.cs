using System.Collections.Generic;
using CookieClicker.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 본진 냄비를 보고 왼쪽 클릭하면 1인분 낙곱새를 꺼내 든다. 들고 있을 때 왼쪽 클릭은 던지기, 오른쪽 클릭은 내려놓기.
    /// 손에서 놓은 낙곱새는 바닥에 닿고 몇 초 뒤 사라진다 (ThrownPortion). 본진 냄비는 그대로 남는다.
    /// 1인칭에서는 오른손이 손잡이를 쥐고(손과 같이 흔들림), 3인칭에서는 캐릭터 가슴 앞에 든다.
    /// </summary>
    // 앉기(120)보다 늦게 읽어서, 같은 프레임에 앉기/잡기가 겹치지 않게 한다.
    [DefaultExecutionOrder(130)]
    public class PotCarrier : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private CameraModeController cameraMode;
        [SerializeField] private SeatInteractor seats;
        [Tooltip("1인칭 손 움직임. 들고 있는 동안 오른손을 주먹(쥔 모양)으로 바꾼다")]
        [SerializeField] private Viewmodel.ViewmodelAnimator hands;
        [Tooltip("꺼내 드는 곳 (본진 냄비)")]
        [SerializeField] private Transform homePot;
        [Tooltip("손에 들 1인분 낙곱새 프리팹 (Rigidbody + ThrownPortion)")]
        [SerializeField] private GameObject portionPrefab;
        [Tooltip("놓은 낙곱새를 모아 둘 부모 (Hierarchy 정리용, 비워도 됨)")]
        [SerializeField] private Transform droppedParent;

        [Header("드는 위치")]
        [SerializeField] private Transform firstPersonHold;
        [SerializeField] private Transform thirdPersonHold;

        [Header("입력")]
        [Tooltip("들기·던지기 (왼쪽 클릭 = Player/Attack)")]
        [SerializeField] private InputActionReference throwAction;
        [SerializeField] private InputAction dropAction = new InputAction("PutDown", InputActionType.Button, "<Mouse>/rightButton");

        [Header("잡기")]
        [Tooltip("냄비와 눈 사이가 이 거리 안이어야 잡을 수 있다(m)")]
        [SerializeField] private float reach = 3.5f;
        [Tooltip("냄비가 시선 중심에서 이 각도 안에 있어야 잡을 수 있다(도)")]
        [SerializeField] private float aimAngle = 25f;

        [Header("놓기 / 던지기")]
        [SerializeField] private float throwSpeed = 9f;
        [SerializeField] private float throwLift = 3f;
        [SerializeField] private float dropSpeed = 1.2f;
        [Tooltip("바닥에 닿고 사라지기까지(초)")]
        [SerializeField] private float lifetimeOnGround = 3f;
        [Tooltip("바닥에 동시에 있을 수 있는 개수. 넘으면 오래된 것부터 치운다")]
        [SerializeField, Min(1)] private int maxDropped = 12;

        [Header("UI")]
        [SerializeField] private GameObject prompt;
        [SerializeField] private Text promptText;

        private readonly Queue<ThrownPortion> dropped = new Queue<ThrownPortion>();
        private GameObject held;

        public bool IsHolding => held != null;

        private void OnEnable()
        {
            dropAction.Enable();
            throwAction.action.Enable();
            SetPrompt(null);
        }

        private void OnDisable()
        {
            dropAction.Disable();
            // 들고 있는 채로 구경하기를 나가면 손에 든 것은 치운다.
            if (held != null) Destroy(held);
            held = null;
            if (hands != null) hands.HoldingRight = false;
            SetPrompt(null);
        }

        private void Update()
        {
            if (hands != null) hands.HoldingRight = held != null;
            bool busy = !player.enabled || player.MovementLocked || Decor.DecorBuilder.AnyBuilding
                        || (seats != null && seats.IsSitting);

            if (held != null)
            {
                FollowHoldPoint();
                if (busy)
                {
                    SetPrompt(null);
                    return;
                }
                SetPrompt(Loc.T("[클릭] 던지기 · [우클릭] 내려놓기", "[Click] Throw · [Right-click] Put down"));
                if (throwAction.action.WasPressedThisFrame()) Release(ThrowVelocity());
                else if (dropAction.WasPressedThisFrame()) Release(DropVelocity());
                return;
            }

            bool looking = !busy && !player.IsSwimming && IsLookingAtPot();
            SetPrompt(looking ? Loc.T("[클릭] 낙곱새 들기", "[Click] Pick up nakgopsae") : null);
            // 같은 클릭으로 바로 던지지 않도록, 든 프레임에는 여기서 끝난다 (다음 Update부터 던지기).
            if (looking && throwAction.action.WasPressedThisFrame()) Grab();
        }

        /// <summary>냄비가 가까이 있고 시선 중심 근처에 있으면 "보고 있다"로 친다 (냄비에 충돌체가 없어도 되게).</summary>
        private bool IsLookingAtPot()
        {
            if (homePot == null) return false;
            Vector3 eye = player.CameraPivot.position;
            Vector3 toPot = homePot.position - eye;
            return toPot.magnitude <= reach && Vector3.Angle(playerCamera.transform.forward, toPot) <= aimAngle;
        }

        private Transform CurrentHoldPoint =>
            cameraMode != null && (cameraMode.IsThirdPerson || cameraMode.ForceThirdPerson) && thirdPersonHold != null
                ? thirdPersonHold : firstPersonHold;

        private void Grab()
        {
            held = Instantiate(portionPrefab, CurrentHoldPoint, false);
            held.name = portionPrefab.name;
            SetPhysics(held, false);
        }

        /// <summary>1인칭 ↔ 3인칭을 바꾸면 드는 위치도 따라 옮긴다.</summary>
        private void FollowHoldPoint()
        {
            var point = CurrentHoldPoint;
            if (held.transform.parent == point) return;
            held.transform.SetParent(point, false);
            held.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private Vector3 ThrowVelocity() =>
            playerCamera.transform.forward * throwSpeed + Vector3.up * throwLift + player.PlanarVelocity;

        private Vector3 DropVelocity()
        {
            var forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
            return forward * dropSpeed + player.PlanarVelocity;
        }

        private void Release(Vector3 velocity)
        {
            var portion = held;
            held = null;
            portion.transform.SetParent(droppedParent, true);
            SetPhysics(portion, true);

            var body = portion.GetComponent<Rigidbody>();
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 4f;

            var thrown = portion.GetComponent<ThrownPortion>();
            thrown.Release(lifetimeOnGround);
            dropped.Enqueue(thrown);
            // 이미 사라진 것은 빼고, 너무 많으면 오래된 것부터 치운다.
            while (dropped.Count > 0 && (dropped.Peek() == null || dropped.Peek().IsGone)) dropped.Dequeue();
            while (dropped.Count > maxDropped) dropped.Dequeue()?.Vanish();
        }

        /// <summary>손에 든 동안은 물리를 끄고(손에 붙어 다님) 충돌도 끈다 (플레이어·벽에 걸리지 않게).</summary>
        private static void SetPhysics(GameObject portion, bool on)
        {
            var body = portion.GetComponent<Rigidbody>();
            body.isKinematic = !on;
            body.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            foreach (var c in portion.GetComponentsInChildren<Collider>()) c.enabled = on;
        }

        private void SetPrompt(string text)
        {
            if (prompt == null) return;
            bool show = text != null;
            if (prompt.activeSelf != show) prompt.SetActive(show);
            if (show && promptText.text != text) promptText.text = text;
        }
    }
}
