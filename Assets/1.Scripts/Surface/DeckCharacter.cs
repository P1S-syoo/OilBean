using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Surface {
    // 덱 위 3인칭 캐릭터 — 카메라 기준 WASD 로컬 이동, 덱 경계 클램프(물에 못 떨어짐)
    public class DeckCharacter : MonoBehaviour {
        [SerializeField] Transform cam;                          // 이동 방향 기준 카메라(미연결 시 Camera.main)
        [SerializeField] float moveSpeed = 1.4f;                 // 실제 사람 보행 속도(≈5km/h) — Walk 클립 원배속과 일치
        [SerializeField] float turnSpeed = 12f;                  // 이동 방향 회전 보간 속도
        [SerializeField] Vector2 deckHalf = new(1.8f, 4.6f);    // 덱 절반 크기(로컬 x, z) — 1차 클램프(거친 한계)
        [SerializeField] Animator animator;                      // 선택 — 이동 시 Speed 파라미터로 모션 전환
        [SerializeField] Collider[] deckColliders;               // 선체 메시 콜라이더 — 실제 표면 위로만 보행 허용
        [SerializeField] float walkableNormalY = 0.65f;          // 보행 가능 경사 한계(표면 법선 y) — 측면으로 못 내려감

        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int OnDeckHash = Animator.StringToHash("OnDeck");
        static readonly int DiveHash = Animator.StringToHash("Dive");

        InputAction move;

        void Awake() {
            try {
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
            Step(move.ReadValue<Vector2>(), Time.deltaTime);
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
                // 정지 중에도 부유하는 선체 표면에 발을 붙임
                if (TrySnapToDeck(transform.localPosition, out var idleSnap)) {
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
                transform.localPosition = snapped;
            }
            // 이동 방향으로 몸 회전
            if (localDir.sqrMagnitude > 0.0001f) {
                var look = Quaternion.LookRotation(transform.parent.TransformDirection(localDir.normalized));
                transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * dt);
            }
        }

        // 후보 로컬 위치 위에서 아래로 선체 콜라이더 레이캐스트 — 명중 + 완만한 면일 때만 유효(발 높이 스냅)
        bool TrySnapToDeck(Vector3 localPos, out Vector3 snapped) {
            snapped = localPos;
            if (deckColliders == null || deckColliders.Length == 0) {
                return true;   // 콜라이더 미배선(그레이박스 폴백) — 기존 클램프만으로 동작
            }
            Vector3 world = transform.parent.TransformPoint(localPos);
            var ray = new Ray(world + Vector3.up * 2.5f, Vector3.down);
            float best = float.MaxValue;
            RaycastHit bestHit = default;
            bool found = false;
            foreach (var c in deckColliders) {
                if (c != null && c.Raycast(ray, out var hit, 6f) && hit.distance < best) {
                    best = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }
            if (!found || bestHit.normal.y < walkableNormalY) {
                return false;
            }
            Vector3 local = transform.parent.InverseTransformPoint(bestHit.point);
            snapped = new Vector3(localPos.x, local.y, localPos.z);
            return true;
        }
    }
}
