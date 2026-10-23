using CookieClicker.Core;
using CookieClicker.Decor;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 꾸미기 모드 화면: 위쪽 분류 탭(구조물·의자·테이블·가전·소품), 아래 물건 줄, 그 위 상태 문구와 색 팔레트.
    /// 물건 줄은 고른 탭의 물건만 보이고, 휠로 고르면 고른 칸이 가운데 오도록 옆으로 미끄러진다(스크롤).
    /// 칸·탭·색 견본은 에디터 도구가 미리 만들어 두고, 여기서는 켜고 끄고 색과 위치만 바꾼다.
    /// 조작 안내는 ExploreHudView의 맨 아래 줄이 바꿔 보여 준다.
    /// </summary>
    public class DecorHudView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [Tooltip("꾸미기 중에만 켜지는 묶음 (탭, 물건 줄, 안내)")]
        [SerializeField] private GameObject buildRoot;
        [SerializeField] private DecorSlotView[] slots = new DecorSlotView[0];
        [Tooltip("물건 칸들이 들어 있는 줄. 고른 칸이 가운데 오도록 이 줄을 옆으로 움직인다")]
        [SerializeField] private RectTransform slotStrip;
        [Tooltip("물건 줄이 보이는 창 (이 폭의 가운데에 고른 칸이 온다)")]
        [SerializeField] private RectTransform slotViewport;
        [SerializeField] private float slotPitch = 112f;
        [SerializeField] private float scrollSpeed = 14f;
        [SerializeField] private Text statusText;
        [Tooltip("부지 위에 서 있을 때 뜨는 \"[B] 꾸미기\" 안내")]
        [SerializeField] private GameObject prompt;
        [Tooltip("DecorCategory 순서대로")]
        [SerializeField] private Image[] tabBackgrounds = new Image[0];
        [SerializeField] private Text[] tabLabels = new Text[0];

        [Header("색 팔레트")]
        [Tooltip("첫 칸은 '원래 색', 그 뒤는 DecorCatalog 팔레트 순서")]
        [SerializeField] private Image[] paintSwatches = new Image[0];
        [SerializeField] private Outline[] paintHighlights = new Outline[0];

        [Header("색상")]
        [SerializeField] private Color statusColor = new Color(0.23f, 0.14f, 0.094f);
        [SerializeField] private Color errorColor = new Color(0.886f, 0.25f, 0.165f);
        [SerializeField] private Color tabOnColor = Color.white;
        [SerializeField] private Color tabOffColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color tabOnText = new Color(0.886f, 0.25f, 0.165f);
        [SerializeField] private Color tabOffText = Color.white;

        private DecorCatalog catalog;
        private DecorCategory category;
        private int selected = -1;
        private int paintColor = -1;
        private float stripX;
        private bool snapStrip = true;

        public void Bind(DecorCatalog decorCatalog)
        {
            catalog = decorCatalog;
            for (int i = 0; i < paintSwatches.Length; i++)
            {
                var color = catalog.PaintColor(i - 1);
                paintSwatches[i].gameObject.SetActive(i == 0 || color != null);
                if (color != null) paintSwatches[i].color = color.Value;
            }
            RefreshSlots();
            SetPaintColor(paintColor);
        }

        public void Show(bool building)
        {
            buildRoot.SetActive(building);
            // 언어를 바꿨을 수 있으니 열 때마다 이름을 다시 쓴다.
            if (building && catalog != null) RefreshSlots();
            snapStrip = true;
        }

        public void ShowPrompt(bool visible)
        {
            if (prompt.activeSelf != visible) prompt.SetActive(visible);
        }

        public void SetSelected(int index) => selected = index;

        public void SetCategory(DecorCategory value)
        {
            if (category == value) return;
            category = value;
            RefreshSlots();
            snapStrip = true;
        }

        /// <summary>-1이면 '원래 색' 칸이 골라진다.</summary>
        public void SetPaintColor(int index)
        {
            paintColor = index;
            for (int i = 0; i < paintHighlights.Length; i++)
            {
                bool on = i == index + 1;
                paintHighlights[i].enabled = on;
                paintSwatches[i].rectTransform.localScale = Vector3.one * (on ? 1.2f : 1f);
            }
        }

        public void SetStatus(string message, bool error)
        {
            statusText.text = message;
            statusText.color = error ? errorColor : statusColor;
        }

        /// <summary>고른 탭의 물건만 켜고, 숫자키 번호는 탭 안에서 1부터 매긴다.</summary>
        private void RefreshSlots()
        {
            int key = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                bool show = i < catalog.Pieces.Count && catalog.Pieces[i].Category == category;
                slots[i].gameObject.SetActive(show);
                if (show) slots[i].Bind(catalog.Pieces[i], key++);
            }
            if (slotStrip != null) slotStrip.sizeDelta = new Vector2(key * slotPitch, slotStrip.sizeDelta.y);
            for (int t = 0; t < tabBackgrounds.Length; t++)
            {
                bool on = t == (int)category;
                tabBackgrounds[t].color = on ? tabOnColor : tabOffColor;
                tabLabels[t].color = on ? tabOnText : tabOffText;
            }
        }

        private void Update()
        {
            if (catalog == null || !buildRoot.activeSelf) return;
            int position = 0, count = 0;
            for (int i = 0; i < slots.Length && i < catalog.Pieces.Count; i++)
            {
                if (!slots[i].gameObject.activeSelf) continue;
                if (i == selected) position = count;
                count++;
                slots[i].Show(i == selected, game.State.Cookies >= catalog.Pieces[i].Cost);
            }
            ScrollTo(position, count);
        }

        /// <summary>고른 칸이 창 가운데 오도록 줄을 민다. 줄이 창보다 짧으면 그냥 가운데 정렬.</summary>
        private void ScrollTo(int position, int count)
        {
            if (slotStrip == null || slotViewport == null) return;
            float viewWidth = slotViewport.rect.width;
            float stripWidth = count * slotPitch;
            float target = stripWidth <= viewWidth
                ? (viewWidth - stripWidth) * 0.5f
                : Mathf.Clamp(viewWidth * 0.5f - (position + 0.5f) * slotPitch, viewWidth - stripWidth, 0f);
            stripX = snapStrip ? target : Mathf.Lerp(stripX, target, 1f - Mathf.Exp(-scrollSpeed * Time.unscaledDeltaTime));
            snapStrip = false;
            slotStrip.anchoredPosition = new Vector2(stripX, slotStrip.anchoredPosition.y);
        }
    }
}
