using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.FX
{
    /// <summary>방송 "생중계" 점처럼 깜빡인다 (켜짐·흐림을 번갈아).</summary>
    [RequireComponent(typeof(Graphic))]
    public class Blink : MonoBehaviour
    {
        [SerializeField] private float interval = 0.6f;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.2f;

        private Graphic graphic;

        private void Awake() => graphic = GetComponent<Graphic>();

        private void Update()
        {
            var c = graphic.color;
            c.a = Mathf.Repeat(Time.unscaledTime, interval * 2f) < interval ? 1f : dimAlpha;
            graphic.color = c;
        }
    }
}
