using System;
using CookieClicker.FX;
using CookieClicker.Gameplay;
using CookieClicker.Decor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 웹(WebGL)에서 버벅이지 않게 씬을 다듬는다. 씬을 새로 만들 때 마지막에 자동으로 실행되고, 메뉴로 따로 실행할 수도 있다.
    /// 1) 자주 바뀌는 UI(숫자 간판, 상점, 속보 띠, 클릭 효과, 중계 창, 낙곱새 비)를 각자 작은 Canvas로 나눈다.
    ///    한 Canvas 안의 글자 하나만 바뀌어도 그 Canvas 전체를 다시 묶어 그리므로, 큰 화면 전체가 매 프레임 다시 그려지는 걸 막는다.
    /// 2) 탐험 맵에서 움직이지 않는 물체를 Batching Static으로 표시해, 빌드할 때 하나로 합쳐 그리기 호출을 줄인다.
    ///    움직이거나 크기가 바뀌는 것(건물 단계 솟아오르기, 배 출렁임, UFO, 공사 장비, 플레이어)은 빼 둔다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        // 따로 Canvas로 떼어 낼 UI (경로, 클릭을 받는지)
        private static readonly (string path, bool raycast)[] IsolatedUi =
        {
            ("UI Canvas/MainHud/CounterCard", false),
            ("UI Canvas/MainHud/StorePanel", true),
            ("UI Canvas/MainHud/NewsTicker", false),
            ("UI Canvas/MainHud/EffectLayer", true),
            ("UI Canvas/MainHud/LiveBroadcast", true),
            ("Background Canvas/CookieRain", false),
        };

        // 이 이름의 오브젝트 아래는 움직이거나 크기가 바뀌므로 static으로 묶지 않는다.
        private static readonly string[] DynamicRoots = { "Player", "Construction", "Stages", "DecorLot", "DroppedPortions" };

        // 이 컴포넌트가 붙은 오브젝트(와 그 아래)는 움직인다.
        private static readonly Type[] MovingComponents =
        {
            typeof(Animator), typeof(Rigidbody), typeof(CharacterController), typeof(Orbiter), typeof(BoatBob),
            typeof(BeamLift), typeof(Spinner), typeof(PlacedDecor),
        };

        [MenuItem("Tools/Cookie Clicker/Optimize For Web (웹 성능 다듬기)")]
        private static void OptimizeForWebMenu()
        {
            var report = OptimizeForWeb();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Cookie Clicker", report + "\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        internal static string OptimizeForWeb()
        {
            int canvases = 0;
            foreach (var (path, raycast) in IsolatedUi)
            {
                var target = FindByPath(path);
                if (target == null) continue;
                if (target.GetComponent<Canvas>() == null)
                {
                    target.gameObject.AddComponent<Canvas>();
                    canvases++;
                }
                if (raycast && target.GetComponent<GraphicRaycaster>() == null) target.gameObject.AddComponent<GraphicRaycaster>();
            }

            int statics = 0;
            var world = GameObject.Find("ExploreWorld");
            if (world != null)
            {
                foreach (var renderer in world.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (IsDynamic(renderer.transform, world.transform)) continue;
                    var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | StaticEditorFlags.BatchingStatic);
                    statics++;
                }
            }
            // 느린 컴퓨터에서는 스스로 화질을 낮추는 관리자 (F3: FPS 표시)
            var manager = Object.FindAnyObjectByType<CookieClicker.Core.CookieGame>();
            if (manager != null && manager.GetComponent<CookieClicker.Core.AdaptiveQuality>() == null)
                manager.gameObject.AddComponent<CookieClicker.Core.AdaptiveQuality>();

            return $"UI Canvas {canvases}개를 나누고, 탐험 맵 물체 {statics}개를 Static으로 묶었습니다.";
        }

        private static bool IsDynamic(Transform t, Transform worldRoot)
        {
            for (var current = t; current != null && current != worldRoot; current = current.parent)
            {
                if (Array.IndexOf(DynamicRoots, current.name) >= 0) return true;
                foreach (var type in MovingComponents)
                    if (current.GetComponent(type) != null) return true;
            }
            return false;
        }

        private static Transform FindByPath(string path)
        {
            int slash = path.IndexOf('/');
            var root = GameObject.Find(slash < 0 ? path : path.Substring(0, slash));
            if (root == null) return null;
            return slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
        }
    }
}
