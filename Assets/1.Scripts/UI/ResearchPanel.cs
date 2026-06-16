using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Craft;
using Game.Items;
using Game.Minigame;

namespace Game.UI {
    // 연구 패널(uGUI) — 분석 버튼 + 진행 게이지 + 상태 텍스트
    public class ResearchPanel : MonoBehaviour {
        [SerializeField] Research research;
        [SerializeField] RunData run;
        [SerializeField] Collector collector;   // 수집 시 실시간 갱신용
        [SerializeField] Button analyzeBtn;
        [SerializeField] Image progressFill;
        [SerializeField] TMP_Text statusText;
        [SerializeField] ResearchMinigame minigame;   // 오염 구조 연결 퍼즐(미연결 시 즉시 분석 폴백)

        bool fallbackWarned;   // 폴백 경고 1회만 출력

        void Start() {
            // minigame 미배선 시 자가탐색(NarrationController/ScoreHud 패턴)
            if (minigame == null) {
                minigame = FindFirstObjectByType<ResearchMinigame>();
            }
        }

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
            // 패널이 닫히면(상태 전환) 열린 퍼즐도 취소 — 팝업·콜백 잔류 방지
            if (minigame != null && minigame.IsOpen) {
                minigame.Cancel();
            }
            Unsubscribe();
        }

        void OnDestroy() {
            Unsubscribe();
        }

        // 이벤트 해제 — OnDisable과 OnDestroy 양쪽에서 호출(중복 해제 안전)
        void Unsubscribe() {
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
            if (research == null || run == null) {
                return;
            }
            int level = LowestSampleLevel();
            if (level <= 0) {
                Refresh();   // 분석할 샘플 없음
                return;
            }
            // 퍼즐 게이트 — 성공 시에만 분석. 미연결이면 즉시 분석(폴백)
            if (minigame != null) {
                if (minigame.IsOpen) {
                    return;   // 이미 퍼즐 진행 중 — 연타로 우회 방지
                }
                minigame.Open(level, () => {
                    research.Analyze(level);
                    Refresh();
                });
            } else {
                // 자가탐색도 실패한 실제 폴백 — 경고 1회 출력
                if (!fallbackWarned) {
                    Debug.LogWarning("[ResearchPanel] minigame 미배선 — 즉시 분석 폴백");
                    fallbackWarned = true;
                }
                research.Analyze(level);
                Refresh();
            }
        }

        // 보유한 가장 낮은 오염수준 샘플(1~3) — 없으면 0
        int LowestSampleLevel() {
            for (int lv = 1; lv <= 3; lv++) {
                if (run.GetSampleCount(lv) > 0) {
                    return lv;
                }
            }
            return 0;
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
                int s = run != null ? run.TotalBankSamples : 0;
                int pts = research != null ? research.Points : 0;
                statusText.text = done ? "모든 정화제 해금"
                                       : $"샘플 분석  보유 샘플 {s}  분석포인트 {pts}";
            }
            if (analyzeBtn != null) {
                bool canAnalyze = !done && run != null && run.TotalBankSamples > 0;
                analyzeBtn.interactable = canAnalyze;
            }
        }
    }
}
