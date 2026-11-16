using System.Collections;
using System.Collections.Generic;
using CookieClicker.Core;
using CookieClicker.View;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

namespace CookieClicker.Gameplay
{
    /// <summary>
    /// 건물을 처음 사거나 단계가 오르면 그 자리에서 공사 장면을 틀고, 클리커 화면의 "LIVE 공사 현장" 창으로 생중계한다.
    /// 장면 자체(펜스·비계·먼지·불꽃·카메라 움직임)는 Timeline 에셋이 맡고, 이 컴포넌트는
    /// 대기열 관리, 장비(Rig)를 건물 자리·크기에 맞추기, 공개 신호(Reveal)에 맞춰 건물 바꾸기만 한다.
    /// </summary>
    public class ConstructionDirector : MonoBehaviour, INotificationReceiver
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private BuildingPlot[] plots;

        [Header("공사 장비 (Timeline이 움직이는 것들)")]
        [SerializeField] private PlayableDirector timeline;
        [Tooltip("펜스·비계·파티클·카메라를 담은 루트. 건물 자리로 옮기고 건물 바닥 크기에 맞춰 키운다")]
        [SerializeField] private Transform rig;
        [Tooltip("비계 네 면. 각 면 아래 Level0, Level1... 층을 건물 높이만큼만 켠다")]
        [SerializeField] private Transform[] scaffoldSides;
        [Tooltip("카메라가 바라보는 점. 건물 높이의 가운데로 올린다")]
        [SerializeField] private Transform cameraPivot;
        [Tooltip("Cinemachine Brain이 붙은 중계 카메라. 어느 샷(가상 카메라)을 쓸지는 Timeline의 Cinemachine 트랙이 정한다")]
        [SerializeField] private Camera broadcastCamera;
        [Tooltip("비계가 바닥에 부딪힐 때 화면을 흔드는 Cinemachine Impulse")]
        [SerializeField] private CinemachineImpulseSource impulse;
        [Tooltip("흔들림 세기 (장비 크기 1 기준, 건물이 클수록 세게)")]
        [SerializeField, Min(0f)] private float impulseForce = 0.05f;
        [SerializeField] private LiveBroadcastView broadcast;

        [Header("Timeline 신호")]
        [Tooltip("비계가 넘어지는 순간: 건물을 새 모습으로 바꾸고 솟아오르게 한다")]
        [SerializeField] private SignalAsset revealSignal;
        [Tooltip("장면 끝: 잠시 뒤 중계 창을 닫는다")]
        [SerializeField] private SignalAsset doneSignal;
        [Tooltip("비계가 바닥에 부딪히는 순간: 카메라를 흔든다")]
        [SerializeField] private SignalAsset collapseSignal;

        [Header("크기·대기열")]
        [Tooltip("비계 한 층 높이 (장비 크기 1 기준)")]
        [SerializeField, Min(0.1f)] private float levelHeight = 0.35f;
        [Tooltip("건물 바닥보다 장비를 얼마나 크게 둘지")]
        [SerializeField, Min(1f)] private float padding = 1.25f;
        [Tooltip("기다리는 공사가 이보다 많으면 오래된 것부터 연출 없이 바로 완성시킨다")]
        [SerializeField, Min(1)] private int maxQueue = 3;
        [Tooltip("장면이 끝나고 창을 닫기까지 기다리는 시간 (초)")]
        [SerializeField, Min(0f)] private float closeDelay = 1f;

        private class Job
        {
            public BuildingPlot plot;
            public int from;
            public int to;
        }

        private readonly List<Job> queue = new List<Job>();
        private Job current;
        private bool revealed;
        private Coroutine closing;

        public bool IsPlaying => current != null;

        private void Awake()
        {
            rig.gameObject.SetActive(false);
            broadcastCamera.enabled = false;
        }

        private void OnEnable()
        {
            foreach (var plot in plots) plot.StageReached += OnStageReached;
            game.ProgressReset += CancelAll;
            broadcast.Clicked += Skip;
        }

        private void OnDisable()
        {
            foreach (var plot in plots) plot.StageReached -= OnStageReached;
            game.ProgressReset -= CancelAll;
            broadcast.Clicked -= Skip;
        }

