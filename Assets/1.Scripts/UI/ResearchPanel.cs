using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Craft;
using Game.Items;

namespace Game.UI {
    // 연구 패널(uGUI) — 분석 버튼 + 진행 게이지 + 상태 텍스트
    public class ResearchPanel : MonoBehaviour {
        [SerializeField] Research research;
        [SerializeField] RunData run;
        [SerializeField] Collector collector;   // 수집 시 실시간 갱신용
        [SerializeField] Button analyzeBtn;
        [SerializeField] Image progressFill;
        [SerializeField] TMP_Text statusText;

        void OnEnable() {
            if (analyzeBtn != null) {
                analyzeBtn.onClick.AddListener(OnAnalyze);
            }
            if (research != null) {
                research.OnUnlocked += OnChanged;   // 해금 → 갱신
            }
            if (collector != null) {
                collector.OnCollect += OnCollected;   // 수집 → 갱신
            }
            Refresh();
        }

        void OnDisable() {
            if (analyzeBtn != null) {
                analyzeBtn.onClick.RemoveListener(OnAnalyze);
            }
            if (research != null) {
                research.OnUnlocked -= OnChanged;
            }
            if (collector != null) {
                collector.OnCollect -= OnCollected;
            }
        }

        void OnAnalyze() {
            if (research != null) {
                research.Analyze();
            }
            Refresh();
        }

        // 해금/수집 이벤트 → 실시간 갱신(이벤트성, 매 프레임 아님)
        void OnChanged() => Refresh();
        void OnCollected(ResourceKind kind) => Refresh();

        // 게이지/버튼/상태 갱신
        void Refresh() {
            if (research != null && progressFill != null) {
                progressFill.fillAmount = research.Progress;
            }
            bool done = research != null && research.Done;
            if (statusText != null) {
                int s = run != null ? run.SampleCount : 0;
                statusText.text = done ? "분석 완료 — 정화 약품 해금"
                                       : $"샘플 분석  보유 샘플 {s}";
            }
            if (analyzeBtn != null) {
                bool canAnalyze = !done && run != null && run.SampleCount > 0;
                analyzeBtn.interactable = canAnalyze;
            }
        }
    }
}
