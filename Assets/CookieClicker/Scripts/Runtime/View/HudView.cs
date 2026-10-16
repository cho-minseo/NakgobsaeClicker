using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>왼쪽 상단의 보유 쿠키 수, 초당 생산량, 버프 표시.</summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Text cookieCountText;
        [SerializeField] private Text cpsText;
        [SerializeField] private Text buffText;

        // 영어("3.55 million Nakgopsae")처럼 길어지면 칸을 넘치지 않게 글자를 줄인다 (원래 크기가 최대).
        private void Awake()
        {
            foreach (var text in new[] { cookieCountText, cpsText })
            {
                if (text == null) continue;
                text.resizeTextMaxSize = text.fontSize;
                text.resizeTextMinSize = Mathf.Min(18, text.fontSize);
                text.resizeTextForBestFit = true;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private string lastCookieLabel;
        private string lastCpsLabel;

        private void Update()
        {
            // 매 프레임 문자열을 새로 대입하면 UI가 매번 다시 그려지므로, 표시가 바뀔 때만 갱신한다.
            string cookieLabel = $"{NumberFormatter.Format(game.State.Cookies)} {game.Config.CurrencyName}";
            if (cookieLabel != lastCookieLabel)
            {
                cookieCountText.text = cookieLabel;
                lastCookieLabel = cookieLabel;
            }

            string cpsLabel = Loc.T($"초당 {NumberFormatter.Format(game.CookiesPerSecond, true)}", $"per second: {NumberFormatter.Format(game.CookiesPerSecond, true)}");
            if (cpsLabel != lastCpsLabel)
            {
                cpsText.text = cpsLabel;
                lastCpsLabel = cpsLabel;
            }

            buffText.text = game.IsFrenzy
                ? Loc.T($"가보자고! 생산량 x{game.Config.FrenzyMultiplier:0} ({Mathf.CeilToInt(game.FrenzyTimeLeft)}초)",
                    $"Let's go! Production x{game.Config.FrenzyMultiplier:0} ({Mathf.CeilToInt(game.FrenzyTimeLeft)}s)")
                : string.Empty;
        }
    }
}
