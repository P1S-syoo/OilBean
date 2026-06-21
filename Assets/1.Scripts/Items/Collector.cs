using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Items {
    // 탐사 기계의 수집기 — 근접 Pickup을 트리거로 감지하고 E로 가장 가까운 것을 획득
    // 2D 트리거는 한쪽에 Rigidbody2D가 있어야 발화하므로 요구
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class Collector : MonoBehaviour {
        [SerializeField] RunData run;          // 인벤토리(공유 RunData 자산)
        [SerializeField] float pickupRadius = 1.5f;   // 수집 감지 반경(전용 트리거 센서)
        [SerializeField] GameConfig config;    // 통합 설정 — 연결 시 수집 반경 덮어씀(미연결 시 위 기본값 유지)

        readonly HashSet<Pickup> inRange = new();
        InputAction interact;                  // E 키(코드 정의)

        // 수집 성공/적재 한계 — 사운드·HUD 등이 구독
        public event Action<ResourceKind> OnCollect;
        public event Action OnFull;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결이면 기존 기본값 유지(센서 반경 설정 전에 적용)
                if (config != null) {
                    pickupRadius = config.collectorPickupRadius;
                }
                // 본체 콜라이더는 벽 충돌용(비트리거) 유지 — 수집 감지는 전용 트리거 센서(CircleCollider2D)
                var sensor = GetComponent<CircleCollider2D>();
                if (sensor == null) {
                    sensor = gameObject.AddComponent<CircleCollider2D>();
                }
                sensor.isTrigger = true;
                sensor.radius = pickupRadius;
                interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            } catch (Exception e) {
                Debug.LogError($"[Collector] Awake 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        void OnEnable() {
            if (interact == null) {
                return;
            }
            interact.performed += OnInteract;
            interact.Enable();
        }

        void OnDisable() {
            if (interact == null) {
                return;
            }
            interact.performed -= OnInteract;
            interact.Disable();
        }

        void OnDestroy() {
            interact?.Dispose();
        }

        void OnTriggerEnter2D(Collider2D other) {
            var p = other.GetComponent<Pickup>();
            if (p != null) {
                inRange.Add(p);
            }
        }

        void OnTriggerExit2D(Collider2D other) {
            var p = other.GetComponent<Pickup>();
            if (p != null) {
                inRange.Remove(p);
            }
        }

        void OnInteract(InputAction.CallbackContext ctx) {
            TryCollectNearest();
        }

        // 가장 가까운 근접 Pickup 1개 획득 시도(테스트도 직접 호출)
        public bool TryCollectNearest() {
            if (run == null) {
                return false;
            }
            var target = Nearest();
            if (target == null) {
                return false;
            }
            if (!run.TryAdd(target.Kind, target.Weight)) {
                OnFull?.Invoke();   // 적재 한계 — 복귀 유도
                return false;
            }
            // 거점 보유 소재 입고 — 고철=강재 등급별 kg, 샘플=오염수준별 개수(데이터 주도)
            var def = target.Def;
            if (def != null) {
                if (def.kind == ResourceKind.Scrap) {
                    run.AddSteel(def.grade, def.weight);
                } else {
                    run.AddSampleAt(def.pollutionLevel);
                }
            }
            inRange.Remove(target);
            OnCollect?.Invoke(target.Kind);
            Destroy(target.gameObject);
            return true;
        }

        // 파괴된 항목은 건너뛰고 최근접 선택
        Pickup Nearest() {
            Pickup best = null;
            float bestSqr = float.MaxValue;
            foreach (var p in inRange) {
                if (p == null) {
                    continue;
                }
                float d = ((Vector2)(p.transform.position - transform.position)).sqrMagnitude;
                if (d < bestSqr) {
                    bestSqr = d;
                    best = p;
                }
            }
            // 순회 후 파괴된 죽은 엔트리 일괄 제거
            inRange.RemoveWhere(x => x == null);
            return best;
        }
    }
}
