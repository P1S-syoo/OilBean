#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.Automation {
    // 배치 검증/재생성 러너 — Main3D에서 전체 게임설정을 적용하고 저장
    public static class Project4BatchRunner {
        const string MainScene = "Assets/2.Scenes/Main3D.unity";

        public static void RebuildUiAndStage() {
            try {
                EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
                bool applyOk = EditorApplication.ExecuteMenuItem("게임설정/전체 설정 적용");
                if (!applyOk) {
                    Debug.LogError("[Project4BatchRunner] 메뉴 실행 실패: 게임설정/전체 설정 적용");
                    EditorApplication.Exit(1);
                    return;
                }
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                Debug.Log("[Project4BatchRunner] 전체 게임설정 적용 및 씬 저장 완료");
            } catch (System.Exception e) {
                Debug.LogError($"[Project4BatchRunner] 실패: {e.Message}\n{e.StackTrace}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
