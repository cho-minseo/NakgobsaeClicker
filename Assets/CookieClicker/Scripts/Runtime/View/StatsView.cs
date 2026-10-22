using System.Text;
using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>통계 창: 왼쪽에 기록, 오른쪽(productionText)에 건물별 생산 비중. productionText가 없으면 한 칸에 이어서 쓴다.</summary>
    public class StatsView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Text statsText;
        [Tooltip("건물별 생산량을 따로 보여 줄 칸 (비워 두면 statsText 아래에 이어서)")]
        [SerializeField] private Text productionText;
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
            string currency = config.CurrencyName;

            builder.Clear();
            builder.AppendLine($"{Loc.T($"총 만든 {currency}", $"Total {currency} made")}   <b>{NumberFormatter.Format(state.TotalBaked)}</b>");
            builder.AppendLine($"{Loc.T($"직접 저어서 만든 {currency}", $"{currency} stirred by hand")}   <b>{NumberFormatter.Format(state.HandmadeCookies)}</b>");
            builder.AppendLine($"{Loc.T("냄비 저은 횟수", "Pot stirs")}   <b>{state.Clicks:#,0}</b>");
            builder.AppendLine($"{Loc.T("한 번 저을 때", "Per stir")}   <b>{NumberFormatter.Format(game.CookiesPerClick, true)}</b>");
            builder.AppendLine($"{Loc.T($"황금 {currency} 획득", $"Golden {currency} caught")}   <b>{state.GoldenClicks}</b>");
            builder.AppendLine($"{Loc.T("플레이 시간", "Play time")}   <b>{NumberFormatter.FormatTime(state.PlayTimeSeconds)}</b>");
            if (productionText != null)
            {
                statsText.text = builder.ToString();
                builder.Clear();
            }
            else builder.AppendLine();
            builder.AppendLine($"<b><size=26>{Loc.T("건물별 생산량", "Production by building")}</size></b>");

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
                    $"{Loc.T($"초당 {NumberFormatter.Format(production.BuildingCps[i], true)}", $"{NumberFormatter.Format(production.BuildingCps[i], true)}/s")}  <color=#aaaaaa>({share:P0})</color>");
            }
            if (!any) builder.AppendLine($"<color=#aaaaaa>{Loc.T($"아직 건물이 없어요. 냄비를 저어서 {config.Buildings[0].DisplayName}부터 사 보세요!", $"No buildings yet. Stir the pot and buy an {config.Buildings[0].DisplayName} first!")}</color>");

            (productionText != null ? productionText : statsText).text = builder.ToString();
        }
    }
}
