using CookieClicker.Gameplay;
using CookieClicker.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 구경하기 UI에 건물 팝업(F 키)을 미리 만들어 둔다. 팝업 오브젝트는 씬에 있으므로 크기·색·문구는 Hierarchy에서 바로 고치면 된다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private static readonly Color PopupBackground = new Color(0.17f, 0.11f, 0.07f, 0.94f);

        [MenuItem("Tools/Cookie Clicker/Install Building Popup (건물 팝업 설치)")]
        private static void InstallPlotPopupMenu()
        {
            var hud = Object.FindAnyObjectByType<ExploreHudView>(FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            var mode = Object.FindAnyObjectByType<ExploreModeController>(FindObjectsInactive.Include);
            if (hud == null || player == null || mode == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "구경하기 월드가 없습니다. 먼저 Build Scene으로 씬을 만들어 주세요.", "확인");
                return;
            }

            InstallPlotPopup(hud, player, player.GetComponentInChildren<Camera>(true), mode);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "건물 팝업을 설치했습니다.\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        internal static void InstallPlotPopup(ExploreHudView hud, FirstPersonController player, Camera playerCamera, ExploreModeController mode)
        {
            var hudRect = (RectTransform)hud.transform;
            var old = hudRect.Find("PlotPopup");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // 팝업 스크립트가 붙는 루트는 항상 켜져 있고, 안내 문구(Prompt)와 창(Panel)만 켜고 끈다.
            var root = Stretch(CreateRect("PlotPopup", hudRect), Vector2.zero, Vector2.one);

            var prompt = AddText(Place(CreateRect("Prompt", root), new Vector2(0.5f, 0.45f), new Vector2(600f, 50f)),
                "[F] 건물 보기", 28, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(prompt);
            prompt.gameObject.SetActive(false);

            var panel = Place(CreateRect("Panel", root), new Vector2(0.5f, 0.5f), new Vector2(720f, 460f));
            AddImage(panel, CookieArtFactory.RoundedRect, PopupBackground, true);

            var title = AddText(TopBand(CreateRect("Title", panel), 26f, 56f, 30f, 30f), "자동 국자 x0", 40, Gold,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(title);
            var body = AddText(TopBand(CreateRect("Body", panel), 92f, 64f, 40f, 40f), "건물 설명", 24, Cream, TextAnchor.UpperCenter);

            // 건물에 조절할 수 있는 설정이 있을 때만 켜지는 칸 (지금은 국자 공전 속도)
            var speed = TopBand(CreateRect("SpeedSection", panel), 166f, 180f, 30f, 30f);
            AddImage(Stretch(CreateRect("Divider", speed), new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -2f), new Vector2(-20f, 0f)),
                null, new Color(1f, 1f, 1f, 0.2f));
            var speedText = AddText(TopBand(CreateRect("SpeedText", speed), 14f, 80f), "공전 속도 x1", 28, Color.white, TextAnchor.MiddleCenter);

            var baseButton = CreateTextButton(speed, "BaseSpeedButton", "처음 속도로", Vector2.zero, new Vector2(300f, 60f), out var baseLabel);
            PlaceButton(baseButton, new Vector2(0.25f, 0f), new Vector2(0f, 20f));
            var countButton = CreateTextButton(speed, "CountSpeedButton", "원래대로", Vector2.zero, new Vector2(300f, 60f), out var countLabel);
            PlaceButton(countButton, new Vector2(0.75f, 0f), new Vector2(0f, 20f));

            var closeButton = CreateTextButton(panel, "CloseButton", "닫기 (F / Esc)", Vector2.zero, new Vector2(240f, 54f), out _);
            PlaceButton(closeButton, new Vector2(0.5f, 0f), new Vector2(0f, 24f));
            ((Image)closeButton.targetGraphic).color = new Color(0.3f, 0.3f, 0.3f);

            panel.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<PlotPopupView>();
            Wire(view,
                ("playerCamera", playerCamera), ("player", player), ("exploreMode", mode),
                ("closeAction", FindAction("UI/Cancel")),
                ("prompt", prompt.gameObject), ("panel", panel.gameObject),
                ("titleText", title), ("bodyText", body),
                ("speedSection", speed.gameObject), ("speedText", speedText),
                ("baseSpeedButton", baseButton), ("baseSpeedLabel", baseLabel),
                ("countSpeedButton", countButton), ("countSpeedLabel", countLabel),
                ("closeButton", closeButton));
        }

        /// <summary>CreateTextButton은 왼쪽 기준이라, 부모의 anchor 지점(아래쪽 기준)으로 다시 놓는다.</summary>
        private static void PlaceButton(Button button, Vector2 anchor, Vector2 offset)
        {
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = offset;
        }
    }
}
