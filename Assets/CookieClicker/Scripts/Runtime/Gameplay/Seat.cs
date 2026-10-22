using UnityEngine;

namespace CookieClicker.Gameplay
{
    /// <summary>앉을 수 있는 물건 (의자, 벤치, 평상). sitPoint는 앉는 면 위 엉덩이 자리이고, 파란 화살표(+z)가 앉아서 바라보는 방향이다.</summary>
    public class Seat : MonoBehaviour
    {
        [SerializeField] private Transform sitPoint;

        public Transform SitPoint => sitPoint != null ? sitPoint : transform;
        public bool Occupied { get; set; }

        private void OnDrawGizmosSelected()
        {
            var p = SitPoint;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(p.position, 0.12f);
            Gizmos.DrawLine(p.position, p.position + p.forward * 0.5f);
        }
    }
}
