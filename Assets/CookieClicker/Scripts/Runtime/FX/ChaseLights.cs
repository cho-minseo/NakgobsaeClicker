using UnityEngine;

namespace CookieClicker.FX
{
    /// <summary>
    /// 둥글게 늘어선 전구들이 차례로 밝아지며 빙글빙글 도는 것처럼 보이게 한다 (UFO 테두리 불빛).
    /// 머티리얼을 복제하지 않고 MaterialPropertyBlock으로 전구마다 밝기만 바꾼다.
    /// </summary>
    public class ChaseLights : MonoBehaviour
    {
        [SerializeField] private Renderer[] lights;
        [SerializeField] private Color color = new Color(0.55f, 1f, 0.7f);
        [Tooltip("초당 몇 칸씩 불빛이 넘어가는지")]
        [SerializeField] private float speed = 6f;
        [Tooltip("밝게 켜진 꼬리 길이 (칸)")]
        [SerializeField, Min(0.5f)] private float tail = 3f;
        [SerializeField] private float onIntensity = 4f;
        [SerializeField] private float offIntensity = 0.4f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;

        private void Update()
        {
            if (lights == null || lights.Length == 0) return;
            block ??= new MaterialPropertyBlock();
            float head = Mathf.Repeat(Time.time * speed, lights.Length);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                // 머리(head)에서 뒤로 몇 칸 떨어졌는지에 따라 점점 어두워진다.
                float behind = Mathf.Repeat(head - i, lights.Length);
                float k = Mathf.Clamp01(1f - behind / tail);
                float intensity = Mathf.Lerp(offIntensity, onIntensity, k * k);
                lights[i].GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(color * 0.5f, color, k));
                block.SetColor(EmissionColor, color * intensity);
                lights[i].SetPropertyBlock(block);
            }
        }
    }
}
