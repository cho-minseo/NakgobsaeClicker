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

    /// <summary>핫바 탭 (Tab으로 바꾼다).</summary>
    public enum DecorCategory
    {
        /// <summary>바닥·벽·기둥·계단·지붕</summary>
        Structure,
        /// <summary>의자·테이블 같은 가구와 소품</summary>
        Furniture,
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
        [Tooltip("바닥 기준점(pivot)이 아래쪽 가운데인 프리팹")]
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public string DisplayName => Loc.T(displayName, string.IsNullOrEmpty(displayNameEn) ? displayName : displayNameEn);
        public DecorSnap Snap => snap;
        public DecorCategory Category => category;
        public string SlotGroup => string.IsNullOrEmpty(slotGroup) ? snap.ToString() : slotGroup;
        public double Cost => cost;
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
#endif
    }
}
