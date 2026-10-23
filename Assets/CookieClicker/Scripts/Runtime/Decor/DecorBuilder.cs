using System.Collections.Generic;
using CookieClicker.Core;
using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CookieClicker.Decor
{
    /// <summary>
    /// 꾸미기 모드 조작. B로 켜고 끄고, 바라보는 곳에 반투명 미리보기를 띄워서 좌클릭으로 사서 놓고 우클릭으로 회수한다.
    /// 미리보기는 놓을 수 있으면 초록, 없으면 빨강. 계산(어디에 붙는지, 놓을 수 있는지)은 DecorArea가 한다.
    /// C로 칠할 색을 고르면 그 색으로 놓이고, 놓인 물건을 보고 G를 누르면 그 색으로 다시 칠한다.
    /// </summary>
    // 건물 팝업(100)보다 늦게 돌아야 팝업을 Esc로 닫는 프레임에 꾸미기까지 같이 끝나지 않는다.
    [DefaultExecutionOrder(110)]
    public class DecorBuilder : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private DecorArea area;
        [SerializeField] private DecorHudView hud;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private ExploreModeController exploreMode;
        [SerializeField] private float reach = 12f;

        [Header("미리보기")]
        [Tooltip("반투명 머티리얼. 색은 아래 두 색으로 바꿔 칠한다")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color validColor = new Color(0.45f, 1f, 0.55f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.35f, 0.3f, 0.45f);

        [Header("입력")]
        [SerializeField] private InputAction toggleAction = new InputAction("DecorToggle", InputActionType.Button, "<Keyboard>/b");
        [SerializeField] private InputAction placeAction = new InputAction("DecorPlace", InputActionType.Button, "<Mouse>/leftButton");
        [SerializeField] private InputAction removeAction = new InputAction("DecorRemove", InputActionType.Button, "<Mouse>/rightButton");
        [SerializeField] private InputAction rotateAction = new InputAction("DecorRotate", InputActionType.Button, "<Keyboard>/r");
        [SerializeField] private InputAction scrollAction = new InputAction("DecorScroll", InputActionType.Value, "<Mouse>/scroll/y");
        [Tooltip("다음 탭 (Shift와 함께 누르면 이전 탭)")]
        [SerializeField] private InputAction categoryAction = new InputAction("DecorCategory", InputActionType.Button, "<Keyboard>/tab");
        [Tooltip("칠할 색 바꾸기 (원래 색 → 팔레트 순서)")]
        [SerializeField] private InputAction colorAction = new InputAction("DecorColor", InputActionType.Button, "<Keyboard>/c");
        [Tooltip("바라보는 물건을 지금 색으로 칠하기")]
        [SerializeField] private InputAction paintAction = new InputAction("DecorPaint", InputActionType.Button, "<Keyboard>/g");
        [SerializeField] private InputActionReference cancelAction;

        public bool IsBuilding { get; private set; }
        /// <summary>꾸미기 중인지 (다른 상호작용이 조준을 같이 쓰지 않도록 확인용)</summary>
        public static bool AnyBuilding { get; private set; }

        private readonly InputAction selectAction = new InputAction("DecorSelect", InputActionType.Button);
        private readonly Dictionary<DecorPiece, GameObject> ghosts = new Dictionary<DecorPiece, GameObject>();
        private Material ghostInstance;
        private GameObject activeGhost;
        private int selected;
        private DecorCategory category;
        /// <summary>탭마다 마지막으로 고른 물건 (탭을 왔다 갔다 해도 고른 게 유지되게)</summary>
        private readonly int[] lastSelected = { -1, -1, -1, -1 };
        /// <summary>칠할 색 번호 (-1 = 원래 색)</summary>
        private int paintColor = -1;
        private int rotationSteps;
        private bool pausedLastFrame;

        private DecorPiece SelectedPiece => area.Catalog.Pieces[selected];

        private void Awake()
        {
            for (int k = 1; k <= 9; k++) selectAction.AddBinding($"<Keyboard>/{k}");
            hud.Bind(area.Catalog);
            hud.Show(false);
        }

        private void OnEnable()
        {
            toggleAction.Enable();
            placeAction.Enable();
            removeAction.Enable();
            rotateAction.Enable();
            scrollAction.Enable();
            categoryAction.Enable();
            colorAction.Enable();
            paintAction.Enable();
            selectAction.Enable();
            cancelAction.action.Enable();
        }

        private void OnDisable()
        {
            toggleAction.Disable();
            placeAction.Disable();
            removeAction.Disable();
            rotateAction.Disable();
            scrollAction.Disable();
            categoryAction.Disable();
            colorAction.Disable();
            paintAction.Disable();
            selectAction.Disable();
            // 구경하기를 나가면서 꺼질 때
            if (IsBuilding) SetBuilding(false);
            hud.ShowPrompt(false);
        }

        private void OnDestroy() => selectAction.Dispose();

        private void Update()
        {
            // 건물 팝업이 열려 있으면(플레이어가 멈춤) 아무것도 하지 않고, 닫힌 바로 다음 프레임도 한 번 쉰다.
            if (!player.enabled || player.IsSwimming || player.MovementLocked)
            {
                pausedLastFrame = true;
                hud.ShowPrompt(false);
                if (activeGhost != null) activeGhost.SetActive(false);
                return;
            }
            if (pausedLastFrame)
            {
                pausedLastFrame = false;
                if (IsBuilding) exploreMode.ExitBlocked = true;
                return;
            }

            if (toggleAction.WasPressedThisFrame()) SetBuilding(!IsBuilding);
            hud.ShowPrompt(!IsBuilding && area.Contains(player.transform.position));
            if (!IsBuilding) return;

            // 꾸미기 중에는 Esc가 "꾸미기 끝내기"가 된다 (구경하기를 바로 나가지 않게).
            exploreMode.ExitBlocked = true;
            if (cancelAction.action.WasPressedThisFrame())
            {
                SetBuilding(false);
                return;
            }

            HandleSelection();
            UpdateAim();
        }

        private void SetBuilding(bool building)
        {
            IsBuilding = building;
            AnyBuilding = building;
            if (building && SelectedPiece.Category != category) SetCategory(SelectedPiece.Category);
            exploreMode.ExitBlocked = false;
            area.SetGridVisible(building);
            hud.Show(building);
            hud.SetSelected(selected);
            hud.SetPaintColor(paintColor);
            if (!building && activeGhost != null) activeGhost.SetActive(false);
        }

        private void HandleSelection()
        {
            if (categoryAction.WasPressedThisFrame())
            {
                int count = System.Enum.GetValues(typeof(DecorCategory)).Length;
                bool back = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                SetCategory((DecorCategory)(((int)category + (back ? count - 1 : 1)) % count));
            }
            if (colorAction.WasPressedThisFrame())
            {
                // -1(원래 색) → 0 → 1 → ... → 마지막 → 다시 -1
                int colors = area.Catalog.PaintColors.Count;
                paintColor = paintColor + 1 >= colors ? -1 : paintColor + 1;
                hud.SetPaintColor(paintColor);
            }

            // 숫자키·휠은 지금 탭 안의 물건만 돈다.
            var inTab = new List<int>();
            var pieces = area.Catalog.Pieces;
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i].Category == category) inTab.Add(i);
            if (inTab.Count == 0) return;
            int position = Mathf.Max(0, inTab.IndexOf(selected));

            if (selectAction.WasPressedThisFrame() && selectAction.activeControl is KeyControl key)
            {
                int index = key.keyCode - Key.Digit1;
                if (index >= 0 && index < inTab.Count) position = index;
            }
            float scroll = scrollAction.ReadValue<float>();
            if (scroll > 0.1f) position = (position - 1 + inTab.Count) % inTab.Count;
            else if (scroll < -0.1f) position = (position + 1) % inTab.Count;

            selected = inTab[position];
            lastSelected[(int)category] = selected;
            if (rotateAction.WasPressedThisFrame()) rotationSteps = (rotationSteps + 1) % 4;
            hud.SetSelected(selected);
        }

        private void SetCategory(DecorCategory value)
        {
            category = value;
            hud.SetCategory(value);
            int last = lastSelected[(int)value];
            if (last >= 0) selected = last;
        }

        private void UpdateAim()
        {
            var piece = SelectedPiece;
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                ShowGhost(null);
                hud.SetStatus(Loc.T("땅이나 바닥을 바라봐 주세요", "Look at the ground"), false);
                return;
            }

            // 놓인 물건을 보고 있으면 우클릭으로 회수할 수 있다.
            var looked = hit.collider.GetComponentInParent<PlacedDecor>();
            double refund = looked != null ? looked.Piece.Cost * area.Catalog.RefundRatio : 0;
            if (looked != null && removeAction.WasPressedThisFrame())
            {
                area.Remove(looked);
                game.Refund(refund);
                return;
            }
            // 놓인 물건을 보고 G를 누르면 지금 고른 색으로 다시 칠한다 (무료).
            if (looked != null && paintAction.WasPressedThisFrame())
            {
                area.Paint(looked, paintColor);
                return;
            }

            var placement = area.GetPlacement(piece, hit, rotationSteps);
            bool affordable = game.State.Cookies >= piece.Cost;
            bool canPlace = placement.Valid && affordable;
            ShowGhost(piece, placement, canPlace);

            string removeInfo = looked != null
                ? Loc.T($"   ·   [우클릭] {looked.Piece.DisplayName} 회수 (+{NumberFormatter.Format(refund)})  [G] 칠하기",
                    $"   ·   [RMB] Remove {looked.Piece.DisplayName} (+{NumberFormatter.Format(refund)})  [G] Paint")
                : string.Empty;
            if (!placement.Valid) hud.SetStatus(placement.Reason + removeInfo, true);
            else if (!affordable) hud.SetStatus(Loc.T($"{game.Config.CurrencyName}가 모자라요", $"Not enough {game.Config.CurrencyName}") + removeInfo, true);
            else hud.SetStatus($"{piece.DisplayName}  {NumberFormatter.Format(piece.Cost)} {game.Config.CurrencyName}{removeInfo}", false);

            if (canPlace && placeAction.WasPressedThisFrame() && game.TrySpend(piece.Cost))
                area.Place(piece, placement, paintColor);
        }

        // ------------------------------------------------------------------ 미리보기

        private void ShowGhost(DecorPiece piece, DecorPlacement placement = default, bool valid = false)
        {
            var ghost = piece != null ? GetGhost(piece) : null;
            if (activeGhost != null && activeGhost != ghost) activeGhost.SetActive(false);
            activeGhost = ghost;
            if (ghost == null) return;

            ghost.SetActive(true);
            ghost.transform.localPosition = placement.LocalPosition;
            ghost.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
            var color = valid ? validColor : invalidColor;
            // 살짝 숨 쉬듯 진해졌다 옅어져서 진짜 물건과 구분된다.
            color.a *= 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            ghostInstance.SetColor("_BaseColor", color);
        }

        /// <summary>
        /// 고른 물건의 미리보기. 프리팹을 복사한 뒤 충돌·효과를 끄고 반투명 머티리얼로 바꾼 것이라,
        /// 프리팹 모양을 고치면 미리보기도 자동으로 같이 바뀐다. 물건마다 한 번만 만들어 재사용한다.
        /// </summary>
        private GameObject GetGhost(DecorPiece piece)
        {
            if (ghosts.TryGetValue(piece, out var ghost)) return ghost;

            if (ghostInstance == null) ghostInstance = new Material(ghostMaterial);
            ghost = Instantiate(piece.Prefab, area.transform);
            ghost.name = $"Ghost_{piece.Id}";
            // Destroy는 프레임 끝에 지워지므로, 이번 프레임 레이캐스트에 걸리지 않게 먼저 끈다.
            foreach (var c in ghost.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
                Destroy(c);
            }
            foreach (var ps in ghost.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            foreach (var placed in ghost.GetComponentsInChildren<PlacedDecor>(true)) placed.enabled = false;
            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[r.sharedMaterials.Length];
                for (int m = 0; m < materials.Length; m++) materials[m] = ghostInstance;
                r.sharedMaterials = materials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ghosts[piece] = ghost;
            return ghost;
        }
    }
}
