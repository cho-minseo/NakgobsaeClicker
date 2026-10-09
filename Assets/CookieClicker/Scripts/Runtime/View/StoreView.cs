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
        [SerializeField] private Color selectedButtonColor = new Color(0.95f, 0.75f, 0.35f);
        [SerializeField] private Color normalButtonColor = new Color(0.35f, 0.24f, 0.15f);

        [Tooltip("가격 색상 등 자주 바뀌는 값을 몇 초마다 갱신할지")]
        [SerializeField] private float refreshInterval = 0.1f;

        private float refreshTimer;

        private void Start()
        {
            for (int i = 0; i < buildingRows.Length; i++) buildingRows[i].Bind(i, OnBuildingClicked);
            for (int i = 0; i < upgradeRows.Length; i++) upgradeRows[i].Bind(i, OnUpgradeClicked);
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
                    buildingRows[i].ShowMystery(cost);
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
        }
    }
}
