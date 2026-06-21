using System;
using UnityEngine;

namespace Game.Items {
    // 오염 덩어리 — 정해진 waypoint 경로를 순환 이동(프로토타입: 단순 경로, AI 추후)
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour {
        [SerializeField] Vector2[] points;     // 로컬 기준 경유점
        [SerializeField] float speed = 2f;
        [SerializeField] Game.Core.GameConfig config;   // 통합 설정 — 연결 시 이동 속도 덮어씀(미연결 시 위 기본값 유지)

        Vector3 origin;       // 시작 위치(로컬 경유점의 기준)
        int idx;

        void Awake() {
            try {
                // 통합 설정 적용 — 인스펙터 연결 시(미연결이면 기존 기본값 유지)
                if (config != null) {
                    speed = config.hazardSpeed;
                }
                GetComponent<Collider2D>().isTrigger = true;
                origin = transform.position;
            } catch (Exception e) {
                Debug.LogError($"[Hazard] config 적용 실패: {e.Message}");
            }
        }

        // 런타임 스폰 주입 — 스트리머가 Instantiate 직후 호출(미주입 시 기존 기본값 유지)
        public void ApplyConfig(Game.Core.GameConfig config) {
            try {
                if (config != null) {
                    speed = config.hazardSpeed;
                }
            } catch (Exception e) {
                Debug.LogError($"[Hazard] ApplyConfig 실패: {e.Message}");
            }
        }

        void Update() {
            if (points == null || points.Length == 0) {
                return;
            }
            Vector3 goal = origin + (Vector3)points[idx];
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
            if ((transform.position - goal).sqrMagnitude < 0.0004f) {
                idx = (idx + 1) % points.Length;   // 다음 경유점(순환)
            }
        }
    }
}
