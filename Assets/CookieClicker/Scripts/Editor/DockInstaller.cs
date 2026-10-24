using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 해안에서 원양어선 쪽으로 뻗은 나무 선착장. 끝에서 배 난간 위로 걸친 경사로(Gangway, 배 단계마다 따로)를 타고 배에 올라간다.
    /// 선착장은 원양어선 터 기준(정면 = 본진 쪽 = 해안 쪽)으로 놓여서 터를 옮기면 같이 맞춰 다시 설치하면 된다.
    /// 여러 번 실행해도 이전 선착장을 지우고 다시 만든다. 판자·말뚝은 씬에서 그대로 옮기거나 지워도 된다.
    /// </summary>
    public static partial class CookieSceneBuilder
    {
        // 원양어선 터 기준 좌표. 선착장은 x = DockX 줄을 따라 z = DockEndZ(배 쪽 끝)부터 해안까지 뻗는다.
        private const float DockX = -1.8f;          // 조타실·크레인을 피한 갑판 빈 자리 앞
        private const float DockEndZ = 6f;          // 가장 큰 배(16m)의 옆구리보다 조금 바깥
        private const float DockWidth = 3.2f;
        private const float DockTopY = 0.3f;        // 월드 기준 높이 (물 -0.4 위 0.7m, 땅 0에서 한 걸음에 올라설 높이)

        [MenuItem("Tools/Cookie Clicker/Install Dock (선착장)")]
        private static void InstallDockMenu()
        {
            var world = GameObject.Find("ExploreWorld");
            bool ok = world != null && InstallDock(world.transform);
            if (ok) EditorSceneManager.MarkSceneDirty(world.scene);
            EditorUtility.DisplayDialog("Cookie Clicker",
                ok ? "선착장을 설치했습니다. 배에 오르는 경사로는 Install Trawler Boat로 함께 만들어집니다.\nCtrl+S로 씬을 저장해 주세요."
                   : "ExploreWorld/Plots/Plot_shrimp_trawler를 찾지 못했습니다.", "확인");
        }

        internal static bool InstallDock(Transform world)
        {
            var plot = world.Find("Plots/Plot_shrimp_trawler");
            if (plot == null) return false;
            var old = world.Find("Dock");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // 터와 같은 방향으로 놓되 높이는 월드 기준(땅 = 0)
            var dock = new GameObject("Dock").transform;
            dock.SetParent(world, false);
            dock.localPosition = new Vector3(plot.localPosition.x, 0f, plot.localPosition.z);
            dock.localRotation = plot.localRotation;

            // 해안선(ShoreZ)을 2m 넘어 땅 위까지 이어지게 길이를 정한다.
            var forward = plot.localRotation * Vector3.forward;
            float shoreZ = (ShoreZ + 2f - plot.localPosition.z) / forward.z;
            float length = shoreZ - DockEndZ;
            float centerZ = (shoreZ + DockEndZ) * 0.5f;

            var plank = Mat("Dock_Plank", new Color(0.72f, 0.52f, 0.33f));
            var plankAlt = Mat("Dock_PlankAlt", new Color(0.66f, 0.47f, 0.3f));
            var beam = Mat("Dock_Beam", new Color(0.42f, 0.29f, 0.19f));
            var metal = Mat("Dock_Metal", new Color(0.25f, 0.26f, 0.28f));

            // 걷는 판정은 판자 전체를 덮는 상자 하나 (판자 틈에 발이 걸리지 않게)
            var walk = new GameObject("WalkCollider").AddComponent<BoxCollider>();
            walk.transform.SetParent(dock, false);
            walk.center = new Vector3(DockX, DockTopY - 0.1f, centerZ);
            walk.size = new Vector3(DockWidth, 0.2f, length);

            // 가로 판자: 색을 번갈아 칠하고 높이를 조금씩 어긋나게 해서 손으로 깐 느낌
            var planks = new GameObject("Planks").transform;
            planks.SetParent(dock, false);
            const float pitch = 0.5f;
            int count = Mathf.FloorToInt(length / pitch);
            for (int i = 0; i < count; i++)
            {
                float z = DockEndZ + pitch * (i + 0.5f);
                float jitter = Mathf.Sin(i * 12.9898f) * 0.015f;
                Block(PrimitiveType.Cube, $"Plank{i}", planks, i % 3 == 1 ? plankAlt : plank,
                    new Vector3(DockX + jitter * 4f, DockTopY - 0.06f + jitter, z), new Vector3(DockWidth + jitter * 6f, 0.12f, pitch - 0.06f));
            }

            // 판자 밑 세로 보 두 줄과, 3m마다 물속까지 박힌 말뚝
            foreach (float side in new[] { -1f, 1f })
            {
                float x = DockX + side * (DockWidth * 0.5f - 0.25f);
                Block(PrimitiveType.Cube, side < 0 ? "BeamL" : "BeamR", dock, beam,
                    new Vector3(x, DockTopY - 0.24f, centerZ), new Vector3(0.22f, 0.24f, length));
                for (float z = DockEndZ + 0.2f; z < shoreZ - 1.5f; z += 3f)
                {
                    Block(PrimitiveType.Cylinder, $"Pile_{(side < 0 ? "L" : "R")}{Mathf.RoundToInt(z)}", dock, beam,
                        new Vector3(DockX + side * (DockWidth * 0.5f + 0.05f), DockTopY - 3.7f, z), new Vector3(0.36f, 4f, 0.36f));
                }
            }

            // 배 쪽 끝: 밧줄 매는 기둥(볼라드) 두 개와 가로등 두 개
            foreach (float side in new[] { -1f, 1f })
            {
                float x = DockX + side * (DockWidth * 0.5f - 0.3f);
                var bollard = new GameObject(side < 0 ? "BollardL" : "BollardR").transform;
                bollard.SetParent(dock, false);
                bollard.localPosition = new Vector3(x, DockTopY, DockEndZ + 0.6f);
                Block(PrimitiveType.Cylinder, "Post", bollard, metal, new Vector3(0f, 0.22f, 0f), new Vector3(0.3f, 0.22f, 0.3f), collider: true);
                Block(PrimitiveType.Cylinder, "Cap", bollard, metal, new Vector3(0f, 0.46f, 0f), new Vector3(0.42f, 0.04f, 0.42f));

                var lamp = new GameObject(side < 0 ? "LampL" : "LampR").transform;
                lamp.SetParent(dock, false);
                lamp.localPosition = new Vector3(DockX + side * (DockWidth * 0.5f + 0.15f), DockTopY, DockEndZ + 2.2f);
                Block(PrimitiveType.Cylinder, "Pole", lamp, metal, new Vector3(0f, 1.3f, 0f), new Vector3(0.12f, 1.3f, 0.12f), collider: true);
                Block(PrimitiveType.Cube, "Arm", lamp, metal, new Vector3(-side * 0.2f, 2.55f, 0f), new Vector3(0.45f, 0.06f, 0.06f));
                var bulb = Block(PrimitiveType.Sphere, "Bulb", lamp, GlowMat("Dock_Bulb", new Color(1f, 0.85f, 0.5f), 1.2f),
                    new Vector3(-side * 0.4f, 2.42f, 0f), Vector3.one * 0.26f);
                bulb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return true;
        }

        /// <summary>스스로 빛나는(Emission) 단색 머티리얼. 이미 있으면 색·밝기만 다시 맞춘다.</summary>
        private static Material GlowMat(string name, Color color, float intensity)
        {
            var mat = Mat(name, color);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", color * intensity);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
