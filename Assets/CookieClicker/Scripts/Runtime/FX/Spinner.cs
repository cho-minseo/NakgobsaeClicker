using UnityEngine;

namespace CookieClicker.FX
{
    /// <summary>쿠키 뒤 후광처럼 계속 돌기만 하는 오브젝트용.</summary>
    public class Spinner : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 8f;
        [SerializeField] private float pulseAmount = 0.04f;
        [SerializeField] private float pulseSpeed = 1.5f;

        private Vector3 baseScale;

        private void Awake() => baseScale = transform.localScale;

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
            transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);
        }
    }
}
