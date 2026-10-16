using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 구경하기 월드에 놓인 3D 모델(glb)에 LOD Group을 붙인다.
    /// Models/LOD 폴더에 "이름_LOD1.glb", "이름_LOD2.glb"가 있으면 멀어질수록 가벼운 모델로 바꿔 그리고,
    /// 없으면 화면에서 아주 작아졌을 때 그리지 않게만(컬링) 한다.
    /// LOD 모델은 Tools/Blender/make_lods.py로 만든다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        internal const string LodFolder = "Assets/CookieClicker/Models/LOD";
        private const string LodWrapperPrefix = "LOD_";

        /// <summary>
        /// 단계가 바뀌는 기준: 모델 높이가 화면 높이의 몇 %일 때인지 (LOD0→1, LOD1→2, 마지막 값 아래는 그리지 않음).
        /// 거리가 아니라 화면 비율이라 큰 건물은 멀리서도 고품질을 오래 유지한다.
        /// LOD Bias 1 기준이고, PC 품질은 LOD Bias가 2라 두 배 먼 곳까지 앞 단계를 유지한다.
        /// </summary>
        private static readonly float[] LodScreenHeights = { 0.25f, 0.08f, 0.015f };

        [MenuItem("Tools/Cookie Clicker/Setup LODs (LOD 설정)")]
        private static void SetupLodsMenu()
        {
            var world = FindExploreWorld();
            if (world == null)
            {
                EditorUtility.DisplayDialog("Cookie Clicker", "열린 씬에 ExploreWorld가 없습니다.\n먼저 Build Scene으로 씬을 만들어 주세요.", "확인");
                return;
            }
            int count = SetupLods(world, true);
            if (count > 0) EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            EditorUtility.DisplayDialog("Cookie Clicker",
                count > 0 ? $"모델 {count}개에 LOD를 설정했습니다.\nCtrl+S로 씬을 저장해 주세요."
                          : "새로 설정할 모델이 없습니다.\n(이미 설정된 모델은 건너뜁니다.)", "확인");
        }

        private static Transform FindExploreWorld()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.parent == null && t.name == "ExploreWorld") return t;
            }
            return null;
        }

        /// <summary>root 아래의 glb 모델마다 LOD Group을 붙인다. 이미 붙은 모델은 건너뛴다. 설정한 개수를 돌려준다.</summary>
        internal static int SetupLods(Transform root, bool undo = false)
        {
            var targets = new List<(GameObject model, string name)>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) continue;
                if (path.StartsWith(LodFolder)) continue; // LOD 모델 자신
                if (t.parent != null && t.parent.GetComponent<LODGroup>() != null) continue; // 이미 설정됨
                targets.Add((go, Path.GetFileNameWithoutExtension(path)));
            }
            foreach (var (model, name) in targets) AddLodGroup(model, name, undo);
            return targets.Count;
        }

        /// <summary>
        /// 모델을 LOD_이름 오브젝트로 감싸고, 그 아래에 LOD1·LOD2 모델을 같은 위치·크기로 겹쳐 놓은 뒤 LOD Group을 붙인다.
        /// LOD 모델은 원본과 같은 좌표계로 내보냈기 때문에 원본의 Transform 값을 그대로 복사하면 정확히 겹친다.
        /// </summary>
        private static void AddLodGroup(GameObject model, string name, bool undo)
        {
            var source = model.transform;
            var wrapper = new GameObject(LodWrapperPrefix + name).transform;
            wrapper.SetParent(source.parent, false);
            wrapper.SetSiblingIndex(source.GetSiblingIndex());
            wrapper.gameObject.layer = model.layer;
            if (undo)
            {
                Undo.RegisterCreatedObjectUndo(wrapper.gameObject, "Setup LODs");
                Undo.SetTransformParent(source, wrapper, "Setup LODs");
            }
            else
            {
                source.SetParent(wrapper, false);
            }

            var levels = new List<Renderer[]> { model.GetComponentsInChildren<Renderer>(true) };
            var reference = levels[0].Length > 0 ? levels[0][0] : null;
            for (int i = 1; ; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{LodFolder}/{name}_LOD{i}.glb");
                if (asset == null) break;

                var lod = (GameObject)PrefabUtility.InstantiatePrefab(asset, wrapper);
                lod.name = $"{model.name}_LOD{i}";
                lod.transform.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
                lod.transform.localScale = source.localScale;
                var renderers = lod.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    r.gameObject.layer = model.layer;
                    if (reference == null) continue;
                    // 그림자 설정은 원본을 따른다 (손·랜드마크마다 다르게 정해 둔 값 유지).
                    r.shadowCastingMode = reference.shadowCastingMode;
                    r.receiveShadows = reference.receiveShadows;
                }
                levels.Add(renderers);
                if (undo) Undo.RegisterCreatedObjectUndo(lod, "Setup LODs");
            }

            // 3단계면 0.25 / 0.08 / 0.015, 2단계면 0.25 / 0.015, LOD 모델이 없으면 0.015(컬링만).
            var lods = new LOD[levels.Count];
            for (int i = 0; i < levels.Count; i++)
            {
                float height = i == levels.Count - 1
                    ? LodScreenHeights[LodScreenHeights.Length - 1]
                    : LodScreenHeights[Mathf.Min(i, LodScreenHeights.Length - 2)];
                lods[i] = new LOD(height, levels[i]);
            }

            var group = wrapper.gameObject.AddComponent<LODGroup>();
            // 단계가 바뀔 때 툭 바뀌지 않고 0.5초 동안 디더링으로 섞인다 (셰이더가 지원할 때).
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.SetLODs(lods);
            FitLodBounds(group, levels[0]);
        }

        /// <summary>
        /// LOD Group의 크기·중심을 원본 모델에 맞춘다. RecalculateBounds()는 꺼진 오브젝트(아직 안 산 건물)에서
        /// 크기를 0으로 재므로, 메시 bounds를 직접 변환해서 계산한다.
        /// </summary>
        private static void FitLodBounds(LODGroup group, Renderer[] renderers)
        {
            var space = group.transform;
            bool any = false;
            var bounds = new Bounds();
            foreach (var r in renderers)
            {
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                else if (r.TryGetComponent(out MeshFilter filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;

                var b = mesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    var point = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
            if (!any) return;
            group.localReferencePoint = bounds.center;
            group.size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        }
    }
}
