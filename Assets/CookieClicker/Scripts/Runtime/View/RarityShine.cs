using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 영웅·전설 칸 뒤에서 은은하게 숨 쉬듯 밝아졌다 어두워지는 빛. 색은 상점 칸(BuildingRowView/UpgradeRowView)이 정한다.
    /// 게임이 멈춰도(시간 배속 0) 상점은 움직이도록 실제 시간을 쓴다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class RarityShine : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.85f;
        [Tooltip("한 번 밝아졌다 어두워지는 시간(초)")]
        [SerializeField] private float period = 1.6f;
        [SerializeField] private float scaleAmount = 0.06f;

        private Image image;
        private Color baseColor = Color.white;
        private float phase;

        private void Awake()
        {
            image = GetComponent<Image>();
            phase = Random.value * period; // 칸마다 박자를 어긋나게 해서 한꺼번에 깜빡이지 않게
        }

        /// <summary>빛의 색을 바꾼다 (투명도는 이 컴포넌트가 정한다).</summary>
        public void SetColor(Color color)
        {
            if (image == null) image = GetComponent<Image>();
            baseColor = color;
        }

        private void Update()
        {
            float t = (Mathf.Sin((Time.unscaledTime + phase) / period * Mathf.PI * 2f) + 1f) * 0.5f;
            var c = baseColor;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            image.color = c;
            transform.localScale = Vector3.one * (1f + scaleAmount * t);
        }
    }
}
