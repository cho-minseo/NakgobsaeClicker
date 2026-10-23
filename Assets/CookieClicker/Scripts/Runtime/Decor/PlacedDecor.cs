using System.Collections.Generic;
using CookieClicker.Core;
using UnityEngine;

namespace CookieClicker.Decor
{
    /// <summary>
    /// 평상 부지에 놓인 물건 하나. 놓을 때 통통 튀며 커지고, 회수할 때 쏙 줄어들며 사라진다.
    /// 튀는 모양은 popCurve(인스펙터 그래프)로 고칠 수 있다.
    /// 크기는 겉모습(model)만 바뀌고 충돌 상자는 루트에 있어서, 튀는 도중에도 위에 다른 물건을 올리거나 겹침 검사를 할 수 있다.
    /// 색 바꾸기는 재질을 복사하지 않고 MaterialPropertyBlock으로 칠해서, 같은 물건을 여러 색으로 놓아도 재질이 늘지 않는다.
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

        // URP Lit/Unlit은 _BaseColor, glTFast(Kenney 모델) 재질은 baseColorFactor를 쓴다.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int GltfBaseColorId = Shader.PropertyToID("baseColorFactor");
        private static MaterialPropertyBlock block;

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

        /// <summary>칠할 재질 칸에 색을 입힌다. null이면 원래 색으로 되돌린다.</summary>
        public void ApplyColor(Color? color)
        {
            block ??= new MaterialPropertyBlock();
            foreach (var (renderer, slot) in PaintTargets())
            {
                if (color == null)
                {
                    renderer.SetPropertyBlock(null, slot);
                    continue;
                }
                renderer.GetPropertyBlock(block, slot);
                block.SetColor(BaseColorId, color.Value);
                block.SetColor(GltfBaseColorId, color.Value);
                renderer.SetPropertyBlock(block, slot);
            }
        }

        /// <summary>
        /// 칠할 (렌더러, 재질 칸) 목록. Piece에 재질 이름이 있으면 그 이름, 없으면 가장 많이 쓰인 재질
        /// (의자라면 몸통 플라스틱, TV라면 나무 몸체처럼 대부분을 차지하는 부분).
        /// </summary>
        private List<(Renderer, int)> PaintTargets()
        {
            var renderers = Visual.GetComponentsInChildren<Renderer>();
            string target = Piece != null ? Piece.PaintMaterial : null;
            if (string.IsNullOrEmpty(target))
            {
                var counts = new Dictionary<string, int>();
                foreach (var r in renderers)
                    foreach (var m in r.sharedMaterials)
                        if (m != null) counts[m.name] = counts.TryGetValue(m.name, out int n) ? n + 1 : 1;
                int best = 0;
                foreach (var pair in counts)
                    if (pair.Value > best) { best = pair.Value; target = pair.Key; }
            }

            var targets = new List<(Renderer, int)>();
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && materials[i].name == target) targets.Add((r, i));
            }
            return targets;
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
