#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.Automation {
    // 배치 검증/재생성 러너 — MCP가 막혔을 때도 Main3D UI/Stage 빌더를 재실행하고 저장
    public static class Project4BatchRunner {
        const string MainScene = "Assets/2.Scenes/Main3D.unity";

        public static void RebuildUiAndStage() {
            try {
                EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
                bool surfaceOk = EditorApplication.ExecuteMenuItem("Tools/한강/수상 리그 생성");
                bool hudOk = EditorApplication.ExecuteMenuItem("Tools/한강/UI/HUD·패널 생성");
                bool stageOk = EditorApplication.ExecuteMenuItem("Tools/한강/정화·클리어 생성");
                bool worldOk = EditorApplication.ExecuteMenuItem("Tools/한강/World/E5·E6 배선");
                if (!surfaceOk || !hudOk || !stageOk || !worldOk) {
                    Debug.LogError($"[Project4BatchRunner] 메뉴 실행 실패 surface={surfaceOk}, hud={hudOk}, stage={stageOk}, world={worldOk}");
                    EditorApplication.Exit(1);
                    return;
                }
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                Debug.Log("[Project4BatchRunner] UI/Stage 재생성 및 씬 저장 완료");
            } catch (System.Exception e) {
                Debug.LogError($"[Project4BatchRunner] 실패: {e.Message}\n{e.StackTrace}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
