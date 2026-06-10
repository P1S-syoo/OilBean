using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Surface {
    // 인트로 연출 — 폐허 도시 부감에서 잠수정으로 내려오는 오프닝 돌리, 스킵(Space/Esc) 가능
    public class IntroDirector : MonoBehaviour {
        [SerializeField] CinemachineCamera introCam;     // 컷신 전용 카메라
        [SerializeField] CinemachineCamera orbitCam;     // 종료 후 인계받을 3인칭 카메라
        [SerializeField] Transform focus;                // 바라볼 대상(잠수정) — 항해 중이면 따라감
        [SerializeField] DeckCharacter deck;             // 컷신 중 입력 잠금
        [SerializeField] OrbitCameraDriver orbitDriver;
        [SerializeField] float duration = 8f;
        [SerializeField] Vector3 startOffset = new(6f, 30f, -42f);   // 부감 시작(도시 사이 강 구도)
        [SerializeField] Vector3 endOffset = new(0f, 5f, -13f);      // 잠수정 가까이

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
            // 필수 참조가 없으면 컷신 없이 즉시 조작 인계
            if (introCam == null || focus == null) {
                Finish();
                return;
            }
            SetControl(false);
            if (orbitCam != null) {
                orbitCam.gameObject.SetActive(false);
            }
            introCam.gameObject.SetActive(true);
            playing = StartCoroutine(Dolly());
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
            SetControl(true);
        }

        void SetControl(bool on) {
            if (deck != null) {
                deck.enabled = on;
            }
            if (orbitDriver != null) {
                orbitDriver.enabled = on;
            }
        }
    }
}
