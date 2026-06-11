using System;
using System.Collections;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.UI;

namespace Game.Surface {
    // 수상 항해 코디네이터 — 목표 도달→잠수(E)→사이드뷰 블렌드+하강 연속 전환 후 2.5D 루프 인계
    public class SurfaceBootstrap : MonoBehaviour {
        [SerializeField] GameBootstrap game;          // FSM 소유자
        [SerializeField] Behaviour sideCamera;        // 2.5D 사이드뷰 카메라(CamFollow) — 수상 동안 비활성
        [SerializeField] SubNavigator nav;            // 잠수정 항해기
        [SerializeField] DeckCharacter deck;          // 덱 캐릭터(잠수 중 입력 잠금)
        [SerializeField] OrbitCameraDriver orbitDriver;
        [SerializeField] CinemachineCamera orbitCam;  // 3인칭 궤도 카메라
        [SerializeField] CinemachineCamera diveCam;   // 잠수 블렌드 목적지(사이드뷰 프레이밍)
        [SerializeField] Transform sideTarget;        // 2.5D 잠수정(블렌드 종료 프레이밍 기준)
        [SerializeField] Toast toast;                 // 잠수 가능 안내
        [SerializeField] ParticleSystem camParticles; // 수중용 부유 입자(카메라 자식) — 수상 동안 끔
        [SerializeField] GameObject worldBlocks;      // 2.5D 블록 스트리머 루트 — 수상에선 강변 벽이 다리처럼 보여 숨김
        [SerializeField] GameObject skyQuad;          // 2.5D 하늘 백드롭 퀀드 — 수상 동안 숨김(안개 하늘과 충돌)
        [SerializeField] float sideZOffset = -20f;    // CamFollow zOffset과 일치해야 끊김 없음
        [SerializeField] float blendTime = 2.2f;      // 카메라 블렌드·하강 연출 시간
        [SerializeField] float descendDepth = 8f;     // 잠수정 하강 깊이

        InputAction diveInput;
        bool diveReady;
        bool diving;
        Camera mainCam;                 // 수상 동안 배경색을 안개색으로 덮고 잠수 시 원복
        Color prevBg;
        CameraClearFlags prevClear;
        bool bgOverridden;
        bool prevParticlesActive = true;   // 숨기기 전 활성 상태 — 원복은 항상 이전 상태로
        bool prevBlocksActive = true;
        bool prevSkyActive = true;

        public bool DiveReady => diveReady;

        void Awake() {
            try {
                diveInput = new InputAction("Dive", InputActionType.Button, "<Keyboard>/e");
                diveInput.performed += OnDiveInput;
            } catch (Exception e) {
                Debug.LogError($"[SurfaceBootstrap] 초기화 실패: {e.Message}");
            }
        }

        void Start() {
            // 수상 시작이면 사이드뷰 카메라를 끄고 궤도 카메라(Cinemachine)가 주도
            if (game != null && game.State == GameState.Surface) {
                if (sideCamera != null) {
                    sideCamera.enabled = false;
                }
                // 수평선이 잿빛 안개에 녹도록 배경색을 안개색과 일치(잠수 인계 때 원복)
                mainCam = Camera.main;
                if (mainCam != null) {
                    prevBg = mainCam.backgroundColor;
                    prevClear = mainCam.clearFlags;
                    mainCam.clearFlags = CameraClearFlags.SolidColor;
                    mainCam.backgroundColor = RenderSettings.fogColor;
                    bgOverridden = true;
                }
                if (camParticles != null) {
                    prevParticlesActive = camParticles.gameObject.activeSelf;
                    camParticles.gameObject.SetActive(false);   // 수중 입자는 잠수 후에만
                }
                if (worldBlocks != null) {
                    prevBlocksActive = worldBlocks.activeSelf;
                    worldBlocks.SetActive(false);   // 2.5D 백드롭은 잠수 후에만
                }
                if (skyQuad != null) {
                    prevSkyActive = skyQuad.activeSelf;
                    skyQuad.SetActive(false);
                }
            }
            if (nav != null) {
                nav.OnArrived += OnArrived;
            }
        }

        void OnEnable() {
            diveInput?.Enable();
        }

        void OnDisable() {
            diveInput?.Disable();
        }

        void OnDestroy() {
            if (nav != null) {
                nav.OnArrived -= OnArrived;
            }
            if (diveInput != null) {
                diveInput.performed -= OnDiveInput;
                diveInput.Dispose();
            }
        }

        // 정화 목표 도달 — 잠수 허용 + 안내
        void OnArrived() {
            diveReady = true;
            if (toast != null) {
                toast.Show("정화 지점 도착 — E키로 잠수");
            }
        }

        void OnDiveInput(InputAction.CallbackContext ctx) {
            if (diveReady && !diving) {
                RequestDive();
            }
        }

        // 잠수 — 연속 연출(3인칭→사이드뷰 블렌드 + 하강) 후 기존 탐사 루프로 인계
        public void RequestDive() {
            if (diving) {
                return;
            }
            if (game == null) {
                Debug.LogError("[SurfaceBootstrap] GameBootstrap 미연결 — 인스펙터에서 할당하세요.");
                return;
            }
            diving = true;
            try {
                StartCoroutine(DiveSequence());
            } catch (Exception e) {
                Debug.LogError($"[SurfaceBootstrap] 잠수 시퀀스 시작 실패: {e.Message}");
                diving = false;
            }
        }

