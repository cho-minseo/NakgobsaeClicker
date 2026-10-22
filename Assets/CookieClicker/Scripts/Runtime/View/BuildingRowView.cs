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
    /// 살 수 있는 줄만 빨간 테두리로 튀어나와 보이고, 못 사는 줄은 납작하고 흐리게 보인다.
    /// </summary>
    public class BuildingRowView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [Tooltip("살 수 있을 때만 켜지는 빨간 테두리")]
        [SerializeField] private Outline highlight;
        [Tooltip("카드 아래 두께(그림자)")]
        [SerializeField] private Shadow depth;
        [SerializeField] private Image icon;
        [Tooltip("그림이 없는 건물에 쓰는 색 칸 모양")]
        [SerializeField] private Sprite fallbackIcon;
        [SerializeField] private Text iconLabel;
        [SerializeField] private Text nameText;
        [SerializeField] private Text costText;
        [SerializeField] private Text infoText;
        [SerializeField] private Text countText;

        [Header("색상")]
        [SerializeField] private Color cardColor = Color.white;
        [SerializeField] private Color flatCardColor = new Color(0.95f, 0.9f, 0.8f);
        [SerializeField] private Color mysteryCardColor = new Color(0.95f, 0.9f, 0.8f, 0f);
        [SerializeField] private Color cardDepthColor = new Color(0.9f, 0.83f, 0.7f);
        [SerializeField] private Color affordableDepthColor = new Color(0.66f, 0.16f, 0.1f);
        [SerializeField] private Color affordableCostColor = new Color(0.89f, 0.25f, 0.16f);
        [SerializeField] private Color expensiveCostColor = new Color(0.54f, 0.42f, 0.32f);
        [SerializeField] private Color countColor = new Color(0.23f, 0.14f, 0.09f);
        [SerializeField] private Color dimCountColor = new Color(0.73f, 0.63f, 0.53f);
        [SerializeField] private Color mysteryIconColor = new Color(0f, 0f, 0f, 0.13f);

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
            canvasGroup.alpha = canAfford ? 1f : 0.6f;
            SetCard(canAfford ? cardColor : flatCardColor, canAfford, !canAfford);

            SetIcon(building.Icon, building.IconColor, building.DisplayName, false);
            nameText.text = buyAmount > 1 ? $"{building.DisplayName} x{buyAmount}" : building.DisplayName;
            costText.text = NumberFormatter.Format(cost);
            costText.color = canAfford ? affordableCostColor : expensiveCostColor;
            infoText.text = owned > 0
                ? Loc.T($"1개당 초당 {NumberFormatter.Format(unitCps, true)} · 합계 초당 {NumberFormatter.Format(totalCps, true)}",
                    $"Each {NumberFormatter.Format(unitCps, true)}/s · Total {NumberFormatter.Format(totalCps, true)}/s")
                : Loc.T($"1개당 초당 {NumberFormatter.Format(unitCps, true)}", $"Each {NumberFormatter.Format(unitCps, true)}/s");
            countText.text = owned > 0 ? owned.ToString() : string.Empty;
            countText.color = canAfford ? countColor : dimCountColor;
        }

        /// <summary>다음에 열릴 건물은 이름을 숨긴 채 가격만 보여 줘서 목표를 만든다 (그림은 실루엣).</summary>
        public void ShowMystery(BuildingDefinition building, double cost)
        {
            gameObject.SetActive(true);
            button.interactable = false;
            canvasGroup.alpha = 1f;
            SetCard(mysteryCardColor, false, false);

            SetIcon(building.Icon, building.IconColor, "?", true);
            nameText.text = "???";
            costText.text = NumberFormatter.Format(cost);
            costText.color = expensiveCostColor;
            infoText.text = string.Empty;
            countText.text = string.Empty;
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>그림이 있으면 그림을, 없으면 색 칸에 이름 첫 글자를 보여 준다. silhouette이면 흐린 그림자로.</summary>
        private void SetIcon(Sprite sprite, Color tileColor, string label, bool silhouette)
        {
            icon.sprite = sprite != null ? sprite : fallbackIcon;
            icon.preserveAspect = sprite != null;
            icon.color = silhouette ? mysteryIconColor : sprite != null ? Color.white : tileColor;
            iconLabel.text = sprite != null || string.IsNullOrEmpty(label) ? string.Empty : label.Substring(0, 1);
            iconLabel.color = silhouette ? mysteryIconColor : Color.white;
        }

        private void SetCard(Color color, bool raised, bool flat)
        {
            background.color = color;
            if (highlight != null) highlight.enabled = raised;
            if (depth != null)
            {
                depth.enabled = !flat && color.a > 0f;
                depth.effectColor = raised ? affordableDepthColor : cardDepthColor;
            }
        }
    }
}
