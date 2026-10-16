using System;
using CookieClicker.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 설정 창 (소리 크기, 마우스 감도, 언어, 클릭 대체 키).
    /// 바꾸는 즉시 미리 적용되지만(소리를 들어 보고 고를 수 있게), [저장]을 눌러야 기기에 남는다.
    /// [취소]는 창을 열 때의 값으로, [기본값]은 처음 값으로 되돌린다.
    /// 값의 저장·적용은 GameSettings가 맡고, 이 창은 보여 주고 입력만 받는다.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Text volumeValue;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValue;

        [Header("언어")]
        [SerializeField] private Button koreanButton;
        [SerializeField] private Button englishButton;

        [Header("클릭 대체 키")]
        [SerializeField] private Button clickKeyButton;
        [SerializeField] private Text clickKeyLabel;

        [Header("하단 버튼")]
        [SerializeField] private Button resetButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button saveButton;

        public bool IsOpen => panel.activeSelf;

        private IDisposable keyListener;

        private void Awake()
        {
            volumeSlider.onValueChanged.AddListener(v => GameSettings.Volume = v);
            sensitivitySlider.onValueChanged.AddListener(v => GameSettings.LookSensitivity = v);
            koreanButton.onClick.AddListener(() => GameSettings.Language = Language.Korean);
            englishButton.onClick.AddListener(() => GameSettings.Language = Language.English);
            clickKeyButton.onClick.AddListener(ListenForKey);
            resetButton.onClick.AddListener(ResetToDefaults);
            cancelButton.onClick.AddListener(Cancel);
            saveButton.onClick.AddListener(Save);
            panel.SetActive(false);
        }

        private void OnEnable() => GameSettings.Changed += Refresh;
        private void OnDisable()
        {
            GameSettings.Changed -= Refresh;
            StopListening();
        }

        public void Open()
        {
            GameSettings.Load(); // 혹시 남아 있는 미리보기 값이 있으면 저장된 값으로 맞추고 시작
            panel.SetActive(true);
            Refresh();
        }

        private void Save()
        {
            StopListening();
            GameSettings.Save();
            panel.SetActive(false);
        }

        private void Cancel()
        {
            StopListening();
            GameSettings.Load(); // 미리 적용했던 값을 저장된 값으로 되돌린다
            panel.SetActive(false);
        }

        private void ResetToDefaults()
        {
            StopListening();
            GameSettings.ResetToDefaults();
        }

        /// <summary>
        /// 다음에 누르는 키보드·게임패드 버튼을 클릭 대체 키로 쓴다. Esc는 취소.
        /// 마우스 버튼은 받지 않는다 (이 버튼을 누른 클릭이 그대로 잡히는 것을 막고, 마우스는 이미 클릭이므로).
        /// </summary>
        private void ListenForKey()
        {
            StopListening();
            clickKeyLabel.text = Loc.T("키를 누르세요… (Esc 취소)", "Press a key… (Esc to cancel)");
            keyListener = InputSystem.onAnyButtonPress
                .Where(control => control.device is Keyboard || control.device is Gamepad)
                .CallOnce(control =>
                {
                    keyListener = null;
                    if (control is UnityEngine.InputSystem.Controls.KeyControl key && key.keyCode == Key.Escape)
                    {
                        Refresh();
                        return;
                    }
                    // "/Keyboard/space" → "<Keyboard>/space" (기기 종류 기준이라 키보드를 바꿔 꽂아도 동작)
                    string relative = control.path.Substring(control.device.path.Length);
                    GameSettings.ClickKey = $"<{control.device.layout}>{relative}";
                });
        }

        private void StopListening()
        {
            keyListener?.Dispose();
            keyListener = null;
        }

        private void Refresh()
        {
            if (!IsOpen) return;
            // 저장된 값으로 맞출 때는 onValueChanged가 다시 불리지 않게 한다.
            volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            sensitivitySlider.SetValueWithoutNotify(GameSettings.LookSensitivity);
            volumeValue.text = $"{Mathf.RoundToInt(GameSettings.Volume * 100f)}%";
            sensitivityValue.text = $"x{GameSettings.LookSensitivity:0.0}";

            // 지금 고른 언어는 눌린 상태(비활성)로 보인다.
            koreanButton.interactable = GameSettings.Language != Language.Korean;
            englishButton.interactable = GameSettings.Language != Language.English;

            if (keyListener == null)
            {
                clickKeyLabel.text = InputControlPath.ToHumanReadableString(GameSettings.ClickKey,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }
    }
}
