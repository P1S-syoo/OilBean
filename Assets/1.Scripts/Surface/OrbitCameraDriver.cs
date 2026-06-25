using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Surface {
    // 마우스 궤도 카메라 — Cinemachine OrbitalFollow 축을 마우스 델타로 구동(수직은 Range로 클램프)
    public class OrbitCameraDriver : MonoBehaviour {
        [SerializeField] CinemachineOrbitalFollow orbital;   // 미연결 시 같은 오브젝트에서 검색
        [SerializeField] float sensX;   // 카메라 수평 감도 — 기본값은 수면위설정.좌우감도
        [SerializeField] float sensY;   // 카메라 수직 감도 — 기본값은 수면위설정.상하감도
        [SerializeField] Game.Core.수면위설정 config;   // 수면위 설정 — 연결 시 감도 덮어씀(미연결 시 위 기본값 유지)

        InputAction look;

        void Awake() {
            try {
                // 통합 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거)
                var cfg = config != null ? config : Game.Core.수면위설정.기본;
                sensX = cfg.좌우감도;
                sensY = cfg.상하감도;
                if (orbital == null) {
                    orbital = GetComponent<CinemachineOrbitalFollow>();
                }
                if (orbital == null) {
                    Debug.LogError("[OrbitCameraDriver] OrbitalFollow 미발견 — 같은 오브젝트에 추가하세요.");
                    enabled = false;
                    return;
                }
                look = new InputAction("OrbitLook", InputActionType.Value, "<Mouse>/delta");
            } catch (Exception e) {
                Debug.LogError($"[OrbitCameraDriver] 초기화 실패: {e.Message}");
                enabled = false;
            }
        }

        void OnEnable() {
            look?.Enable();
            // 수상 조작 중 커서 잠금(UI 열면 해제는 추후 UI 쪽에서)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable() {
            look?.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnDestroy() {
            look?.Dispose();
        }

        void Update() {
            if (look == null) {
                return;
            }
            Vector2 d = look.ReadValue<Vector2>();
            orbital.HorizontalAxis.Value += d.x * sensX;
            float v = orbital.VerticalAxis.Value - d.y * sensY;
            orbital.VerticalAxis.Value = Mathf.Clamp(v, orbital.VerticalAxis.Range.x, orbital.VerticalAxis.Range.y);
        }
    }
}