        IEnumerator DiveSequence() {
            // 1) 입력 잠금 + 커서 명시 복원(orbitDriver 생명주기에 의존하지 않음 — 누수 방지)
            SetSurfaceControl(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // 2.5D 잠수정을 3D 잠수정 바로 아래 수중으로 — 시작 위치가 아닌 현재 지점에서 잠수(연속감)
            if (sideTarget != null && nav != null) {
                var divePos = new Vector3(nav.transform.position.x, Game.World.WorldGen.WaterY - 3f, 0f);
                var rb2d = sideTarget.GetComponent<Rigidbody2D>();
                if (rb2d != null) {
                    rb2d.linearVelocity = Vector2.zero;   // 잔류 관성 제거
                    rb2d.position = divePos;
                }
                sideTarget.position = divePos;
            }
            // 2) 사이드뷰 프레이밍 카메라로 블렌드 시작
            if (diveCam != null && sideTarget != null) {
                diveCam.transform.SetPositionAndRotation(
                    sideTarget.position + new Vector3(0f, 0f, sideZOffset), Quaternion.identity);
                diveCam.gameObject.SetActive(true);
                if (orbitCam != null) {
                    orbitCam.gameObject.SetActive(false);
                }
            }
            // 3) 잠수정이 수면 아래로 가라앉음(블렌드와 동시 진행)
            Tween descend = null;
            float subY = nav != null ? nav.transform.position.y : 0f;
            if (nav != null) {
                nav.enabled = false;   // 항해 정지 상태 고정
                try {
                    descend = nav.transform.DOMoveY(subY - descendDepth, blendTime + 0.6f)
                        .SetEase(Ease.InQuad);
                } catch (Exception e) {
                    Debug.LogError($"[SurfaceBootstrap] 하강 트윈 실패: {e.Message}");
                }
            }
            bool handedOff = false;
            // 코루틴 중단·예외 경로에서도 finally가 트윈·상태를 정리 — 인계 성공에만 2.5D 전환을 묶음
            try {
                // 블렌드 동안 DiveCam이 목표 프레이밍을 계속 추적 — 종료 위치 = CamFollow 목표 보장(인계 점프 제거)
                float t = 0f;
                while (t < blendTime) {
                    t += Time.deltaTime;
                    if (diveCam != null && sideTarget != null) {
                        diveCam.transform.position = sideTarget.position + new Vector3(0f, 0f, sideZOffset);
                    }
                    yield return null;
                }
                // 4) 게임 루프 인계 — 성공해야만 2.5D로 전환
                handedOff = game.EnterDockFromSurface();
                if (handedOff && game.Run != null && nav != null) {
                    game.Run.SetSurfaceTarget(nav.TargetIndex + 1);   // 다음 수상 목표 기록(복귀 항해 재개용)
                }
            } finally {
                if (handedOff) {
                    descend?.Kill(true);          // 하강 종착점 고정 — 리그 비활성 후 트윈 접근 방지
                    RestoreUnderwaterVisuals();
                } else {
                    // 인계 거부·코루틴 중단 — 수상 상태로 되돌려 재시도 가능(데드 상태 방지)
                    Debug.LogWarning("[SurfaceBootstrap] 잠수 인계 실패 — 수상 상태로 복귀");
                    descend?.Kill(false);
                    if (nav != null) {
                        var p = nav.transform.position;
                        p.y = subY;
                        nav.transform.position = p;
                    }
                    if (diveCam != null) {
                        diveCam.gameObject.SetActive(false);
                    }
                    if (orbitCam != null) {
                        orbitCam.gameObject.SetActive(true);
                    }
                    SetSurfaceControl(true);
                    diving = false;
                }
            }
            // 5) 인계 성공 — 3D 잠수정·카메라만 끄고 환경(수면·스카이라인·다리)은 2.5D 백드롭으로 유지
            //    (W6 수면 복귀는 이들 재활성 + 코디네이터 재enable이 전제 — 계획서 W6 참조)
            if (handedOff) {
                if (nav != null) {
                    nav.gameObject.SetActive(false);
                }
                if (orbitCam != null) {
                    orbitCam.gameObject.SetActive(false);
                }
                if (diveCam != null) {
                    diveCam.gameObject.SetActive(false);
                }
                enabled = false;   // E 입력 종료(OnDisable) — 코루틴은 여기가 마지막이라 안전
            }
        }

        // 덱 조작·궤도 카메라 입력 일괄 토글(궤도 드라이버 OnEnable이 커서 잠금을 처리)
        void SetSurfaceControl(bool on) {
            if (deck != null) {
                deck.enabled = on;
            }
            if (orbitDriver != null) {
                orbitDriver.enabled = on;
            }
        }

        // 수상 동안 덮었던 2.5D 요소를 숨기기 전 상태로 복원 + 사이드뷰 인계
        void RestoreUnderwaterVisuals() {
            if (bgOverridden && mainCam != null) {
                mainCam.clearFlags = prevClear;
                mainCam.backgroundColor = prevBg;
            }
            if (camParticles != null) {
                camParticles.gameObject.SetActive(prevParticlesActive);
            }
            if (worldBlocks != null) {
                worldBlocks.SetActive(prevBlocksActive);
            }
            if (skyQuad != null) {
                skyQuad.SetActive(prevSkyActive);
            }
            if (sideCamera != null) {
                sideCamera.enabled = true;
            }
        }
    }
}
