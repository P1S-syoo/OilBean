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
        [SerializeField] float pickupRadius;   // 수집 감지 반경 — 기본값은 수집설정.수집반경
        [SerializeField] 수집설정 config;    // 수집 설정 — 연결 시 수집 반경 덮어씀(미연결 시 위 기본값 유지)
        [SerializeField] Game.UI.CollectMinigame minigame;   // 수집 미니게임(미연결 시 즉시 수집 폴백)
        [SerializeField] GameBootstrap game;   // Dive 상태에서만 수집 허용

        Pickup pendingTarget;   // 미니게임 진행 중인 수집 대상

        readonly HashSet<Pickup> inRange = new();
        InputAction interact;                  // E 키(코드 정의)

        // 수집 성공/적재 한계 — 사운드·HUD 등이 구독
        public event Action<ResourceKind> OnCollect;
        public event Action<Pickup> OnCollectDetail;   // 구체 아이템 안내용(기존 OnCollect 호환 유지)
        public event Action OnFull;
        public event Action<bool> OnInteractableChanged;   // 범위 내 대상 유무(E 프롬프트용)

        Pickup highlightTarget;   // 현재 강조 중인 최근접 대상
        Game.Player.PlayerMove mover;   // 미니게임 중 이동 잠금용
        Game.Player.HazardDetector hazard;   // 미니게임(이동 잠금) 동안 피격 면제용
        bool moveLocked;   // 수집 미니게임이 이동을 잠갔는지
        bool collectionStateCleared;   // 수면 상태 정리 반복 호출 방지

        public bool HasInteractable => highlightTarget != null;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거, 센서 반경 설정 전에 적용)
                var cfg = config != null ? config : 수집설정.기본;
                pickupRadius = cfg.수집반경;
                // 본체 콜라이더는 벽 충돌용(비트리거) 유지 — 수집 감지는 전용 트리거 센서(CircleCollider2D)
                var sensor = GetComponent<CircleCollider2D>();
                if (sensor == null) {
                    sensor = gameObject.AddComponent<CircleCollider2D>();
                }
                sensor.isTrigger = true;
                sensor.radius = pickupRadius;
                interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
                mover = GetComponent<Game.Player.PlayerMove>();   // 미니게임 중 정지용(같은 잠수정)
                hazard = GetComponentInParent<Game.Player.HazardDetector>();   // 같은 잠수정의 오염원 감지기(면제 토글)
                game = game != null ? game : FindFirstObjectByType<GameBootstrap>();
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
            if (!CanCollectNow()) {
                return;
            }
            var p = other.GetComponent<Pickup>();
            if (p != null) {
                inRange.Add(p);
            }
        }

        void OnTriggerExit2D(Collider2D other) {
            var p = other.GetComponent<Pickup>();
            if (p != null) {
                inRange.Remove(p);
                // 미니게임 대상이 범위 이탈 — 취소 + 이동 잠금 해제
                if (minigame != null && minigame.Active && p == pendingTarget) {
                    minigame.Cancel();
                    pendingTarget = null;
                    SetMoveLock(false);
                }
            }
        }

        // 최근접 대상 강조 갱신 — 진입 효과(하이라이트) + E 프롬프트 이벤트
        void Update() {
            if (!CanCollectNow()) {
                ClearCollectionState();
                return;
            }
            RestoreCollectionState();
            var n = Nearest();
            if (n == highlightTarget) {
                return;
            }
            if (highlightTarget != null) {
                highlightTarget.SetHighlighted(false);
            }
            highlightTarget = n;
            if (highlightTarget != null) {
                highlightTarget.SetHighlighted(true);
            }
            OnInteractableChanged?.Invoke(highlightTarget != null);
        }

        void OnInteract(InputAction.CallbackContext ctx) {
            if (!CanCollectNow()) {
                ClearCollectionState();
                return;
            }
            // 미니게임 미배선 — 즉시 수집(폴백, 테스트 호환)
            if (minigame == null) {
                TryCollectNearest();
                return;
            }
            // 미니게임 진행 중 — E는 멈춤 판정
            if (minigame.Active) {
                minigame.Judge();
                return;
            }
            // 근접 대상에 대해 미니게임 시작(오염 농도 등급별 난이도)
            var target = Nearest();
            if (target == null) {
                return;
            }
            pendingTarget = target;
            SetMoveLock(true);   // 미니게임 동안 잠수정 정지
            minigame.StartGame(target.PollutionLevel, OnMinigameResult);
        }

        // 미니게임 결과 — 명중 시에만 실제 수집
        void OnMinigameResult(bool hit) {
            SetMoveLock(false);   // 이동 재개
            var t = pendingTarget;
            pendingTarget = null;
            if (hit && t != null) {
                CollectTarget(t);
            }
        }

        // 미니게임 중 잠수정 이동 잠금/해제 — 잠금 동안엔 회피 불가라 오염원 피격도 면제
        void SetMoveLock(bool locked) {
            moveLocked = locked;
            ApplyMoveState();
            if (hazard != null) {
                hazard.SetImmune(locked);
            }
        }

        // 가장 가까운 근접 Pickup 1개 획득 시도(테스트도 직접 호출)
        public bool TryCollectNearest() {
            if (!CanCollectNow()) {
                return false;
            }
            return CollectTarget(Nearest());
        }

        // 지정 대상 수집 — 적재/입고/이벤트/파괴(미니게임 성공·즉시 수집 공용)
        bool CollectTarget(Pickup target) {
            if (!CanCollectNow()) {
                return false;
            }
            if (run == null || target == null) {
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
            OnCollectDetail?.Invoke(target);
            OnCollect?.Invoke(target.Kind);
            Destroy(target.gameObject);
            return true;
        }

        // 수집은 2D 잠수 상태에서만 허용한다
        bool CanCollectNow() {
            if (game == null) {
                game = FindFirstObjectByType<GameBootstrap>();
            }
            return game == null || game.State == GameState.Dive;
        }

        // 수면 복귀 등으로 수집 불가가 되면 진행·강조를 모두 정리한다
        void ClearCollectionState() {
            if (collectionStateCleared) {
                return;
            }
            collectionStateCleared = true;
            if (minigame != null && minigame.Active) {
                minigame.Cancel();
            }
            SetMoveLock(false);
            pendingTarget = null;
            inRange.Clear();
            if (highlightTarget != null) {
                highlightTarget.SetHighlighted(false);
                highlightTarget = null;
                OnInteractableChanged?.Invoke(false);
            }
        }

        // Dive 재진입 시 Surface에서 꺼둔 2D 이동을 복구한다
        void RestoreCollectionState() {
            if (!collectionStateCleared) {
                return;
            }
            collectionStateCleared = false;
            ApplyMoveState();
        }

        // 현재 게임 상태와 미니게임 잠금 상태를 합쳐 이동 가능 여부를 정한다
        void ApplyMoveState() {
            if (mover != null) {
                mover.enabled = CanCollectNow() && !moveLocked;
            }
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
