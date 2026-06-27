using UnityEditor;
using UnityEngine;

namespace Game.Editor.Surface {
    // 임시 검증 — 플레이모드에서 입력 없이 잠수 시퀀스를 강제 트리거
    public static class DiveDebug {
        // [MenuItem("Tools/한강/디버그/잠수 트리거")]
        public static void TriggerDive() {
            if (!Application.isPlaying) {
                Debug.LogWarning("[DiveDebug] 플레이모드에서만 동작");
                return;
            }
            var boot = Object.FindFirstObjectByType<Game.Surface.SurfaceBootstrap>();
            if (boot == null) {
                Debug.LogError("[DiveDebug] SurfaceBootstrap 없음");
                return;
            }
            boot.RequestDive();
            Debug.Log("[DiveDebug] 잠수 시퀀스 강제 시작");
        }
    }
}
