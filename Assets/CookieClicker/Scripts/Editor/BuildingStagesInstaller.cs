using CookieClicker.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 건물 자리마다 "단계" 구조를 만든다: Built/Stages/Stage_1, Stage_10, Stage_25, Stage_50, Stage_100.
    /// 보유 개수에 따라 한 단계만 켜지고, 단계가 오른 뒤 처음 눈에 들어오면 솟아오르며 반짝인다 (BuildingPlot).
    /// 처음에는 지금 박스 건물을 단계마다 조금씩 높인 임시 모습으로 채운다.
    /// 진짜 모델을 넣을 때는 Stage_* 아래의 박스를 지우고 모델을 넣은 뒤, Setup LODs를 실행하면 된다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        private const string StagesName = "Stages";
        // 단계가 바뀌는 보유 개수와, 임시 박스 건물의 높이 배율
        private static readonly int[] StageThresholds = { 1, 10, 25, 50, 100 };
        private static readonly float[] StageHeightScales = { 1f, 1.2f, 1.4f, 1.6f, 1.8f };

        [MenuItem("Tools/Cookie Clicker/Install Building Stages (건물 단계 설치)")]
        private static void InstallBuildingStagesMenu()
        {
            int installed = 0, skipped = 0;
            foreach (var plot in Object.FindObjectsByType<BuildingPlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (InstallStages(plot, true)) installed++;
                else skipped++;
            }
            if (installed > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Cookie Clicker",
                $"건물 {installed}곳에 단계를 설치했습니다." +
                (skipped > 0 ? $"\n이미 단계가 있는 {skipped}곳은 건너뛰었습니다 (넣어 둔 모델 보호)." : "") +
                "\nCtrl+S로 씬을 저장해 주세요.", "확인");
        }

        /// <summary>건물 본체(Body). 단계를 설치한 뒤에는 Stages/Stage_1 아래에 있다.</summary>
        internal static Transform FindBuildingBody(Transform built) =>
            built.Find("Body") ?? built.Find($"{StagesName}/Stage_{StageThresholds[0]}/Body");

        /// <summary>
        /// 건물 하나에 단계를 만든다. 이미 Stages가 있으면 (직접 넣은 모델이 있을 수 있으므로) 건드리지 않고 false.
        /// </summary>
        internal static bool InstallStages(BuildingPlot plot, bool undo = false)
        {
            var serialized = new SerializedObject(plot);
            var builtObject = serialized.FindProperty("builtRoot").objectReferenceValue as GameObject;
            if (builtObject == null) return false;
            var built = builtObject.transform;
            if (built.Find(StagesName) != null) return false;

            var body = built.Find("Body");
            if (body == null) return false;
            var roof = built.Find("Roof");
            var door = built.Find("Door");

            var stagesRoot = new GameObject(StagesName).transform;
            stagesRoot.SetParent(built, false);
            if (undo) Undo.RegisterCreatedObjectUndo(stagesRoot.gameObject, "Install Building Stages");

            // 1단계 = 지금 건물 그대로 (Body·Roof·Door를 옮겨 담는다)
            var first = new GameObject($"Stage_{StageThresholds[0]}").transform;
            first.SetParent(stagesRoot, false);
            foreach (var part in new[] { body, roof, door })
            {
                if (part == null) continue;
                if (undo) Undo.SetTransformParent(part, first, "Install Building Stages");
                else part.SetParent(first, false);
            }

            float bodyHeight = body.localScale.y;
            var sign = built.Find("Sign");
            float signBase = sign != null ? sign.localPosition.y : 0f;

            var visuals = new GameObject[StageThresholds.Length];
            var signHeights = new float[StageThresholds.Length];
            visuals[0] = first.gameObject;
            signHeights[0] = signBase;
            for (int i = 1; i < StageThresholds.Length; i++)
            {
                var copy = Object.Instantiate(first.gameObject, stagesRoot).transform;
                copy.name = $"Stage_{StageThresholds[i]}";
                float extra = bodyHeight * (StageHeightScales[i] - 1f);
                RaiseStage(copy, StageHeightScales[i], extra);
                copy.gameObject.SetActive(false);
                if (undo) Undo.RegisterCreatedObjectUndo(copy.gameObject, "Install Building Stages");
                visuals[i] = copy.gameObject;
                signHeights[i] = signBase + extra;
            }

            var burst = CreateStageUpBurst(built, bodyHeight * StageHeightScales[StageHeightScales.Length - 1]);
            if (undo) Undo.RegisterCreatedObjectUndo(burst.gameObject, "Install Building Stages");

            serialized.Update();
            var stages = serialized.FindProperty("stages");
            stages.arraySize = StageThresholds.Length;
            for (int i = 0; i < StageThresholds.Length; i++)
            {
                var element = stages.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("minCount").intValue = StageThresholds[i];
                element.FindPropertyRelative("visual").objectReferenceValue = visuals[i];
                element.FindPropertyRelative("signHeight").floatValue = sign != null ? signHeights[i] : 0f;
            }
            serialized.FindProperty("sign").objectReferenceValue = sign;
            serialized.FindProperty("stageUpEffect").objectReferenceValue = burst;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>임시 박스 건물: 몸통을 scale배 높이고, 지붕을 그만큼 올린다 (문은 그대로).</summary>
        private static void RaiseStage(Transform stage, float scale, float extra)
        {
            var body = stage.Find("Body");
            if (body != null)
            {
                var size = body.localScale;
                body.localScale = new Vector3(size.x, size.y * scale, size.z);
                var position = body.localPosition;
                body.localPosition = new Vector3(position.x, position.y * scale, position.z);
            }
            var roof = stage.Find("Roof");
            if (roof != null) roof.localPosition += Vector3.up * extra;
        }

        /// <summary>단계가 오를 때 건물 위로 터지는 금빛 반짝이 (냄비 반짝임과 같은 머티리얼).</summary>
        private static ParticleSystem CreateStageUpBurst(Transform built, float topHeight)
        {
            var ps = CreateParticleSystem("StageUpBurst", built, new Vector3(0f, topHeight * 0.6f, 0f), 120);
            var main = ps.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.35f), new Color(1f, 0.97f, 0.75f));
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = Mathf.Max(1f, topHeight * 0.3f);
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(1f, 0f)));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial("Sparkle", CookieArtFactory.Sparkle.texture, ParticleBlend.Additive);
            return ps;
        }
    }
}
