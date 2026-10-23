using CookieClicker.Core;
using UnityEngine;

namespace CookieClicker.Decor
{
    /// <summary>꾸미기 물건이 격자의 어디에 붙는지.</summary>
    public enum DecorSnap
    {
        /// <summary>칸 한가운데 (바닥, 지붕, 계단)</summary>
        Floor,
        /// <summary>칸의 테두리 선 (벽, 창문 벽, 문 벽)</summary>
        Wall,
        /// <summary>칸의 꼭짓점 (기둥)</summary>
        Pillar,
        /// <summary>바닥 위 작은 격자 (의자, 테이블 같은 가구)</summary>
        Free,
    }

    /// <summary>핫바 탭 (Tab으로 바꾼다). 세이브·에셋에 숫자로 저장되므로 순서를 바꾸지 말고 뒤에 추가한다.</summary>
    public enum DecorCategory
    {
        /// <summary>바닥·벽·기둥·계단·지붕</summary>
        Structure,
        /// <summary>의자·테이블·평상</summary>
        Furniture,
        /// <summary>가전 (버너, TV, 냉장고, 조명...)</summary>
        Appliance,
        /// <summary>소품 (화분, 러그, 상자...)</summary>
        Prop,
    }

    /// <summary>
    /// 꾸미기 물건 한 종류. 모양은 prefab에, 가격·붙는 위치·이름은 여기에 둔다.
    /// 가격을 바꾸거나 새 물건을 추가할 때는 이 에셋만 고치고 DecorCatalog에 넣으면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Cookie Clicker/Decor Piece", fileName = "Decor_")]
    public class DecorPiece : ScriptableObject
    {
        [Tooltip("세이브에 저장되는 이름. 한 번 정하면 바꾸지 마세요 (바꾸면 이미 놓은 물건이 사라짐)")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string displayNameEn;
        [SerializeField] private DecorSnap snap;
        [SerializeField] private DecorCategory category;
        [Tooltip("같은 자리에 함께 놓을 수 없는 묶음. 비우면 붙는 위치 이름을 쓴다 (예: 계단은 \"Stair\"로 두면 바닥과 같은 칸에 놓을 수 있음)")]
        [SerializeField] private string slotGroup;
        [SerializeField] private double cost = 50;
        [Tooltip("러그처럼 바닥에 까는 물건: 다른 물건과 겹쳐도 놓을 수 있고, 다른 물건도 이 위·옆에 겹쳐 놓을 수 있다")]
        [SerializeField] private bool flat;
        [Tooltip("색 바꾸기로 칠할 재질 이름 (비우면 가장 넓게 쓰인 재질)")]
        [SerializeField] private string paintMaterial;
        [Tooltip("바닥 기준점(pivot)이 아래쪽 가운데인 프리팹")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => Loc.T(displayName, string.IsNullOrEmpty(displayNameEn) ? displayName : displayNameEn);
        public DecorSnap Snap => snap;
        public DecorCategory Category => category;
        public string SlotGroup => string.IsNullOrEmpty(slotGroup) ? snap.ToString() : slotGroup;
        public double Cost => cost;
        public bool Flat => flat;
        public string PaintMaterial => paintMaterial;
        public GameObject Prefab => prefab;
        public Sprite Icon => icon;

#if UNITY_EDITOR
        public void EditorSetup(string newId, string name, string nameEn, DecorSnap newSnap, DecorCategory newCategory, string group, double newCost, GameObject newPrefab)
        {
            category = newCategory;
            id = newId;
            displayName = name;
            displayNameEn = nameEn;
            snap = newSnap;
            slotGroup = group;
            cost = newCost;
            prefab = newPrefab;
        }

        public void EditorSetIcon(Sprite sprite) => icon = sprite;

        /// <summary>코드(설치 도구)가 정하는 분류 값. 가격·이름처럼 사람이 고치는 값이 아니라서 설치할 때마다 맞춘다.</summary>
        public void EditorSetKind(DecorCategory newCategory, bool newFlat, string newPaintMaterial)
        {
            category = newCategory;
            flat = newFlat;
            paintMaterial = newPaintMaterial;
        }
#endif
    }
}
