using System;
using System.Collections.Generic;
using CookieClicker.Data;
using UnityEngine;

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
        /// <summary>꾸미기 모드로 평상 부지에 놓은 물건들</summary>
        public List<DecorRecord> Decor = new List<DecorRecord>();

        public static GameState CreateNew(GameConfig config) => new GameState
        {
            BuildingCounts = new int[config.Buildings.Count],
            UpgradesBought = new bool[config.Upgrades.Count],
        };
    }

    /// <summary>놓은 물건 하나. 위치는 평상 부지 기준 좌표, slot은 칸 겹침 검사용 이름 (자유 배치 가구는 비어 있음).</summary>
    [Serializable]
    public class DecorRecord
    {
        public string id;
        public string slot;
        public Vector3 position;
        public float yaw;
        /// <summary>칠한 색 번호 (DecorCatalog 팔레트). -1이면 원래 색</summary>
        public int color = -1;
    }
}
