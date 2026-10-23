using System;
using System.Collections.Generic;
using UnityEngine;

namespace CookieClicker.Data
{
    /// <summary>
    /// 게임 전체 밸런스와 콘텐츠 목록. 기획 수치는 전부 여기 모여 있어서 코드를 건드리지 않고 조정할 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Cookie Clicker/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("콘텐츠 (상점에 표시되는 순서)")]
        [SerializeField] private BuildingDefinition[] buildings = Array.Empty<BuildingDefinition>();
        [SerializeField] private UpgradeDefinition[] upgrades = Array.Empty<UpgradeDefinition>();

        [Header("표시")]
        [Tooltip("모으는 재화 이름. UI 문구 전체에 쓰인다.")]
        [SerializeField] private string currencyName = "낙곱새";
        [SerializeField] private string currencyNameEn = "Nakgopsae";

        [Header("경제")]
        [Tooltip("건물을 하나 살 때마다 다음 가격에 곱해지는 값")]
        [SerializeField] private float costGrowth = 1.15f;
        [Tooltip("누적 생산량이 '건물 가격 x 이 값'을 넘으면 상점에 공개")]
        [SerializeField, Range(0f, 1f)] private float revealCostRatio = 0.5f;

        [Header("황금 낙곱새")]
        [SerializeField] private Vector2 goldenSpawnInterval = new Vector2(60f, 150f);
        [SerializeField] private float goldenLifetime = 13f;
        [SerializeField] private float frenzyDuration = 77f;
        [SerializeField] private float frenzyMultiplier = 7f;
        [Tooltip("럭키: 보유량의 몇 %를 줄지")]
        [SerializeField, Range(0f, 1f)] private float luckyBankRatio = 0.15f;
        [Tooltip("럭키: 최대 몇 초 분량의 생산량까지 줄지")]
        [SerializeField] private float luckyMaxCpsSeconds = 900f;

        [Header("상점 등급 색 (일반·고급·희귀·영웅·전설)")]
        [SerializeField] private Color[] rarityColors =
        {
            new Color(0.80f, 0.70f, 0.56f), // 일반: 베이지
            new Color(0.40f, 0.76f, 0.33f), // 고급: 초록
            new Color(0.27f, 0.58f, 0.96f), // 희귀: 파랑
            new Color(0.66f, 0.38f, 0.93f), // 영웅: 보라
            new Color(1.00f, 0.74f, 0.16f), // 전설: 금
        };
        [Tooltip("건물을 이 개수 이상 가지면 그 등급 (일반·고급·희귀·영웅·전설 순)")]
        [SerializeField] private int[] buildingRarityCounts = { 0, 10, 25, 50, 100 };

        [Header("저장")]
        [SerializeField] private string saveKey = "Nakgopsae.Save";
        [SerializeField] private float autosaveInterval = 30f;

        public IReadOnlyList<BuildingDefinition> Buildings => buildings;
        public IReadOnlyList<UpgradeDefinition> Upgrades => upgrades;
        public string CurrencyName => Core.Loc.T(currencyName, currencyNameEn);
        public float CostGrowth => costGrowth;
        public float RevealCostRatio => revealCostRatio;
        public Vector2 GoldenSpawnInterval => goldenSpawnInterval;
        public float GoldenLifetime => goldenLifetime;
        public float FrenzyDuration => frenzyDuration;
        public float FrenzyMultiplier => frenzyMultiplier;
        public float LuckyBankRatio => luckyBankRatio;
        public float LuckyMaxCpsSeconds => luckyMaxCpsSeconds;
        public string SaveKey => saveKey;
        public float AutosaveInterval => autosaveInterval;

        public int IndexOf(BuildingDefinition building) => Array.IndexOf(buildings, building);

        public Color RarityColor(Rarity rarity)
        {
            int i = Mathf.Clamp((int)rarity, 0, rarityColors.Length - 1);
            return rarityColors.Length > 0 ? rarityColors[i] : Color.white;
        }

        /// <summary>보유 개수로 정하는 건물 등급. 많이 가질수록 높아진다.</summary>
        public Rarity BuildingRarity(int owned)
        {
            var rarity = Rarity.Common;
            for (int i = 0; i < buildingRarityCounts.Length && i <= (int)Rarity.Legendary; i++)
            {
                if (owned >= buildingRarityCounts[i]) rarity = (Rarity)i;
            }
            return rarity;
        }

#if UNITY_EDITOR
        public void EditorSetContent(BuildingDefinition[] newBuildings, UpgradeDefinition[] newUpgrades)
        {
            buildings = newBuildings;
            upgrades = newUpgrades;
        }
#endif
    }
}
