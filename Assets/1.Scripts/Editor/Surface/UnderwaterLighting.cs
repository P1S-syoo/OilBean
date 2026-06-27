using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Surface {
    // 수중 가시성 보강 — 디렉셔널 광량·환경광을 살짝 올려 수집물·지형이 또렷하게(수중 분위기는 유지)
    public static class UnderwaterLighting {
        // [MenuItem("Tools/한강/환경/수중 라이팅 조정")]
        public static void Apply() {
            try {
                var dir = FindDirectional();
                if (dir != null) {
                    dir.intensity = 0.95f;   // 0.75 → 0.95 (살짝 밝게)
                    EditorUtility.SetDirty(dir);
                }
                // 환경광 — 수중이 탁한 회색으로 묻히지 않게 약간 밝은 청록 톤
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientIntensity = 1.2f;
                RenderSettings.ambientSkyColor = new Color(0.38f, 0.55f, 0.58f);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log($"[UnderwaterLighting] 조정 완료 — 디렉셔널={(dir != null ? dir.intensity : 0f):F2}, 환경광 {RenderSettings.ambientIntensity:F2}");
            } catch (Exception e) {
                Debug.LogError($"[UnderwaterLighting] 조정 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        static Light FindDirectional() {
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                if (l.type == LightType.Directional) {
                    return l;
                }
            }
            return null;
        }
    }
}
