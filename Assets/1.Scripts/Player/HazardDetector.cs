using System;
using UnityEngine;
using Game.Items;

namespace Game.Player {
    // 탐사 기계의 오염원 충돌 감지 — Hazard와 닿으면 OnHit(코디네이터가 강제 복귀 구동)
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class HazardDetector : MonoBehaviour {
        public event Action OnHit;

        // 발화 가드 — 탐사 중에만 true(코디네이터가 상태에 맞춰 토글)
        bool armed = true;

        // 발화 허용/차단(Dive 진입 시 true, Dock 복귀 시 false)
        public void SetArmed(bool on) {
            armed = on;
        }

        void OnTriggerEnter2D(Collider2D other) {
            // armed가 아니면 무시 — Dock 상태·다중 발화 방지
            if (!armed) {
                return;
            }
            if (other.GetComponentInParent<Hazard>() != null) {
                OnHit?.Invoke();
            }
        }
    }
}
