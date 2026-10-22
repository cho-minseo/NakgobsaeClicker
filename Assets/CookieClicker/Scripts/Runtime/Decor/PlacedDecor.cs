using CookieClicker.Core;
using UnityEngine;

namespace CookieClicker.Decor
{
    /// <summary>
    /// 평상 부지에 놓인 물건 하나. 놓을 때 통통 튀며 커지고, 회수할 때 쏙 줄어들며 사라진다.
    /// 튀는 모양은 popCurve(인스펙터 그래프)로 고칠 수 있다.
    /// 크기는 겉모습(model)만 바뀌고 충돌 상자는 루트에 있어서, 튀는 도중에도 위에 다른 물건을 올리거나 겹침 검사를 할 수 있다.
    /// </summary>
    public class PlacedDecor : MonoBehaviour
    {
        [Tooltip("튀는 애니메이션으로 크기가 바뀌는 겉모습 (비우면 루트 전체)")]
        [SerializeField] private Transform model;
        [SerializeField] private float popDuration = 0.4f;
        [Tooltip("시간(0~1)에 따른 크기. 1을 살짝 넘었다가 돌아오면 통통 튀는 느낌")]
        [SerializeField] private AnimationCurve popCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 4f), new Keyframe(0.55f, 1.15f), new Keyframe(0.8f, 0.95f), new Keyframe(1f, 1f));
        [SerializeField] private float removeDuration = 0.18f;

        public DecorPiece Piece { get; private set; }
        public DecorRecord Record { get; private set; }

        private Transform Visual => model != null ? model : transform;
        private Vector3 baseScale;
        private float timer = -1f;
        private bool removing;

        public void Init(DecorPiece piece, DecorRecord record, bool animate)
        {
            Piece = piece;
            Record = record;
            baseScale = Visual.localScale;
            if (!animate) return;
            timer = 0f;
            Visual.localScale = Vector3.zero;
        }

        /// <summary>회수: 줄어드는 동안 충돌을 꺼서 바로 그 자리에 다른 걸 놓을 수 있게 한다.</summary>
        public void PlayRemove()
        {
            removing = true;
            timer = 0f;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        private void Update()
        {
            if (timer < 0f) return;
            timer += Time.deltaTime;

            if (removing)
            {
                float t = Mathf.Clamp01(timer / removeDuration);
                Visual.localScale = baseScale * (1f - t * t);
                if (t >= 1f) Destroy(gameObject);
                return;
            }

            float p = Mathf.Clamp01(timer / popDuration);
            Visual.localScale = baseScale * popCurve.Evaluate(p);
            if (p >= 1f)
            {
                Visual.localScale = baseScale;
                timer = -1f;
            }
        }
    }
}
