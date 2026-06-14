using System;
using UnityEngine;
using Game.Core;

namespace Game.Craft {
    // 연구 2축 — ① 분석: 샘플 소비 → 분석포인트 누적 + 최고 오염수준 갱신
    //            ② 약품: 누적 포인트 tier + 선행·최고수준 게이트로 정화제 해금(RunData.recipes에 id 추가)
    public class Research : MonoBehaviour {
        [SerializeField] RunData run;

        // 정화 약품 정의 — 엑셀 ItemRecipeTable(약품) 이식
        struct AgentDef {
            public string id;       // agent_mild / mid / strong
            public int need;        // 필요 누적 분석포인트
            public string prereq;   // 선행 약품 id(없으면 빈 문자열)
            public int minLevel;    // 필요 최고 분석 오염수준(고농도 게이트)
        }

        static readonly AgentDef[] Agents = {
            new AgentDef { id = "agent_mild",   need = 3,  prereq = "",           minLevel = 0 },
            new AgentDef { id = "agent_mid",    need = 8,  prereq = "agent_mild", minLevel = 0 },
            new AgentDef { id = "agent_strong", need = 15, prereq = "agent_mid",  minLevel = 3 },
        };

        public event Action OnUnlocked;   // 약품 1종 해금(사운드·UI 구독) — 기존 호환
        public event Action OnAnalyzed;   // 분석 1회(포인트 변동)

        public int Points => run != null ? run.ResearchPoints : 0;
        public int MaxLevel => run != null ? run.MaxAnalyzedLevel : 0;

        // 모든 약품 해금 여부
        public bool Done => run != null && NextNeed() < 0;

        // 다음 해금 약품까지 진행도 0~1(UI 게이지)
        public float Progress {
            get {
                if (run == null) {
                    return 0f;
                }
                int need = NextNeed();
                if (need < 0) {
                    return 1f;   // 다 해금
                }
                int prev = PrevNeed(need);
                int span = need - prev;
                return span <= 0 ? 1f : Mathf.Clamp01((float)(run.ResearchPoints - prev) / span);
            }
        }

        // 지정 오염수준 샘플 1개 분석 — 포인트 누적 + 약품 해금 체크
        public bool Analyze(int level) {
            try {
                if (run == null || !run.TryConsumeSampleAt(level)) {
                    return false;   // 해당 수준 샘플 부족
                }
                run.AddResearchPoints(level);
                OnAnalyzed?.Invoke();
                CheckUnlocks();
                return true;
            } catch (Exception e) {
                Debug.LogError($"[Research] Analyze 오류: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // 인자 없는 분석 — 보유한 가장 낮은 수준 샘플 자동 선택(기존 호출 호환)
        public bool Analyze() {
            for (int lv = 1; lv <= 3; lv++) {
                if (run != null && run.GetSampleCount(lv) > 0) {
                    return Analyze(lv);
                }
            }
            return false;
        }

        public bool IsAgentUnlocked(string id) {
            return run != null && run.IsUnlocked(id);
        }

        // 조건 충족 약품을 해금(여러 개 동시 충족 가능)
        void CheckUnlocks() {
            foreach (var a in Agents) {
                if (run.IsUnlocked(a.id) || run.ResearchPoints < a.need) {
                    continue;
                }
                if (!string.IsNullOrEmpty(a.prereq) && !run.IsUnlocked(a.prereq)) {
                    continue;   // 선행 약품 미해금
                }
                if (run.MaxAnalyzedLevel < a.minLevel) {
                    continue;   // 최고 분석 수준 부족(고농도 게이트)
                }
                run.Unlock(a.id);
                OnUnlocked?.Invoke();
                Debug.Log($"[Research] 약품 해금: {a.id}");
            }
        }

        // 다음(미해금) 약품의 필요 포인트 — 없으면 -1
        int NextNeed() {
            foreach (var a in Agents) {
                if (!run.IsUnlocked(a.id)) {
                    return a.need;
                }
            }
            return -1;
        }

        // 해당 need 직전 약품의 need(게이지 구간 하한)
        static int PrevNeed(int need) {
            int prev = 0;
            foreach (var a in Agents) {
                if (a.need >= need) {
                    break;
                }
                prev = a.need;
            }
            return prev;
        }
    }
}
