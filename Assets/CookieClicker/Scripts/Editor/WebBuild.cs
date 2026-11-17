using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// 웹(WebGL) 빌드. 결과는 Builds/Web 폴더에 나오고, 폴더째 압축해서 itch.io 같은 사이트에 올리면 된다.
    /// 압축 파일(.gz)을 서버가 어떻게 보내든 열리도록 Decompression Fallback을 켠다 (서버 설정을 몰라도 됨).
    /// 씬은 게임 씬(CookieClicker) 하나만 넣는다.
    /// </summary>
    public static class WebBuild
    {
        private const string OutputFolder = "Builds/Web";
        private const string GameScenePath = "Assets/CookieClicker/Scenes/CookieClicker.unity";

        [MenuItem("Tools/Cookie Clicker/Build Web (웹 빌드)")]
        private static void BuildMenu()
        {
            var report = Build();
            EditorUtility.DisplayDialog("Cookie Clicker",
                report.summary.result == BuildResult.Succeeded
                    ? $"웹 빌드 완료: {OutputFolder}\n크기 {report.summary.totalSize / (1024f * 1024f):0.0}MB"
                    : $"웹 빌드 실패: {report.summary.result}\nConsole 창을 확인해 주세요.", "확인");
        }

        /// <summary>배치 모드용: 실패하면 종료 코드 1로 끝낸다.</summary>
        public static void BuildWithoutDialogs()
        {
            var report = Build();
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// 배치 모드용 개발 빌드 (Builds/WebDev). 치트 키(F12)가 켜져 있어 건물을 다 사 놓고 웹에서 성능을 재 볼 때 쓴다.
        /// itch.io에는 올리지 않는다.
        /// </summary>
        public static void BuildDevelopmentWithoutDialogs()
        {
            var report = Build("Builds/WebDev", BuildOptions.Development);
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// 웹은 그리기 호출 하나하나가 비싸서, Static으로 표시한 물체를 빌드할 때 하나로 합치는 Static Batching을 켠다.
        /// (Player Settings > Other Settings > Static Batching. 플랫폼별 설정이라 공개 API가 없어 내부 함수를 부른다.)
        /// </summary>
        private static void EnableStaticBatching()
        {
            var method = typeof(PlayerSettings).GetMethod("SetBatchingForPlatform",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (method != null) method.Invoke(null, new object[] { BuildTarget.WebGL, 1, 0 });
            else Debug.LogWarning("[WebBuild] Static Batching을 켜지 못했습니다. Player Settings에서 직접 켜 주세요.");
        }

        /// <summary>
        /// 웹이 쓰는 품질(Mobile)의 URP 설정에서 HDR을 끈다. 화면 효과(블룸·톤매핑)를 쓰지 않아서 겉모습은 같고,
        /// 매 프레임 큰 실수(float) 화면 버퍼를 쓰지 않아 가벼워진다.
        /// </summary>
        private static void DisableHdrForWeb()
        {
            var asset = QualitySettings.GetRenderPipelineAssetAt(0);
            if (asset == null) return;
            var so = new SerializedObject(asset);
            var hdr = so.FindProperty("m_SupportsHDR");
            if (hdr == null || !hdr.boolValue) return;
            hdr.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static BuildReport Build(string outputFolder = OutputFolder, BuildOptions options = BuildOptions.None)
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            // 예외 확인 코드를 줄여서 빌드를 작고 빠르게 (직접 throw한 예외만 잡는다)
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            EnableStaticBatching();
            DisableHdrForWeb();
            // 로딩 화면: Unity 로고 대신 썸네일 그림 + 게임 색 진행 막대 (Assets/WebGLTemplates/Nakgopsae)
            PlayerSettings.WebGL.template = "PROJECT:Nakgopsae";
            // "Made with Unity" 시작 로고 끄기 (Unity 6부터 무료 버전도 끌 수 있다)
            PlayerSettings.SplashScreen.show = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameScenePath },
                locationPathName = outputFolder,
                target = BuildTarget.WebGL,
                options = options,
            });
            Debug.Log($"[WebBuild] {report.summary.result} · {report.summary.totalSize / (1024f * 1024f):0.0}MB · " +
                      $"{report.summary.totalTime.TotalMinutes:0.0}분 · 에러 {report.summary.totalErrors}개");
            return report;
        }
    }
}
