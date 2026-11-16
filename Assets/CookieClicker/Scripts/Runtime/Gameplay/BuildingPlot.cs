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

        /// <summary>단계가 올랐을 때 (이전 단계, 새 단계). 처음 불러올 때와 초기화로 내려갈 때는 알리지 않는다.</summary>
        public event Action<BuildingPlot, int, int> StageReached;

        public BuildingDefinition Building => building;
        /// <summary>공사 연출이 붙잡아 둔 동안 보여 주는 단계 (붙잡지 않았으면 null)</summary>
        public int? HeldStage { get; private set; }
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
        private bool initialized;

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
            if (stage != StageIndex)
            {
                int from = StageIndex;
                ApplyStage(stage);
                if (initialized && stage > from) StageReached?.Invoke(this, from, stage);
            }
            initialized = true;
            ShowVisuals();

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

        /// <summary>건물 본체와 단계 모습을 켜고 끈다. 공사 중이면 붙잡아 둔 단계를, 아니면 지금 단계를 보여 준다.</summary>
        private void ShowVisuals()
        {
            int shown = HeldStage ?? StageIndex;
            builtRoot.SetActive(HeldStage.HasValue ? shown >= 0 : Count > 0);
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i].visual != null) stages[i].visual.SetActive(i == shown);
            }
        }

        /// <summary>공사가 끝날 때까지 이 단계 모습으로 붙잡아 둔다 (-1이면 빈 땅).</summary>
        public void Hold(int stage)
        {
            StopPop();
            HeldStage = stage;
            ShowVisuals();
        }

        /// <summary>붙잡은 것을 풀고 지금 단계 모습을 보여 준다. pop이면 솟아오르는 연출까지 바로 한다.</summary>
        public void Release(bool pop)
        {
            HeldStage = null;
            ShowVisuals();
            if (pop) StartPop();
        }

        /// <summary>
        /// 그 단계 건물 모습을 감싸는 상자 (이 건물 자리 기준 좌표). 꺼져 있는 단계도 잠깐 켜서 잰다.
        /// 공사 연출이 비계 크기를 건물에 맞출 때 쓴다.
        /// </summary>
        public Bounds MeasureStage(int stage)
        {
            var visual = stage >= 0 && stage < stages.Length ? stages[stage].visual : null;
            if (visual == null) return new Bounds(Vector3.up, Vector3.one * 2f);
            bool rootActive = builtRoot.activeSelf, visualActive = visual.activeSelf;
            builtRoot.SetActive(true);
            visual.SetActive(true);
            bool any = false;
            var bounds = new Bounds();
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = transform.InverseTransformPoint(b.center + Vector3.Scale(b.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(corner, Vector3.zero); any = true; }
                    else bounds.Encapsulate(corner);
                }
            }
            visual.SetActive(visualActive);
            builtRoot.SetActive(rootActive);
            return any ? bounds : new Bounds(Vector3.up, Vector3.one * 2f);
        }

        private void Update()
        {
            // 구경하기 카메라에 실제로 그려질 때(= 플레이어가 볼 수 있을 때) 연출을 시작한다. 공사 중에는 공사 연출이 맡는다.
            if (popPending && !HeldStage.HasValue && IsOnScreen()) StartPop();
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

        /// <summary>지금 보이는 건물 모습을 솟아오르게 한다 (공사 연출이 건물을 공개할 때 부른다).</summary>
        public void Pop() => StartPop();

        private void StartPop()
        {
            popPending = false;
            PlayerPrefs.SetInt(SeenKey, StageIndex);
            int shown = HeldStage ?? StageIndex;
            if (shown < 0 || stages[shown].visual == null) return;

            popTarget = stages[shown].visual.transform;
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
