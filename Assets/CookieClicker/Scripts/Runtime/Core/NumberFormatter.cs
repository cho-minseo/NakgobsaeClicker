using System;

namespace CookieClicker.Core
{
    /// <summary>큰 숫자를 "1,234.5만", "3.2억"처럼 한국식 단위로 표시한다. 모든 UI가 이 함수 하나를 공유한다.</summary>
    public static class NumberFormatter
    {
        private static readonly string[] Units =
            { "", "만", "억", "조", "경", "해", "자", "양", "구", "간", "정", "재", "극" };

        public static string Format(double value, bool allowDecimals = false)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "∞";
            if (value < 0) return "-" + Format(-value, allowDecimals);

            if (value < 1_000_000)
            {
                if (allowDecimals && value < 100 && value != Math.Floor(value))
                    return value.ToString("0.#");
                return Math.Floor(value).ToString("#,0");
            }

            int group = (int)(Math.Log10(value) / 4);
            if (group >= Units.Length) return value.ToString("0.###e+0");

            double mantissa = value / Math.Pow(10000, group);
            return mantissa.ToString("#,0.##") + Units[group];
        }

        public static string FormatTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}시간 {t.Minutes}분 {t.Seconds}초"
                : $"{t.Minutes}분 {t.Seconds}초";
        }
    }
}
