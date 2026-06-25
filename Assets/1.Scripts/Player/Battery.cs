using System;
using UnityEngine;

namespace Game.Player {
    // 탐사 제한 게이지 — 시간에 따라 소모, 0이 되면 복귀 유도(OnEmpty)
    public class Battery : MonoBehaviour {
        [SerializeField] float max;            // 기본값은 잠수설정.배터리최대
        [SerializeField] float drainPerSec;    // 초당 소모 — 기본값은 잠수설정.배터리소모
        [SerializeField] bool draining = false;   // 코디네이터가 탐사 진입 시 SetDraining(true)로 켬 — 미배선 시 무단 소모 방지
        [SerializeField] Game.Core.잠수설정 config;   // 잠수 설정 — 연결 시 용량/소모 적용(미연결 시 SO 기본값)

        float current;
        bool emptied;

        public float Max => max;
        public float Current => current;
        public float Ratio => max > 0f ? current / max : 0f;

        // 배터리 고갈 — 강제 복귀 등이 구독
        public event Action OnEmpty;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거)
                var cfg = config != null ? config : Game.Core.잠수설정.기본;
                max = cfg.배터리최대;
                drainPerSec = cfg.배터리소모;
                // 최초 1회만 충전(재활성화 시 무단 리필 방지 — 탐사 재시작은 Refill로 명시 호출)
                current = max;
                emptied = false;
            } catch (Exception e) {
                Debug.LogError($"[Battery] 설정 적용 실패: {e.Message}");
            }
        }

        void Update() {
            if (!draining || emptied) {
                return;
            }
            current = Mathf.Max(0f, current - drainPerSec * Time.deltaTime);
            if (current <= 0f) {
                emptied = true;
                OnEmpty?.Invoke();
            }
        }

        // 즉시 소모 — 오염원 충돌 등 페널티(0이 되면 방전 이벤트 1회)
        public void Drain(float amount) {
            current = Mathf.Max(0f, current - Mathf.Max(0f, amount));
            if (current <= 0f && !emptied) {
                emptied = true;
                OnEmpty?.Invoke();
            }
        }

        // 재충전(거점 복귀 시)
        public void Refill() {
            current = max;
            emptied = false;
        }

        // 용량 업그레이드(배터리 셀 제작) — 최대치↑, 늘린 만큼 현재치도 채움
        public void AddCapacity(float d) {
            d = Mathf.Max(0f, d);
            max += d;
            current = Mathf.Min(max, current + d);
        }

        // 소모 on/off(탐사 중에만 소모) — 코디네이터가 상태에 맞춰 토글
        public void SetDraining(bool on) {
            draining = on;
        }
    }
}
