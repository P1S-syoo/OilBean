using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Craft;

namespace Game.UI {
    // 거점 콘솔 — 자원 바 갱신 + 행동 버튼 라우팅 + 현재 목표 카드 표시
    // GameState.Dock 진입 시 활성화(호출자가 SetActive 제어)
    public class DockConsoleUI : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] Research research;
        [SerializeField] Crafting crafting;
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] Game.Surface.SurfaceBootstrap surface;   // 수상에서 잠수 트리거(미연결 시 자동 탐색)

        [SerializeField] TMP_Text resourceBarText;   // 상단 자원 바 텍스트
        [SerializeField] TMP_Text goalBodyText;       // 우측 현재 목표 카드 본문

        [SerializeField] Button diveBtn;       // 탐사 시작
        [SerializeField] Button researchBtn;   // 연구 패널 열기
        [SerializeField] Button craftBtn;      // 제작 패널 열기
        [SerializeField] Button purifyBtn;     // 정화 설치로 전환
        [SerializeField] Button closeBtn;      // 거점 콘솔 닫기(수상 — 배 조작 복귀)

        // 직전 표시값 캐시(매 프레임 재할당 방지)
        float lastSteel0 = -1f, lastSteel1 = -1f, lastSteel2 = -1f;
        int lastSmp = -1, lastPts = -1, lastBuoy = -1;
        float lastWeight = -1f;

        void Awake() {
            // 미연결 참조 자동 탐색 — 수상 잠수 트리거용
            if (surface == null) {
                surface = FindFirstObjectByType<Game.Surface.SurfaceBootstrap>();
            }
        }

        void OnEnable() {
            // 버튼 이벤트 등록
            if (diveBtn != null) {
                diveBtn.onClick.AddListener(OnDive);
            }
            if (researchBtn != null) {
                researchBtn.onClick.AddListener(OnResearch);
            }
            if (craftBtn != null) {
                craftBtn.onClick.AddListener(OnCraft);
            }
            if (purifyBtn != null) {
                purifyBtn.onClick.AddListener(OnPurify);
            }
            if (closeBtn != null) {
                closeBtn.onClick.AddListener(OnClose);
            }
            Refresh();
        }

        void OnDisable() {
            Unsubscribe();
        }

        void OnDestroy() {
            Unsubscribe();
        }

        // 이벤트 해제 — OnDisable/OnDestroy 양쪽 안전 호출
        void Unsubscribe() {
            if (diveBtn != null) {
                diveBtn.onClick.RemoveListener(OnDive);
            }
            if (researchBtn != null) {
                researchBtn.onClick.RemoveListener(OnResearch);
            }
            if (craftBtn != null) {
                craftBtn.onClick.RemoveListener(OnCraft);
            }
            if (purifyBtn != null) {
                purifyBtn.onClick.RemoveListener(OnPurify);
            }
            if (closeBtn != null) {
                closeBtn.onClick.RemoveListener(OnClose);
            }
        }

        void LateUpdate() {
            // 값 변경 시에만 갱신(무할당)
            if (run == null) {
                return;
            }
            float s0 = run.GetSteel(0), s1 = run.GetSteel(1), s2 = run.GetSteel(2);
            int smp = run.TotalBankSamples;
            int pts  = research != null ? research.Points : 0;
            int buoy = run.BuoyStage;
            float w  = run.Weight;
            if (Mathf.Approximately(s0, lastSteel0) && Mathf.Approximately(s1, lastSteel1)
                && Mathf.Approximately(s2, lastSteel2) && smp == lastSmp
                && pts == lastPts && buoy == lastBuoy && Mathf.Approximately(w, lastWeight)) {
                return;
            }
            lastSteel0 = s0; lastSteel1 = s1; lastSteel2 = s2;
            lastSmp = smp; lastPts = pts; lastBuoy = buoy; lastWeight = w;
            Refresh();
        }

        // 자원 바 + 목표 카드 갱신
        void Refresh() {
            UpdateResourceBar();
            UpdateGoalCard();
            UpdateButtonStates();
        }

        // 상단 자원 바: 강재 3등급 / 샘플 / 적재 / 해금 정화제
        void UpdateResourceBar() {
            if (resourceBarText == null || run == null) {
                return;
            }
            string agentStatus = GetAgentStatus();
            resourceBarText.text =
                $"강재  일반 {run.GetSteel(0):0}kg  합금 {run.GetSteel(1):0}kg  특수 {run.GetSteel(2):0}kg" +
                $"    샘플 {run.TotalBankSamples}개" +
                $"    적재 {run.Weight:0}/{run.MaxWeight:0}kg" +
                $"    정화제 {agentStatus}";
        }

        // 현재 목표 카드 — 부유체 단계/분석 진행에 따라 동적 텍스트
        void UpdateGoalCard() {
            if (goalBodyText == null || run == null) {
                return;
            }
            int buoy = run.BuoyStage;
            bool done = research != null && research.Done;
            if (done && buoy >= 3) {
                goalBodyText.text = "모든 구역 정화 완료!\n클리어 조건 충족";
                goalBodyText.color = UITheme.ColSuccess;
                return;
            }
            goalBodyText.color = UITheme.TextPrimary;
            if (!done) {
                int pts  = research != null ? research.Points : 0;
                int need = buoy == 0 ? 3 : buoy == 1 ? 8 : 15;
                goalBodyText.text =
                    $"샘플을 수집하고 연구실에서 분석하세요.\n" +
                    $"현재 분석 포인트: {pts} / {need}pt\n" +
                    $"다음: {GetNextAgentName()} 해금 필요";
            } else {
                float depth = run.MaxDepth();
                goalBodyText.text =
                    $"부유체 {buoy}단계 완료 — 수심 {depth:0}m 탐사 가능\n" +
                    $"부유체를 설치하고 다음 구역으로 진입하세요.\n" +
                    $"설치 대기: {(run.BuoyReady ? "준비됨" : "제작 필요")}";
            }
        }

        // 버튼 활성 상태 — 패널 열기는 조건 최소화(가능 항목 판단은 패널 내부에서)
        void UpdateButtonStates() {
            if (researchBtn != null) {
                // 연구는 재료(샘플)가 있어야만 열림
                researchBtn.interactable = run != null && run.TotalBankSamples > 0;
            }
            if (craftBtn != null) {
                // 제작은 조건 없이 항상 열림
                craftBtn.interactable = true;
            }
            if (purifyBtn != null) {
                purifyBtn.interactable = run != null && run.BuoyReady;
            }
        }

        // 버튼 핸들러 — GameBootstrap FSM 전환
        // 탐사 시작 — 수상(도착)에선 잠수 연출(E키와 동일), 거점(Dock)에선 즉시 탐사
        void OnDive() {
            if (bootstrap == null) {
                Debug.LogWarning("[DockConsoleUI] GameBootstrap 미연결 — 탐사 전환 불가");
                return;
            }
            if (bootstrap.State == GameState.Surface) {
                if (surface != null) {
                    surface.RequestDive();   // 정화 지점에서 잠수 인계
                } else {
                    Debug.LogWarning("[DockConsoleUI] SurfaceBootstrap 미연결 — 잠수 불가");
                }
            } else {
                bootstrap.StartDive();
            }
        }

        void OnResearch() {
            if (bootstrap != null) {
                bootstrap.GoResearch();
            }
        }

        void OnCraft() {
            if (bootstrap != null) {
                bootstrap.GoCraft();
            }
        }

        void OnPurify() {
            if (bootstrap != null) {
                bootstrap.GoPurify();
            }
        }

        // 닫기 — 수상에선 콘솔 접고 배 조작 복귀(P키와 동일), 그 외 상태는 무시
        void OnClose() {
            if (surface != null && bootstrap != null && bootstrap.State == GameState.Surface) {
                surface.CloseConsole();
            }
        }

        // 해금된 약품 목록 문자열 (없으면 "없음")
        string GetAgentStatus() {
            if (run == null) {
                return "없음";
            }
            string result = "";
            if (run.IsUnlocked("agent_mild"))   { result += "약품Ⅰ "; }
            if (run.IsUnlocked("agent_mid"))    { result += "약품Ⅱ "; }
            if (run.IsUnlocked("agent_strong")) { result += "약품Ⅲ"; }
            return string.IsNullOrWhiteSpace(result) ? "없음" : result.Trim();
        }

        // 다음 해금 대상 약품명
        string GetNextAgentName() {
            if (run == null) {
                return "약품Ⅰ";
            }
            if (!run.IsUnlocked("agent_mild"))   { return "약품Ⅰ (3pt)"; }
            if (!run.IsUnlocked("agent_mid"))    { return "약품Ⅱ (8pt)"; }
            if (!run.IsUnlocked("agent_strong")) { return "약품Ⅲ (15pt)"; }
            return "완료";
        }
    }
}
