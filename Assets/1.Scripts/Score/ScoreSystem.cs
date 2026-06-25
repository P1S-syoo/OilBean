using System;
using UnityEngine;
using Game.Core;
using Game.Items;

namespace Game.Score {
    // 콤보·점수·등급 로직 — UI 의존 없이 이벤트로 외부에 노출
    public class ScoreSystem : MonoBehaviour {
        [SerializeField] float comboWindow;   // 콤보 유지 시간(초) — 기본값은 점수설정.콤보시간
        [SerializeField] int baseValue;       // 수집 1회 기본 점수 — 기본값은 점수설정.기본점수
        [SerializeField] int gradeS;          // S 등급 임계값 — 기본값은 점수설정.S등급
        [SerializeField] int gradeA;          // A 등급 임계값(미만은 B) — 기본값은 점수설정.A등급
        [SerializeField] 점수설정 config;         // 점수 설정 — 연결 시 점수 수치 덮어씀(미연결 시 위 기본값 유지)

        // 점수·콤보 공개 상태
        public int Score { get; private set; }
        public int Combo { get; private set; } = 1;

        float lastCollectTime = float.NegativeInfinity;

        Collector collector;
        GameBootstrap bootstrap;

        // 점수·콤보 변경(score, combo)
        public event Action<int, int> OnScoreChanged;
        // 콤보 끊김 알림
        public event Action OnComboReset;
        // 클리어 등급 확정(grade, score, best)
        public event Action<string, int, int> OnGrade;

        void Start() {
            try {
                // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거)
                var cfg = config != null ? config : 점수설정.기본;
                comboWindow = cfg.콤보시간;
                baseValue = cfg.기본점수;
                gradeS = cfg.S등급;
                gradeA = cfg.A등급;
                collector = FindFirstObjectByType<Collector>();
                if (collector != null) {
                    collector.OnCollect += HandleCollect;
                } else {
                    Debug.LogWarning("[ScoreSystem] Collector 미발견 — 수집 이벤트를 구독할 수 없습니다.");
                }

                bootstrap = FindFirstObjectByType<GameBootstrap>();
                if (bootstrap != null) {
                    bootstrap.OnStateChanged += HandleStateChanged;
                } else {
                    Debug.LogWarning("[ScoreSystem] GameBootstrap 미발견 — 상태 전환 이벤트를 구독할 수 없습니다.");
                }
            } catch (Exception e) {
                Debug.LogError($"[ScoreSystem] 초기화 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        void Update() {
            // comboWindow 초과 시 콤보 리셋(콤보 > 1일 때만 끊김 이벤트 발생)
            if (Combo > 1 && Time.time - lastCollectTime > comboWindow) {
                Combo = 1;
                OnComboReset?.Invoke();
            }
        }

        void OnDestroy() {
            if (collector != null) {
                collector.OnCollect -= HandleCollect;
            }
            if (bootstrap != null) {
                bootstrap.OnStateChanged -= HandleStateChanged;
            }
        }

        // 수집 이벤트 핸들러 — 콤보 판정 후 점수 누산
        void HandleCollect(ResourceKind kind) {
            if (Time.time - lastCollectTime <= comboWindow) {
                Combo++;
            } else {
                Combo = 1;
            }
            lastCollectTime = Time.time;
            Score += baseValue * Combo;
            OnScoreChanged?.Invoke(Score, Combo);
        }

        // 상태 전환 핸들러 — Clear 시 등급 확정, Surface 복귀 시 세션 리셋
        void HandleStateChanged(GameState from, GameState to) {
            if (to == GameState.Clear) {
                CalcGrade();
            }
            if (to == GameState.Surface) {
                ResetScore();
            }
        }

        // 등급 계산 및 베스트 스코어 갱신
        void CalcGrade() {
            string grade = Score >= gradeS ? "S" : Score >= gradeA ? "A" : "B";
            int best = Score;
            try {
                int saved = PlayerPrefs.GetInt("OilBean_BestScore", 0);
                if (Score > saved) {
                    PlayerPrefs.SetInt("OilBean_BestScore", Score);
                    PlayerPrefs.Save();
                } else {
                    best = saved;
                }
            } catch (Exception e) {
                Debug.LogError($"[ScoreSystem] PlayerPrefs 접근 실패: {e.Message}");
            }
            OnGrade?.Invoke(grade, Score, best);
        }

        // 세션 점수·콤보 초기화
        public void ResetScore() {
            Score = 0;
            Combo = 1;
            lastCollectTime = float.NegativeInfinity;
            OnScoreChanged?.Invoke(Score, Combo);
        }
    }
}
