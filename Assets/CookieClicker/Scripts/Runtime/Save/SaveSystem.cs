using System;
using System.Collections.Generic;
using CookieClicker.Core;
using CookieClicker.Data;
using UnityEngine;

namespace CookieClicker.Save
{
    /// <summary>디스크에 기록되는 형태. 배열 인덱스 대신 ID로 저장해서 건물 순서를 바꿔도 세이브가 깨지지 않는다.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public long savedAtUnixSeconds;
        public double cookies;
        public double totalBaked;
        public double handmadeCookies;
        public long clicks;
        public int goldenClicks;
        public double playTimeSeconds;
        public List<BuildingEntry> buildings = new List<BuildingEntry>();
        public List<string> upgrades = new List<string>();
        public List<DecorRecord> decor = new List<DecorRecord>();
    }

    [Serializable]
    public class BuildingEntry
    {
        public string id;
        public int count;
    }

    /// <summary>
    /// GameState ↔ JSON 변환과 저장소 접근을 담당. 저장 위치(PlayerPrefs)를 바꾸고 싶으면 이 파일만 고치면 된다.
    /// </summary>
    public static class SaveSystem
    {
        public static void Save(GameConfig config, GameState state)
        {
            var data = new SaveData
            {
                savedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                cookies = state.Cookies,
                totalBaked = state.TotalBaked,
                handmadeCookies = state.HandmadeCookies,
                clicks = state.Clicks,
                goldenClicks = state.GoldenClicks,
                playTimeSeconds = state.PlayTimeSeconds,
                decor = state.Decor,
            };

            for (int i = 0; i < config.Buildings.Count; i++)
            {
                if (state.BuildingCounts[i] > 0)
                    data.buildings.Add(new BuildingEntry { id = config.Buildings[i].Id, count = state.BuildingCounts[i] });
            }
            for (int i = 0; i < config.Upgrades.Count; i++)
            {
                if (state.UpgradesBought[i]) data.upgrades.Add(config.Upgrades[i].Id);
            }

            PlayerPrefs.SetString(config.SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static bool TryLoad(GameConfig config, out GameState state)
        {
            state = null;
            if (!PlayerPrefs.HasKey(config.SaveKey)) return false;

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(config.SaveKey));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] 세이브를 읽지 못해 새 게임으로 시작합니다: {e.Message}");
                return false;
            }
            if (data == null) return false;

            state = GameState.CreateNew(config);
            state.Cookies = data.cookies;
            state.TotalBaked = data.totalBaked;
            state.HandmadeCookies = data.handmadeCookies;
            state.Clicks = data.clicks;
            state.GoldenClicks = data.goldenClicks;
            state.PlayTimeSeconds = data.playTimeSeconds;
            // 꾸미기가 생기기 전 세이브에는 decor가 없다.
            if (data.decor != null) state.Decor = data.decor;

            foreach (var entry in data.buildings)
            {
                int index = FindIndex(config.Buildings, entry.id);
                if (index >= 0) state.BuildingCounts[index] = entry.count;
            }
            foreach (var id in data.upgrades)
            {
                int index = FindIndex(config.Upgrades, id);
                if (index >= 0) state.UpgradesBought[index] = true;
            }
            return true;
        }

        public static void Delete(GameConfig config)
        {
            PlayerPrefs.DeleteKey(config.SaveKey);
            PlayerPrefs.Save();
        }

        private static int FindIndex(IReadOnlyList<BuildingDefinition> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return i;
            return -1;
        }

        private static int FindIndex(IReadOnlyList<UpgradeDefinition> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return i;
            return -1;
        }
    }
}
