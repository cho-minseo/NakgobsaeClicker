using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 오른쪽 상점 패널. 씬에 미리 배치된 줄(Row)들에 데이터를 채우고, 클릭을 CookieGame 구매 함수로 연결한다.
    /// </summary>
    public class StoreView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private BuildingRowView[] buildingRows;
        [SerializeField] private UpgradeRowView[] upgradeRows;
        [SerializeField] private GameObject noUpgradesLabel;

        [Header("구매 수량 버튼")]
        [SerializeField] private Button[] buyAmountButtons;
        [SerializeField] private int[] buyAmounts = { 1, 10, 100 };
        [SerializeField] private Color selectedButtonColor = Color.white;
        [SerializeField] private Color normalButtonColor = new Color(1f, 1f, 1f, 0f);

        [Header("업그레이드 말풍선 (마우스를 올린 칸의 이름·설명)")]
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private Text tooltipName;
        [SerializeField] private Text tooltipDescription;
        [SerializeField] private Text tooltipCost;
        [SerializeField] private Color affordableCostColor = new Color(0.89f, 0.25f, 0.16f);
        [SerializeField] private Color expensiveCostColor = new Color(0.54f, 0.42f, 0.32f);

        [Tooltip("가격 색상 등 자주 바뀌는 값을 몇 초마다 갱신할지")]
        [SerializeField] private float refreshInterval = 0.1f;

        private float refreshTimer;
        private int hoveredUpgrade = -1;
        private RectTransform hoveredRect;

        private void Start()
        {
            for (int i = 0; i < buildingRows.Length; i++) buildingRows[i].Bind(i, OnBuildingClicked);
            for (int i = 0; i < upgradeRows.Length; i++) upgradeRows[i].Bind(i, OnUpgradeClicked, OnUpgradeHovered);
            if (tooltip != null) tooltip.gameObject.SetActive(false);
            for (int i = 0; i < buyAmountButtons.Length && i < buyAmounts.Length; i++)
            {
                int amount = buyAmounts[i];
                buyAmountButtons[i].onClick.AddListener(() => game.SetBuyAmount(amount));
            }

            game.BuildingPurchased += OnPurchased;
            game.UpgradePurchased += OnPurchased;
            game.BuyAmountChanged += OnBuyAmountChanged;
            game.ProgressReset += Refresh;

            OnBuyAmountChanged(game.BuyAmount);
        }

        private void OnDestroy()
        {
            if (game == null) return;
            game.BuildingPurchased -= OnPurchased;
            game.UpgradePurchased -= OnPurchased;
            game.BuyAmountChanged -= OnBuyAmountChanged;
            game.ProgressReset -= Refresh;
        }

        private void Update()
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= refreshInterval) Refresh();
        }

        private void OnBuildingClicked(int index) => game.TryBuyBuilding(index);
        private void OnUpgradeClicked(int index) => game.TryBuyUpgrade(index);
        private void OnPurchased(int _) => Refresh();

        private void OnBuyAmountChanged(int amount)
        {
            for (int i = 0; i < buyAmountButtons.Length && i < buyAmounts.Length; i++)
                buyAmountButtons[i].image.color = buyAmounts[i] == amount ? selectedButtonColor : normalButtonColor;
            Refresh();
        }

        private void Refresh()
        {
            refreshTimer = 0f;
            var config = game.Config;
            var state = game.State;

            // 공개된 건물 바로 다음 하나만 "???"로 보여 주고, 그 뒤는 숨긴다.
            bool previousRevealed = true;
            int buildingCount = Mathf.Min(buildingRows.Length, config.Buildings.Count);
            for (int i = 0; i < buildingCount; i++)
            {
                bool revealed = game.IsBuildingRevealed(i);
                double cost = game.GetBuildingCost(i, game.BuyAmount);

                if (revealed)
                {
                    buildingRows[i].ShowBuilding(config.Buildings[i], state.BuildingCounts[i], game.BuyAmount, cost,
                        game.Production.UnitCps[i], game.Production.BuildingCps[i], state.Cookies >= cost);
                }
                else if (previousRevealed)
                {
                    buildingRows[i].ShowMystery(config.Buildings[i], cost);
                }
                else
                {
                    buildingRows[i].Hide();
                }
                previousRevealed = revealed;
            }

            bool anyUpgrade = false;
            int upgradeCount = Mathf.Min(upgradeRows.Length, config.Upgrades.Count);
            for (int i = 0; i < upgradeCount; i++)
            {
                if (game.IsUpgradeAvailable(i))
                {
                    upgradeRows[i].Show(config.Upgrades[i], state.Cookies >= config.Upgrades[i].Cost);
                    anyUpgrade = true;
                }
                else
                {
                    upgradeRows[i].Hide();
                }
            }
            if (noUpgradesLabel != null) noUpgradesLabel.SetActive(!anyUpgrade);
            RefreshTooltip();
        }

        private void OnUpgradeHovered(int index, RectTransform rect)
        {
            hoveredUpgrade = index;
            hoveredRect = rect;
            RefreshTooltip();
        }

        /// <summary>마우스를 올린 업그레이드 칸 바로 왼쪽(상점 바깥)에 이름·설명·가격 말풍선을 띄운다.</summary>
        private void RefreshTooltip()
        {
            if (tooltip == null) return;
            bool show = hoveredUpgrade >= 0 && hoveredRect != null && hoveredRect.gameObject.activeInHierarchy;
            tooltip.gameObject.SetActive(show);
            if (!show) return;

            var upgrade = game.Config.Upgrades[hoveredUpgrade];
            tooltipName.text = upgrade.DisplayName;
            tooltipDescription.text = upgrade.Description;
            tooltipCost.text = $"{NumberFormatter.Format(upgrade.Cost)} {game.Config.CurrencyName}";
            tooltipCost.color = game.State.Cookies >= upgrade.Cost ? affordableCostColor : expensiveCostColor;

            // 칸의 세로 위치에 맞춘다 (말풍선은 상점 패널 기준으로 배치되어 있다).
            var parent = (RectTransform)tooltip.parent;
            Vector3 world = hoveredRect.TransformPoint(hoveredRect.rect.center);
            Vector2 local = parent.InverseTransformPoint(world);
            tooltip.anchoredPosition = new Vector2(tooltip.anchoredPosition.x, local.y - parent.rect.yMax);
        }
    }
}
