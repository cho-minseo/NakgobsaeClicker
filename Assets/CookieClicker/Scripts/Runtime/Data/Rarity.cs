using CookieClicker.Core;

namespace CookieClicker.Data
{
    /// <summary>상점 칸 색 등급. 색과 건물 기준 개수는 GameConfig에서 정한다.</summary>
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    public static class RarityNames
    {
        public static string Get(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => Loc.T("고급", "Uncommon"),
            Rarity.Rare => Loc.T("희귀", "Rare"),
            Rarity.Epic => Loc.T("영웅", "Epic"),
            Rarity.Legendary => Loc.T("전설", "Legendary"),
            _ => Loc.T("일반", "Common"),
        };
    }
}
