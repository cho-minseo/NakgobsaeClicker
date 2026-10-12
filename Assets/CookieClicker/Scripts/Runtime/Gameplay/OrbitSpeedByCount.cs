using System;
using CookieClicker.FX;
using UnityEngine;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 건물 보유 개수에 따라 공전 속도를 올린다 (예: 국자 50개마다 +25%).
    /// 플레이어가 팝업에서 "처음 속도"를 고르면 개수와 상관없이 기본 속도로 돈다. 이 선택은 기기에 저장된다.
    /// 속도 규칙(몇 개마다, 얼마나)은 여기 Inspector에서만 바꾸면 되고, Orbiter는 배율만 받아서 돈다.
    /// </summary>
    public class OrbitSpeedByCount : MonoBehaviour
    {
        [SerializeField] private BuildingPlot plot;
        [SerializeField] private Orbiter orbiter;
        [Tooltip("이 개수마다 한 단계씩 빨라진다")]
        [SerializeField, Min(1)] private int countPerStep = 50;
        [Tooltip("한 단계마다 늘어나는 배율 (0.25 = +25%)")]
        [SerializeField] private float bonusPerStep = 0.25f;
        [SerializeField] private float maxMultiplier = 4f;
        [Tooltip("\"처음 속도\" 선택을 저장하는 키 (건물마다 달라야 함)")]
        [SerializeField] private string prefsKey = "Nakgopsae.OrbitSpeed.ladle";

        public bool UseBaseSpeed { get; private set; }
        public int CountPerStep => countPerStep;
        public float BonusPerStep => bonusPerStep;
        public int Steps => plot.Count / countPerStep;
        /// <summary>보유 개수대로 계산한 배율 ("원래대로"일 때의 속도)</summary>
        public float CountMultiplier => Mathf.Min(maxMultiplier, 1f + Steps * bonusPerStep);
        public float CurrentMultiplier => UseBaseSpeed ? 1f : CountMultiplier;
        public bool IsMaxed => CountMultiplier >= maxMultiplier;
        public int NextStepCount => (Steps + 1) * countPerStep;
        public float DegreesPerSecond => orbiter.DegreesPerSecond * CurrentMultiplier;

        public event Action Changed;

        private void Awake() => UseBaseSpeed = PlayerPrefs.GetInt(prefsKey, 0) == 1;

        private void Update() => orbiter.SpeedMultiplier = CurrentMultiplier;

        public void SetUseBaseSpeed(bool useBase)
        {
            if (UseBaseSpeed == useBase) return;
            UseBaseSpeed = useBase;
            PlayerPrefs.SetInt(prefsKey, useBase ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
