using UnityEngine;

namespace CookieClicker.Data
{
    public enum UpgradeEffectType
    {
        /// <summary>대상 건물의 생산량에 배수를 곱한다 (옵션으로 클릭에도 적용).</summary>
        BuildingMultiplier,
        /// <summary>클릭할 때마다 초당 생산량의 일정 비율을 추가로 얻는다.</summary>
        ClickCpsPercent,
    }

    /// <summary>
    /// 업그레이드 하나의 효과와 해금 조건. 새 업그레이드는 코드 수정 없이 에셋만 추가하면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Cookie Clicker/Upgrade", fileName = "Upgrade_")]
    public class UpgradeDefinition : ScriptableObject
    {
        [Tooltip("세이브 파일에 기록되는 고유 ID. 출시 후에는 바꾸지 마세요.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private double cost = 100;
        [SerializeField] private Color iconColor = Color.white;

        [Header("효과")]
        [SerializeField] private UpgradeEffectType effectType;
        [SerializeField] private BuildingDefinition targetBuilding;
        [SerializeField] private float multiplier = 2f;
        [SerializeField] private bool alsoAffectsClick;
        [SerializeField, Range(0f, 1f)] private float clickCpsPercent = 0.01f;

        [Header("해금 조건")]
        [SerializeField] private BuildingDefinition requiredBuilding;
        [SerializeField, Min(0)] private int requiredCount = 1;
        [SerializeField] private double requiredHandmadeCookies;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public double Cost => cost;
        public Color IconColor => iconColor;
        public UpgradeEffectType EffectType => effectType;
        public BuildingDefinition TargetBuilding => targetBuilding;
        public float Multiplier => multiplier;
        public bool AlsoAffectsClick => alsoAffectsClick;
        public float ClickCpsPercent => clickCpsPercent;
        public BuildingDefinition RequiredBuilding => requiredBuilding;
        public int RequiredCount => requiredCount;
        public double RequiredHandmadeCookies => requiredHandmadeCookies;

#if UNITY_EDITOR
        public void EditorInitialize(string newId, string newName, string newDescription, double newCost,
            Color color, UpgradeEffectType type, BuildingDefinition target, float mult, bool affectsClick,
            float cpsPercent, BuildingDefinition required, int count, double handmade)
        {
            id = newId;
            displayName = newName;
            description = newDescription;
            cost = newCost;
            iconColor = color;
            effectType = type;
            targetBuilding = target;
            multiplier = mult;
            alsoAffectsClick = affectsClick;
            clickCpsPercent = cpsPercent;
            requiredBuilding = required;
            requiredCount = count;
            requiredHandmadeCookies = handmade;
        }
#endif
    }
}
