using System;
using CookieClicker.Core;
using CookieClicker.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 상점의 업그레이드 한 칸 (그림 + 가격). 이름·설명은 칸이 좁아서 마우스를 올렸을 때 StoreView의 말풍선으로 보여 준다.
    /// BuildingRowView와 같은 방식(데이터를 받아 그리고, 클릭·마우스 올림은 콜백)으로 동작한다.
    /// </summary>
    public class UpgradeRowView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [Tooltip("살 수 있을 때만 켜지는 빨간 테두리")]
        [SerializeField] private Outline highlight;
        [SerializeField] private Shadow depth;
        [SerializeField] private Image icon;
        [Tooltip("그림이 없는 업그레이드에 쓰는 색 칸 모양")]
        [SerializeField] private Sprite fallbackIcon;
        [SerializeField] private Text iconLabel;
        [SerializeField] private Text costText;

        [Header("등급 색")]
        [Tooltip("그림 뒤 칸. 등급 색(일반·고급·희귀·영웅·전설)으로 칠한다")]
        [SerializeField] private Image rarityTile;
        [Tooltip("영웅·전설일 때만 켜지는 빛")]
        [SerializeField] private RarityShine rarityGlow;

        [Header("색상")]
        [SerializeField] private Color cardColor = Color.white;
        [SerializeField] private Color flatCardColor = new Color(0.95f, 0.9f, 0.8f);
        [SerializeField] private Color cardDepthColor = new Color(0.9f, 0.83f, 0.7f);
        [SerializeField] private Color affordableDepthColor = new Color(0.66f, 0.16f, 0.1f);
        [SerializeField] private Color affordableCostColor = new Color(0.89f, 0.25f, 0.16f);
        [SerializeField] private Color expensiveCostColor = new Color(0.54f, 0.42f, 0.32f);

        private int index;
        private Action<int> onClicked;
        private Action<int, RectTransform> onHover;

        public UpgradeDefinition Upgrade { get; private set; }

        public void Bind(int rowIndex, Action<int> clickHandler, Action<int, RectTransform> hoverHandler = null)
        {
            index = rowIndex;
            onClicked = clickHandler;
            onHover = hoverHandler;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(index));
        }

        public void Show(UpgradeDefinition upgrade, bool canAfford)
        {
            Upgrade = upgrade;
            gameObject.SetActive(true);
            canvasGroup.alpha = canAfford ? 1f : 0.6f;
            background.color = canAfford ? cardColor : flatCardColor;
            if (highlight != null) highlight.enabled = canAfford;
            if (depth != null)
            {
                depth.enabled = canAfford;
                depth.effectColor = canAfford ? affordableDepthColor : cardDepthColor;
            }

            var sprite = upgrade.Icon;
            icon.sprite = sprite != null ? sprite : fallbackIcon;
            icon.preserveAspect = sprite != null;
            icon.color = sprite != null ? Color.white : upgrade.IconColor;
            iconLabel.text = sprite != null || string.IsNullOrEmpty(upgrade.DisplayName) ? string.Empty : upgrade.DisplayName.Substring(0, 1);

            costText.text = NumberFormatter.Format(upgrade.Cost);
            costText.color = canAfford ? affordableCostColor : expensiveCostColor;
        }

        /// <summary>그림 뒤 칸을 등급 색으로 칠하고, 높은 등급이면 뒤에서 빛나게 한다.</summary>
        public void SetRarity(Color color, bool shine)
        {
            if (rarityTile != null) rarityTile.color = color;
            if (rarityGlow == null) return;
            rarityGlow.gameObject.SetActive(shine);
            if (shine) rarityGlow.SetColor(color);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) onHover?.Invoke(-1, null);
            gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData) => onHover?.Invoke(index, (RectTransform)transform);
        public void OnPointerExit(PointerEventData eventData) => onHover?.Invoke(-1, null);
    }
}
