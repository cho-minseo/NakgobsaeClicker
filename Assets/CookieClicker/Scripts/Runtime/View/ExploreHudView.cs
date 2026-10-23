using CookieClicker.Core;
using CookieClicker.Decor;
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
        [SerializeField, TextArea] private string walkHint = "WASD 이동 · Shift 달리기 · Space 점프 · 클릭 주먹 · V 시점 · B 꾸미기 · Esc 돌아가기";
        [SerializeField, TextArea] private string swimHint = "바라보는 방향으로 헤엄 · Space 위로 · C 잠수 · Shift 빠르게 · Esc 돌아가기";
        [SerializeField, TextArea] private string walkHintEn = "WASD Move · Shift Run · Space Jump · Click Punch · V View · B Decorate · Esc Back";
        [SerializeField, TextArea] private string swimHintEn = "Swim where you look · Space Up · C Dive · Shift Faster · Esc Back";
        [Header("꾸미기")]
        [SerializeField] private DecorBuilder decor;
        [SerializeField, TextArea] private string decorHint = "좌클릭 놓기 · 우클릭 회수 · R 돌리기 · 휠 고르기 · Tab 분류 · C 색 · G 칠하기 · B 끝내기";
        [SerializeField, TextArea] private string decorHintEn = "LMB Place · RMB Remove · R Rotate · Wheel Select · Tab Category · C Color · G Paint · B Done";
        [SerializeField] private Color breathColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] private Color breathLowColor = new Color(1f, 0.4f, 0.35f);

        private void Update()
        {
            currencyText.text =
                $"{NumberFormatter.Format(game.State.Cookies)} {game.Config.CurrencyName}   {Loc.T("초당", "per second:")} {NumberFormatter.Format(game.CookiesPerSecond, true)}";

            lookInfoText.text = !IsUnderwater() && TryGetLookedPlot(out var plot)
                ? $"{plot.Building.DisplayName} x{plot.Count}\n<size=20>{plot.Building.Description}</size>"
                : string.Empty;

            UpdateSwimUI();
        }

        private bool IsUnderwater() => player != null && player.IsHeadUnderwater;

        private void UpdateSwimUI()
        {
            if (player == null) return;

            string hint = player.IsSwimming ? Loc.T(swimHint, swimHintEn)
                : decor != null && decor.IsBuilding ? Loc.T(decorHint, decorHintEn)
                : Loc.T(walkHint, walkHintEn);
            if (hintText != null && hintText.text != hint) hintText.text = hint;

            if (breath == null) return;
            // 산소 게이지는 물속에 있거나 아직 다 차지 않았을 때만 보인다.
            bool showBreath = player.IsHeadUnderwater || breath.Breath01 < 0.999f;
            if (breathRoot.activeSelf != showBreath) breathRoot.SetActive(showBreath);
            breathFill.fillAmount = breath.Breath01;
            breathFill.color = breath.Breath01 < 0.25f
                ? Color.Lerp(breathLowColor, breathColor, Mathf.PingPong(Time.time * 4f, 1f))
                : breathColor;

            if (alertText != null) alertText.text = breath.IsOutOfBreath ? Loc.T("숨 막혀! 수면으로!", "Out of air! Swim up!") : string.Empty;
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
