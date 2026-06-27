using System;
using UnityEngine;
using UnityEngine.Splines;

namespace Game.Surface {
    // 잠수정 자동 항해 — 강 스플라인을 일정 속도로 추종, 정화 목표 지점에서 감속 정지
    public class SubNavigator : MonoBehaviour {
        [SerializeField] SplineContainer river;       // 강 중심선 스플라인
        [SerializeField] float speed;            // 순항 속도(m/s) — 기본값은 수면위설정.항해속도
        [SerializeField] float brakeDistance;    // 목표 앞 감속 시작 거리(m) — 기본값은 수면위설정.항해감속거리
        [SerializeField] float minSpeed;         // 감속 중 최저 속도 — 기본값은 수면위설정.항해최저속도
        [SerializeField] float turnSpeed;        // 회전 보간 속도 — 기본값은 수면위설정.항해회전속도
        [SerializeField, Range(0f, 1f)] float[] targets = { 0.5f, 1f };   // 정화 목표(스플라인 정규화 거리)
        [SerializeField] string[] routeNames = { "세빛섬", "동작대교" }; // 수면 항해 랜드마크(프로토타입 2구역)
        [SerializeField] Game.Core.수면위설정 config;   // 수면위 설정 — 연결 시 항해 수치 적용(미연결 시 SO 기본값)

        float distance;     // 스플라인 위 누적 이동 거리(m)
        float length;       // 스플라인 전체 길이 캐시
        int targetIdx;
        bool sailing;

        // 목표 도달(감속 정지 완료) 알림 — SurfaceBootstrap이 구독해 잠수 가능 토글(W5)
        public event Action OnArrived;

        public bool Sailing => sailing;
        public int TargetIndex => targetIdx;
        public SplineContainer River => river;
        public float CurrentTargetT => TargetT(targetIdx);
        public float PreviousTargetT => targetIdx <= 0 ? 0f : TargetT(targetIdx - 1);
        public float RouteProgress01 => RouteProgress();
        public string CurrentLandmarkName => LandmarkName(targetIdx);
        public string PreviousLandmarkName => targetIdx <= 0 ? "출발" : LandmarkName(targetIdx - 1);

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거)
                var cfg = config != null ? config : Game.Core.수면위설정.기본;
                speed = cfg.항해속도;
                brakeDistance = cfg.항해감속거리;
                minSpeed = cfg.항해최저속도;
                turnSpeed = cfg.항해회전속도;
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

        // 현재 목표를 유지한 채 항해만 일시정지
        public void PauseCurrent() {
            sailing = false;
        }

        // 현재 목표를 유지한 채 항해 재개
        public void ContinueCurrent() {
            if (TargetDistance() - distance <= 0.01f) {
                return;
            }
            sailing = true;
        }

        float TargetDistance() {
            return TargetT(targetIdx) * length;
        }

        float RouteProgress() {
            float start = targetIdx <= 0 ? 0f : TargetT(targetIdx - 1) * length;
            float end = TargetDistance();
            return Mathf.InverseLerp(start, end, distance);
        }

        // 목표 배열 접근을 한 곳에 모아 정화 구간 연출도 같은 기준을 사용
        float TargetT(int index) {
            return index < targets.Length ? Mathf.Clamp01(targets[index]) : 1f;
        }

        string LandmarkName(int index) {
            if (routeNames == null || routeNames.Length == 0) {
                return $"정화구역 {index + 1}";
            }
            return routeNames[Mathf.Clamp(index, 0, routeNames.Length - 1)];
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
