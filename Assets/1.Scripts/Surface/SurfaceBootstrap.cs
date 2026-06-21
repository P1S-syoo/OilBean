using System;
using System.Collections;
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
        [SerializeField] float descendDepth = 8f;     // 자동 하강 깊이(수면 직하 → 탐사 시작 위치)
        [SerializeField] float plungeTime = 1.1f;     // 덱 캐릭터 입수(RunToDive) 연출 시간
        [SerializeField] float plungeDistance = 6f;   // 입수 시 측면 도약 거리 — 넓어진 선체를 확실히 넘어 물로
        [SerializeField] GameConfig config;           // 통합 설정 — 연결 시 잠수 연출 수치 덮어씀(미연결 시 위 기본값 유지)

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
        bool prevCaptured;                 // 수상 시각 오버라이드 prev값 1회 캡처(원래 수중 상태 보존)
        Vector3 deckHomeLocalPos;          // 덱 캐릭터 원위치 — 수면 복귀 시 복원
        Quaternion deckHomeLocalRot = Quaternion.identity;
        bool deckHomeSet;

        public bool DiveReady => diveReady;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결이면 기존 기본값 유지
                if (config != null) {
                    blendTime = config.surfaceBlendTime;
                    descendDepth = config.surfaceDescendDepth;
                    plungeTime = config.surfacePlungeTime;
                    plungeDistance = config.surfacePlungeDistance;
                }
                diveInput = new InputAction("Dive", InputActionType.Button, "<Keyboard>/e");
                diveInput.performed += OnDiveInput;
            } catch (Exception e) {
                Debug.LogError($"[SurfaceBootstrap] 초기화 실패: {e.Message}");
            }
        }

        void Start() {
            // 덱 캐릭터 원위치 기억(수면 복귀 시 복원)
            if (deck != null) {
                deckHomeLocalPos = deck.transform.localPosition;
                deckHomeLocalRot = deck.transform.localRotation;
                deckHomeSet = true;
            }
            // 상태 전환 구독 — 클리어 후 Surface 복귀 시 수상 리그 재활성(W6)
            if (game != null) {
                game.OnStateChanged += OnGameStateChanged;
                if (game.State == GameState.Surface) {
                    ApplySurfaceVisuals();   // 수상 시작이면 사이드뷰 끔 + 안개 배경 + 2.5D 백드롭 숨김
                }
            }
            if (nav != null) {
                nav.OnArrived += OnArrived;
            }
        }

        // 수상 시각 상태 적용 — 시작·수면복귀 공용(사이드뷰 끔 + 배경 안개색 + 2.5D 백드롭 숨김)
        void ApplySurfaceVisuals() {
            if (mainCam == null) {
                mainCam = Camera.main;
            }
            // 원래(수중) 상태는 1회만 캡처 — 잠수 인계 때 RestoreUnderwaterVisuals가 이 값으로 원복
            if (!prevCaptured) {
                if (mainCam != null) {
                    prevBg = mainCam.backgroundColor;
                    prevClear = mainCam.clearFlags;
                }
                if (camParticles != null) {
                    prevParticlesActive = camParticles.gameObject.activeSelf;
                }
                if (worldBlocks != null) {
                    prevBlocksActive = worldBlocks.activeSelf;
                }
                if (skyQuad != null) {
                    prevSkyActive = skyQuad.activeSelf;
                }
                prevCaptured = true;
            }
            if (sideCamera != null) {
                sideCamera.enabled = false;
            }
            if (mainCam != null) {
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = RenderSettings.fogColor;   // 수평선이 잿빛 안개에 녹도록
                bgOverridden = true;
            }
            if (camParticles != null) {
                camParticles.gameObject.SetActive(false);   // 수중 입자는 잠수 후에만
            }
            if (worldBlocks != null) {
                worldBlocks.SetActive(false);   // 2.5D 백드롭은 잠수 후에만
            }
            if (skyQuad != null) {
                skyQuad.SetActive(false);
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
            if (game != null) {
                game.OnStateChanged -= OnGameStateChanged;
            }
            if (diveInput != null) {
                diveInput.performed -= OnDiveInput;
                diveInput.Dispose();
            }
        }

        // 클리어 후 Surface 복귀 감지 — 비활성 상태에서도 델리게이트는 수신되므로 여기서 재활성
        void OnGameStateChanged(GameState from, GameState to) {
            if (to == GameState.Surface && from != GameState.Surface) {
                ReturnToSurface();
            }
        }

        // 수면 복귀(W6) — 잠수 시퀀스의 역경로: 수상 요소 재활성 + 다음 목표로 항해 재개
        void ReturnToSurface() {
            try {
                enabled = true;      // E 입력 재개
                diving = false;
                diveReady = false;
                // 덱 캐릭터 원위치 복원(입수 단계에서 숨겼던 것)
                if (deck != null && deckHomeSet) {
                    deck.gameObject.SetActive(true);
                    deck.transform.localPosition = deckHomeLocalPos;
                    deck.transform.localRotation = deckHomeLocalRot;
                }
                // 카메라 — 궤도 on, 사이드뷰/다이브 off
                if (diveCam != null) {
                    diveCam.gameObject.SetActive(false);
                }
                if (orbitCam != null) {
                    orbitCam.gameObject.SetActive(true);
                }
                // 복귀 시점의 수중 상태를 새 원복 기준으로 재캡처 — 반복 잠수/복귀에서 누적 오류 방지
                prevCaptured = false;
                bgOverridden = false;
                ApplySurfaceVisuals();   // 사이드뷰 끔 + 안개 배경 + 2.5D 백드롭 숨김
                // 항해 재개 — nav가 정지 상태로 남아 있으니 다음 목표로 재출발
                if (nav != null) {
                    nav.enabled = true;
                    nav.Resume();
                }
                SetSurfaceControl(true);   // 덱 조작·궤도 드라이버 재활성(커서 잠금은 드라이버 OnEnable이 처리)
                Debug.Log("[SurfaceBootstrap] 수면 복귀 — 다음 목표로 항해 재개");
            } catch (Exception e) {
                Debug.LogError($"[SurfaceBootstrap] 수면 복귀 실패: {e.Message}\n{e.StackTrace}");
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
            // 1) 입력 잠금 + 커서 명시 복원(orbitDriver 생명주기에 의존하지 않음 — 누수 방지) + 항해 정지
            SetSurfaceControl(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (nav != null) {
                nav.enabled = false;   // 모선은 수면에 그대로(복귀 거점)
            }
            // 2) 덱 캐릭터 입수 — RunToDive 모션과 함께 전방 포물선으로 물에 뛰어듦(궤도 카메라가 지켜봄)
            Vector3 deckLocalPos = Vector3.zero;
            Quaternion deckLocalRot = Quaternion.identity;
            if (deck != null) {
                deckLocalPos = deck.transform.localPosition;
                deckLocalRot = deck.transform.localRotation;
                deck.TriggerDive();
                Vector3 start = deck.transform.position;
                // 입수 방향 = 잠수정 측면(가까운 현 = 강물 쪽) — 캐릭터가 선체 쪽을 봐도 물로 뛰게 고정(잠수정 관통 방지)
                Vector3 fwd;
                if (nav != null) {
                    float beamSign = deck.transform.localPosition.x >= 0f ? 1f : -1f;   // 가까운 현
                    fwd = nav.transform.right * beamSign;
                } else {
                    fwd = deck.transform.forward;
                }
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.right;
                deck.transform.rotation = Quaternion.LookRotation(fwd);   // 뛰는 방향(물)을 바라보게
                float plunge = 0f;
                while (plunge < plungeTime) {
                    plunge += Time.deltaTime;
                    float k = Mathf.Clamp01(plunge / plungeTime);
                    var pos = start + fwd * (plungeDistance * k);
                    pos.y = Mathf.Lerp(start.y, Game.World.WorldGen.WaterY - 1.2f, k * k);   // 가속 낙하(포물선)
                    deck.transform.position = pos;
                    yield return null;
                }
                deck.gameObject.SetActive(false);   // 입수 완료 — 이후는 2.5D 캐릭터가 이어받음
            }
            // 3) 2.5D 캐릭터를 모선 아래 수면 직하로 — 여기서부터 자동 하강
            float diveStartY = Game.World.WorldGen.WaterY - 1.2f;
            Rigidbody2D rb2d = null;
            Game.Player.PlayerMove mover2d = null;
            if (sideTarget != null && nav != null) {
                var divePos = new Vector3(nav.transform.position.x, diveStartY, 0f);
                rb2d = sideTarget.GetComponent<Rigidbody2D>();
                mover2d = sideTarget.GetComponent<Game.Player.PlayerMove>();
                if (rb2d != null) {
                    rb2d.linearVelocity = Vector2.zero;   // 잔류 관성 제거
                    rb2d.position = divePos;
                }
                sideTarget.position = divePos;
            }
            // 4) 사이드뷰 프레이밍 카메라로 블렌드 시작
            if (diveCam != null && sideTarget != null) {
                diveCam.transform.SetPositionAndRotation(
                    sideTarget.position + new Vector3(0f, 0f, sideZOffset), Quaternion.identity);
                diveCam.gameObject.SetActive(true);
                if (orbitCam != null) {
                    orbitCam.gameObject.SetActive(false);
                }
            }
            bool handedOff = false;
            // 코루틴 중단·예외 경로에서도 finally가 상태를 정리 — 인계 성공에만 2.5D 전환을 묶음
            try {
                // 블렌드 동안 아래를 보며 자동 헤엄 하강(조작 불가), DiveCam이 계속 추적 — 종료 위치 = CamFollow 목표(인계 점프 제거)
                float t = 0f;
                while (t < blendTime) {
                    t += Time.deltaTime;
                    if (sideTarget != null) {
                        float p = Mathf.Clamp01(t / blendTime);
                        var pos = sideTarget.position;
                        pos.y = Mathf.Lerp(diveStartY, diveStartY - descendDepth, p * p);
                        sideTarget.position = pos;
                        if (rb2d != null) {
                            rb2d.position = pos;
                            rb2d.linearVelocity = Vector2.zero;
                        }
                        mover2d?.SetCinematicPose(-90f, 5f);   // 아래를 바라보는 헤엄 자세
                        if (diveCam != null) {
                            diveCam.transform.position = sideTarget.position + new Vector3(0f, 0f, sideZOffset);
                        }
                    }
                    yield return null;
                }
                mover2d?.SetCinematicPose(0f, 0f);   // 도착 — 자세 복원, 이후 플레이어 조작
                // 5) 거점 좌표를 하강 종착점으로 갱신 — 복귀(R/강제) 시 모선 아래로 돌아와 위로 모선이 보임
                if (sideTarget != null) {
                    game.SetDockPoint(sideTarget.position);
                }
                // 게임 루프 인계 — 성공해야만 2.5D로 전환
                handedOff = game.EnterDockFromSurface();
                if (handedOff && game.Run != null && nav != null) {
                    game.Run.SetSurfaceTarget(nav.TargetIndex + 1);   // 다음 수상 목표 기록(복귀 항해 재개용)
                }
            } finally {
                if (handedOff) {
                    RestoreUnderwaterVisuals();
                } else {
                    // 인계 거부·코루틴 중단 — 수상 상태로 되돌려 재시도 가능(데드 상태 방지)
                    Debug.LogWarning("[SurfaceBootstrap] 잠수 인계 실패 — 수상 상태로 복귀");
                    if (deck != null) {
                        deck.gameObject.SetActive(true);   // 입수했던 캐릭터를 덱 원위치로
                        deck.transform.localPosition = deckLocalPos;
                        deck.transform.localRotation = deckLocalRot;
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
            // 6) 인계 성공 — 모선(3D 잠수정)은 수면 거점으로 유지, 수상 카메라만 끔(덱 캐릭터는 입수 단계에서 숨김)
            //    (W6 수면 복귀는 덱 캐릭터 재활성 + 코디네이터 재enable이 전제 — 계획서 W6 참조)
            if (handedOff) {
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
