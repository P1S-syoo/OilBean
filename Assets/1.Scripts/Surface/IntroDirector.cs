using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Surface {
    // 인트로 연출 — 폐허 도시 부감에서 잠수정으로 내려오는 오프닝 돌리, 스킵(Space/Esc) 가능
    public class IntroDirector : MonoBehaviour {
        [SerializeField] CinemachineCamera introCam;     // 컷신 전용 카메라
        [SerializeField] CinemachineCamera orbitCam;     // 종료 후 인계받을 3인칭 카메라
        [SerializeField] Transform focus;                // 바라볼 대상(잠수정) — 항해 중이면 따라감
        [SerializeField] DeckCharacter deck;             // 컷신 중 입력 잠금
        [SerializeField] OrbitCameraDriver orbitDriver;
        [SerializeField] SubNavigator nav;               // 컷신 동안 항해 보류 — 컷신 중 목표 도달 경합 방지
        [SerializeField] float duration = 8f;
        [SerializeField] Vector3 startOffset = new(-26f, 16f, -8f);  // 후방 상공 와이드 — 하류(명소·안개 스카이라인)를 향해 열린 구도
        [SerializeField] Vector3 endOffset = new(0f, 5f, -13f);      // 잠수정 가까이
        [SerializeField] 수면위설정 config;              // 수면위 설정 — 연결 시 인트로 컷신 수치 적용

        InputAction skip;
        Coroutine playing;
        bool done;

        public bool Running => playing != null && !done;

        void Awake() {
            try {
                skip = new InputAction("IntroSkip", InputActionType.Button);
                skip.AddBinding("<Keyboard>/space");
                skip.AddBinding("<Keyboard>/escape");
                skip.performed += OnSkip;
            } catch (Exception e) {
                Debug.LogError($"[IntroDirector] 초기화 실패: {e.Message}");
            }
        }

        void Start() {
            ApplyConfig();
            // 필수 참조가 없으면 컷신 없이 즉시 조작 인계
            if (introCam == null || focus == null) {
                Finish();
                return;
            }
            SetControl(false);
            if (nav != null) {
                nav.enabled = false;   // 항해는 컷신 끝나고 출발
            }
            if (orbitCam != null) {
                orbitCam.gameObject.SetActive(false);
            }
            introCam.gameObject.SetActive(true);
            playing = StartCoroutine(Dolly());
        }

        // 인트로 컷신 설정 적용 — 미연결 시 SO 기본값 사용
        void ApplyConfig() {
            var cfg = config != null ? config : 수면위설정.기본;
            duration = cfg.인트로시간;
            startOffset = cfg.인트로시작오프셋;
            endOffset = cfg.인트로종료오프셋;
        }

        void OnEnable() {
            skip?.Enable();
        }

        void OnDisable() {
            skip?.Disable();
        }

        void OnDestroy() {
            if (skip != null) {
                skip.performed -= OnSkip;
                skip.Dispose();
            }
        }

        void OnSkip(InputAction.CallbackContext ctx) {
            if (!done) {
                Finish();
            }
        }

        // 부감→근접 돌리 — 대상이 항해 중이어도 매 프레임 따라가며 내려옴
        IEnumerator Dolly() {
            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                Vector3 off = Vector3.Lerp(startOffset, endOffset, k);
                Vector3 pos = focus.position + off;
                introCam.transform.position = pos;
                introCam.transform.rotation = Quaternion.LookRotation(focus.position + Vector3.up * 1.5f - pos);
                yield return null;
            }
            Finish();
        }

        // 컷신 종료 — 3인칭으로 블렌드 인계 + 조작 잠금 해제
        void Finish() {
            if (done) {
                return;
            }
            done = true;
            if (playing != null) {
                StopCoroutine(playing);
                playing = null;
            }
            if (orbitCam != null) {
                orbitCam.gameObject.SetActive(true);
            }
            if (introCam != null) {
                introCam.gameObject.SetActive(false);
            }
            if (nav != null) {
                nav.enabled = true;   // 컷신 종료 — 항해 출발
            }
            SetControl(true);
        }

        void SetControl(bool on) {
            if (deck != null) {
                deck.ControlLocked = !on;   // 입력만 잠금 — 컷신 동안에도 접지 스냅은 유지
            }
            if (orbitDriver != null) {
                orbitDriver.enabled = on;
            }
        }
    }
}
