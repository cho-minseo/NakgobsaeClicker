using UnityEngine;
using UnityEngine.EventSystems;

namespace CookieClicker.View
{
    /// <summary>
    /// 마우스를 올리면 대상이 살짝 커진다 (글씨만 있는 버튼이 "누를 수 있다"는 걸 보여 주는 용도).
    /// 버튼 자체 크기는 Animator가 움직이므로, 겹치지 않게 자식(글씨)의 크기를 바꾼다.
    /// </summary>
    public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Transform target;
        [SerializeField] private float hoverScale = 1.12f;
        [Tooltip("클수록 빨리 커지고 줄어든다")]
        [SerializeField] private float speed = 14f;

        private bool hovered;

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        private void Update()
        {
            if (target == null) return;
            float goal = hovered ? hoverScale : 1f;
            float scale = Mathf.Lerp(target.localScale.x, goal, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            target.localScale = Vector3.one * scale;
        }

        private void OnDisable()
        {
            hovered = false;
            if (target != null) target.localScale = Vector3.one;
        }
    }
}
