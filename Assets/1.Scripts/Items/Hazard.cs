using UnityEngine;

namespace Game.Items {
    // 오염 덩어리 — 정해진 waypoint 경로를 순환 이동(프로토타입: 단순 경로, AI 추후)
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour {
        [SerializeField] Vector2[] points;     // 로컬 기준 경유점
        [SerializeField] float speed = 2f;

        Vector3 origin;       // 시작 위치(로컬 경유점의 기준)
        int idx;

        void Awake() {
            GetComponent<Collider2D>().isTrigger = true;
            origin = transform.position;
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
