using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CookieClicker.Core
{
    /// <summary>
    /// 웹에서 컴퓨터가 느리면 스스로 화질을 낮춘다. 2초마다 평균 FPS를 재서 목표보다 낮으면 한 단계씩 내린다.
    /// 1~2단계: 3D 화면 해상도(Render Scale)를 낮춤 (글자·UI는 그대로 선명) → 3단계: 그림자 끄기.
    /// 한 번 내린 단계는 다시 올리지 않는다 (올렸다 내렸다 하며 주기적으로 끊기는 것보다 낫다).
    /// 바꾼 값은 URP 설정 에셋에 쓰이므로, 꺼질 때 원래 값으로 되돌려 에디터의 에셋이 바뀌지 않게 한다.
    /// F3: 화면 구석에 FPS 표시 (어디서 느린지 알려 줄 때 쓴다).
    /// </summary>
    public class AdaptiveQuality : MonoBehaviour
    {
        [Tooltip("이 FPS보다 평균이 낮으면 화질을 한 단계 내린다")]
        [SerializeField] private float targetFps = 45f;
        [Tooltip("FPS를 재는 구간(초)")]
        [SerializeField] private float sampleWindow = 2f;
        [Tooltip("켜진 직후·화면 전환 직후에는 로딩 때문에 느릴 수 있어 이 시간(초)은 재지 않는다")]
        [SerializeField] private float warmup = 3f;
        [Tooltip("단계별 3D 해상도 배율 (첫 값은 무시하고 설정 에셋의 원래 값을 쓴다)")]
        [SerializeField] private float[] renderScales = { 1f, 0.65f, 0.5f };
        [Tooltip("켜면 에디터에서도 화질을 낮춘다 (테스트용)")]
        [SerializeField] private bool runInEditor;
        [SerializeField] private InputAction toggleFpsAction = new InputAction("ToggleFps", InputActionType.Button, "<Keyboard>/f3");

        private UniversalRenderPipelineAsset asset;
        private float originalScale;
        private float originalShadowDistance;
        private int level;
        private float windowTime;
        private int windowFrames;
        private float warmupLeft;
        private float shownFps;
        private bool showFps;
        private GUIStyle fpsStyle;

        private int MaxLevel => renderScales.Length; // 마지막 단계 = 해상도 최저 + 그림자 끄기

        private void Awake()
        {
            asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return;
            originalScale = asset.renderScale;
            originalShadowDistance = asset.shadowDistance;
            warmupLeft = warmup;
        }

        private void OnEnable()
        {
            toggleFpsAction.performed += OnToggleFps;
            toggleFpsAction.Enable();
        }

        private void OnDisable()
        {
            toggleFpsAction.performed -= OnToggleFps;
            toggleFpsAction.Disable();
        }

        private void OnDestroy() => Restore();
        private void OnApplicationQuit() => Restore();

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            windowTime += dt;
            windowFrames++;
            if (windowTime < sampleWindow) return;

            float fps = windowFrames / windowTime;
            windowTime = 0f;
            windowFrames = 0;
            shownFps = fps;

            if (warmupLeft > 0f)
            {
                warmupLeft -= sampleWindow;
                return;
            }
            if (!CanAdjust || fps >= targetFps || level >= MaxLevel) return;

            level++;
            Apply();
            // 바뀐 화질로 다시 잴 시간을 준다.
            warmupLeft = sampleWindow;
            Debug.Log($"[AdaptiveQuality] 평균 {fps:0} FPS → 화질 {level}단계로 낮춤");
        }

        private bool CanAdjust => asset != null && (!Application.isEditor || runInEditor);

        private void Apply()
        {
            int scaleIndex = Mathf.Min(level, renderScales.Length - 1);
            asset.renderScale = Mathf.Min(originalScale, scaleIndex == 0 ? originalScale : renderScales[scaleIndex]);
            asset.shadowDistance = level >= MaxLevel ? 0f : originalShadowDistance;
        }

        private void Restore()
        {
            if (asset == null || level == 0) return;
            asset.renderScale = originalScale;
            asset.shadowDistance = originalShadowDistance;
            level = 0;
        }

        private void OnToggleFps(InputAction.CallbackContext _) => showFps = !showFps;

        private void OnGUI()
        {
            if (!showFps) return;
            fpsStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            fpsStyle.normal.textColor = shownFps >= targetFps ? Color.green : Color.red;
            GUI.Label(new Rect(10, 10, 300, 30), $"{shownFps:0} FPS  ·  화질 {level}/{MaxLevel}", fpsStyle);
        }
    }
}
