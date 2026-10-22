using System.Linq;
using CookieClicker.FX;
using CookieClicker.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 타이틀 화면(로고 "보글보글 낙곱새", 시작하기·설정·종료, 설정 창)을 씬에 미리 만들어 둔다.
    /// 로고·버튼 움직임은 애니메이션 클립 3개 + Animator Controller 에셋(Animations/Title)으로 만들어서,
    /// Animation 창에서 키프레임을 직접 고칠 수 있다. 에셋이 이미 있으면 고친 내용을 지키기 위해 새로 만들지 않는다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string TitleAnimationFolder = "Assets/CookieClicker/Animations/Title";
        private const string TitleCanvasName = "Title Canvas";

        private static readonly Color TitleOrange = new Color(1f, 0.58f, 0.22f);
        private static readonly Color StartRed = new Color(0.86f, 0.3f, 0.18f);

        // 각 요소가 쉬는 자리. 애니메이션 클립의 키프레임도 이 값 기준이라 한곳에서 관리한다.
        private static readonly Vector2 LogoSubRest = new Vector2(0f, 95f);
        private static readonly Vector2 LogoMainRest = new Vector2(0f, -45f);
        private static readonly Vector2 StartButtonRest = new Vector2(0f, 205f);
        private static readonly Vector2 SettingsButtonRest = new Vector2(-122f, 85f);
        private static readonly Vector2 QuitButtonRest = new Vector2(122f, 85f);

        [MenuItem("Tools/Cookie Clicker/Install Title Screen (타이틀 화면 설치)", priority = 23)]
        private static void InstallTitleScreenMenu()
        {
            var layout = Object.FindAnyObjectByType<ResponsiveLayout>(FindObjectsInactive.Include);
            var potFX = Object.FindAnyObjectByType<StewPotFX>(FindObjectsInactive.Include);
            if (layout == null || potFX == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "게임 UI(ResponsiveLayout)나 냄비 연출(StewPotFX)을 찾지 못했습니다.", "확인");
                return;
            }
            InstallTitleScreen(layout, potFX);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "타이틀 화면을 설치했습니다.\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        public static void InstallTitleScreen(ResponsiveLayout layout, StewPotFX potFX)
        {
            if (font == null) font = AssetDatabase.LoadAssetAtPath<Font>(FontPath) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var old = GameObject.Find(TitleCanvasName);
            if (old != null) Object.DestroyImmediate(old);

            // 게임 UI 위에 그리도록 정렬 순서를 높인다.
            var canvas = (RectTransform)CreateCanvas(TitleCanvasName, RenderMode.ScreenSpaceOverlay, null, 20, true).transform;
            var root = Stretch(CreateRect("TitleScreen", canvas), Vector2.zero, Vector2.one);
            root.gameObject.AddComponent<CanvasGroup>();
            // 투명한 판으로 화면 전체를 덮어, 타이틀이 떠 있는 동안 뒤의 냄비가 클릭되지 않게 한다.
            AddImage(Stretch(CreateRect("Blocker", root), Vector2.zero, Vector2.one), null, new Color(0f, 0f, 0f, 0f), true);
            // 아래쪽을 살짝 어둡게 해서 버튼이 잘 보이게
            AddImage(Stretch(CreateRect("BottomShade", root), Vector2.zero, new Vector2(1f, 0.3f)), null, new Color(0f, 0f, 0f, 0.3f));

            // 로고
            var logo = Place(CreateRect("Logo", root), new Vector2(0.5f, 0.82f), new Vector2(1100f, 320f));
            var sub = Place(CreateRect("Sub", logo), new Vector2(0.5f, 0.5f), new Vector2(900f, 120f));
            sub.anchoredPosition = LogoSubRest;
            var subText = Localize(AddText(sub, "보글보글", 86, Cream, TextAnchor.MiddleCenter, FontStyle.Bold), "Bubble Bubble");
            AddThickOutline(subText, new Color(0.35f, 0.12f, 0.05f, 0.9f), 4f);
            var main = Place(CreateRect("Main", logo), new Vector2(0.5f, 0.5f), new Vector2(1100f, 220f));
            main.anchoredPosition = LogoMainRest;
            var mainText = Localize(AddText(main, "낙곱새", 190, TitleOrange, TextAnchor.MiddleCenter, FontStyle.Bold), "Nakgopsae");
            AddThickOutline(mainText, new Color(0.45f, 0.08f, 0.04f, 1f), 6f);
            var shadow = mainText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(0f, -14f);

            // 버튼 (화면 아래 가운데 기준)
            var buttons = CreateRect("Buttons", root);
            buttons.anchorMin = buttons.anchorMax = new Vector2(0.5f, 0f);
            buttons.sizeDelta = Vector2.zero;
            // 배경 없이 글씨만 보이는 버튼. 마우스를 올리면 글씨가 노랗게 바뀌며 살짝 커진다.
            var start = CreateTextOnlyButton(buttons, "Start", "시작하기", StartButtonRest, new Vector2(460f, 104f), 60);
            var settingsButton = CreateTextOnlyButton(buttons, "Settings", "설정", SettingsButtonRest, new Vector2(220f, 74f), 38);
            var quit = CreateTextOnlyButton(buttons, "Quit", "종료", QuitButtonRest, new Vector2(220f, 74f), 38);

            // 설정 창은 타이틀(root) 밖에 둔다: 타이틀이 꺼진 뒤에도 게임 화면의 [설정] 버튼으로 열 수 있게.
            var settings = CreateSettingsWindow(canvas);

            var hud = layout.GetComponent<CanvasGroup>();
            if (hud == null) hud = layout.gameObject.AddComponent<CanvasGroup>();

            var animator = root.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = GetOrCreateTitleController();

            Wire(root.gameObject.AddComponent<TitleScreenView>(),
                ("animator", animator), ("gameHud", hud), ("layout", layout), ("potFX", potFX),
                ("hideDuringTitle", Object.FindObjectsByType<CookieRain>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(r => r.gameObject).ToArray()),
                ("startButton", start), ("settingsButton", settingsButton), ("quitButton", quit), ("settings", settings));
        }

        private static Button CreateTitleButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size,
            int fontSize, Color color)
        {
            var button = CreateTextButton(parent, name, label, position, size, out var text);
            var rt = (RectTransform)button.transform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            button.targetGraphic.color = color;
            text.fontSize = fontSize;
            AddOutline(text);
            return button;
        }

        /// <summary>
        /// 글씨만 보이는 버튼. 배경 이미지는 지우지 않고 투명하게만 둔다 (글씨는 클릭을 받지 않으므로
        /// 투명한 이미지가 클릭 영역 역할을 한다). 마우스 반응 색은 글씨에 걸리도록 Target Graphic을 글씨로 바꾼다.
        /// </summary>
        private static Button CreateTextOnlyButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, int fontSize)
        {
            var button = CreateTitleButton(parent, name, label, position, size, fontSize, new Color(1f, 1f, 1f, 0f));
            var text = button.GetComponentInChildren<Text>();
            text.color = Color.white; // 실제 색은 아래 버튼 색(ColorTint)이 곱해져 정해진다
            text.GetComponents<Outline>().ToList().ForEach(Object.DestroyImmediate);
            AddThickOutline(text, new Color(0.3f, 0.1f, 0.04f, 0.95f), 3f);

            button.targetGraphic = text;
            var colors = button.colors;
            colors.normalColor = Cream;
            colors.highlightedColor = Gold;
            colors.pressedColor = new Color(0.85f, 0.55f, 0.2f);
            colors.selectedColor = Cream;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Wire(button.gameObject.AddComponent<HoverScale>(), ("target", text.transform));
            return button;
        }

        /// <summary>글자 둘레를 두껍게 감싸는 테두리 (Outline을 대각선 방향으로 두 번 겹친다).</summary>
        private static void AddThickOutline(Graphic graphic, Color color, float distance)
        {
            foreach (var d in new[] { new Vector2(distance, -distance), new Vector2(-distance, distance) })
            {
                var outline = graphic.gameObject.AddComponent<Outline>();
                outline.effectColor = color;
                outline.effectDistance = d;
            }
        }

        // ------------------------------------------------------------------ 설정 창

        /// <summary>
        /// 설정 창만 새로 만든다 (타이틀 화면의 다른 부분은 그대로). 설정 항목이 늘었을 때 기존 씬에 반영하는 용도.
        /// </summary>
        [MenuItem("Tools/Cookie Clicker/Update Settings Window (설정 창 업데이트)", priority = 24)]
        private static void UpdateSettingsWindowMenu()
        {
            if (font == null) font = AssetDatabase.LoadAssetAtPath<Font>(FontPath) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = Object.FindAnyObjectByType<TitleScreenView>(FindObjectsInactive.Include);
            var old = Object.FindAnyObjectByType<SettingsView>(FindObjectsInactive.Include);
            if (title == null || old == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "타이틀 화면(설정 창)이 없습니다.\n먼저 Install Title Screen을 실행해 주세요.", "확인");
                return;
            }

            var parent = (RectTransform)old.transform.parent;
            int index = old.transform.GetSiblingIndex();
            Undo.DestroyObjectImmediate(old.gameObject);
            var view = CreateSettingsWindow(parent);
            view.transform.SetSiblingIndex(index);
            Undo.RegisterCreatedObjectUndo(view.gameObject, "Update Settings Window");
            Wire(title, ("settings", view));

            EditorSceneManager.MarkSceneDirty(title.gameObject.scene);
            EditorUtility.DisplayDialog("Cookie Clicker", "설정 창을 새로 만들었습니다.\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        private static SettingsView CreateSettingsWindow(RectTransform parent)
        {
            var root = Stretch(CreateRect("Settings", parent), Vector2.zero, Vector2.one);
            var panel = Stretch(CreateRect("Panel", root), Vector2.zero, Vector2.one);
            AddImage(panel, null, new Color(0f, 0f, 0f, 0.55f), true);

            var window = Place(CreateRect("Window", panel), new Vector2(0.5f, 0.5f), new Vector2(760f, 640f));
            AddImage(window, CookieArtFactory.RoundedRect, new Color(0.24f, 0.15f, 0.09f, 0.97f), true);
            var title = AddText(TopBand(CreateRect("Title", window), 26f, 64f), "설정", 46, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            AddOutline(title);
            Localize(title, "Settings");

            var volume = CreateSettingRow(window, "Volume", "소리 크기", "Volume", 110f, 0f, 1f, out var volumeValue);
            var sensitivity = CreateSettingRow(window, "Sensitivity", "마우스 감도\n<size=20>(구경하기)</size>",
                "Mouse speed\n<size=20>(explore)</size>", 200f, 0.2f, 3f, out var sensitivityValue);

            // 언어: 고른 쪽이 밝은 금색으로 보인다 (SettingsView가 고른 버튼을 비활성 = 선택 표시로 둔다).
            var languageRow = CreateSettingLabelRow(window, "Language", "언어", "Language", 290f);
            var korean = CreateChoiceButton(languageRow, "Korean", "한국어", new Vector2(0.34f, 0.12f), new Vector2(0.57f, 0.88f), out _);
            var english = CreateChoiceButton(languageRow, "English", "English", new Vector2(0.59f, 0.12f), new Vector2(0.82f, 0.88f), out _);

            // 클릭 대체 키: 버튼을 누른 뒤 키를 누르면 바뀐다. 글자(현재 키 이름)는 SettingsView가 채운다.
            var keyRow = CreateSettingLabelRow(window, "ClickKey", "클릭 대체 키\n<size=20>(냄비 누르기)</size>",
                "Click key\n<size=20>(stir the pot)</size>", 380f);
            var keyButton = CreateChoiceButton(keyRow, "KeyButton", "Space", new Vector2(0.34f, 0.12f), new Vector2(0.82f, 0.88f), out var keyLabel);
            keyButton.colors = WithDisabledAsSelected(keyButton.colors, 1f);

            // 아래: [기본값] [취소] [저장]. 바꾼 값은 저장을 눌러야 남는다.
            var reset = CreateWindowButton(window, "Reset", "기본값", "Defaults", new Vector2(-220f, 64f), new Color(0.5f, 0.36f, 0.22f));
            var cancel = CreateWindowButton(window, "Cancel", "취소", "Cancel", new Vector2(0f, 64f), new Color(0.42f, 0.32f, 0.26f));
            var save = CreateWindowButton(window, "Save", "저장", "Save", new Vector2(220f, 64f), StartRed);

            var view = root.gameObject.AddComponent<SettingsView>();
            Wire(view, ("panel", panel.gameObject), ("volumeSlider", volume), ("volumeValue", volumeValue),
                ("sensitivitySlider", sensitivity), ("sensitivityValue", sensitivityValue),
                ("koreanButton", korean), ("englishButton", english),
                ("clickKeyButton", keyButton), ("clickKeyLabel", keyLabel),
                ("resetButton", reset), ("cancelButton", cancel), ("saveButton", save));
            panel.gameObject.SetActive(false);
            return view;
        }

        /// <summary>왼쪽에 항목 이름(언어별), 오른쪽은 비워 둔 한 줄.</summary>
        private static RectTransform CreateSettingLabelRow(RectTransform window, string name, string label, string englishLabel, float top)
        {
            var row = TopBand(CreateRect(name, window), top, 80f, 48f, 48f);
            Localize(AddText(Stretch(CreateRect("Label", row), Vector2.zero, new Vector2(0.32f, 1f)), label, 28, Cream, TextAnchor.MiddleLeft), englishLabel);
            return row;
        }

        private static Slider CreateSettingRow(RectTransform window, string name, string label, string englishLabel, float top,
            float min, float max, out Text valueText)
        {
            var row = CreateSettingLabelRow(window, name, label, englishLabel, top);
            valueText = AddText(Stretch(CreateRect("Value", row), new Vector2(0.84f, 0f), Vector2.one), "", 28, Gold, TextAnchor.MiddleRight, FontStyle.Bold);

            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources
            {
                standard = CookieArtFactory.RoundedRect,
                background = CookieArtFactory.RoundedRect,
                knob = CookieArtFactory.RoundedRect,
            });
            foreach (var t in sliderObject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = UILayer;
            var slider = sliderObject.GetComponent<Slider>();
            var rt = (RectTransform)slider.transform;
            rt.SetParent(row, false);
            Stretch(rt, new Vector2(0.34f, 0.5f), new Vector2(0.82f, 0.5f), new Vector2(0f, -14f), new Vector2(0f, 14f));
            slider.minValue = min;
            slider.maxValue = max;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

            rt.Find("Background").GetComponent<Image>().color = new Color(0.12f, 0.07f, 0.04f);
            rt.Find("Fill Area/Fill").GetComponent<Image>().color = Gold;
            var handle = (RectTransform)rt.Find("Handle Slide Area/Handle");
            handle.GetComponent<Image>().color = Cream;
            handle.sizeDelta = new Vector2(36f, 20f);
            return slider;
        }

        /// <summary>
        /// 한 줄 안에 들어가는 고르기 버튼. 고른 버튼은 비활성(interactable = false)으로 두는데,
        /// 비활성 색을 가장 밝게 해서 "선택됨"으로 보이게 한다 (안 고른 쪽은 어둡게).
        /// </summary>
        private static Button CreateChoiceButton(RectTransform row, string name, string label, Vector2 anchorMin, Vector2 anchorMax, out Text text)
        {
            var rt = Stretch(CreateRect(name, row), anchorMin, anchorMax);
            var image = AddImage(rt, CookieArtFactory.RoundedRect, Gold, true);
            var button = AddButton(rt, image);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = WithDisabledAsSelected(button.colors, 0.45f);
            text = AddText(Stretch(CreateRect("Label", rt), Vector2.zero, Vector2.one), label, 26, new Color(0.25f, 0.13f, 0.05f),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            return button;
        }

        private static ColorBlock WithDisabledAsSelected(ColorBlock colors, float normal)
        {
            colors.normalColor = new Color(normal + 0.15f, normal + 0.1f, normal, 1f);
            colors.highlightedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
            colors.pressedColor = new Color(0.7f, 0.6f, 0.45f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        /// <summary>설정 창 아래쪽 버튼 (창 아래 가운데 기준 위치).</summary>
        private static Button CreateWindowButton(RectTransform window, string name, string label, string englishLabel, Vector2 position, Color color)
        {
            var button = CreateTitleButton(window, name, label, Vector2.zero, new Vector2(200f, 70f), 28, color);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = position;
            Localize(button.GetComponentInChildren<Text>(), englishLabel);
            return button;
        }

        /// <summary>씬에 고정된 글자에 영어 문구를 붙인다 (언어를 바꾸면 LocalizedText가 바꿔 끼운다).</summary>
        internal static Text Localize(Text text, string english)
        {
            if (text == null || string.IsNullOrEmpty(english)) return text;
            var localized = text.GetComponent<LocalizedText>();
            if (localized == null) localized = text.gameObject.AddComponent<LocalizedText>();
            localized.EditorSet(text.text, english);
            EditorUtility.SetDirty(localized);
            return text;
        }

        // ------------------------------------------------------------------ 애니메이션

        /// <summary>
        /// Intro(로고가 떨어지며 출렁 → 버튼이 차례로 톡톡) → Idle(로고가 둥실·보글) → Outro(Start 트리거: 로고가 날아가고 사라짐).
        /// </summary>
        private static RuntimeAnimatorController GetOrCreateTitleController()
        {
            string path = $"{TitleAnimationFolder}/TitleScreen.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;

            EditorFolders.Ensure(TitleAnimationFolder);
            var intro = SaveClip(CreateIntroClip(), "TitleIntro");
            var idle = SaveClip(CreateIdleClip(), "TitleIdle");
            var outro = SaveClip(CreateOutroClip(), "TitleOutro");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Start", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var introState = machine.AddState("Intro");
            introState.motion = intro;
            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            var outroState = machine.AddState("Outro");
            outroState.motion = outro;
            machine.defaultState = introState;

            // Intro가 끝나면 Idle로
            var toIdle = introState.AddTransition(idleState);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 1f;
            toIdle.duration = 0f;

            // Intro 중이든 Idle 중이든 시작하기를 누르면 바로 Outro
            foreach (var from in new[] { introState, idleState })
            {
                var toOutro = from.AddTransition(outroState);
                toOutro.hasExitTime = false;
                toOutro.duration = 0.05f;
                toOutro.AddCondition(AnimatorConditionMode.If, 0f, "Start");
            }
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip CreateIntroClip()
        {
            var clip = new AnimationClip { frameRate = 60f };
            // 낙곱새: 위에서 떨어져 바닥에 닿으며 납작(스쿼시) → 튀어 오르며 길쭉(스트레치) → 제자리
            Curve(clip, "Logo/Main", "m_AnchoredPosition.y", (0f, LogoMainRest.y + 700f), (0.42f, LogoMainRest.y - 30f), (0.58f, LogoMainRest.y + 14f), (0.72f, LogoMainRest.y));
            Curve(clip, "Logo/Main", "m_LocalScale.x", (0f, 0.9f), (0.42f, 1.2f), (0.58f, 0.94f), (0.74f, 1f));
            Curve(clip, "Logo/Main", "m_LocalScale.y", (0f, 1.1f), (0.42f, 0.78f), (0.58f, 1.08f), (0.74f, 1f));
            // 보글보글: 낙곱새가 자리 잡으면 퐁 하고 부풀어 나타남
            Pop(clip, "Logo/Sub", 0.55f, 0.25f);
            // 버튼: 차례로 톡톡
            Pop(clip, "Buttons/Start", 0.85f, 0.25f);
            Pop(clip, "Buttons/Settings", 0.97f, 0.25f);
            Pop(clip, "Buttons/Quit", 1.07f, 0.25f);
            return clip;
        }

        private static AnimationClip CreateIdleClip()
        {
            var clip = new AnimationClip { frameRate = 60f };
            const float loop = 2.4f;
            Curve(clip, "Logo", "m_AnchoredPosition.y", (0f, 0f), (loop * 0.5f, 12f), (loop, 0f));
            Curve(clip, "Logo/Main", "localEulerAnglesRaw.z", (0f, -2f), (loop * 0.5f, 2f), (loop, -2f));
            Curve(clip, "Logo/Main", "localEulerAnglesRaw.x", (0f, 0f), (loop, 0f));
            Curve(clip, "Logo/Main", "localEulerAnglesRaw.y", (0f, 0f), (loop, 0f));
            // 보글보글은 끓듯이 두 번씩 부풀고, 시작하기 버튼은 눌러 달라는 듯 천천히 숨쉬기
            Uniform(clip, "Logo/Sub", (0f, 1f), (loop * 0.25f, 1.07f), (loop * 0.5f, 1f), (loop * 0.75f, 1.07f), (loop, 1f));
            Uniform(clip, "Buttons/Start", (0f, 1f), (loop * 0.5f, 1.05f), (loop, 1f));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static AnimationClip CreateOutroClip()
        {
            var clip = new AnimationClip { frameRate = 60f };
            const float length = 0.6f;
            Curve(clip, "Logo", "m_AnchoredPosition.y", (0f, 0f), (0.12f, -20f), (length, 520f));
            Uniform(clip, "Buttons/Start", (0f, 1f), (0.08f, 1.12f), (0.3f, 0f));
            Uniform(clip, "Buttons/Settings", (0f, 1f), (0.25f, 0f));
            Uniform(clip, "Buttons/Quit", (0f, 1f), (0.25f, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(CanvasGroup), "m_Alpha"),
                Smooth((0f, 1f), (0.2f, 1f), (length, 0f)));
            // 마지막 프레임에 TitleScreenView.OnOutroFinished 호출
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = length, functionName = "OnOutroFinished" } });
            return clip;
        }

        /// <summary>start초에 0에서 커지며 살짝 넘쳤다가(1.15) 1로 자리 잡는 등장.</summary>
        private static void Pop(AnimationClip clip, string path, float start, float duration) =>
            Uniform(clip, path, (0f, 0f), (start, 0f), (start + duration * 0.65f, 1.15f), (start + duration, 1f));

        private static void Uniform(AnimationClip clip, string path, params (float time, float value)[] keys)
        {
            foreach (var axis in new[] { "x", "y", "z" }) Curve(clip, path, $"m_LocalScale.{axis}", keys);
        }

        private static void Curve(AnimationClip clip, string path, string property, params (float time, float value)[] keys) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(RectTransform), property), Smooth(keys));

        private static AnimationCurve Smooth(params (float time, float value)[] keys)
        {
            var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.time, k.value)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                // 같은 값을 유지하는 구간(예: 등장 전 크기 0)은 평평하게 둔다.
                // 부드럽게(Smooth) 하면 곡선이 0 아래로 살짝 내려가 크기가 음수 → 글자가 좌우로 뒤집혀 보인다.
                bool holdBefore = i > 0 && Mathf.Approximately(curve[i - 1].value, curve[i].value);
                bool holdAfter = i < curve.length - 1 && Mathf.Approximately(curve[i + 1].value, curve[i].value);
                if (holdBefore || holdAfter)
                {
                    var key = curve[i];
                    key.inTangent = key.outTangent = 0f;
                    curve.MoveKey(i, key);
                }
                else
                {
                    curve.SmoothTangents(i, 0f);
                }
            }
            return curve;
        }

        private static AnimationClip SaveClip(AnimationClip clip, string name)
        {
            string path = $"{TitleAnimationFolder}/{name}.anim";
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
    }
}
