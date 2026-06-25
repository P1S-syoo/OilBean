using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Surface {
    // 정화선 스크린 콘솔 — 플레이어 근접 시 E로 거점 콘솔(허브) 열기(잠수 E와 분기)
    public class ScreenConsole : MonoBehaviour {
        [SerializeField] GameBootstrap game;        // 수상 상태 판정용
        [SerializeField] SurfaceBootstrap surface;  // 거점 콘솔 열기 + 잠수 E 억제 통지
        [SerializeField] GameObject prompt;         // "E 연구" 프롬프트(근접 시 표시)
        [SerializeField] float range = 2.2f;        // 상호작용 근접 거리(m)

        InputAction interact;
        Transform player;
        bool near;

        void Awake() {
            try {
                if (game == null) {
                    game = FindFirstObjectByType<GameBootstrap>();
                }
                if (surface == null) {
                    surface = FindFirstObjectByType<SurfaceBootstrap>();
                }
                interact = new InputAction("ScreenInteract", InputActionType.Button, "<Keyboard>/e");
                interact.performed += OnInteract;
                if (prompt != null) {
                    prompt.SetActive(false);
                }
            } catch (Exception e) {
                Debug.LogError($"[ScreenConsole] 초기화 실패: {e.Message}");
            }
        }

        void OnEnable() {
            interact?.Enable();
        }

        void OnDisable() {
            interact?.Disable();
        }

        void OnDestroy() {
            if (interact != null) {
                interact.performed -= OnInteract;
                interact.Dispose();
            }
        }

        void Update() {
            // 덱 캐릭터와의 거리로 근접 판정(콜라이더/리짓 의존 없이 안전)
            if (player == null) {
                var dc = FindFirstObjectByType<DeckCharacter>();
                if (dc != null) {
                    player = dc.transform;
                }
            }
            bool n = player != null && (player.position - transform.position).sqrMagnitude < range * range;
            if (n != near) {
                SetNear(n);
            }
        }

        void SetNear(bool n) {
            near = n;
            if (prompt != null) {
                prompt.SetActive(n);
            }
            if (surface != null) {
                surface.SetScreenNear(n);   // 잠수 E 억제(스크린 우선)
            }
        }

        void OnInteract(InputAction.CallbackContext ctx) {
            // 근접 + 수상 상태에서만 — 거점 콘솔(허브: 탐사 시작/연구/제작/정화) 열기. 커서·조작 해제는 콘솔이 처리
            if (!near || game == null || game.State != GameState.Surface) {
                return;
            }
            if (surface != null) {
                surface.OpenConsole();
            } else {
                game.GoResearch();   // 폴백 — SurfaceBootstrap 미연결 시 기존 동작
            }
        }
    }
}
