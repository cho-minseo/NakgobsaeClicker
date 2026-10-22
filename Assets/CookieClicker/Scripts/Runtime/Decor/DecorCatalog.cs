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

        public IReadOnlyList<DecorPiece> Pieces => pieces;
        public float RefundRatio => refundRatio;

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
