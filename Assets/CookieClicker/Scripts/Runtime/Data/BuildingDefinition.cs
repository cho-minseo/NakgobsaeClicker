using CookieClicker.Core;
using UnityEngine;

namespace CookieClicker.Data
{
    /// <summary>
    /// 건물 하나의 "설계도". 가격·생산량 같은 고정 데이터만 담고, 보유 개수 같은 변하는 값은 GameState가 가진다.
    /// </summary>
    [CreateAssetMenu(menuName = "Cookie Clicker/Building", fileName = "Building_")]
    public class BuildingDefinition : ScriptableObject
    {
        [Tooltip("세이브 파일에 기록되는 고유 ID. 출시 후에는 바꾸지 마세요.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [Header("영어 (비워 두면 한국어를 그대로 보여 줌)")]
        [SerializeField] private string displayNameEn;
        [SerializeField, TextArea] private string descriptionEn;
        [SerializeField] private double baseCost = 15;
        [Tooltip("건물 1개가 1초에 만드는 양 (업그레이드 적용 전)")]
        [SerializeField] private double baseCps = 0.1;
        [SerializeField] private Color iconColor = Color.white;

        public string Id => id;
        public string DisplayName => Loc.T(displayName, displayNameEn);
        public string Description => Loc.T(description, descriptionEn);
        public double BaseCost => baseCost;
        public double BaseCps => baseCps;
        public Color IconColor => iconColor;

#if UNITY_EDITOR
        public void EditorSetEnglish(string name, string newDescription)
        {
            displayNameEn = name;
            descriptionEn = newDescription;
        }

        public void EditorInitialize(string newId, string newName, string newDescription,
            double cost, double cps, Color color)
        {
            id = newId;
            displayName = newName;
            description = newDescription;
            baseCost = cost;
            baseCps = cps;
            iconColor = color;
        }
#endif
    }
}
