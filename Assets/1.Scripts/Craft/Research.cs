using System;
using UnityEngine;
using Game.Core;

namespace Game.Craft {
    // 연구(오염 구조 분석) — 퍼즐 규칙 미확정이라 플레이스홀더: 샘플 소비→게이지→레시피 해금
    public class Research : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] string recipeId = "purifier";   // 해금할 정화 약품 레시피
        [SerializeField] int needSamples = 2;             // 분석에 필요한 샘플 수
        [SerializeField] float sampleUnitWeight = 6f;     // 샘플 1개 무게(차감용)

        // 진행도 0~1 (RunData에 저장되어 ResetRun으로 리셋됨)
        public float Progress => (run != null && needSamples > 0) ? Mathf.Clamp01((float)run.ResearchProgress / needSamples) : 0f;
        public bool Done => run != null && run.IsUnlocked(recipeId);

        // 분석 완료 — 사운드/UI가 구독
        public event Action OnUnlocked;

        // 분석 1스텝(버튼 클릭) — 샘플 1개 소비, 다 모으면 레시피 해금
        public bool Analyze() {
            try {
                if (run == null || Done) {
                    return false;
                }
                if (!run.TryConsumeSample(sampleUnitWeight)) {
                    return false;   // 샘플 부족
                }
                run.AddResearch();
                if (run.ResearchProgress >= needSamples) {
                    run.Unlock(recipeId);
                    OnUnlocked?.Invoke();
                    Debug.Log($"[Research] 레시피 해금: {recipeId}");
                }
                return true;
            } catch (Exception e) {
                Debug.LogError($"[Research] Analyze 오류: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }
    }
}
