using System.Collections.Generic;
using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 방송 속보 자막처럼 오른쪽에서 왼쪽으로 흘러가는 뉴스 한 줄. 한 문장이 다 지나가면
    /// 기본 문구와 보유 건물에 맞춘 문구 중 하나를 골라 다음 문장을 흘려보낸다.
    /// newsText는 마스크(RectMask2D)가 있는 칸 안에 둔다.
    /// </summary>
    public class NewsTickerView : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private Text newsText;
        [Tooltip("흘러가는 속도 (픽셀/초, 캔버스 기준 해상도)")]
        [SerializeField] private float scrollSpeed = 120f;
        [Tooltip("앞 문장이 다 지나가고 다음 문장이 들어오기 전 빈칸 (픽셀)")]
        [SerializeField] private float gap = 60f;
        [SerializeField, TextArea] private string[] generalNews =
        {
            "낙지 회장님, 갯벌 탈출 시도… 3초 만에 체포",
            "소 '음메에바', 오늘도 무표정으로 출근",
            "새우 대리, 허리 펴기 도전 실패… \"원래 이랬다\"",
            "전문가: \"낙곱새는 마지막에 밥 볶을 때가 진짜다\"",
            "동네 주민들, 낙곱새 냄새에 단체로 배고픔 호소",
            "오늘의 운세: 냄비를 저으면 좋은 일이 생김. 가보자고!",
            "킹받는 사실: 낙곱새를 저을수록 배가 더 고파진다",
            "야르! 오늘도 냄비는 보글보글",
        };
        [SerializeField, TextArea] private string[] generalNewsEn =
        {
            "Chairman Octopus attempts mudflat escape... caught in 3 seconds",
            "Cow 'Moomeeba' clocks in with a blank stare, as usual",
            "Assistant Manager Shrimp fails to straighten his back: \"It was always like this\"",
            "Expert: \"The real nakgopsae starts when you fry the rice at the end\"",
            "Neighbors report mass hunger caused by nakgopsae smell",
            "Today's fortune: stir the pot and good things happen. Let's go!",
            "Annoying fact: the more you stir nakgopsae, the hungrier you get",
            "Yarr! The pot is bubbling again today",
        };

        private readonly List<string> candidates = new List<string>();
        private RectTransform textRect;
        private RectTransform area;

        private void Start()
        {
            textRect = newsText.rectTransform;
            area = (RectTransform)textRect.parent;
            // 한 줄로 길게 늘어나도록 하고, 왼쪽 끝을 기준점으로 삼아 움직인다.
            newsText.horizontalOverflow = HorizontalWrapMode.Overflow;
            newsText.resizeTextForBestFit = false;
            newsText.alignment = TextAnchor.MiddleLeft;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 0.5f);
            ShowNext();
        }

        private void Update()
        {
            var position = textRect.anchoredPosition;
            position.x -= scrollSpeed * Time.unscaledDeltaTime;
            textRect.anchoredPosition = position;

            // 문장 끝이 칸 왼쪽 밖으로 완전히 나가면 다음 문장
            if (position.x + textRect.sizeDelta.x < -gap) ShowNext();
        }

        private void ShowNext()
        {
            candidates.Clear();
            candidates.AddRange(Loc.English && generalNewsEn.Length > 0 ? generalNewsEn : generalNews);

            var config = game.Config;
            for (int i = 0; i < config.Buildings.Count; i++)
            {
                int count = game.State.BuildingCounts[i];
                if (count > 0)
                    candidates.Add(Loc.T($"{config.Buildings[i].DisplayName} {count}개가 쉬지 않고 {config.CurrencyName}를 만들고 있습니다",
                        $"{count} x {config.Buildings[i].DisplayName} making {config.CurrencyName} nonstop"));
            }
            if (game.State.Cookies >= 1_000_000)
                candidates.Add(Loc.T($"화제의 맛집, {config.CurrencyName} {NumberFormatter.Format(game.State.Cookies)}인분 보유 중! 줄이 끝이 안 보인다",
                    $"Hot spot holds {NumberFormatter.Format(game.State.Cookies)} servings of {config.CurrencyName}! The line never ends"));

            newsText.text = candidates[Random.Range(0, candidates.Count)];
            textRect.sizeDelta = new Vector2(newsText.preferredWidth, 0f);
            // 칸 오른쪽 끝 바로 밖에서 출발한다.
            textRect.anchoredPosition = new Vector2(area.rect.width + gap, 0f);
        }
    }
}
