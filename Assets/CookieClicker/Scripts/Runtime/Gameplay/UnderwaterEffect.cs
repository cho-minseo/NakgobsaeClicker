using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 물속 화면 효과. 머리가 잠기면 안개와 푸른 화면을 켜고, 깊이 들어갈수록 어둡고 시야가 짧아진다.
    /// RenderSettings(안개)는 전역 설정이라, 물 밖으로 나오거나 꺼질 때 원래 값으로 되돌린다.
    /// </summary>
    public class UnderwaterEffect : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private Graphic overlay;

        [Header("얕은 물 → 깊은 물")]
        [SerializeField] private Color shallowColor = new Color(0.25f, 0.62f, 0.78f);
        [SerializeField] private Color deepColor = new Color(0.03f, 0.12f, 0.25f);
        [Tooltip("이 깊이(m)에서 가장 어두워진다")]
        [SerializeField] private float deepAt = 20f;
        [SerializeField] private float shallowFogDistance = 35f;
        [SerializeField] private float deepFogDistance = 9f;
        [SerializeField, Range(0f, 1f)] private float shallowOverlayAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] private float deepOverlayAlpha = 0.6f;

        private bool savedFog;
        private Color savedFogColor;
        private FogMode savedFogMode;
        private float savedFogStart;
        private float savedFogEnd;
        private bool applied;

        private void LateUpdate()
        {
            if (player.IsHeadUnderwater)
            {
                if (!applied) Save();
                float t = Mathf.Clamp01(player.HeadDepth / deepAt);
                Color water = Color.Lerp(shallowColor, deepColor, t);

                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = water;
                RenderSettings.fogStartDistance = 0f;
                RenderSettings.fogEndDistance = Mathf.Lerp(shallowFogDistance, deepFogDistance, t);

                if (overlay != null)
                {
                    overlay.enabled = true;
                    water.a = Mathf.Lerp(shallowOverlayAlpha, deepOverlayAlpha, t);
                    overlay.color = water;
                }
            }
            else if (applied)
            {
                Restore();
            }
        }

        private void OnDisable()
        {
            if (applied) Restore();
        }

        private void Save()
        {
            savedFog = RenderSettings.fog;
            savedFogColor = RenderSettings.fogColor;
            savedFogMode = RenderSettings.fogMode;
            savedFogStart = RenderSettings.fogStartDistance;
            savedFogEnd = RenderSettings.fogEndDistance;
            applied = true;
        }

        private void Restore()
        {
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogStartDistance = savedFogStart;
            RenderSettings.fogEndDistance = savedFogEnd;
            if (overlay != null) overlay.enabled = false;
            applied = false;
        }
    }
}
