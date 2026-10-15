using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 설정 창 (소리 크기, 마우스 감도). 슬라이더를 움직이면 바로 적용되고, 닫을 때 기기에 저장한다.
    /// 값의 저장·적용은 GameSettings가 맡고, 이 창은 보여 주고 입력만 받는다.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValue;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValue;
        [SerializeField] private Button closeButton;

        public bool IsOpen => panel.activeSelf;

        private void Awake()
        {
            volumeSlider.onValueChanged.AddListener(v =>
            {
                GameSettings.Volume = v;
                Refresh();
            });
            sensitivitySlider.onValueChanged.AddListener(v =>
            {
                GameSettings.LookSensitivity = v;
                Refresh();
            });
            closeButton.onClick.AddListener(Close);
            panel.SetActive(false);
        }

        public void Open()
        {
            // 저장된 값으로 맞출 때는 onValueChanged가 다시 불리지 않게 한다.
            volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            sensitivitySlider.SetValueWithoutNotify(GameSettings.LookSensitivity);
            Refresh();
            panel.SetActive(true);
        }

        public void Close()
        {
            GameSettings.Save();
            panel.SetActive(false);
        }

        private void Refresh()
        {
            volumeValue.text = $"{Mathf.RoundToInt(GameSettings.Volume * 100f)}%";
            sensitivityValue.text = $"x{GameSettings.LookSensitivity:0.0}";
        }
    }
}
