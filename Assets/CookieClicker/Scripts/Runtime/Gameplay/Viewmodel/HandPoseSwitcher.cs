using System;
using UnityEngine;

namespace CookieClicker.Gameplay.Viewmodel
{
    public enum HandPose
    {
        Open,
        Fist,
        /// <summary>국자·낚싯대처럼 막대를 쥔 손</summary>
        Grip,
        /// <summary>양동이 손잡이처럼 위에서 잡는 손</summary>
        Carry,
    }

    /// <summary>
    /// 떠다니는 손(리깅 없음) 하나. 포즈마다 손 모델을 따로 두고 하나만 켠다.
    /// 바뀌는 순간 살짝 커졌다 돌아오는 "팝" 연출로 모델이 갈아 끼워지는 게 티 나지 않게 한다.
    /// 나중에 AI로 만든 손 모델은 각 포즈 오브젝트 아래에 넣기만 하면 된다.
    /// </summary>
    public class HandPoseSwitcher : MonoBehaviour
    {
        [Serializable]
        public class PoseVisual
        {
            public HandPose pose;
            public GameObject visual;
        }

        [SerializeField] private PoseVisual[] poses = Array.Empty<PoseVisual>();
        [SerializeField] private HandPose startPose = HandPose.Open;
        [Tooltip("이 포즈 모델이 없을 때 대신 쓸 포즈")]
        [SerializeField] private HandPose fallbackPose = HandPose.Fist;
        [SerializeField] private float popScale = 1.15f;
        [SerializeField] private float popRecoverSpeed = 12f;

        public HandPose Current { get; private set; }

        private Vector3 baseScale;
        private float pop = 1f;

        private void Awake()
        {
            baseScale = transform.localScale;
            Current = (HandPose)(-1);
            SetPose(startPose);
        }

        private void Update()
        {
            pop = Mathf.Lerp(pop, 1f, popRecoverSpeed * Time.deltaTime);
            transform.localScale = baseScale * pop;
        }

        public void SetPose(HandPose pose)
        {
            if (pose == Current) return;
            if (Find(pose) == null) pose = fallbackPose;

            foreach (var p in poses)
            {
                if (p.visual != null) p.visual.SetActive(p.pose == pose);
            }
            Current = pose;
            pop = popScale;
        }

        private GameObject Find(HandPose pose)
        {
            foreach (var p in poses) if (p.pose == pose) return p.visual;
            return null;
        }
    }
}
