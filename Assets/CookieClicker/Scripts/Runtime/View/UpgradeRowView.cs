using System;
using CookieClicker.Core;
using CookieClicker.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>상점의 업그레이드 한 줄. BuildingRowView와 같은 방식(데이터를 받아 그리고, 클릭은 콜백)으로 동작한다.</summary>
    public class UpgradeRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text costText;

        [Header("색상")]
        [SerializeField] private Color affordableCostColor = new Color(0.55f, 1f, 0.55f);
        [SerializeField] private Color expensiveCostColor = new Color(1f, 0.45f, 0.4f);

        private int index;
        private Action<int> onClicked;

        public void Bind(int rowIndex, Action<int> clickHandler)
        {
            index = rowIndex;
            onClicked = clickHandler;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(index));
        }

        public void Show(UpgradeDefinition upgrade, bool canAfford)
        {
            gameObject.SetActive(true);
            canvasGroup.alpha = canAfford ? 1f : 0.7f;
            icon.color = upgrade.IconColor;
            nameText.text = upgrade.DisplayName;
            descriptionText.text = upgrade.Description;
            costText.text = NumberFormatter.Format(upgrade.Cost);
            costText.color = canAfford ? affordableCostColor : expensiveCostColor;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
