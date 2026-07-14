using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// CI 배치모드 빌드 — executeMethod로 Windows64 산출물을 Build/Windows에 생성
public static class CIBuild {
    public static void BuildWindows() {
        // 빌드 세팅에서 활성화된 씬만 포함
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        if (scenes.Length == 0) {
            Debug.LogError("[CIBuild] 빌드 씬이 없습니다 — Build Settings 확인");
            EditorApplication.Exit(2);
            return;
        }
        var options = new BuildPlayerOptions {
            scenes = scenes,
            locationPathName = "Build/Windows/OilBean.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"[CIBuild] 결과={summary.result} 크기={summary.totalSize} bytes 시간={summary.totalTime}");
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // CI 배치모드 빌드 — executeMethod로 WebGL 산출물을 Build/WebGL에 생성
    public static void BuildWebGL() {
        // 빌드 세팅에서 활성화된 씬만 포함
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        if (scenes.Length == 0) {
            Debug.LogError("[CIBuild] 빌드 씬이 없습니다 — Build Settings 확인");
            EditorApplication.Exit(2);
            return;
        }
        var options = new BuildPlayerOptions {
            scenes = scenes,
            locationPathName = "Build/WebGL",   // WebGL은 폴더 산출물(index.html + Build/ + TemplateData/)
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"[CIBuild] 결과={summary.result} 크기={summary.totalSize} bytes 시간={summary.totalTime}");
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
