using System;
using CookieClicker.Core;
using CookieClicker.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 월드에 놓인 건물 자리 하나. 한 개도 안 샀으면 빈 땅, 사면 건물이 보이고, 개수가 늘면 단계별 장식이 켜진다.
    /// 인덱스 대신 BuildingDefinition을 직접 참조해서, 상점 순서를 바꿔도 자리가 엉키지 않는다.
    /// </summary>
    public class BuildingPlot : MonoBehaviour
    {
        [Serializable]
        public class Tier
        {
            [Min(1)] public int minCount = 1;
            public GameObject visual;
        }

        [SerializeField] private CookieGame game;
        [SerializeField] private BuildingDefinition building;
        [Tooltip("하나라도 사면 켜지는 루트 (건물 본체 + 간판 + 장식)")]
        [SerializeField] private GameObject builtRoot;
        [SerializeField] private Tier[] tiers = Array.Empty<Tier>();
        [SerializeField] private Text signText;

        public BuildingDefinition Building => building;
        public int Count { get; private set; }

        private void OnEnable()
        {
            game.BuildingPurchased += OnBuildingPurchased;
            game.ProgressReset += Refresh;
            if (game.State != null) Refresh();
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            game.BuildingPurchased -= OnBuildingPurchased;
            game.ProgressReset -= Refresh;
        }

        private void OnBuildingPurchased(int _) => Refresh();

        public void Refresh()
        {
            int index = game.Config.IndexOf(building);
            Count = index >= 0 ? game.State.BuildingCounts[index] : 0;

            builtRoot.SetActive(Count > 0);
            foreach (var tier in tiers)
            {
                if (tier.visual != null) tier.visual.SetActive(Count >= tier.minCount);
            }
            if (signText != null) signText.text = $"{building.DisplayName}\nx{Count}";
        }
    }
}
