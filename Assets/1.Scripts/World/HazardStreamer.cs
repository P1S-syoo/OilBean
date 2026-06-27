using System;
using UnityEngine;
using Game.Core;
using Game.Items;

namespace Game.World {
    // 돌진형 오염원 디렉터 — Dive 중 일정 간격으로 화면 좌/우 밖에서 위험체를 생성해 횡단 공격시킨다
    // 각 위험체는 경고(텔레그래프) 후 반대편으로 직진(Hazard.Setup). 깊이연동 정적 스폰을 대체
    public class HazardStreamer : MonoBehaviour {
        [SerializeField] Transform target;          // 2.5D 잠수정(화면 중심 기준)
        [SerializeField] GameObject hazardPrefab;   // 클론할 오염원 프리팹
        [SerializeField] Camera diveCam;            // 화면 좌우 경계 산정(미연결 시 Camera.main)
        [SerializeField] 위험설정 config;           // 위험 설정 — 연결 시 공격/스폰 수치 덮어씀(미연결 시 SO 기본값)
        [SerializeField] float attackInterval;      // 돌진 공격 간격 — 기본값은 위험설정.공격간격
        [SerializeField] float warnTime;            // 경고(텔레그래프) 시간 — 기본값은 위험설정.경고시간
        [SerializeField] float dashSpeed;           // 돌진 속도 — 기본값은 위험설정.돌진속도
        [SerializeField] float marginX;             // 화면 밖 시작/종료 여유(u) — 기본값은 위험설정.화면밖등장거리
        [SerializeField] float laneTopMargin;       // 공격 라인 상단 여백 — 기본값은 위험설정.공격라인상단여백
        [SerializeField] float laneBottomMargin;    // 공격 라인 하단 여백 — 기본값은 위험설정.공격라인하단여백
        [SerializeField] float warnInset;           // 경고 아이콘 안쪽 여백 — 기본값은 위험설정.경고아이콘안쪽여백

        const uint Xorshift32Seed = 2463534242;   // xorshift32 초기 시드(런타임 난수 — Time/Random 의존 최소)

        GameBootstrap bootstrap;
        bool spawning;
        float timer;
        uint seed = Xorshift32Seed;
        bool warnedTarget;

        void Start() {
            try {
                // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거)
                var cfg = config != null ? config : 위험설정.기본;
                attackInterval = cfg.공격간격;
                warnTime = cfg.경고시간;
                dashSpeed = cfg.돌진속도;
                marginX = cfg.화면밖등장거리;
                laneTopMargin = cfg.공격라인상단여백;
                laneBottomMargin = cfg.공격라인하단여백;
                warnInset = cfg.경고아이콘안쪽여백;
                if (diveCam == null) {
                    diveCam = Camera.main;
                }
                bootstrap = FindFirstObjectByType<GameBootstrap>();
                if (bootstrap != null) {
                    bootstrap.OnStateChanged += OnState;
                    spawning = (bootstrap.State == GameState.Dive);
                } else {
                    Debug.LogWarning("[HazardStreamer] GameBootstrap 미발견 — 상시 공격 동작");
                    spawning = true;
                }
                // target 미배선 시 자가탐색
                if (target == null) {
                    var p = GameObject.FindGameObjectWithTag("Player");
                    if (p != null) {
                        target = p.transform;
                    }
                }
            } catch (Exception e) {
                Debug.LogError($"[HazardStreamer] 초기화 실패: {e.Message}");
            }
        }

        void OnDestroy() {
            if (bootstrap != null) {
                bootstrap.OnStateChanged -= OnState;
            }
        }

        // Dive 진입 시 공격 시작, 이탈 시 정지 + 타이머 리셋
        void OnState(GameState from, GameState to) {
            spawning = (to == GameState.Dive);
            timer = 0f;
        }

        void Update() {
            if (!spawning || hazardPrefab == null) {
                return;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f) {
                timer = attackInterval;
                SpawnAttack();
            }
        }

        // 화면 좌/우 밖에서 1기 생성 → 반대편으로 돌진(경고 위치는 화면 안쪽 진입 가장자리)
        void SpawnAttack() {
            try {
                if (diveCam == null) {
                    diveCam = Camera.main;
                    if (diveCam == null) {
                        if (!warnedTarget) {
                            Debug.LogWarning("[HazardStreamer] 카메라 미발견 — 공격 보류");
                            warnedTarget = true;
                        }
                        return;
                    }
                }
                Bounds view = CameraWorldBounds();
                float left = view.min.x;
                float right = view.max.x;
                float bottom = view.min.y;
                float top = view.max.y;

                bool leftToRight = (NextRand() & 1u) == 0u;
                float startX = leftToRight ? left - marginX : right + marginX;
                float endX = leftToRight ? right + marginX : left - marginX;
                float warnX = leftToRight ? left + warnInset : right - warnInset;
                float laneY = Mathf.Lerp(bottom + laneBottomMargin, top - laneTopMargin, NextRand01());

                var go = Instantiate(hazardPrefab, new Vector3(startX, laneY, 0f), Quaternion.identity, transform);
                go.name = "HazardDash";
                go.GetComponent<Hazard>()?.Setup(laneY, startX, endX, warnX, warnTime, dashSpeed);
            } catch (Exception e) {
                Debug.LogError($"[HazardStreamer] SpawnAttack 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // 카메라 화면의 z=0 평면 월드 경계 — 경고 아이콘을 항상 화면 안쪽에 배치
        Bounds CameraWorldBounds() {
            float depth = Mathf.Abs(diveCam.transform.position.z);
            var lb = diveCam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            var rt = diveCam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            var min = new Vector3(Mathf.Min(lb.x, rt.x), Mathf.Min(lb.y, rt.y), 0f);
            var max = new Vector3(Mathf.Max(lb.x, rt.x), Mathf.Max(lb.y, rt.y), 0f);
            var b = new Bounds((min + max) * 0.5f, max - min);
            return b;
        }

        // xorshift32 — 시드 진행식 난수(좌우 방향·진입 높이 분산)
        uint NextRand() {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed;
        }

        // 0~1 정규화 난수
        float NextRand01() {
            return (NextRand() & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
