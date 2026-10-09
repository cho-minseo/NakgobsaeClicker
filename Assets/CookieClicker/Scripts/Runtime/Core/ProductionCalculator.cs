using CookieClicker.Data;

namespace CookieClicker.Core
{
    /// <summary>생산량 계산 결과. 건물/업그레이드가 바뀔 때만 다시 계산해서 매 프레임 재사용한다.</summary>
    public sealed class ProductionResult
    {
        /// <summary>건물 1개당 초당 생산량 (업그레이드 적용 후)</summary>
        public double[] UnitCps;
        /// <summary>건물 종류별 총 초당 생산량</summary>
        public double[] BuildingCps;
        /// <summary>버프 적용 전 전체 초당 생산량</summary>
        public double RawCps;
        public double ClickMultiplier = 1;
        public double ClickCpsPercent;
    }

    /// <summary>
    /// 상태(GameState)와 설정(GameConfig)만 받아서 생산량을 계산하는 순수 함수.
    /// MonoBehaviour에 의존하지 않으므로 씬 없이도 단위 테스트할 수 있다.
    /// </summary>
    public static class ProductionCalculator
    {
        public static ProductionResult Calculate(GameConfig config, GameState state)
        {
            int buildingCount = config.Buildings.Count;
            var multipliers = new double[buildingCount];
            for (int i = 0; i < buildingCount; i++) multipliers[i] = 1;

            var result = new ProductionResult
            {
                UnitCps = new double[buildingCount],
                BuildingCps = new double[buildingCount],
            };

            for (int u = 0; u < config.Upgrades.Count; u++)
            {
                if (!state.UpgradesBought[u]) continue;
                var upgrade = config.Upgrades[u];
                switch (upgrade.EffectType)
                {
                    case UpgradeEffectType.BuildingMultiplier:
                        int target = config.IndexOf(upgrade.TargetBuilding);
                        if (target >= 0) multipliers[target] *= upgrade.Multiplier;
                        if (upgrade.AlsoAffectsClick) result.ClickMultiplier *= upgrade.Multiplier;
                        break;
                    case UpgradeEffectType.ClickCpsPercent:
                        result.ClickCpsPercent += upgrade.ClickCpsPercent;
                        break;
                }
            }

            for (int i = 0; i < buildingCount; i++)
            {
                result.UnitCps[i] = config.Buildings[i].BaseCps * multipliers[i];
                result.BuildingCps[i] = result.UnitCps[i] * state.BuildingCounts[i];
                result.RawCps += result.BuildingCps[i];
            }
            return result;
        }

        /// <summary>현재 보유 수에서 amount개를 연속으로 살 때의 총 가격 (등비수열 합).</summary>
        public static double BulkCost(double baseCost, int owned, int amount, float growth)
        {
            double first = baseCost * System.Math.Pow(growth, owned);
            if (amount <= 1) return System.Math.Ceiling(first);
            return System.Math.Ceiling(first * (System.Math.Pow(growth, amount) - 1) / (growth - 1));
        }
    }
}
