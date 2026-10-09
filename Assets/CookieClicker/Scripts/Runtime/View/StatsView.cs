using System.Text;
using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>가운데 패널의 통계와 건물별 생산 비중.</summary>
    public class StatsView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Text statsText;
        [SerializeField] private float refreshInterval = 0.5f;

        private readonly StringBuilder builder = new StringBuilder(1024);
        private float timer;

        private void Start() => Refresh();

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < refreshInterval) return;
            timer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            var state = game.State;
            var config = game.Config;
            var production = game.Production;

            builder.Clear();
            builder.AppendLine($"총 구운 쿠키   <b>{NumberFormatter.Format(state.TotalBaked)}</b>");
            builder.AppendLine($"직접 클릭한 쿠키   <b>{NumberFormatter.Format(state.HandmadeCookies)}</b>");
            builder.AppendLine($"클릭 횟수   <b>{state.Clicks:#,0}</b>");
            builder.AppendLine($"클릭당 쿠키   <b>{NumberFormatter.Format(game.CookiesPerClick, true)}</b>");
            builder.AppendLine($"황금 쿠키 클릭   <b>{state.GoldenClicks}</b>");
            builder.AppendLine($"플레이 시간   <b>{NumberFormatter.FormatTime(state.PlayTimeSeconds)}</b>");
            builder.AppendLine();
            builder.AppendLine("<b><size=26>건물별 생산량</size></b>");

            bool any = false;
            for (int i = 0; i < config.Buildings.Count; i++)
            {
                int count = state.BuildingCounts[i];
                if (count == 0) continue;
                any = true;

                double share = production.RawCps > 0 ? production.BuildingCps[i] / production.RawCps : 0;
                string color = ColorUtility.ToHtmlStringRGB(config.Buildings[i].IconColor);
                builder.AppendLine(
                    $"<color=#{color}>■</color> {config.Buildings[i].DisplayName} x{count}   " +
                    $"초당 {NumberFormatter.Format(production.BuildingCps[i], true)}  <color=#aaaaaa>({share:P0})</color>");
            }
            if (!any) builder.AppendLine("<color=#aaaaaa>아직 건물이 없어요. 쿠키를 클릭해서 커서를 사 보세요!</color>");

            statsText.text = builder.ToString();
        }
    }
}