        private void OnStageReached(BuildingPlot plot, int from, int to)
        {
            // 같은 건물이 이미 기다리고 있으면 마지막 단계 하나로 합친다.
            var waiting = queue.Find(j => j.plot == plot);
            if (waiting != null)
            {
                waiting.to = to;
                return;
            }
            if (current != null && current.plot == plot)
            {
                // 지금 공사 중인 건물이 또 오르면, 이번 공사가 끝난 모습에서 이어서 한 번 더 공사한다.
                queue.Add(new Job { plot = plot, from = current.to, to = to });
            }
            else
            {
                plot.Hold(from);
                queue.Add(new Job { plot = plot, from = from, to = to });
            }

            while (queue.Count > maxQueue)
            {
                var skipped = queue[0];
                queue.RemoveAt(0);
                if (current == null || current.plot != skipped.plot) skipped.plot.Release(false);
            }
            if (current == null) StartNext();
        }

        private void StartNext()
        {
            if (queue.Count == 0) return;
            current = queue[0];
            queue.RemoveAt(0);
            revealed = false;

            FitRig(current.plot, current.to);
            rig.gameObject.SetActive(true);
            broadcastCamera.enabled = true;
            broadcast.Show(current.plot.Building.DisplayName);
            timeline.time = 0;
            timeline.Evaluate();
            timeline.Play();
        }

        /// <summary>장비를 건물 자리로 옮기고, 바닥 크기에 맞춰 키우고, 비계를 건물 높이만큼 쌓는다.</summary>
        private void FitRig(BuildingPlot plot, int stage)
        {
            var bounds = plot.MeasureStage(stage);
            var size = bounds.size;
            float scale = Mathf.Max(size.x, size.z) * padding;
            rig.SetPositionAndRotation(plot.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)),
                plot.transform.rotation);
            rig.localScale = Vector3.one * scale;

            float height = size.y / scale; // 장비 크기 1 기준 건물 높이
            foreach (var side in scaffoldSides)
            {
                int levels = Mathf.Clamp(Mathf.CeilToInt(height * 1.05f / levelHeight), 1, side.childCount);
                for (int i = 0; i < side.childCount; i++)
                {
                    var level = side.GetChild(i);
                    if (level.name.StartsWith("Level")) level.gameObject.SetActive(i < levels);
                }
            }
            cameraPivot.localPosition = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>Timeline의 Signal 트랙이 보내는 신호를 받는다.</summary>
        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (notification is not SignalEmitter emitter) return;
            if (emitter.asset == revealSignal) Reveal();
            else if (emitter.asset == doneSignal && closing == null) closing = StartCoroutine(Close(closeDelay));
            else if (emitter.asset == collapseSignal && impulse != null) impulse.GenerateImpulseWithForce(rig.localScale.x * impulseForce);
        }

        private void Reveal()
        {
            if (revealed || current == null) return;
            revealed = true;
            if (queue.Exists(j => j.plot == current.plot))
            {
                // 이어지는 공사가 있으면 이번 단계 모습까지만 보여 주고 계속 붙잡아 둔다.
                current.plot.Hold(current.to);
                current.plot.Pop();
            }
            else current.plot.Release(true);
        }

        /// <summary>중계 창을 클릭하면 남은 장면을 건너뛰고 바로 완성시킨다.</summary>
        private void Skip()
        {
            if (current == null || closing != null) return;
            closing = StartCoroutine(Close(0f));
        }

        private IEnumerator Close(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Reveal();
            broadcast.Hide();
            // TV가 꺼지는 애니메이션이 끝날 때까지 카메라를 켜 둔다.
            while (!broadcast.IsFullyClosed) yield return null;
            timeline.Stop();
            rig.gameObject.SetActive(false);
            broadcastCamera.enabled = false;
            current = null;
            closing = null;
            StartNext();
        }

        /// <summary>진행 초기화: 공사를 모두 멈추고 건물들을 지금 상태 그대로 보여 준다.</summary>
        private void CancelAll()
        {
            if (closing != null) StopCoroutine(closing);
            closing = null;
            if (current != null) current.plot.Release(false);
            foreach (var job in queue) job.plot.Release(false);
            queue.Clear();
            current = null;
            timeline.Stop();
            rig.gameObject.SetActive(false);
            broadcastCamera.enabled = false;
            broadcast.Hide();
        }
    }
}
