using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>저장 / 초기화 버튼. 초기화는 실수로 누르지 않도록 두 번 눌러야 실행된다.</summary>
    public class MenuView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text resetButtonLabel;
        [SerializeField] private Text noticeText;
        [SerializeField] private float confirmWindow = 3f;
        [SerializeField] private float noticeDuration = 2f;

        private static string ResetLabel => Loc.T("초기화", "Reset");
        private static string ConfirmLabel => Loc.T("정말요?", "Sure?");

        private float confirmTimer;
        private float noticeTimer;

        private void Start()
        {
            saveButton.onClick.AddListener(game.SaveGame);
            resetButton.onClick.AddListener(OnResetClicked);
            game.Saved += OnSaved;
            GameSettings.Changed += OnSettingsChanged;
            resetButtonLabel.text = ResetLabel;
            noticeText.text = string.Empty;
        }

        private void OnDestroy()
        {
            if (game != null) game.Saved -= OnSaved;
            GameSettings.Changed -= OnSettingsChanged;
        }

        // 언어를 바꾸면 버튼 글자도 바꾼다 (확인 대기 중이면 그대로 둔다).
        private void OnSettingsChanged()
        {
            if (confirmTimer <= 0f) resetButtonLabel.text = ResetLabel;
        }

        private void Update()
        {
            if (confirmTimer > 0f)
            {
                confirmTimer -= Time.deltaTime;
                if (confirmTimer <= 0f) resetButtonLabel.text = ResetLabel;
            }
            if (noticeTimer > 0f)
            {
                noticeTimer -= Time.deltaTime;
                if (noticeTimer <= 0f) noticeText.text = string.Empty;
            }
        }

        private void OnResetClicked()
        {
            if (confirmTimer <= 0f)
            {
                confirmTimer = confirmWindow;
                resetButtonLabel.text = ConfirmLabel;
                return;
            }

            confirmTimer = 0f;
            resetButtonLabel.text = ResetLabel;
            game.ResetProgress();
            ShowNotice(Loc.T("처음부터 다시 시작합니다.", "Starting over from scratch."));
        }

        private void OnSaved() => ShowNotice(Loc.T("저장했습니다.", "Saved."));

        private void ShowNotice(string message)
        {
            noticeText.text = message;
            noticeTimer = noticeDuration;
        }
    }
}
