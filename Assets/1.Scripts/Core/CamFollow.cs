using System;
using UnityEngine;

namespace Game.Core {
    // 대상을 부드럽게 추적하는 2D 카메라 — SmoothDamp(프레임률 독립·진동 없음). 물리 대상은 Rigidbody2D Interpolate 전제
    public class CamFollow : MonoBehaviour {
        [SerializeField] Transform target;        // 따라갈 대상(탐사 기계)
        [SerializeField] float smoothTime = 0.18f; // 추적 지연(작을수록 빠름)
        [SerializeField] float zOffset = -20f;     // 카메라 깊이(프레이밍과 일치)
        [SerializeField] GameConfig config;        // 통합 설정 — 연결 시 추적 지연 덮어씀(미연결 시 위 기본값 유지)

        Vector3 vel;   // SmoothDamp 내부 속도(상태 유지)

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결이면 기존 기본값 유지
                if (config != null) {
                    smoothTime = config.camSmoothTime;
                }
            } catch (Exception e) {
                Debug.LogError($"[CamFollow] config 적용 실패: {e.Message}");
            }
        }

        public void SetTarget(Transform t) {
            target = t;
        }

        void LateUpdate() {
            if (target == null) {
                return;
            }
            var goal = new Vector3(target.position.x, target.position.y, zOffset);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref vel, smoothTime);
        }
    }
}
