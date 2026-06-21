using UnityEngine;

namespace Game.Core {
    // 셰이더 깨짐(분홍) 폴백 — Unity/URP 버전 불일치로 셰이더가 깨질 때의 런타임 안전망
    // (5단계: 버전에 따라 셰이더 깨짐현상 해결). 외부 에셋·버전 업그레이드 후 누락/에러 셰이더를 URP 기본으로 교체
    public static class ShaderGuard {
        const string ErrorShader = "Hidden/InternalErrorShader";

        // 루트 이하 모든 Renderer의 깨진 머티리얼을 URP 폴백 셰이더로 교체. 교체 수 반환
        public static int FixBroken(GameObject root, bool unlit = false) {
            if (root == null) {
                return 0;
            }
            var fallback = Shader.Find(unlit
                ? "Universal Render Pipeline/Unlit"
                : "Universal Render Pipeline/Lit");
            if (fallback == null) {
                return 0;   // URP 미설치 환경 — 폴백 불가
            }
            int fixedCount = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++) {
                    var m = mats[i];
                    if (m != null && (m.shader == null || m.shader.name == ErrorShader)) {
                        m.shader = fallback;
                        changed = true;
                        fixedCount++;
                    }
                }
                if (changed) {
                    r.sharedMaterials = mats;
                }
            }
            if (fixedCount > 0) {
                Debug.LogWarning($"[ShaderGuard] 깨진 셰이더 {fixedCount}개를 URP 폴백으로 교체");
            }
            return fixedCount;
        }
    }
}
