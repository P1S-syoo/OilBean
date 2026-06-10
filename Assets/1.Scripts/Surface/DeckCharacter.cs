using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Surface {
    // 덱 위 3인칭 캐릭터 — 카메라 기준 WASD 로컬 이동, 덱 경계 클램프(물에 못 떨어짐)
    public class DeckCharacter : MonoBehaviour {
        [SerializeField] Transform cam;                          // 이동 방향 기준 카메라(미연결 시 Camera.main)
        [SerializeField] float moveSpeed = 3.5f;
        [SerializeField] float turnSpeed = 12f;                  // 이동 방향 회전 보간 속도
        [SerializeField] Vector2 deckHalf = new(1.8f, 4.6f);    // 덱 절반 크기(로컬 x, z) — 잠수정 선체와 일치

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
            if (input.sqrMagnitude < 0.0001f || transform.parent == null) {
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
            Vector3 p = transform.localPosition + localDir * (moveSpeed * dt);
            // 덱 경계 클램프 — 잠수정 밖으로 못 나감
            p.x = Mathf.Clamp(p.x, -deckHalf.x, deckHalf.x);
            p.z = Mathf.Clamp(p.z, -deckHalf.y, deckHalf.y);
            transform.localPosition = p;
            // 이동 방향으로 몸 회전
            if (localDir.sqrMagnitude > 0.0001f) {
                var look = Quaternion.LookRotation(transform.parent.TransformDirection(localDir.normalized));
                transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * dt);
            }
        }
    }
}
