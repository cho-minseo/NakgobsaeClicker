using System;
using CookieClicker.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>RectTransform 하나의 앵커/오프셋 값. 가로·세로 배치를 데이터로 저장하기 위해 쓴다.</summary>
    [Serializable]
    public struct RectLayout
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 offsetMin;
        public Vector2 offsetMax;

        public RectLayout(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            this.anchorMin = anchorMin;
            this.anchorMax = anchorMax;
            this.offsetMin = offsetMin;
            this.offsetMax = offsetMax;
        }

        public void ApplyTo(RectTransform target)
        {
            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.offsetMin = offsetMin;
            target.offsetMax = offsetMax;
        }
    }

    [Serializable]
    public class PanelLayout
    {
        public RectTransform target;
        public RectLayout landscape;
        public RectLayout portrait;
    }

    [Serializable]
    public struct CookieLayout
    {
        [Tooltip("화면 비율 좌표 (0,0 = 왼쪽 아래)")]
        public Vector2 viewportPosition;
        [Range(0.1f, 1f)] public float heightRatio;
        [Range(0.1f, 1f)] public float maxWidthRatio;
        [Tooltip("황금 낙곱새가 나타날 수 있는 범위 (화면 비율)")]
        public Rect goldenSpawnRect;
    }

    /// <summary>
    /// 화면이 가로인지 세로인지 보고 패널 배치를 바꾼다.
    /// 배치값은 코드가 아니라 Inspector의 데이터라서, 디자인을 바꿀 때 코드를 고칠 필요가 없다.
    /// 통계는 평소에 닫혀 있다가 [통계] 버튼으로 여닫는 창이다. 세로 화면에서는 상점과 통계를 탭으로 전환한다.
    /// </summary>
    public class ResponsiveLayout : MonoBehaviour
    {
        [SerializeField] private PanelLayout[] panels = Array.Empty<PanelLayout>();

        [Header("클릭 대상(냄비) 위치")]
        [SerializeField] private CookieClickTarget cookie;
        [SerializeField] private GoldenCookieView goldenCookie;
        [Tooltip("클릭 대상 위치를 따라다니는 장식 (후광 등). 앵커가 그 화면 좌표로 맞춰진다.")]
        [SerializeField] private RectTransform[] followCookie = Array.Empty<RectTransform>();
        [SerializeField] private CookieLayout landscapeCookie;
        [SerializeField] private CookieLayout portraitCookie;

        [Header("세로 화면 탭")]
        [SerializeField] private GameObject tabBar;
        [SerializeField] private Button storeTabButton;
        [SerializeField] private Button statsTabButton;
        [SerializeField] private GameObject storePanel;
        [SerializeField] private GameObject statsPanel;
        [SerializeField] private Color selectedTabColor = Color.white;
        [SerializeField] private Color normalTabColor = new Color(0.95f, 0.9f, 0.8f);

        public bool IsPortrait { get; private set; }

        private bool applied;
        private bool showingStats;

        private void Start()
        {
            storeTabButton.onClick.AddListener(() => SelectTab(false));
            statsTabButton.onClick.AddListener(() => SelectTab(true));
            Apply(Screen.height > Screen.width);
        }

        private void Update()
        {
            // 창 크기 변경·기기 회전을 감지한다. 바뀌었을 때만 다시 배치한다.
            bool portrait = Screen.height > Screen.width;
            if (!applied || portrait != IsPortrait) Apply(portrait);
        }

        public void Apply(bool portrait)
        {
            applied = true;
            IsPortrait = portrait;

            foreach (var panel in panels)
            {
                if (panel.target != null) (portrait ? panel.portrait : panel.landscape).ApplyTo(panel.target);
            }

            var layout = CurrentCookieLayout;
            if (!CookieLayoutOverridden) PlaceCookie(layout.viewportPosition, layout.heightRatio, layout.maxWidthRatio);
            if (goldenCookie != null) goldenCookie.SpawnRect = layout.goldenSpawnRect;

            if (tabBar != null) tabBar.SetActive(portrait);
            RefreshTabs();
        }

        /// <summary>지금 화면 방향(가로/세로)에서 게임 중 쿠키가 있어야 할 자리.</summary>
        public CookieLayout CurrentCookieLayout => IsPortrait ? portraitCookie : landscapeCookie;

        /// <summary>true인 동안은 화면을 돌려도 쿠키 자리를 건드리지 않는다 (타이틀 화면이 쿠키를 가운데로 옮겨 쓰는 동안).</summary>
        public bool CookieLayoutOverridden { get; set; }

        /// <summary>쿠키와 그 뒤의 후광(followCookie)을 화면의 한 자리로 함께 옮긴다.</summary>
        public void PlaceCookie(Vector2 viewportPosition, float heightRatio, float maxWidthRatio)
        {
            if (cookie != null) cookie.SetScreenLayout(viewportPosition, heightRatio, maxWidthRatio);
            foreach (var rt in followCookie)
            {
                if (rt == null) continue;
                rt.anchorMin = rt.anchorMax = viewportPosition;
                rt.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>통계 창을 열거나 닫는다 ([통계] 버튼, 창의 [닫기] 버튼).</summary>
        public void ToggleStats() => SelectTab(!showingStats);

        public void CloseStats() => SelectTab(false);

        private void SelectTab(bool stats)
        {
            showingStats = stats;
            RefreshTabs();
        }

        private void RefreshTabs()
        {
            // 가로: 상점은 늘 보이고 통계는 연 동안만. 세로: 둘 중 하나만.
            storePanel.SetActive(!IsPortrait || !showingStats);
            statsPanel.SetActive(showingStats);
            storeTabButton.image.color = showingStats ? normalTabColor : selectedTabColor;
            statsTabButton.image.color = showingStats ? selectedTabColor : normalTabColor;
        }

        [ContextMenu("미리보기: 가로 배치")]
        private void PreviewLandscape() => Apply(false);

        [ContextMenu("미리보기: 세로 배치")]
        private void PreviewPortrait() => Apply(true);

#if UNITY_EDITOR
        public void EditorConfigure(PanelLayout[] newPanels, CookieClickTarget newCookie, GoldenCookieView newGolden,
            RectTransform[] newFollowCookie, CookieLayout landscape, CookieLayout portrait,
            GameObject newTabBar, Button storeTab, Button statsTab, GameObject newStorePanel, GameObject newStatsPanel)
        {
            panels = newPanels;
            cookie = newCookie;
            goldenCookie = newGolden;
            followCookie = newFollowCookie;
            landscapeCookie = landscape;
            portraitCookie = portrait;
            tabBar = newTabBar;
            storeTabButton = storeTab;
            statsTabButton = statsTab;
            storePanel = newStorePanel;
            statsPanel = newStatsPanel;
        }
#endif
    }
}
