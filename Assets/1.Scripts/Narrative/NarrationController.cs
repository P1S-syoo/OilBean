using System;
using UnityEngine;
using Game.Core;

namespace Game.Narrative {
    // 인트로/클리어 내레이션 트리거 — GameBootstrap 상태 변화를 구독해 NarrationView에 재생 요청
    public class NarrationController : MonoBehaviour {

        // 대사 정본(SSOT)은 GameConfig.introLines/clearLines — 아래 하드코딩은 config 미연결 폴백일 뿐
        // ── 인트로 대사 (Surface 최초 진입 시 폴백) ─────────────────
        static readonly string[] IntroLines = {
            "2042년, 한강은 죽었다.",
            "오염 물질이 강바닥을 덮고, 물고기는 사라졌다.",
            "당신은 정화선의 마지막 잠수사.",
            "잠수정을 타고 내려가 강을 되살려라.",
        };

        // ── 클리어 대사 (Clear 진입 시) ────────────────────────────
        static readonly string[] ClearLines = {
            "정화 부유체가 자리를 잡았다.",
            "탁한 물이 조금씩 맑아지기 시작한다.",
            "강은 아직 기억하고 있었다 — 흐르는 법을.",
        };

        // ── 직렬화 필드 ─────────────────────────────────────────────
        [SerializeField] NarrationView    view;
        [SerializeField] GameBootstrap    bootstrap;
        [SerializeField] GameConfig       config;   // 통합 설정 — 대사가 채워져 있으면 하드코딩 대신 사용

        // ── 재생 가드 ────────────────────────────────────────────────
        bool introPlayed;
        bool clearPlayed;

        // ── 대사 출처 ────────────────────────────────────────────────
        // config에 대사가 채워져 있으면 그것을, 없으면 하드코딩 폴백 사용
        string[] IntroSource => (config != null && config.introLines != null && config.introLines.Length > 0) ? config.introLines : IntroLines;
        string[] ClearSource => (config != null && config.clearLines != null && config.clearLines.Length > 0) ? config.clearLines : ClearLines;

        // ── 라이프사이클 ─────────────────────────────────────────────

        void Start() {
            try {
                // 인스펙터 미연결 시 씬에서 자가 탐색
                if (view == null) {
                    view = FindFirstObjectByType<NarrationView>();
                    if (view == null) {
                        Debug.LogWarning("[NarrationController] NarrationView를 찾을 수 없습니다. 내레이션이 비활성화됩니다.");
                    }
                }
                if (bootstrap == null) {
                    bootstrap = FindFirstObjectByType<GameBootstrap>();
                    if (bootstrap == null) {
                        Debug.LogWarning("[NarrationController] GameBootstrap을 찾을 수 없습니다. 상태 구독 불가.");
                        return;
                    }
                }
                bootstrap.OnStateChanged += HandleStateChanged;
                // 시작 상태가 이미 Surface면 진입 이벤트가 없으므로(FSM 초기 상태는 OnChanged 미발생) 인트로 직접 재생
                if (bootstrap.State == GameState.Surface && !introPlayed) {
                    introPlayed = true;
                    PlayNarration(IntroSource);
                }
            } catch (Exception e) {
                Debug.LogError($"[NarrationController] Start 초기화 실패: {e.Message}");
            }
        }

        void OnDestroy() {
            try {
                if (bootstrap != null) {
                    bootstrap.OnStateChanged -= HandleStateChanged;
                }
            } catch (Exception e) {
                Debug.LogError($"[NarrationController] OnDestroy 구독 해제 실패: {e.Message}");
            }
        }

        // ── 상태 변화 핸들러 ─────────────────────────────────────────

        void HandleStateChanged(GameState from, GameState to) {
            try {
                // Surface 최초 진입 — 인트로 재생(1회)
                if (to == GameState.Surface && !introPlayed) {
                    introPlayed = true;
                    PlayNarration(IntroSource);
                    return;
                }
                // Clear 진입 — 클리어 재생(1회)
                if (to == GameState.Clear && !clearPlayed) {
                    clearPlayed = true;
                    PlayNarration(ClearSource);
                }
            } catch (Exception e) {
                Debug.LogError($"[NarrationController] 상태 변화 처리 실패 (from={from} to={to}): {e.Message}");
            }
        }

        // NarrationView에 재생 위임 — view가 없으면 조용히 건너뜀
        void PlayNarration(string[] lines) {
            if (view == null) {
                return;
            }
            try {
                view.Play(lines, null);
            } catch (Exception e) {
                Debug.LogError($"[NarrationController] 내레이션 재생 실패: {e.Message}");
            }
        }
    }
}
