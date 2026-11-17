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

        private static BuildReport Build()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            // 예외 확인 코드를 줄여서 빌드를 작고 빠르게 (직접 throw한 예외만 잡는다)
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameScenePath },
                locationPathName = OutputFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            Debug.Log($"[WebBuild] {report.summary.result} · {report.summary.totalSize / (1024f * 1024f):0.0}MB · " +
                      $"{report.summary.totalTime.TotalMinutes:0.0}분 · 에러 {report.summary.totalErrors}개");
            return report;
        }
    }
}
