using CookieClicker.Core;
using CookieClicker.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>구경하기 화면의 UI. 보유량, 바라보는 건물 정보, 산소 게이지, 상황별 조작 안내를 보여 준다.</summary>
    public class ExploreHudView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Text currencyText;
        [SerializeField] private Text lookInfoText;
        [SerializeField] private float lookDistance = 40f;

        [Header("수영")]
        [SerializeField] private FirstPersonController player;
        [SerializeField] private PlayerBreath breath;
        [SerializeField] private GameObject breathRoot;
        [SerializeField] private Image breathFill;
        [SerializeField] private Text alertText;
        [SerializeField] private Text hintText;
        [SerializeField, TextArea] private string walkHint = "WASD 이동 · Shift 달리기(주먹 쥐고 뛰기) · Space 점프 · 클릭 주먹 · Esc 돌아가기";
        [SerializeField, TextArea] private string swimHint = "바라보는 방향으로 헤엄 · Space 위로 · C 잠수 · Shift 빠르게 · Esc 돌아가기";
        [SerializeField] private Color breathColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] private Color breathLowColor = new Color(1f, 0.4f, 0.35f);

        private void Update()
        {
            currencyText.text =
                $"{NumberFormatter.Format(game.State.Cookies)} {game.Config.CurrencyName}   초당 {NumberFormatter.Format(game.CookiesPerSecond, true)}";

            lookInfoText.text = !IsUnderwater() && TryGetLookedPlot(out var plot)
                ? $"{plot.Building.DisplayName} x{plot.Count}\n<size=20>{plot.Building.Description}</size>"
                : string.Empty;

            UpdateSwimUI();
        }

        private bool IsUnderwater() => player != null && player.IsHeadUnderwater;

        private void UpdateSwimUI()
        {
            if (player == null) return;

            string hint = player.IsSwimming ? swimHint : walkHint;
            if (hintText != null && hintText.text != hint) hintText.text = hint;

            if (breath == null) return;
            // 산소 게이지는 물속에 있거나 아직 다 차지 않았을 때만 보인다.
            bool showBreath = player.IsHeadUnderwater || breath.Breath01 < 0.999f;
            if (breathRoot.activeSelf != showBreath) breathRoot.SetActive(showBreath);
            breathFill.fillAmount = breath.Breath01;
            breathFill.color = breath.Breath01 < 0.25f
                ? Color.Lerp(breathLowColor, breathColor, Mathf.PingPong(Time.time * 4f, 1f))
                : breathColor;

            if (alertText != null) alertText.text = breath.IsOutOfBreath ? "숨 막혀! 수면으로!" : string.Empty;
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
