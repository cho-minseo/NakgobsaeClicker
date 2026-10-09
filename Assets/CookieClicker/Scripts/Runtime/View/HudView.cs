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

        private string lastCookieLabel;
        private string lastCpsLabel;

        private void Update()
        {
            // 매 프레임 문자열을 새로 대입하면 UI가 매번 다시 그려지므로, 표시가 바뀔 때만 갱신한다.
            string cookieLabel = $"{NumberFormatter.Format(game.State.Cookies)} 쿠키";
            if (cookieLabel != lastCookieLabel)
            {
                cookieCountText.text = cookieLabel;
                lastCookieLabel = cookieLabel;
            }

            string cpsLabel = $"초당 {NumberFormatter.Format(game.CookiesPerSecond, true)}";
            if (cpsLabel != lastCpsLabel)
            {
                cpsText.text = cpsLabel;
                lastCpsLabel = cpsLabel;
            }

            buffText.text = game.IsFrenzy
                ? $"프렌지! 생산량 x{game.Config.FrenzyMultiplier:0} ({Mathf.CeilToInt(game.FrenzyTimeLeft)}초)"
                : string.Empty;
        }
    }
}
