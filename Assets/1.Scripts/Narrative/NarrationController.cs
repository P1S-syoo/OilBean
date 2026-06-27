using System;
using UnityEngine;
using Game.Core;
using Game.Surface;

namespace Game.Narrative {
    // 인트로/클리어 내레이션 트리거 — GameBootstrap 상태 변화를 구독해 NarrationView에 재생 요청
    public class NarrationController : MonoBehaviour {

        // ── 직렬화 필드 ─────────────────────────────────────────────
        [SerializeField] NarrationView    view;
        [SerializeField] GameBootstrap    bootstrap;
        [SerializeField] SubNavigator     navigator;
        [SerializeField] 연출설정         config;   // 연출 설정 — 미연결 시 SO 기본값(연출설정.기본)의 대사 사용

        // ── 재생 가드 ────────────────────────────────────────────────
        bool introPlayed;
        bool clearPlayed;

        // ── 대사 출처 ────────────────────────────────────────────────
        // 대사 정본(SSOT)은 연출설정.인트로대사/클리어대사 — 미연결 시 SO 기본값 인스턴스에서 가져옴(하드코딩 중복 제거)
        string[] IntroSource => (config != null ? config : 연출설정.기본).인트로대사;
        string[] ClearSource => (config != null ? config : 연출설정.기본).클리어대사;

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
                if (navigator == null) {
                    navigator = FindFirstObjectByType<SubNavigator>();
                }
                bootstrap.OnStateChanged += HandleStateChanged;
                // 시작 상태가 이미 Surface면 진입 이벤트가 없으므로(FSM 초기 상태는 OnChanged 미발생) 인트로 직접 재생
                if (bootstrap.State == GameState.Surface && !introPlayed) {
                    introPlayed = true;
                    PlayNarration(IntroSource, true);
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
                    PlayNarration(IntroSource, true);
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
        void PlayNarration(string[] lines, bool pauseSurface = false) {
            if (view == null) {
                return;
            }
            bool paused = false;
            try {
                if (pauseSurface && navigator != null) {
                    navigator.PauseCurrent();
                    paused = true;
                }
                view.Play(lines, () => {
                    if (pauseSurface && navigator != null) {
                        navigator.ContinueCurrent();
                    }
                });
            } catch (Exception e) {
                Debug.LogError($"[NarrationController] 내레이션 재생 실패: {e.Message}");
                if (paused && navigator != null) {
                    navigator.ContinueCurrent();
                }
            }
        }
    }
}
