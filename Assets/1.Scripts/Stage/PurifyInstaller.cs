using System;
using UnityEngine;
using Game.Core;
using Game.Player;

namespace Game.Stage {
    // 정화 부유체 설치 스팟 — 스팟 안에 탐사 기계가 있고 부유체가 준비되면 설치(정화) 진행
    [RequireComponent(typeof(Collider2D))]
    public class PurifyInstaller : MonoBehaviour {
        [SerializeField] RunData run;
        [SerializeField] float installTime = 2f;   // 설치 소요(초)

        bool inside;       // 스팟 안에 탐사 기계가 있나
        bool armed = true; // 탐사 중에만 설치 허용(코디네이터가 토글). 단독 사용 시 기본 on
        bool installing;
        bool done;
        float t;

        // 정화 완료 — 코디네이터가 클리어 전환에 사용
        public event Action OnPurified;

        void Awake() {
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
                return;
            }
            // 스팟 안 + 부유체 준비 시 설치 시작(enter-only가 아니라 상태 기반 게이트)
            if (!installing && inside && run.BuoyReady) {
                installing = true;
            }
            if (!installing) {
                return;
            }
            t += Time.deltaTime;
            run.SetPurify(Mathf.Clamp01(t / installTime));   // 설치 진행 게이지
            if (t >= installTime) {
                done = true;
                installing = false;
                run.SetBuoyReady(false);   // 부유체 소비
                OnPurified?.Invoke();
                Debug.Log("[PurifyInstaller] 정화 완료");
            }
        }

        // 세션 재시작용 상태 리셋(done 래치 해제 — ResetRun과 함께 호출)
        public void ResetState() {
            done = false;
            installing = false;
            inside = false;
            t = 0f;
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
            if (done || !installing) {
                return;
            }
            installing = false;
            t = 0f;
            if (run != null) {
                run.SetPurify(0f);   // HUD 게이지 잔류 제거
            }
            Debug.Log("[PurifyInstaller] 설치 중단 — 진행 리셋");
        }
    }
}
