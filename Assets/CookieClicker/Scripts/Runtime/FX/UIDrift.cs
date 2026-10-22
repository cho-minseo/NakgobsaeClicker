using UnityEngine;

namespace CookieClicker.FX
{
    /// <summary>구름처럼 UI를 천천히 좌우로 오가게 한다 (제자리에서 distance만큼 왕복).</summary>
    public class UIDrift : MonoBehaviour
    {
        [SerializeField] private float distance = 60f;
        [Tooltip("한 번 왕복하는 데 걸리는 시간(초)")]
        [SerializeField] private float period = 40f;
        [SerializeField, Range(0f, 1f)] private float phase;

        private RectTransform rect;
        private Vector2 origin;

        private void Awake()
        {
            rect = (RectTransform)transform;
            origin = rect.anchoredPosition;
        }

        private void Update()
        {
            float t = (Time.unscaledTime / period + phase) * Mathf.PI * 2f;
            rect.anchoredPosition = origin + new Vector2(Mathf.Sin(t) * distance, 0f);
        }
    }
}
