using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.World;

namespace Game.Player {
    // 탐사 기계 2.5D 이동 — 입력 방향으로 기수를 서서히 정렬(측면 유지: 화면 Z회전 + 좌우 flip)하며 그 방향으로 전진
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMove : MonoBehaviour {
        [SerializeField] float accel = 30f;       // 가속력
        [SerializeField] float maxSpeed = 6f;     // 최고 속도
        [SerializeField] Transform model;         // 비주얼 피벗(화면 Z회전+flip 적용, 물리와 분리)
        [SerializeField] float turnSpeed = 360f;  // 기수 선회 각속도(deg/s) — '서서히' 정도
        [SerializeField] Transform searchLight;   // 전방 서치라이트(진행 방향 회전)
        [SerializeField] Animator animator;       // 캐릭터 애니(Idle↔Swim) — 속도로 전환
        [SerializeField] RunData run;             // 수심 게이트(부유체 단계로 진입 한계) — 미연결 시 자동 탐색
        [SerializeField] GameConfig config;       // 통합 설정 — 연결 시 이동 수치 덮어씀(미연결 시 위 기본값 유지)

        Rigidbody2D rb;
        Vector2 input;            // 정규화된 이동 입력
        InputAction move;         // WASD 2D 벡터(코드 정의)
        float heading;            // 현재 기수 화면각(도, 오른쪽=0 / 위=+90)
        bool depthGate;           // 수심 게이트 활성(탐사 중에만 — 코디네이터가 토글)

        // 수심 게이트 on/off — Dive 진입 시 켜고 그 외엔 끔(거점/연출 좌표와 충돌 방지)
        public void SetDepthGate(bool on) {
            depthGate = on;
        }

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결이면 기존 [SerializeField] 기본값 유지
                if (config != null) {
                    accel = config.playerAccel;
                    maxSpeed = config.playerMaxSpeed;
                    turnSpeed = config.playerTurnSpeed;
                }
                rb = GetComponent<Rigidbody2D>();
                rb.gravityScale = 0f;        // 수중: 중력 없음
                rb.linearDamping = 3f;       // 관성 감속
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;   // 카메라 추적 떨림 방지(물리 스텝 보간)
                move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
                move.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            } catch (System.Exception e) {
                Debug.LogError($"[PlayerMove] Awake 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        void Start() {
            // 수심 게이트용 RunData 자동 연결(미연결 시 코디네이터에서)
            if (run == null) {
                var gb = FindFirstObjectByType<Game.Core.GameBootstrap>();
                if (gb != null) {
                    run = gb.Run;
                }
            }
        }

        void OnEnable() {
            if (move == null) {
                return;
            }
            move.performed += OnMove;
            move.canceled += OnMove;
            move.Enable();
        }

        void OnDisable() {
            input = Vector2.zero;   // 비활성 중 눌린 키 잔류 방지
            if (move == null) {
                return;
            }
            move.performed -= OnMove;
            move.canceled -= OnMove;
            move.Disable();
        }

        void OnDestroy() {
            move?.Dispose();        // 코드 생성 InputAction 자원 해제
        }

        void OnMove(InputAction.CallbackContext ctx) {
            SetMoveInput(ctx.ReadValue<Vector2>());
        }

        // 입력원 분리 진입점(키보드/AI/테스트 공용)
        public void SetMoveInput(Vector2 v) {
            input = Vector2.ClampMagnitude(v, 1f);
        }

        public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
        public float Heading => heading;   // 현재 기수각(테스트/검증용)

        // 연출용 — 입력 없이 기수각·헤엄 속도를 외부에서 구동(잠수 자동 하강, 비활성 상태에서도 동작)
        public void SetCinematicPose(float headingDeg, float animSpeed) {
            heading = headingDeg;
            ApplyModel();
            if (searchLight != null) {
                searchLight.rotation = Quaternion.Euler(0f, 0f, heading);
            }
            if (animator != null) {
                animator.SetFloat("Speed", animSpeed);
            }
        }

        // 즉시 정지(강제 복귀 시 관성 제거)
        public void Halt() {
            input = Vector2.zero;
            if (rb != null) {
                rb.linearVelocity = Vector2.zero;
            }
        }

        void FixedUpdate() {
            if (rb == null) {
                return;
            }
            float dt = Time.fixedDeltaTime;
            // 입력 방향을 목표 기수각으로 — 현재 각에서 서서히 선회(각속도 제한)
            if (input.sqrMagnitude > 0.01f) {
                float target = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
                heading = Mathf.MoveTowardsAngle(heading, target, turnSpeed * dt);
            }
            ApplyModel();
            // 바라보는 방향(heading)으로 전진 — 입력 크기만큼 추진
            float r = heading * Mathf.Deg2Rad;
            Vector2 fwd = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            float mag = input.magnitude;
            if (rb.linearVelocity.sqrMagnitude < maxSpeed * maxSpeed) {
                rb.AddForce(fwd * accel * mag);
            } else {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }
            if (searchLight != null && input.sqrMagnitude > 0.01f) {
                searchLight.rotation = Quaternion.Euler(0f, 0f, heading);   // 빛은 실제 진행 방향
            }
            if (animator != null) {
                animator.SetFloat("Speed", rb.linearVelocity.magnitude);   // 이동 속도 → Idle/Swim 전환
            }
            ApplyDepthGate();
        }

        // 부유체 단계가 허용하는 최대 수심 아래로는 못 내려감 — 한계에서 멈추고 하강 속도 제거
        void ApplyDepthGate() {
            if (!depthGate || run == null) {
                return;
            }
            float limitY = DepthMap.DepthToWorld(run.MaxDepth());
            if (rb.position.y < limitY) {
                var p = rb.position;
                p.y = limitY;
                rb.position = p;
                var v = rb.linearVelocity;
                if (v.y < 0f) {
                    v.y = 0f;
                }
                rb.linearVelocity = v;
            }
        }

        // 기수각 → 측면뷰 유지 변환 — 음수 스케일 대신 Y180 뒤집기(좌향) + Z피치. 회전/깊이 왜곡 없음, 상하 정상
        void ApplyModel() {
            if (model == null) {
                return;
            }
            bool faceLeft = Mathf.Cos(heading * Mathf.Deg2Rad) < 0f;   // 진행 방향이 좌측 반
            if (!faceLeft) {
                model.localRotation = Quaternion.Euler(0f, 0f, heading);          // 우향: 화면 Z피치
            } else {
                model.localRotation = Quaternion.Euler(0f, 180f, 180f - heading); // 좌향: Y180 뒤집고 피치 보정
            }
        }
    }
}
