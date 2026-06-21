using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Player;

namespace Game.Stage {
    // 정화 부유체 설치 스팟 — 스팟 안 + 부유체 준비 + F 홀드로 설치(정화) 진행 (E6 hold-to-install)
    [RequireComponent(typeof(Collider2D))]
    public class PurifyInstaller : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] float installTime = 2.5f;   // 설치 소요(초, 홀드 누적)
        [SerializeField] Transform progressFill;      // 월드 진행 게이지(스케일 X = 진행), 선택
        [SerializeField] GameObject holdPrompt;        // 'F 홀드' 안내 오브젝트, 선택
        [SerializeField] GameConfig config;            // 통합 설정 — 연결 시 설치 시간 덮어씀(미연결 시 위 기본값 유지)

        bool inside;       // 스팟 안에 탐사 기계가 있나
        bool armed = true; // 탐사 중에만 설치 허용(코디네이터가 토글). 단독 사용 시 기본 on
        bool done;
        float t;           // 설치 누적 시간(홀드 중단 시 보존 — 부분진행 유지)
        bool holdOverride; // 키보드 없는 환경(PlayMode 테스트)에서 홀드를 대체

        // 정화 완료 — 코디네이터가 클리어 전환에 사용
        public event Action OnPurified;

        void Awake() {
            // 통합 설정 적용 — 미연결이면 기존 기본값 유지
            if (config != null) {
                installTime = config.purifyInstallTime;
            }
            GetComponent<Collider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other) {
            // 탐사 기계 진입 표시(부유체 게이트는 Update에서 — 진입 후 제작해도 인식)
            if (other.GetComponentInParent<PlayerMove>() != null) {
                inside = true;
            }
        }

        void OnTriggerExit2D(Collider2D other) {
            if (other.GetComponentInParent<PlayerMove>() != null) {
                inside = false;
            }
        }

        void Update() {
            if (done || run == null || !armed) {
                ShowPrompt(false);
                return;
            }
            bool ready = inside && run.BuoyReady;
            // F 홀드 중에만 진행(능동감) — 수집(E)과 키 충돌 회피. 중단해도 t 보존(부분진행 유지)
            bool keyHeld = Keyboard.current != null && Keyboard.current.fKey.isPressed;
            bool holding = ready && (keyHeld || holdOverride);
            ShowPrompt(ready && !holding);   // 준비됐는데 아직 안 누르면 'F 홀드' 안내
            if (!holding) {
                return;
            }
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / installTime);
            run.SetPurify(p);   // HUD 게이지
            UpdateFill(p);      // 월드 게이지
            if (t >= installTime) {
                done = true;
                run.SetBuoyReady(false);   // 부유체 소비
                ShowPrompt(false);
                OnPurified?.Invoke();
                Debug.Log("[PurifyInstaller] 정화 완료");
            }
        }

        // 월드 진행 게이지 스케일 갱신(있을 때만)
        void UpdateFill(float p) {
            if (progressFill != null) {
                var s = progressFill.localScale;
                progressFill.localScale = new Vector3(Mathf.Clamp01(p), s.y, s.z);
            }
        }

        // 'F 홀드' 안내 표시 토글(있을 때만)
        void ShowPrompt(bool show) {
            if (holdPrompt != null && holdPrompt.activeSelf != show) {
                holdPrompt.SetActive(show);
            }
        }

        // 구역 정화 후 재무장 — done 래치만 해제(다음 단계 부유체 재설치 허용). 진행 게이지 리셋
        public void ReArm() {
            done = false;
            inside = false;
            t = 0f;
            UpdateFill(0f);
            ShowPrompt(false);
        }

        // 세션 재시작용 상태 리셋(done 래치 해제 — ResetRun과 함께 호출)
        public void ResetState() {
            done = false;
            inside = false;
            t = 0f;
            UpdateFill(0f);
            ShowPrompt(false);
            if (run != null) {
                run.SetPurify(0f);
            }
        }

        // 탐사 상태 토글 — 비탐사(Dock 등)면 진행 중 설치 취소 + 신규 설치 차단
        public void SetArmed(bool v) {
            armed = v;
            if (!v) {
                inside = false;   // 텔레포트(복귀)로 OnTriggerExit이 누락돼도 잔류 제거 — 재잠수 시 자동정화 방지
                CancelInstall();
            }
        }

        // 강제 복귀 등으로 설치 중단 — 게이지/타이머 리셋(부유체는 유지해 재시도 가능)
        public void CancelInstall() {
            if (done || t <= 0f) {
                return;
            }
            t = 0f;
            UpdateFill(0f);
            ShowPrompt(false);
            if (run != null) {
                run.SetPurify(0f);   // HUD 게이지 잔류 제거
            }
            Debug.Log("[PurifyInstaller] 설치 중단 — 진행 리셋");
        }
    }
}
