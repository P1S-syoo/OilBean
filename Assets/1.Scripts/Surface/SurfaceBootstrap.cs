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
        [SerializeField] float sideZOffset = -20f;    // CamFollow zOffset과 일치해야 끊김 없음
        [SerializeField] float blendTime = 2.2f;      // 카메라 블렌드·하강 연출 시간
        [SerializeField] float descendDepth = 8f;     // 잠수정 하강 깊이

        InputAction diveInput;
        bool diveReady;
        bool diving;

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
            if (game != null && game.State == GameState.Surface && sideCamera != null) {
                sideCamera.enabled = false;
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
            if (deck != null) {
                deck.enabled = false;
            }
            if (orbitDriver != null) {
                orbitDriver.enabled = false;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
            if (nav != null) {
                nav.enabled = false;   // 항해 정지 상태 고정
                try {
                    descend = nav.transform.DOMoveY(nav.transform.position.y - descendDepth, blendTime + 0.6f)
                        .SetEase(Ease.InQuad);
                } catch (Exception e) {
                    Debug.LogError($"[SurfaceBootstrap] 하강 트윈 실패: {e.Message}");
                }
            }
            // 블렌드 동안 DiveCam이 목표 프레이밍을 계속 추적 — 종료 위치 = CamFollow 목표 보장(인계 점프 제거)
            float t = 0f;
            while (t < blendTime) {
                t += Time.deltaTime;
                if (diveCam != null && sideTarget != null) {
                    diveCam.transform.position = sideTarget.position + new Vector3(0f, 0f, sideZOffset);
                }
                yield return null;
            }
            // 리그 비활성 후에도 트윈이 비활성 트랜스폼을 만지지 않도록 정리
            descend?.Kill(true);
            // 4) 카메라 인계 + 게임 루프 인계 + 다음 수상 목표 기록(복귀 항해 재개용)
            if (sideCamera != null) {
                sideCamera.enabled = true;
            }
            if (game.EnterDockFromSurface()) {
                if (game.Run != null && nav != null) {
                    game.Run.SetSurfaceTarget(nav.TargetIndex + 1);
                }
            } else {
                Debug.LogWarning("[SurfaceBootstrap] 잠수 인계 거부됨");
            }
            // 5) 수상 리그 종료 — 이 코루틴도 함께 멈추므로 반드시 마지막 줄
            gameObject.SetActive(false);
        }
    }
}
