using System;
using CookieClicker.Core;
using CookieClicker.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 월드에 놓인 건물 자리 하나. 한 개도 안 샀으면 빈 땅, 사면 건물이 보이고, 개수가 늘면
    /// - 단계(Stage): 건물 모습이 통째로 바뀐다 (하나만 켜짐).
    /// - 장식(Tier): 상자 더미처럼 조건을 넘은 것이 모두 켜진다 (누적).
    /// 단계가 오른 뒤 플레이어 눈에 처음 들어오면 건물이 솟아오르며 반짝이는 연출을 한다.
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

        [Serializable]
        public class Stage
        {
            [Min(1)] public int minCount = 1;
            [Tooltip("이 단계에서만 켜지는 건물 모습. 모델을 바꾸려면 이 오브젝트 아래만 교체하면 된다.")]
            public GameObject visual;
            [Tooltip("이 단계일 때 간판 높이 (Built 기준 로컬 Y). 0이면 옮기지 않는다.")]
            public float signHeight;
        }

        [SerializeField] private CookieGame game;
        [SerializeField] private BuildingDefinition building;
        [Tooltip("하나라도 사면 켜지는 루트 (건물 본체 + 간판 + 장식)")]
        [SerializeField] private GameObject builtRoot;
        [SerializeField] private Tier[] tiers = Array.Empty<Tier>();
        [SerializeField] private Text signText;

        [Header("건물 단계 (보유 개수에 따라 하나만 켜짐, minCount 오름차순)")]
        [SerializeField] private Stage[] stages = Array.Empty<Stage>();
        [Tooltip("단계마다 높이를 옮길 간판")]
        [SerializeField] private Transform sign;
        [Tooltip("단계가 오른 뒤 처음 눈에 들어올 때 터지는 반짝임")]
        [SerializeField] private ParticleSystem stageUpEffect;
        [SerializeField, Min(0)] private int stageUpParticles = 40;
        [SerializeField, Min(0.05f)] private float popDuration = 0.8f;
        [Tooltip("솟아오를 때 납작해졌다 늘어나는 정도")]
        [SerializeField, Range(0f, 0.6f)] private float popStrength = 0.3f;

        public BuildingDefinition Building => building;
        public int Count { get; private set; }
        /// <summary>현재 단계 (0부터, 아직 안 샀으면 -1)</summary>
        public int StageIndex { get; private set; } = -1;
        public int StageCount => stages.Length;
        /// <summary>다음 단계에 필요한 보유 개수. 마지막 단계면 -1.</summary>
        public int NextStageCount => StageIndex + 1 < stages.Length ? stages[StageIndex + 1].minCount : -1;

        // 플레이어가 마지막으로 본 단계. 기기에 저장해서, 다음에 와도 "자란 건물"을 처음 볼 때 연출한다.
        private string SeenKey => $"Nakgopsae.SeenStage.{building.Id}";
        private bool popPending;
        private float popTime = -1f;
        private Transform popTarget;
        private Vector3 popBaseScale;
        private Renderer[] stageRenderers = Array.Empty<Renderer>();

        private void OnEnable()
        {
            game.BuildingPurchased += OnBuildingPurchased;
            game.ProgressReset += Refresh;
            GameSettings.Changed += OnSettingsChanged; // 언어가 바뀌면 간판 이름도 바꾼다
            if (game.State != null) Refresh();
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            game.BuildingPurchased -= OnBuildingPurchased;
            game.ProgressReset -= Refresh;
            GameSettings.Changed -= OnSettingsChanged;
            StopPop();
        }

        private void OnBuildingPurchased(int _) => Refresh();
        private void OnSettingsChanged()
        {
            if (game.State != null) Refresh();
        }

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

            int stage = -1;
            for (int i = 0; i < stages.Length; i++)
            {
                if (Count >= stages[i].minCount) stage = i;
            }
            if (stage != StageIndex) ApplyStage(stage);

            int seen = PlayerPrefs.GetInt(SeenKey, -1);
            if (stage > seen)
            {
                popPending = true;
            }
            else if (stage < seen)
            {
                // 초기화로 단계가 내려가면 본 기록도 맞춰 내린다.
                PlayerPrefs.SetInt(SeenKey, stage);
                popPending = false;
            }
        }

        private void ApplyStage(int stage)
        {
            StopPop();
            StageIndex = stage;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i].visual != null) stages[i].visual.SetActive(i == stage);
            }

            stageRenderers = Array.Empty<Renderer>();
            if (stage < 0) return;
            var current = stages[stage];
            if (current.visual != null) stageRenderers = current.visual.GetComponentsInChildren<Renderer>(true);
            if (sign != null && current.signHeight > 0f)
            {
                var position = sign.localPosition;
                position.y = current.signHeight;
                sign.localPosition = position;
            }
        }

        private void Update()
        {
            // 구경하기 카메라에 실제로 그려질 때(= 플레이어가 볼 수 있을 때) 연출을 시작한다.
            if (popPending && IsOnScreen()) StartPop();
            if (popTime >= 0f) AnimatePop();
        }

        private bool IsOnScreen()
        {
            foreach (var r in stageRenderers)
            {
                if (r != null && r.isVisible) return true;
            }
            return false;
        }

        private void StartPop()
        {
            popPending = false;
            PlayerPrefs.SetInt(SeenKey, StageIndex);
            if (StageIndex < 0 || stages[StageIndex].visual == null) return;

            popTarget = stages[StageIndex].visual.transform;
            popBaseScale = popTarget.localScale;
            popTime = 0f;
            if (stageUpEffect != null) stageUpEffect.Emit(stageUpParticles);
        }

        /// <summary>
        /// 땅에서 솟아오르듯: 납작하게 눌렸다가 위로 늘어나며 두세 번 출렁이고 제자리.
        /// 세로가 늘면 가로는 그 제곱근만큼 줄여 부피가 유지돼 보이게 한다. 단계 오브젝트의 기준점이 바닥이라 땅에서 자란다.
        /// </summary>
        private void AnimatePop()
        {
            popTime += Time.deltaTime;
            float t = popTime / popDuration;
            if (t >= 1f)
            {
                StopPop();
                return;
            }
            float decay = (1f - t) * (1f - t);
            float y = 1f - popStrength * Mathf.Cos(t * Mathf.PI * 3f) * decay;
            float xz = 1f / Mathf.Sqrt(Mathf.Max(0.1f, y));
            popTarget.localScale = Vector3.Scale(popBaseScale, new Vector3(xz, y, xz));
        }

        private void StopPop()
        {
            if (popTarget != null) popTarget.localScale = popBaseScale;
            popTarget = null;
            popTime = -1f;
        }
    }
}
