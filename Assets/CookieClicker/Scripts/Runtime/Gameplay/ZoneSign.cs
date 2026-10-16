using System;
using CookieClicker.Core;
using CookieClicker.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 지대 간판. 그 지대의 건물을 하나도 갖고 있지 않으면 "??? 지대"로 가려 두고,
    /// 하나라도 사면 진짜 이름을 보여 준다. 미리 이름을 알려 주지 않아 탐험하는 재미를 남긴다.
    /// </summary>
    public class ZoneSign : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Text label;
        [SerializeField] private string zoneName = "대창 지대";
        [SerializeField] private string hiddenName = "??? 지대";
        [SerializeField] private string zoneNameEn = "Daechang Zone";
        [SerializeField] private string hiddenNameEn = "??? Zone";
        [Tooltip("이 중 하나라도 보유하면 이름이 공개된다")]
        [SerializeField] private BuildingDefinition[] buildings = Array.Empty<BuildingDefinition>();

        public bool IsRevealed { get; private set; }

        private void OnEnable()
        {
            game.BuildingPurchased += OnBuildingPurchased;
            game.ProgressReset += Refresh;
            GameSettings.Changed += OnSettingsChanged;
            if (game.State != null) Refresh();
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            game.BuildingPurchased -= OnBuildingPurchased;
            game.ProgressReset -= Refresh;
            GameSettings.Changed -= OnSettingsChanged;
        }

        private void OnBuildingPurchased(int _) => Refresh();
        private void OnSettingsChanged()
        {
            if (game.State != null) Refresh();
        }

        public void Refresh()
        {
            IsRevealed = false;
            foreach (var building in buildings)
            {
                int index = game.Config.IndexOf(building);
                if (index >= 0 && game.State.BuildingCounts[index] > 0)
                {
                    IsRevealed = true;
                    break;
                }
            }
            label.text = IsRevealed ? Loc.T(zoneName, zoneNameEn) : Loc.T(hiddenName, hiddenNameEn);
        }
    }
}
