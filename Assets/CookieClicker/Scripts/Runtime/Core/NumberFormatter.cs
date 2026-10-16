using System;

namespace CookieClicker.Core
{
    /// <summary>
    /// 큰 숫자를 한국어는 "1,234.5만", "3.2억"(만 단위), 영어는 "1.234 million"(천 단위)처럼 표시한다.
    /// 모든 UI가 이 함수 하나를 공유한다.
    /// </summary>
    public static class NumberFormatter
    {
        private static readonly string[] KoreanUnits =
            { "", "만", "억", "조", "경", "해", "자", "양", "구", "간", "정", "재", "극" };

        // 1,000,000 이상부터 쓰므로 0번(천)은 건너뛴다.
        private static readonly string[] EnglishUnits =
        {
            "", "thousand", "million", "billion", "trillion", "quadrillion", "quintillion",
            "sextillion", "septillion", "octillion", "nonillion", "decillion",
        };

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

            return Loc.English ? FormatEnglish(value) : FormatKorean(value);
        }

        private static string FormatKorean(double value)
        {
            int group = (int)(Math.Log10(value) / 4);
            if (group >= KoreanUnits.Length) return value.ToString("0.###e+0");
            double mantissa = value / Math.Pow(10000, group);
            return mantissa.ToString("#,0.##") + KoreanUnits[group];
        }

        private static string FormatEnglish(double value)
        {
            int group = (int)(Math.Log10(value) / 3);
            if (group >= EnglishUnits.Length) return value.ToString("0.###e+0");
            double mantissa = value / Math.Pow(1000, group);
            return mantissa.ToString("0.###") + " " + EnglishUnits[group];
        }

        public static string FormatTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            if (Loc.English)
            {
                return t.TotalHours >= 1
                    ? $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s"
                    : $"{t.Minutes}m {t.Seconds}s";
            }
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}시간 {t.Minutes}분 {t.Seconds}초"
                : $"{t.Minutes}분 {t.Seconds}초";
        }
    }
}
