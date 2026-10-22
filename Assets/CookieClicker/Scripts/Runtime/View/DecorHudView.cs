using CookieClicker.Core;
using CookieClicker.Decor;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 꾸미기 모드 화면: 위쪽 "구조물 / 가구" 탭, 아래 핫바(물건 칸)와 그 위 상태 문구.
    /// 조작 안내는 ExploreHudView의 맨 아래 줄이 바꿔 보여 준다.
    /// 칸은 에디터 도구가 목록 개수만큼 미리 만들어 두고, 여기서는 고른 탭의 칸만 켜고 내용·상태를 바꾼다.
    /// </summary>
    public class DecorHudView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [Tooltip("꾸미기 중에만 켜지는 묶음 (탭, 핫바, 안내)")]
        [SerializeField] private GameObject buildRoot;
        [SerializeField] private DecorSlotView[] slots = new DecorSlotView[0];
        [SerializeField] private Text statusText;
        [Tooltip("부지 위에 서 있을 때 뜨는 \"[B] 꾸미기\" 안내")]
        [SerializeField] private GameObject prompt;
        [Tooltip("DecorCategory 순서대로 (구조물, 가구)")]
        [SerializeField] private Image[] tabBackgrounds = new Image[0];
        [SerializeField] private Text[] tabLabels = new Text[0];
        [SerializeField] private Color statusColor = new Color(0.23f, 0.14f, 0.094f);
        [SerializeField] private Color errorColor = new Color(0.886f, 0.25f, 0.165f);
        [SerializeField] private Color tabOnColor = Color.white;
        [SerializeField] private Color tabOffColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color tabOnText = new Color(0.886f, 0.25f, 0.165f);
        [SerializeField] private Color tabOffText = Color.white;

        private DecorCatalog catalog;
        private DecorCategory category;
        private int selected = -1;

        public void Bind(DecorCatalog decorCatalog)
        {
            catalog = decorCatalog;
            RefreshSlots();
        }

        public void Show(bool building)
        {
            buildRoot.SetActive(building);
            // 언어를 바꿨을 수 있으니 열 때마다 이름을 다시 쓴다.
            if (building && catalog != null) RefreshSlots();
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
            for (int i = 0; i < slots.Length && i < catalog.Pieces.Count; i++)
            {
                if (slots[i].gameObject.activeSelf)
                    slots[i].Show(i == selected, game.State.Cookies >= catalog.Pieces[i].Cost);
            }
        }
    }
}
