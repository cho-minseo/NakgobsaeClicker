using System.Collections.Generic;
using UnityEngine;

namespace CookieClicker.Audio
{
    /// <summary>
    /// 이 영역 위를 걸으면 다른 발소리가 난다 (예: 갯벌 = 질척질척).
    /// WaterVolume처럼 충돌체 대신 좌표로 판정해서, 바닥 판에 충돌체가 없어도 붙이기만 하면 된다.
    /// 영역이 겹치면 나중에 켜진 쪽이 우선이다.
    /// </summary>
    public class GroundSurfaceZone : MonoBehaviour
    {
        [Tooltip("영역의 가로(X)·세로(Z) 크기. 오브젝트 위치가 중심")]
        [SerializeField] private Vector2 area = new Vector2(10f, 10f);
        [SerializeField] private SoundSet footsteps;

        private static readonly List<GroundSurfaceZone> Active = new List<GroundSurfaceZone>();

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public bool Contains(Vector3 point)
        {
            Vector3 local = point - transform.position;
            return Mathf.Abs(local.x) <= area.x * 0.5f && Mathf.Abs(local.z) <= area.y * 0.5f;
        }

        /// <summary>point 아래 바닥의 발소리. 어느 영역에도 없으면 fallback(기본 땅)을 돌려준다.</summary>
        public static SoundSet FootstepsAt(Vector3 point, SoundSet fallback)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (Active[i].Contains(point) && Active[i].footsteps != null) return Active[i].footsteps;
            }
            return fallback;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 0.2f, 0.3f);
            Gizmos.DrawCube(transform.position, new Vector3(area.x, 0.1f, area.y));
        }
    }
}
