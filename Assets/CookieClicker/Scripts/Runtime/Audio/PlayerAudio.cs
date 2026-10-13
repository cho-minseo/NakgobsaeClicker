using CookieClicker.Gameplay;
using CookieClicker.Gameplay.Viewmodel;
using UnityEngine;

namespace CookieClicker.Audio
{
    /// <summary>
    /// 구경하기 플레이어 효과음. 이동 코드는 소리를 모르고, 이 스크립트가 상태·이벤트를 듣고 소리를 낸다.
    /// - 걷기: 실제로 움직인 거리마다 발소리 (달리면 자연히 빨라짐). 바닥(GroundSurfaceZone)·얕은 물에 따라 다른 소리
    /// - 점프·착지: 높이 떨어질수록 착지음이 커짐
    /// - 수영: 손이 물을 미는 박자(ViewmodelAnimator.Stroked)에 맞춰 첨벙
    /// - 잠수: 귀(AudioListener)에 로우패스 필터를 걸어 모든 소리를 먹먹하게, 깊을수록 더 먹먹하게.
    ///         물속 웅웅거림·보글거림을 깔고 가끔 공기 방울이 올라감
    /// 소리 파일·세기는 SoundSet 에셋과 이 Inspector에서 바꾼다.
    /// </summary>
    public class PlayerAudio : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [Tooltip("수영 박자를 받을 손 애니메이터 (없으면 일정 간격으로 젓는 소리)")]
        [SerializeField] private ViewmodelAnimator hands;

        [Header("소리 내는 곳")]
        [Tooltip("한 번씩 나는 소리용. 여러 개를 돌려 써서 소리가 겹쳐도 앞 소리가 끊기지 않는다")]
        [SerializeField] private AudioSource[] voices;
        [Tooltip("물속에서만 들리는 반복 소리 (웅웅거림, 보글거림)")]
        [SerializeField] private AudioSource[] underwaterLoops;
        [Tooltip("AudioListener와 같은 오브젝트에 있어야 모든 소리에 걸린다")]
        [SerializeField] private AudioLowPassFilter muffle;

        [Header("소리 묶음")]
        [SerializeField] private SoundSet defaultFootsteps;
        [SerializeField] private SoundSet landing;
        [SerializeField] private SoundSet swimStroke;
        [SerializeField] private SoundSet waterEnter;
        [SerializeField] private SoundSet bubbles;

        [Header("걷기")]
        [Tooltip("이 거리(m)를 걸을 때마다 발소리 한 번")]
        [SerializeField] private float strideLength = 2.1f;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float wadeVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float jumpVolume = 0.6f;
        [Tooltip("이 속도(m/s)로 떨어지면 착지음이 가장 크다")]
        [SerializeField] private float hardLandingSpeed = 14f;

        [Header("수영")]
        [SerializeField, Range(0f, 1f)] private float strokeVolume = 0.5f;
        [Tooltip("제자리에 떠 있을 때 젓는 소리 세기 (헤엄칠 때 대비)")]
        [SerializeField, Range(0f, 1f)] private float idleStrokeRatio = 0.35f;
        [Tooltip("물속에서는 소리가 낮게 들린다")]
        [SerializeField] private float underwaterPitch = 0.8f;
        [Tooltip("이 속도(m/s) 이상으로 물에 뛰어들면 큰 첨벙")]
        [SerializeField] private float bigSplashSpeed = 6f;

        [Header("잠수 (먹먹함)")]
        [Tooltip("물 밖: 필터 없음 / 머리가 막 잠겼을 때 / 가장 깊을 때 남기는 최고 주파수(Hz)")]
        [SerializeField] private float surfaceCutoff = 1100f;
        [SerializeField] private float deepCutoff = 380f;
        [Tooltip("이 깊이(m)에서 가장 먹먹해진다")]
        [SerializeField] private float deepAt = 20f;
        [Tooltip("물에 들어가고 나올 때 먹먹함이 바뀌는 빠르기")]
        [SerializeField] private float muffleSpeed = 10f;
        [SerializeField, Range(0f, 1f)] private float underwaterLoopVolume = 0.55f;
        [Tooltip("공기 방울 소리 간격(초) 최소·최대")]
        [SerializeField] private Vector2 bubbleInterval = new Vector2(1.5f, 4f);

        private const float NoFilterCutoff = 22000f;

        private int nextVoice;
        private float strideDistance;
        private Vector3 lastPosition;
        private float verticalSpeed;
        private bool wasSwimming;
        private bool wasHeadUnderwater;
        private float cutoff = NoFilterCutoff;
        private float underwaterBlend;
        private float bubbleTimer;
        private float fallbackStrokeTimer;

        private void OnEnable()
        {
            player.Jumped += OnJumped;
            player.Landed += OnLanded;
            if (hands != null) hands.Stroked += OnStroked;

            lastPosition = player.transform.position;
            wasSwimming = player.IsSwimming;
            wasHeadUnderwater = player.IsHeadUnderwater;
            strideDistance = strideLength * 0.5f; // 걷기 시작하면 첫 발소리가 금방 나도록
            foreach (var loop in underwaterLoops)
            {
                if (loop == null) continue;
                loop.volume = 0f;
                loop.Play();
            }
        }

