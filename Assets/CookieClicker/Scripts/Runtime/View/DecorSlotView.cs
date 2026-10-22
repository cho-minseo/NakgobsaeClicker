using CookieClicker.Core;
using CookieClicker.Decor;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>꾸미기 핫바의 한 칸: 그림, 이름, 가격, 단축키. 고른 칸은 흰 카드에 빨간 테두리, 못 사는 칸은 흐리게.</summary>
    public class DecorSlotView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Outline highlight;
        [SerializeField] private Image icon;
        [SerializeField] private Text nameText;
        [SerializeField] private Text costText;
        [SerializeField] private Text keyText;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Color normalColor = new Color(0.992f, 0.965f, 0.91f);
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float unaffordableAlpha = 0.55f;

        public void Bind(DecorPiece piece, int index)
        {
            nameText.text = piece.DisplayName;
            costText.text = NumberFormatter.Format(piece.Cost);
            keyText.text = index < 9 ? (index + 1).ToString() : string.Empty;
            icon.sprite = piece.Icon;
            icon.enabled = piece.Icon != null;
        }

        public void Show(bool selected, bool affordable)
        {
            background.color = selected ? selectedColor : normalColor;
            highlight.enabled = selected;
            group.alpha = affordable ? 1f : unaffordableAlpha;
        }
    }
}
