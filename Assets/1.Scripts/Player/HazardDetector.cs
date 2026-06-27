using System;
using UnityEngine;
using Game.Items;
using Game.Core;

namespace Game.Player {
    // 탐사 기계의 오염원 충돌 감지 — Hazard와 닿으면 OnHit(코디네이터가 강제 복귀 구동)
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class HazardDetector : MonoBehaviour {
        public event Action OnHit;

        // 발화 가드 — 탐사 중에만 true(코디네이터가 상태에 맞춰 토글)
        bool armed = true;

        // 일시 면제 — 상호작용(수집 미니게임)·스턴 등 회피 불가 상황에서 피격 면제(armed와 독립)
        bool immune = false;
        [SerializeField] 위험설정 config;       // 위험 설정 — 피격 후 무적 시간 적용
        [SerializeField] float hitInvulnTime;   // 피격 후 무적 시간(초)
        float invulnUntil;

        void Awake() {
            var cfg = config != null ? config : 위험설정.기본;
            hitInvulnTime = cfg.피격후무적시간;
        }

        // 발화 허용/차단(Dive 진입 시 true, Dock 복귀 시 false)
        public void SetArmed(bool on) {
            armed = on;
            if (!on) {
                invulnUntil = 0f;
            }
        }

        // 일시 면제 토글 — 미니게임·스턴처럼 이동이 잠겨 회피 불가한 동안 켠다(켜는 쪽이 끄는 책임)
        public void SetImmune(bool on) {
            immune = on;
        }

        void OnTriggerEnter2D(Collider2D other) {
            // armed가 아니거나 면제 중이면 무시 — Dock 상태·상호작용/스턴 중 피격 방지
            if (!armed || immune || Time.time < invulnUntil) {
                return;
            }
            if (other.GetComponentInParent<Hazard>() != null) {
                invulnUntil = Time.time + Mathf.Max(0f, hitInvulnTime);
                OnHit?.Invoke();
            }
        }
    }
}
