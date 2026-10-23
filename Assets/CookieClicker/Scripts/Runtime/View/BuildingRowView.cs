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
        [Tooltip("카드 테두리. 모든 줄이 같은 색이다 (살 수 있는지는 밝기·가격 색으로 구분)")]
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

        [Header("등급 색")]
        [Tooltip("그림 뒤 칸. 등급 색(일반·고급·희귀·영웅·전설)으로 칠한다")]
        [SerializeField] private Image rarityTile;
        [Tooltip("영웅·전설일 때만 켜지는 빛")]
        [SerializeField] private RarityShine rarityGlow;

        [Header("색상")]
        [SerializeField] private Color cardColor = Color.white;
        [SerializeField] private Color flatCardColor = new Color(0.95f, 0.9f, 0.8f);
        [SerializeField] private Color mysteryCardColor = new Color(0.95f, 0.9f, 0.8f, 0f);
        [SerializeField] private Color affordableDepthColor = new Color(0.66f, 0.16f, 0.1f);
        [SerializeField] private Color affordableCostColor = new Color(0.89f, 0.25f, 0.16f);
        [SerializeField] private Color expensiveCostColor = new Color(0.54f, 0.42f, 0.32f);
        [SerializeField] private Color countColor = new Color(0.23f, 0.14f, 0.09f);
        [SerializeField] private Color dimCountColor = new Color(0.73f, 0.63f, 0.53f);
        [SerializeField] private Color mysteryIconColor = new Color(0f, 0f, 0f, 0.13f);
        [SerializeField] private Color mysteryTileColor = new Color(0.92f, 0.86f, 0.75f);

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
            SetCard(canAfford ? cardColor : flatCardColor);

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
            SetCard(mysteryCardColor);

            SetIcon(building.Icon, building.IconColor, "?", true);
            if (rarityTile != null) SetRarity(mysteryTileColor, false);
            nameText.text = "???";
            costText.text = NumberFormatter.Format(cost);
            costText.color = expensiveCostColor;
            infoText.text = string.Empty;
            countText.text = string.Empty;
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>그림 뒤 칸을 등급 색으로 칠하고, 높은 등급이면 뒤에서 빛나게 한다.</summary>
        public void SetRarity(Color color, bool shine)
        {
            if (rarityTile != null) rarityTile.color = color;
            if (rarityGlow == null) return;
            rarityGlow.gameObject.SetActive(shine);
            if (shine) rarityGlow.SetColor(color);
        }

        /// <summary>그림이 있으면 그림을, 없으면 색 칸에 이름 첫 글자를 보여 준다. silhouette이면 흐린 그림자로.</summary>
        private void SetIcon(Sprite sprite, Color tileColor, string label, bool silhouette)
        {
            icon.sprite = sprite != null ? sprite : fallbackIcon;
            icon.preserveAspect = sprite != null;
            icon.color = silhouette ? mysteryIconColor : sprite != null ? Color.white : tileColor;
            iconLabel.text = sprite != null || string.IsNullOrEmpty(label) ? string.Empty : label.Substring(0, 1);
            iconLabel.color = silhouette ? mysteryIconColor : Color.white;
        }

        /// <summary>
        /// 카드 색. 테두리·두께는 모든 줄이 같은 모양으로 통일하고(아직 안 열린 ??? 줄만 빼고),
        /// 살 수 있는지는 카드 밝기·흐림·가격 색으로만 구분한다.
        /// </summary>
        private void SetCard(Color color)
        {
            background.color = color;
            bool visible = color.a > 0f;
            if (highlight != null) highlight.enabled = visible;
            if (depth != null)
            {
                depth.enabled = visible;
                depth.effectColor = affordableDepthColor;
            }
        }
    }
}
