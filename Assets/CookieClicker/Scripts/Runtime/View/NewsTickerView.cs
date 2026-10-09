using System.Collections.Generic;
using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>상단 뉴스 한 줄. 기본 문구와 보유 건물에 맞춘 문구를 섞어서 일정 시간마다 바꾼다.</summary>
    public class NewsTickerView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Text newsText;
        [SerializeField] private float interval = 8f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField, TextArea] private string[] generalNews =
        {
            "오늘의 날씨: 맑음, 가끔 쿠키 부스러기.",
            "전문가: \"쿠키는 많을수록 좋다.\"",
            "동네 주민, 갑자기 쿠키 냄새가 난다고 신고.",
            "과학자들, 쿠키의 무한한 가능성에 주목.",
        };

        private readonly List<string> candidates = new List<string>();
        private float timer;

        private void Start() => ShowNext();

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= interval) ShowNext();

            // 바뀐 직후 서서히 나타나고, 바뀌기 직전에 서서히 사라진다.
            float fadeIn = Mathf.Clamp01(timer / fadeDuration);
            float fadeOut = Mathf.Clamp01((interval - timer) / fadeDuration);
            var c = newsText.color;
            c.a = Mathf.Min(fadeIn, fadeOut);
            newsText.color = c;
        }

        private void ShowNext()
        {
            timer = 0f;
            candidates.Clear();
            candidates.AddRange(generalNews);

            var config = game.Config;
            for (int i = 0; i < config.Buildings.Count; i++)
            {
                int count = game.State.BuildingCounts[i];
                if (count > 0)
                    candidates.Add($"속보: {config.Buildings[i].DisplayName} {count}개가 쉬지 않고 쿠키를 만들고 있습니다.");
            }
            if (game.State.Cookies >= 1_000_000)
                candidates.Add($"화제의 빵집, 쿠키 {NumberFormatter.Format(game.State.Cookies)}개 보유 중!");

            newsText.text = candidates[Random.Range(0, candidates.Count)];
        }
    }
}
