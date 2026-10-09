using CookieClicker.Core;
using CookieClicker.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>구경하기 화면의 UI. 보유량과, 화면 가운데로 바라보고 있는 건물 정보를 보여 준다.</summary>
    public class ExploreHudView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Text currencyText;
        [SerializeField] private Text lookInfoText;
        [SerializeField] private float lookDistance = 40f;

        private void Update()
        {
            currencyText.text =
                $"{NumberFormatter.Format(game.State.Cookies)} {game.Config.CurrencyName}   초당 {NumberFormatter.Format(game.CookiesPerSecond, true)}";

            lookInfoText.text = TryGetLookedPlot(out var plot)
                ? $"{plot.Building.DisplayName} x{plot.Count}\n<size=20>{plot.Building.Description}</size>"
                : string.Empty;
        }

        private bool TryGetLookedPlot(out BuildingPlot plot)
        {
            plot = null;
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, lookDistance, ~0, QueryTriggerInteraction.Ignore)) return false;
            plot = hit.collider.GetComponentInParent<BuildingPlot>();
            return plot != null;
        }
    }
}
