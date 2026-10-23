using System.Collections.Generic;
using UnityEngine;

namespace CookieClicker.Decor
{
    /// <summary>꾸미기 모드에서 고를 수 있는 물건 목록. 아래 칸(핫바)에 이 순서대로 나온다.</summary>
    [CreateAssetMenu(menuName = "Cookie Clicker/Decor Catalog", fileName = "DecorCatalog")]
    public class DecorCatalog : ScriptableObject
    {
        [SerializeField] private List<DecorPiece> pieces = new List<DecorPiece>();
        [Tooltip("회수할 때 돌려받는 비율 (0.5 = 반값)")]
        [SerializeField, Range(0f, 1f)] private float refundRatio = 0.5f;
        [Tooltip("색 바꾸기(C)로 고를 수 있는 색. 순서가 세이브에 번호로 저장되니 바꿀 때는 뒤에 추가하는 게 안전하다")]
        [SerializeField] private Color[] paintColors =
        {
            new Color(0.88f, 0.22f, 0.17f), // 빨강
            new Color(0.96f, 0.55f, 0.2f),  // 주황
            new Color(0.98f, 0.82f, 0.3f),  // 노랑
            new Color(0.45f, 0.72f, 0.36f), // 초록
            new Color(0.4f, 0.8f, 0.75f),   // 민트
            new Color(0.38f, 0.65f, 0.92f), // 하늘
            new Color(0.22f, 0.32f, 0.6f),  // 남색
            new Color(0.62f, 0.45f, 0.85f), // 보라
            new Color(0.97f, 0.62f, 0.72f), // 분홍
            new Color(0.97f, 0.95f, 0.9f),  // 흰색
            new Color(0.25f, 0.24f, 0.26f), // 검정
            new Color(0.55f, 0.36f, 0.22f), // 나무색
        };

        public IReadOnlyList<DecorPiece> Pieces => pieces;
        public float RefundRatio => refundRatio;
        public IReadOnlyList<Color> PaintColors => paintColors;

        /// <summary>색 번호의 색. 범위 밖(-1 등)이면 null = 원래 색</summary>
        public Color? PaintColor(int index) => index >= 0 && index < paintColors.Length ? paintColors[index] : (Color?)null;

        public DecorPiece Find(string id)
        {
            foreach (var piece in pieces)
                if (piece != null && piece.Id == id) return piece;
            return null;
        }

#if UNITY_EDITOR
        public void EditorSetPieces(List<DecorPiece> newPieces) => pieces = newPieces;
#endif
    }
}
