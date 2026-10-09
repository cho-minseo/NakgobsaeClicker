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

        [Header("경제")]
        [Tooltip("건물을 하나 살 때마다 다음 가격에 곱해지는 값")]
        [SerializeField] private float costGrowth = 1.15f;
        [Tooltip("누적 생산량이 '건물 가격 x 이 값'을 넘으면 상점에 공개")]
        [SerializeField, Range(0f, 1f)] private float revealCostRatio = 0.5f;

        [Header("황금 쿠키")]
        [SerializeField] private Vector2 goldenSpawnInterval = new Vector2(60f, 150f);
        [SerializeField] private float goldenLifetime = 13f;
        [SerializeField] private float frenzyDuration = 77f;
        [SerializeField] private float frenzyMultiplier = 7f;
        [Tooltip("럭키: 보유 쿠키의 몇 %를 줄지")]
        [SerializeField, Range(0f, 1f)] private float luckyBankRatio = 0.15f;
        [Tooltip("럭키: 최대 몇 초 분량의 생산량까지 줄지")]
        [SerializeField] private float luckyMaxCpsSeconds = 900f;

        [Header("저장")]
        [SerializeField] private string saveKey = "CookieClicker.Save";
        [SerializeField] private float autosaveInterval = 30f;

        public IReadOnlyList<BuildingDefinition> Buildings => buildings;
        public IReadOnlyList<UpgradeDefinition> Upgrades => upgrades;
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

#if UNITY_EDITOR
        public void EditorSetContent(BuildingDefinition[] newBuildings, UpgradeDefinition[] newUpgrades)
        {
            buildings = newBuildings;
            upgrades = newUpgrades;
        }
#endif
    }
}
