using UnityEngine;
using Game.Core;

namespace Game.Surface {
    // 수상 항해 코디네이터 — Main3D 단일 씬에서 수면 위 페이즈(항해·덱 캐릭터·궤도 카메라)를 관장하고 잠수를 게임 코디네이터에 인계
    public class SurfaceBootstrap : MonoBehaviour {
        [SerializeField] GameBootstrap game;     // 같은 씬의 게임 코디네이터(FSM 소유자)
        [SerializeField] Behaviour sideCamera;   // 2.5D 사이드뷰 카메라(CamFollow) — 수상 동안 비활성

        void Start() {
            // 수상 시작이면 사이드뷰 카메라를 끄고 궤도 카메라(Cinemachine)가 주도
            if (game != null && game.State == GameState.Surface && sideCamera != null) {
                sideCamera.enabled = false;
            }
        }

        // 잠수 요청 — 목표 지점 도달 후 해치 상호작용(W5)에서 호출
        public void RequestDive() {
            if (game == null) {
                Debug.LogError("[SurfaceBootstrap] GameBootstrap 미연결 — 인스펙터에서 할당하세요.");
                return;
            }
            if (!game.EnterDockFromSurface()) {
                Debug.LogWarning("[SurfaceBootstrap] 잠수 요청이 거부됨");
                return;
            }
            // 카메라 인계: 사이드뷰 복원 + 수상 리그 전체 비활성 (부드러운 블렌드 연출은 W5에서 확장)
            if (sideCamera != null) {
                sideCamera.enabled = true;
            }
            gameObject.SetActive(false);
        }
    }
}