        private void OnDisable()
        {
            player.Jumped -= OnJumped;
            player.Landed -= OnLanded;
            if (hands != null) hands.Stroked -= OnStroked;

            // 구경하기를 나가면 먹먹함이 남지 않게 되돌린다.
            cutoff = NoFilterCutoff;
            underwaterBlend = 0f;
            ApplyUnderwater();
            foreach (var loop in underwaterLoops) if (loop != null) loop.Stop();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 position = player.transform.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            // 순간이동처럼 한 프레임에 크게 움직인 건 걸음으로 치지 않는다.
            if (delta.sqrMagnitude > 4f) delta = Vector3.zero;
            if (dt > 0f) verticalSpeed = delta.y / dt;

            UpdateFootsteps(position, delta);
            UpdateWaterTransitions();
            UpdateUnderwater(dt);
            if (hands == null) UpdateFallbackStroke(dt);
        }

        // ------------------------------------------------------------------ 땅

        private void UpdateFootsteps(Vector3 position, Vector3 delta)
        {
            if (player.IsSwimming || !player.IsGrounded) return;

            strideDistance += new Vector2(delta.x, delta.z).magnitude;
            if (strideDistance < strideLength) return;
            strideDistance -= strideLength;
            PlayFootstep(position, 1f);
        }

        private void PlayFootstep(Vector3 position, float volumeScale, float pitchScale = 1f)
        {
            // 발이 얕은 물에 잠겨 있으면 찰박찰박
            if (WaterVolume.TryGetSurface(position, out float surface) && surface > position.y + 0.05f)
                PlayOneShot(swimStroke, wadeVolume * volumeScale, pitchScale);
            else
                PlayOneShot(GroundSurfaceZone.FootstepsAt(position, defaultFootsteps), footstepVolume * volumeScale, pitchScale);
        }

        private void OnJumped() => PlayFootstep(player.transform.position, jumpVolume / footstepVolume, 1.1f);

        private void OnLanded(float fallSpeed)
        {
            float strength = Mathf.InverseLerp(0f, hardLandingSpeed, fallSpeed);
            PlayOneShot(landing, Mathf.Lerp(0.25f, 1f, strength));
            PlayFootstep(player.transform.position, 1f);
            strideDistance = 0f;
        }

        // ------------------------------------------------------------------ 물

        private void UpdateWaterTransitions()
        {
            // 물에 뛰어들면 첨벙, 세게 들어갈수록 크게
            if (player.IsSwimming && !wasSwimming)
            {
                float strength = Mathf.InverseLerp(0f, bigSplashSpeed, -verticalSpeed);
                PlayOneShot(waterEnter, Mathf.Lerp(0.35f, 1f, strength));
            }

            // 머리가 잠기는 순간 공기 방울이 확, 떠오르는 순간 수면을 깨는 첨벙
            if (player.IsHeadUnderwater && !wasHeadUnderwater)
            {
                PlayOneShot(bubbles, 1f);
                bubbleTimer = Random.Range(bubbleInterval.x, bubbleInterval.y);
            }
            else if (!player.IsHeadUnderwater && wasHeadUnderwater)
            {
                PlayOneShot(swimStroke, strokeVolume * 1.2f);
            }

            wasSwimming = player.IsSwimming;
            wasHeadUnderwater = player.IsHeadUnderwater;
        }

        private void OnStroked(float moveRatio)
        {
            float volume = strokeVolume * Mathf.Lerp(idleStrokeRatio, 1f, moveRatio);
            if (player.IsHeadUnderwater)
            {
                // 물속에서는 첨벙 대신 낮고 먹먹한 휘익 (필터가 높은 소리를 깎는다) + 가끔 방울
                PlayOneShot(swimStroke, volume, underwaterPitch);
                if (Random.value < 0.3f) PlayOneShot(bubbles, 0.5f);
            }
            else
            {
                PlayOneShot(swimStroke, volume);
            }
        }

        private void UpdateFallbackStroke(float dt)
        {
            if (!player.IsSwimming) return;
            fallbackStrokeTimer -= dt;
            if (fallbackStrokeTimer > 0f) return;
            fallbackStrokeTimer = 1.4f;
            OnStroked(Mathf.Clamp01(player.MoveRatio));
        }

        private void UpdateUnderwater(float dt)
        {
            bool under = player.IsHeadUnderwater;
            float target = under
                ? Mathf.Lerp(surfaceCutoff, deepCutoff, Mathf.Clamp01(player.HeadDepth / deepAt))
                : NoFilterCutoff;
            // 주파수는 사람이 로그로 느끼므로 로그 공간에서 부드럽게 옮긴다.
            float k = 1f - Mathf.Exp(-muffleSpeed * dt);
            cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(target), k));
            underwaterBlend = Mathf.Lerp(underwaterBlend, under ? 1f : 0f, k);
            ApplyUnderwater();

            if (!under) return;
            bubbleTimer -= dt;
            if (bubbleTimer > 0f) return;
            bubbleTimer = Random.Range(bubbleInterval.x, bubbleInterval.y);
            PlayOneShot(bubbles, Random.Range(0.4f, 0.8f));
        }

        private void ApplyUnderwater()
        {
            if (muffle != null)
            {
                muffle.cutoffFrequency = cutoff;
                // 거의 물 밖이면 필터를 꺼서 소리를 그대로 둔다.
                muffle.enabled = cutoff < NoFilterCutoff - 100f;
            }
            foreach (var loop in underwaterLoops)
            {
                if (loop != null) loop.volume = underwaterBlend * underwaterLoopVolume;
            }
        }

        private void PlayOneShot(SoundSet set, float volumeScale, float pitchScale = 1f)
        {
            if (set == null || voices == null || voices.Length == 0) return;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            set.Play(voice, volumeScale, pitchScale);
        }
    }
}
