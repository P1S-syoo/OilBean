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
        int selectedSampleLevel; // 선택한 샘플 레벨(0이면 자동 선택)

        public int SelectedSampleLevel => selectedSampleLevel;

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
            // 런타임 패널 등장 모션(수면 부상) — 에디터 베이크 버그 회피 위해 플레이 중에만
            if (Application.isPlaying) {
                UITheme.RiseIn(gameObject);
            }
        }

        void OnDisable() {
            // 패널이 닫히면(상태 전환) 열린 퍼즐도 취소 — 팝업·콜백 잔류 방지
            if (minigame != null && minigame.IsOpen) {
                minigame.Cancel();
            }
            KillRiseInTweens();
            Unsubscribe();
        }

        // RiseIn이 건 RectTransform·CanvasGroup 트윈 정리(죽은 타깃 경고/위치 드리프트 방지)
        void KillRiseInTweens() {
            var rt = GetComponent<RectTransform>();
            if (rt != null) {
                DG.Tweening.DOTween.Kill(rt);
            }
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) {
                DG.Tweening.DOTween.Kill(cg);
            }
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
            int level = SelectedOrBestSampleLevel();
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

        // UI에서 분석할 샘플 레벨 선택 — 없는 레벨이면 자동 선택으로 복귀
        public void SelectSampleLevel(int level) {
            if (run == null || level < 1 || level > 3 || run.GetSampleCount(level) <= 0) {
                selectedSampleLevel = BestSampleLevel();
            } else {
                selectedSampleLevel = level;
            }
            Refresh();
        }

        // 선택 샘플이 있으면 유지, 없으면 가장 높은 보유 샘플로 자동 선택
        int SelectedOrBestSampleLevel() {
            if (run != null && selectedSampleLevel >= 1 && selectedSampleLevel <= 3
                && run.GetSampleCount(selectedSampleLevel) > 0) {
                return selectedSampleLevel;
            }
            selectedSampleLevel = BestSampleLevel();
            return selectedSampleLevel;
        }

        // 보유한 가장 높은 오염수준 샘플(3→1) — 상위 재료를 바로 분석할 수 있게 기본 선택
        int BestSampleLevel() {
            for (int lv = 3; lv >= 1; lv--) {
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
            SelectedOrBestSampleLevel();
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
