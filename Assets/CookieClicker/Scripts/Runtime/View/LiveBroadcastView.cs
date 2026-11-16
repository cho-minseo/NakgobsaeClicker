using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CookieClicker.View
{
    /// <summary>
    /// 클리커 화면 구석의 "● LIVE 공사 현장" 중계 창. 공사 카메라가 그리는 Render Texture를 보여 준다.
    /// 켜고 끄는 모습(TV가 켜지듯 가운데 줄에서 펼쳐짐)은 Animator의 Open 값으로 애니메이션 클립이 맡는다.
    /// 클릭하면 Clicked를 알려서 공사 연출이 바로 끝내게 한다.
    /// </summary>
    public class LiveBroadcastView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Animator animator;
        [Tooltip("\"원양 새우잡이배\"처럼 공사 중인 건물 이름")]
        [SerializeField] private Text subtitle;
        [Tooltip("다 닫혔을 때 클릭을 막지 않도록 끄는 그룹")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Header("소리")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("TV가 켜질 때 지직거리는 소리")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;

        private static readonly int OpenId = Animator.StringToHash("Open");
        private static readonly int ClosedStateId = Animator.StringToHash("Closed");

        public event Action Clicked;

        public bool IsOpen => animator.GetBool(OpenId);
        /// <summary>닫히는 애니메이션까지 다 끝나서 화면에서 사라졌는지</summary>
        public bool IsFullyClosed => !IsOpen && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == ClosedStateId && !animator.IsInTransition(0);

        public void Show(string buildingName)
        {
            subtitle.text = buildingName;
            if (!IsOpen && audioSource != null && openSound != null) audioSource.PlayOneShot(openSound);
            animator.SetBool(OpenId, true);
            canvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            if (IsOpen && audioSource != null && closeSound != null) audioSource.PlayOneShot(closeSound);
            animator.SetBool(OpenId, false);
            canvasGroup.blocksRaycasts = false;
        }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    }
}
