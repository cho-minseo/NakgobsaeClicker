using System;
using CookieClicker.Core;
using CookieClicker.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 상점의 건물 한 줄. 데이터를 받아 그리기만 하고, 클릭되면 자기 인덱스를 콜백으로 알린다.
    /// 프리팹 하나로 모든 건물 줄을 만들기 때문에 디자인 수정은 프리팹만 고치면 전부 반영된다.
    /// </summary>
    public class BuildingRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image icon;
        [SerializeField] private Text iconLabel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text costText;
        [SerializeField] private Text infoText;
        [SerializeField] private Text countText;

        [Header("색상")]
        [SerializeField] private Color affordableCostColor = new Color(0.55f, 1f, 0.55f);
        [SerializeField] private Color expensiveCostColor = new Color(1f, 0.45f, 0.4f);
        [SerializeField] private Color mysteryIconColor = new Color(0.25f, 0.25f, 0.25f);

        private int index;
        private Action<int> onClicked;

        public void Bind(int rowIndex, Action<int> clickHandler)
        {
            index = rowIndex;
            onClicked = clickHandler;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(index));
        }

        public void ShowBuilding(BuildingDefinition building, int owned, int buyAmount, double cost,
            double unitCps, double totalCps, bool canAfford)
        {
            gameObject.SetActive(true);
            button.interactable = true;
            canvasGroup.alpha = canAfford ? 1f : 0.7f;

            icon.color = building.IconColor;
            iconLabel.text = string.IsNullOrEmpty(building.DisplayName) ? "?" : building.DisplayName.Substring(0, 1);
            nameText.text = buyAmount > 1 ? $"{building.DisplayName} x{buyAmount}" : building.DisplayName;
            costText.text = $"{Loc.T("가격", "Cost")} {NumberFormatter.Format(cost)}";
            costText.color = canAfford ? affordableCostColor : expensiveCostColor;
            infoText.text = owned > 0
                ? Loc.T($"1개당 초당 {NumberFormatter.Format(unitCps, true)} · 합계 초당 {NumberFormatter.Format(totalCps, true)}",
                    $"Each {NumberFormatter.Format(unitCps, true)}/s · Total {NumberFormatter.Format(totalCps, true)}/s")
                : Loc.T($"1개당 초당 {NumberFormatter.Format(unitCps, true)}", $"Each {NumberFormatter.Format(unitCps, true)}/s");
            countText.text = owned > 0 ? owned.ToString() : string.Empty;
        }

        /// <summary>다음에 열릴 건물은 이름을 숨긴 채 가격만 보여 줘서 목표를 만든다.</summary>
        public void ShowMystery(double cost)
        {
            gameObject.SetActive(true);
            button.interactable = false;
            canvasGroup.alpha = 0.5f;

            icon.color = mysteryIconColor;
            iconLabel.text = "?";
            nameText.text = "???";
            costText.text = $"{Loc.T("가격", "Cost")} {NumberFormatter.Format(cost)}";
            costText.color = expensiveCostColor;
            infoText.text = string.Empty;
            countText.text = string.Empty;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
