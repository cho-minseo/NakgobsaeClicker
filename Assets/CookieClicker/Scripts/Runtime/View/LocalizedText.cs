using CookieClicker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 씬에 고정된 글자(버튼 이름, 패널 제목 등)를 언어 설정에 맞게 바꾼다. 같은 오브젝트의 Text에 붙인다.
    /// 코드가 매번 새로 쓰는 글자(보유량, 통계 등)에는 붙이지 않는다 — 그쪽은 코드에서 Loc.T로 고른다.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField, TextArea] private string korean;
        [SerializeField, TextArea] private string english;

        private Text text;

        private void Awake() => text = GetComponent<Text>();

        private void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        private void OnDisable() => GameSettings.Changed -= Apply;

        private void Apply()
        {
            if (text == null) text = GetComponent<Text>();
            text.text = Loc.T(korean, english);
        }

#if UNITY_EDITOR
        public void EditorSet(string newKorean, string newEnglish)
        {
            korean = newKorean;
            english = newEnglish;
        }
#endif
    }
}
