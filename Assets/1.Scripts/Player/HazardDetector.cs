using System;
using UnityEngine;
using Game.Items;

namespace Game.Player {
    // 탐사 기계의 오염원 충돌 감지 — Hazard와 닿으면 OnHit(코디네이터가 강제 복귀 구동)
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class HazardDetector : MonoBehaviour {
        public event Action OnHit;

        void OnTriggerEnter2D(Collider2D other) {
            if (other.GetComponent<Hazard>() != null) {
                OnHit?.Invoke();
            }
        }
    }
}
