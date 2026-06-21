using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Surface {
    // 덱 위 3인칭 캐릭터 — 카메라 기준 WASD 로컬 이동, 덱 경계 클램프(물에 못 떨어짐)
    public class DeckCharacter : MonoBehaviour {
        [SerializeField] Transform cam;                          // 이동 방향 기준 카메라(미연결 시 Camera.main)
        [SerializeField] float moveSpeed = 1.6f;                 // 보행 속도(사람보다 살짝 빠름) — Walk 클립과 큰 어긋남 없음
        [SerializeField] float turnSpeed = 12f;                  // 이동 방향 회전 보간 속도
        [SerializeField] Vector2 deckHalf = new(1.8f, 4.6f);    // 덱 절반 크기(로컬 x, z) — 1차 클램프(거친 한계)
        [SerializeField] Animator animator;                      // 선택 — 이동 시 Speed 파라미터로 모션 전환
        [SerializeField] Collider[] deckColliders;               // 선체 메시 콜라이더 — 실제 표면 위로만 보행 허용
        [SerializeField] float walkableNormalY = 0.45f;          // 보행 가능 경사 한계(표면 법선 y≈63°) — 완만~중경사 허용, 수직 벽은 차단
        [SerializeField] float maxStepUp = 1.1f;                 // 오를 수 있는 단차/경사 한계(m) — 웬만한 턱·계단은 넘되 함교 벽은 차단
        [SerializeField] float maxStepDown = 0.7f;               // 내려갈 수 있는 단차 한계(m) — 절벽(물)으로 못 떨어짐
        [SerializeField] float footSink = 0.12f;                 // 발을 표면에 묻는 깊이(m) — 확실한 접지감
        [SerializeField] float snapLerp = 16f;                   // 발 높이 추종 속도(m/s) — 경사·턱을 덜컹임 없이 부드럽게
        [SerializeField] Game.Core.GameConfig config;            // 통합 설정 — 연결 시 보행 수치 덮어씀(미연결 시 위 기본값 유지)

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int OnDeckHash = Animator.StringToHash("OnDeck");
        static readonly int DiveHash = Animator.StringToHash("Dive");

        InputAction move;

        // 입력만 잠금(컷신용) — 접지 스냅은 계속 동작. 완전 정지는 enabled=false(잠수 연출의 수동 이동과 충돌 방지)
        public bool ControlLocked { get; set; }

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결이면 기존 기본값 유지
                if (config != null) {
                    moveSpeed = config.deckMoveSpeed;
                    turnSpeed = config.deckTurnSpeed;
                }
                if (cam == null && Camera.main != null) {
                    cam = Camera.main.transform;
                }
                move = new InputAction("DeckMove", InputActionType.Value);
                move.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
            } catch (Exception e) {
                Debug.LogError($"[DeckCharacter] 초기화 실패: {e.Message}");
            }
        }

        void OnEnable() {
            move?.Enable();
            if (animator != null) {
                animator.SetBool(OnDeckHash, true);   // 덱 위 — 이동 시 Swim 대신 Walk
            }
        }

        // 잠수 입수 — RunToDive 모션 1회 재생(연출 코디네이터가 호출)
        public void TriggerDive() {
            if (animator != null) {
                animator.SetTrigger(DiveHash);
            }
        }

        void OnDisable() {
            move?.Disable();
        }

        void OnDestroy() {
            move?.Dispose();
        }

        void Update() {
            if (move == null) {
                return;
            }
            Step(ControlLocked ? Vector2.zero : move.ReadValue<Vector2>(), Time.deltaTime);
        }

        // 이동 1스텝 — 테스트에서 입력을 직접 주입할 수 있게 분리
        public void Step(Vector2 input, float dt) {
            float mag = Mathf.Clamp01(input.magnitude);
            if (animator != null) {
                animator.SetFloat(SpeedHash, mag);   // Idle↔Walk/Swim 전환(임계 0.1)
            }
            if (transform.parent == null) {
                return;
            }
            if (mag * mag < 0.0001f) {
                // 정지 중에도 부유하는 선체 표면에 발을 붙임(부드럽게 추종)
                if (TrySnapToDeck(transform.localPosition, out var idleSnap)) {
                    idleSnap.y = Mathf.MoveTowards(transform.localPosition.y, idleSnap.y, snapLerp * dt);
                    transform.localPosition = idleSnap;
                }
                return;
            }
            // 카메라 기준 수평 방향(카메라 없으면 잠수정 전방 기준)
            Vector3 f = cam != null ? cam.forward : transform.parent.forward;
            Vector3 r = cam != null ? cam.right : transform.parent.right;
            f.y = 0f;
            r.y = 0f;
            Vector3 worldDir = f.normalized * input.y + r.normalized * input.x;
            if (worldDir.sqrMagnitude < 0.0001f) {
                return;
            }
            worldDir.Normalize();
            // 부모(잠수정) 로컬 좌표로 이동 — 움직이는 덱 위에서도 미끄러짐 없음
            Vector3 localDir = transform.parent.InverseTransformDirection(worldDir);
            localDir.y = 0f;
            Vector3 p = transform.localPosition + localDir * (moveSpeed * mag * dt);
            // 1차 클램프(거친 한계) 후 선체 표면 검사 — 표면 밖/가파른 측면이면 이동 취소
            p.x = Mathf.Clamp(p.x, -deckHalf.x, deckHalf.x);
            p.z = Mathf.Clamp(p.z, -deckHalf.y, deckHalf.y);
            if (TrySnapToDeck(p, out var snapped)) {
                snapped.y = Mathf.MoveTowards(transform.localPosition.y, snapped.y, snapLerp * dt);
                transform.localPosition = snapped;
            }
            // 이동 방향으로 몸 회전
            if (localDir.sqrMagnitude > 0.0001f) {
                var look = Quaternion.LookRotation(transform.parent.TransformDirection(localDir.normalized));
                transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * dt);
            }
        }

        // 발 밑 선체 표면 프로브 — 중심 기둥의 표면 높이로 스냅. 단차는 한 번에 잡고 snapLerp가 부드럽게 보간
        bool TrySnapToDeck(Vector3 localPos, out Vector3 snapped) {
            snapped = localPos;
            if (deckColliders == null || deckColliders.Length == 0) {
                return true;   // 콜라이더 미배선(그레이박스 폴백) — 기존 클램프만으로 동작
            }
            float footY = transform.localPosition.y;
            // 발 밑이 보행 가능면(완만)이어야 — 가장자리 밖/수직 벽이면 이동 취소(물로 못 나감)
            if (!ProbeColumn(localPos, out float surfaceY, out float surfaceN) || surfaceN < walkableNormalY) {
                return false;
            }
            float targetY = surfaceY - footSink;
            if (targetY - footY > maxStepUp) {
                return false;   // 너무 높은 벽(함교 지붕 등) — 못 올라감
            }
            if (footY - targetY > maxStepDown) {
                return false;   // 절벽(물) — 못 떨어짐
            }
            snapped = new Vector3(localPos.x, targetY, localPos.z);
            return true;
        }

        // 한 지점 기둥 프로브 — 현재 발 높이 기준 stepUp 위에서 stepDown 아래까지 하향 레이캐스트(경사·단차 모두 포착)
        bool ProbeColumn(Vector3 localPos, out float localY, out float normalY) {
            localY = 0f;
            normalY = 0f;
            Vector3 world = transform.parent.TransformPoint(localPos);
            Vector3 origin = world + Vector3.up * (maxStepUp + 1f);
            float len = maxStepUp + 1f + maxStepDown;
            float best = float.MaxValue;
            RaycastHit bestHit = default;
            bool found = false;
            var ray = new Ray(origin, Vector3.down);
            foreach (var c in deckColliders) {
                if (c != null && c.Raycast(ray, out var hit, len) && hit.distance < best) {
                    best = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }
            if (!found) {
                return false;
            }
            localY = transform.parent.InverseTransformPoint(bestHit.point).y;
            normalY = bestHit.normal.y;
            return true;
        }
    }
}
