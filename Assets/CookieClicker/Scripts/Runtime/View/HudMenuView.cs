using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>게임 화면 위쪽의 [통계] [설정] 버튼과 통계 창의 [닫기] 버튼을 연결한다.</summary>
    public class HudMenuView : MonoBehaviour
    {
        [SerializeField] private ResponsiveLayout layout;
        [SerializeField] private Button statsButton;
        [SerializeField] private Button closeStatsButton;
        [SerializeField] private Button settingsButton;
        [Tooltip("타이틀 화면과 같은 설정 창을 쓴다")]
        [SerializeField] private SettingsView settings;

        private void Start()
        {
            statsButton.onClick.AddListener(layout.ToggleStats);
            if (closeStatsButton != null) closeStatsButton.onClick.AddListener(layout.CloseStats);
            if (settings != null) settingsButton.onClick.AddListener(settings.Open);
            else settingsButton.gameObject.SetActive(false);
        }
    }
}
