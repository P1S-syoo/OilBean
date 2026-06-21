using System;
using UnityEngine;
using UnityEngine.Splines;

namespace Game.Surface {
    // 잠수정 자동 항해 — 강 스플라인을 일정 속도로 추종, 정화 목표 지점에서 감속 정지
    public class SubNavigator : MonoBehaviour {
        [SerializeField] SplineContainer river;       // 강 중심선 스플라인
        [SerializeField] float speed = 4f;            // 순항 속도(m/s)
        [SerializeField] float brakeDistance = 8f;    // 목표 앞 감속 시작 거리(m)
        [SerializeField] float minSpeed = 0.4f;       // 감속 중 최저 속도(완전 멈춤 직전까지 전진)
        [SerializeField] float turnSpeed = 2f;        // 회전 보간 속도(급커브 홱 돌기 방지)
        [SerializeField, Range(0f, 1f)] float[] targets = { 0.5f, 1f };   // 정화 목표(스플라인 정규화 거리)
        [SerializeField] Game.Core.GameConfig config;   // 통합 설정 — 연결 시 항해 수치 덮어씀(미연결 시 위 기본값 유지)

        float distance;     // 스플라인 위 누적 이동 거리(m)
        float length;       // 스플라인 전체 길이 캐시
        int targetIdx;
        bool sailing;

        // 목표 도달(감속 정지 완료) 알림 — SurfaceBootstrap이 구독해 잠수 가능 토글(W5)
        public event Action OnArrived;

        public bool Sailing => sailing;
        public int TargetIndex => targetIdx;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결이면 기존 기본값 유지
                if (config != null) {
                    speed = config.navSpeed;
                    brakeDistance = config.navBrakeDistance;
                    minSpeed = config.navMinSpeed;
                    turnSpeed = config.navTurnSpeed;
                }
                if (river == null) {
                    Debug.LogError("[SubNavigator] 강 스플라인 미연결 — 인스펙터에서 할당하세요.");
                    enabled = false;
                    return;
                }
                length = river.CalculateLength();
                if (length <= 0f) {
                    Debug.LogError("[SubNavigator] 스플라인 길이가 0 — 노트 배치를 확인하세요.");
                    enabled = false;
                    return;
                }
                sailing = true;
                Apply(0f);
            } catch (Exception e) {
                Debug.LogError($"[SubNavigator] 초기화 실패: {e.Message}\n{e.StackTrace}");
                enabled = false;
            }
        }

        void Update() {
            if (!sailing) {
                return;
            }
            float targetDist = TargetDistance();
            float remain = targetDist - distance;
            // 목표 앞 brakeDistance 안에서 선형 감속(최저 속도 보장으로 무한 접근 방지)
            float v = remain < brakeDistance ? Mathf.Max(minSpeed, speed * (remain / brakeDistance)) : speed;
            distance = Mathf.Min(distance + v * Time.deltaTime, targetDist);
            Apply(distance / length);
            if (targetDist - distance <= 0.01f) {
                sailing = false;
                try {
                    OnArrived?.Invoke();
                } catch (Exception e) {
                    // 구독자 예외가 항해 로직을 멈추지 않도록 격리
                    Debug.LogError($"[SubNavigator] OnArrived 처리 오류: {e.Message}");
                }
            }
        }

        // 다음 목표를 향해 재출발(수면 복귀 시, W6)
        public void Resume() {
            if (targetIdx >= targets.Length - 1) {
                Debug.Log("[SubNavigator] 마지막 목표 — 더 갈 곳 없음");
                return;
            }
            targetIdx++;
            sailing = true;
        }

        float TargetDistance() {
            float t = targetIdx < targets.Length ? Mathf.Clamp01(targets[targetIdx]) : 1f;
            return t * length;
        }

        // 스플라인 위치·접선 적용 — 수면 항해라 기수 상하 흔들림 없이 수평 방향만 회전
        void Apply(float t) {
            transform.position = (Vector3)river.EvaluatePosition(t);
            Vector3 dir = ((Vector3)(Unity.Mathematics.float3)river.EvaluateTangent(t));
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) {
                var look = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
            }
        }
    }
}
