using System;
using CookieClicker.Data;
using CookieClicker.Save;
using UnityEngine;

namespace CookieClicker.Core
{
    public enum GoldenCookieEffect { Lucky, Frenzy }

    public readonly struct GoldenCookieResult
    {
        public readonly GoldenCookieEffect Effect;
        public readonly double Cookies;
        public readonly float Duration;

        public GoldenCookieResult(GoldenCookieEffect effect, double cookies, float duration)
        {
            Effect = effect;
            Cookies = cookies;
            Duration = duration;
        }
    }

    /// <summary>
    /// 게임 규칙의 중심. 쿠키 계산·구매·저장을 처리하고, 결과는 이벤트로만 알린다.
    /// UI를 전혀 모르기 때문에 화면을 갈아엎어도 이 클래스는 그대로 쓸 수 있다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CookieGame : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [Tooltip("끄면 항상 새 게임으로 시작합니다 (테스트용)")]
        [SerializeField] private bool loadSaveOnStart = true;

        public event Action<double> CookieClicked;
        public event Action<int> BuildingPurchased;
        public event Action<int> UpgradePurchased;
        public event Action<GoldenCookieResult> GoldenCookieClaimed;
        public event Action<int> BuyAmountChanged;
        public event Action Saved;
        public event Action ProgressReset;

        public GameConfig Config => config;
        public GameState State { get; private set; }
        public ProductionResult Production { get; private set; }
        public int BuyAmount { get; private set; } = 1;
        public float FrenzyTimeLeft { get; private set; }
        public bool IsFrenzy => FrenzyTimeLeft > 0f;

        public double CookiesPerSecond => Production.RawCps * (IsFrenzy ? config.FrenzyMultiplier : 1f);
        public double CookiesPerClick => Production.ClickMultiplier + CookiesPerSecond * Production.ClickCpsPercent;

        private float autosaveTimer;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[CookieGame] GameConfig가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            State = loadSaveOnStart && SaveSystem.TryLoad(config, out var loaded)
                ? loaded
                : GameState.CreateNew(config);
            Recalculate();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (FrenzyTimeLeft > 0f) FrenzyTimeLeft = Mathf.Max(0f, FrenzyTimeLeft - dt);

            Earn(CookiesPerSecond * dt);
            State.PlayTimeSeconds += dt;

            autosaveTimer += dt;
            if (autosaveTimer >= config.AutosaveInterval) SaveGame();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && State != null) SaveGame();
        }

        private void OnApplicationQuit()
        {
            if (State != null) SaveGame();
        }

        // ---------- 클릭 ----------

        public double ClickCookie()
        {
            double amount = CookiesPerClick;
            Earn(amount);
            State.HandmadeCookies += amount;
            State.Clicks++;
            CookieClicked?.Invoke(amount);
            return amount;
        }

        /// <summary>클릭이 아닌 경로(테스트 치트, 나중의 보상 등)로 재화를 지급한다.</summary>
        public void GrantCookies(double amount)
        {
            if (amount > 0) Earn(amount);
        }

        /// <summary>건물·업그레이드가 아닌 곳(꾸미기 물건 등)에 재화를 쓴다. 모자라면 아무것도 하지 않고 false.</summary>
        public bool TrySpend(double amount)
        {
            if (State.Cookies < amount) return false;
            State.Cookies -= amount;
            return true;
        }

        /// <summary>쓴 재화를 돌려준다 (꾸미기 물건 회수). 누적 생산량(TotalBaked)에는 더하지 않는다.</summary>
        public void Refund(double amount)
        {
            if (amount > 0) State.Cookies += amount;
        }

        // ---------- 건물 ----------

        public int GetBuildingCount(int index) => State.BuildingCounts[index];

        public double GetBuildingCost(int index, int amount) =>
            ProductionCalculator.BulkCost(config.Buildings[index].BaseCost, State.BuildingCounts[index], amount, config.CostGrowth);

        /// <summary>처음 건물은 항상 공개, 나머지는 하나라도 샀거나 누적 생산량이 가격에 가까워지면 공개.</summary>
        public bool IsBuildingRevealed(int index)
        {
            if (index == 0 || State.BuildingCounts[index] > 0) return true;
            return State.TotalBaked >= config.Buildings[index].BaseCost * config.RevealCostRatio;
        }

        public void SetBuyAmount(int amount)
        {
            BuyAmount = Mathf.Max(1, amount);
            BuyAmountChanged?.Invoke(BuyAmount);
        }

        public bool TryBuyBuilding(int index)
        {
            double cost = GetBuildingCost(index, BuyAmount);
            if (State.Cookies < cost) return false;

            State.Cookies -= cost;
            State.BuildingCounts[index] += BuyAmount;
            Recalculate();
            BuildingPurchased?.Invoke(index);
            return true;
        }

        // ---------- 업그레이드 ----------

        public bool IsUpgradeAvailable(int index)
        {
            if (State.UpgradesBought[index]) return false;
            var upgrade = config.Upgrades[index];

            if (upgrade.RequiredBuilding != null)
            {
                int required = config.IndexOf(upgrade.RequiredBuilding);
                if (required < 0 || State.BuildingCounts[required] < upgrade.RequiredCount) return false;
            }
            return State.HandmadeCookies >= upgrade.RequiredHandmadeCookies;
        }

        public bool TryBuyUpgrade(int index)
        {
            var upgrade = config.Upgrades[index];
            if (!IsUpgradeAvailable(index) || State.Cookies < upgrade.Cost) return false;

            State.Cookies -= upgrade.Cost;
            State.UpgradesBought[index] = true;
            Recalculate();
            UpgradePurchased?.Invoke(index);
            return true;
        }

        // ---------- 황금 낙곱새 ----------

        public GoldenCookieResult ClaimGoldenCookie()
        {
            State.GoldenClicks++;
            GoldenCookieResult result;

            if (UnityEngine.Random.value < 0.5f)
            {
                double fromBank = State.Cookies * config.LuckyBankRatio;
                double fromCps = Production.RawCps * config.LuckyMaxCpsSeconds;
                double reward = Math.Min(fromBank, fromCps) + 13;
                Earn(reward);
                result = new GoldenCookieResult(GoldenCookieEffect.Lucky, reward, 0f);
            }
            else
            {
                FrenzyTimeLeft = config.FrenzyDuration;
                result = new GoldenCookieResult(GoldenCookieEffect.Frenzy, 0, config.FrenzyDuration);
            }

            GoldenCookieClaimed?.Invoke(result);
            return result;
        }

        // ---------- 저장 ----------

        public void SaveGame()
        {
            autosaveTimer = 0f;
            SaveSystem.Save(config, State);
            Saved?.Invoke();
        }

        public void ResetProgress()
        {
            SaveSystem.Delete(config);
            State = GameState.CreateNew(config);
            FrenzyTimeLeft = 0f;
            Recalculate();
            ProgressReset?.Invoke();
        }

        private void Earn(double amount)
        {
            State.Cookies += amount;
            State.TotalBaked += amount;
        }

        private void Recalculate()
        {
            Production = ProductionCalculator.Calculate(config, State);
        }
    }
}
