using System;
using CookieClicker.Data;

namespace CookieClicker.Core
{
    /// <summary>
    /// 플레이 중에 변하는 값만 모은 순수 데이터. 배열 인덱스는 GameConfig의 건물/업그레이드 순서와 같다.
    /// </summary>
    [Serializable]
    public class GameState
    {
        public double Cookies;
        public double TotalBaked;
        public double HandmadeCookies;
        public long Clicks;
        public int GoldenClicks;
        public double PlayTimeSeconds;
        public int[] BuildingCounts;
        public bool[] UpgradesBought;

        public static GameState CreateNew(GameConfig config) => new GameState
        {
            BuildingCounts = new int[config.Buildings.Count],
            UpgradesBought = new bool[config.Upgrades.Count],
        };
    }
}
