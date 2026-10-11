using System.Collections.Generic;
using UnityEngine;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 물 영역 하나. 오브젝트 위치가 수면이고, 아래로 depth만큼이 물이다.
    /// 물리 트리거 대신 좌표로 판정해서, 플레이어뿐 아니라 무엇이든 "여기가 물속인가"를 물어볼 수 있다.
    /// </summary>
    public class WaterVolume : MonoBehaviour
    {
        [Tooltip("물 영역의 가로(X)·세로(Z) 크기")]
        [SerializeField] private Vector2 area = new Vector2(10f, 10f);
        [SerializeField] private float depth = 25f;

        private static readonly List<WaterVolume> Active = new List<WaterVolume>();

        public float SurfaceY => transform.position.y;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public bool Contains(Vector3 point)
        {
            Vector3 local = point - transform.position;
            return Mathf.Abs(local.x) <= area.x * 0.5f && Mathf.Abs(local.z) <= area.y * 0.5f && local.y >= -depth;
        }

        /// <summary>point가 어떤 물 영역의 평면 범위 안에 있으면 그 수면 높이를 돌려준다.</summary>
        public static bool TryGetSurface(Vector3 point, out float surfaceY)
        {
            foreach (var water in Active)
            {
                if (!water.Contains(point)) continue;
                surfaceY = water.SurfaceY;
                return true;
            }
            surfaceY = 0f;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.25f);
            Gizmos.DrawCube(transform.position + Vector3.down * depth * 0.5f, new Vector3(area.x, depth, area.y));
        }
    }
}
