using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Game.Editor.Audio;
using Game.Editor.Data;
using Game.Editor.Juice;
using Game.Editor.Stage;
using Game.Editor.Surface;
using Game.Editor.UI;
using Game.EditorTools;

namespace Game.Editor.Tools {
    // 게임설정 적용 메뉴 — SO 이름과 같은 메뉴만 노출해 헷갈리는 개별 빌더 메뉴를 숨김
    public static class 게임설정적용도구 {

        [MenuItem("게임설정/수면위설정 적용")]
        public static void 수면위설정_적용() {
            ApplyWiringOnly();
            SurfaceRigBuilder.Build();
            SaveScene();
        }

        [MenuItem("게임설정/잠수설정 적용")]
        public static void 잠수설정_적용() {
            ApplyWiringOnly();
            HudUIBuilder.Build();
            SaveScene();
        }

        [MenuItem("게임설정/월드설정 적용")]
        public static void 월드설정_적용() {
            ApplyWiringOnly();
            WorldNarrationBuilder.Build();
            SaveScene();
        }

        [MenuItem("게임설정/오디오설정 적용")]
        public static void 오디오설정_적용() {
            ApplyWiringOnly();
            AudioWirer.Wire();
            SaveScene();
        }

        [MenuItem("게임설정/위험설정 적용")]
        public static void 위험설정_적용() {
            ApplyWiringOnly();
            WorldNarrationBuilder.Build();
            SaveScene();
        }

        [MenuItem("게임설정/수집설정 적용")]
        public static void 수집설정_적용() {
            ApplyWiringOnly();
            CollectibleStreamerWirer.Wire();
            HudUIBuilder.Build();
            SaveScene();
        }

        [MenuItem("게임설정/제작설정 적용")]
        public static void 제작설정_적용() {
            ApplyWiringOnly();
            HudUIBuilder.Build();
            ScoreHudBuilder.Build();
            StageWiringBuilder.Build();
            SaveScene();
        }

        [MenuItem("게임설정/점수설정 적용")]
        public static void 점수설정_적용() {
            ApplyWiringOnly();
            ScoreHudBuilder.Build();
            SaveScene();
        }

        [MenuItem("게임설정/연출설정 적용")]
        public static void 연출설정_적용() {
            ApplyWiringOnly();
            HudUIBuilder.Build();
            JuiceBuilder.Build();
            WorldNarrationBuilder.Build();
            SaveScene();
        }

        [MenuItem("게임설정/디버그설정 적용")]
        public static void 디버그설정_적용() {
            ApplyWiringOnly();
            SaveScene();
        }

        [MenuItem("게임설정/전체 설정 적용")]
        public static void 전체설정_적용() {
            ApplyWiringOnly();
            SurfaceRigBuilder.Build();
            HudUIBuilder.Build();
            ScoreHudBuilder.Build();
            StageWiringBuilder.Build();
            WorldNarrationBuilder.Build();
            CollectibleStreamerWirer.Wire();
            AudioWirer.Wire();
            JuiceBuilder.Build();
            SaveScene();
        }

        static void ApplyWiringOnly() {
            게임설정도구.ApplyWiring();
        }

        static void SaveScene() {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }
    }
}
