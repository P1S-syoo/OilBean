using UnityEngine;
using Game.Core;

namespace Game.Surface {
    // 수상 항해 코디네이터 — Main3D 단일 씬에서 수면 위 페이즈(항해·덱 캐릭터)를 관장하고 잠수를 게임 코디네이터에 인계
    public class SurfaceBootstrap : MonoBehaviour {
        [SerializeField] GameBootstrap game;   // 같은 씬의 게임 코디네이터(FSM 소유자)

        // 잠수 요청 — 목표 지점 도달 후 해치 상호작용(W5)에서 호출. 성공 시 수상 리그 비활성은 W5에서 처리
        public void RequestDive() {
            if (game == null) {
                Debug.LogError("[SurfaceBootstrap] GameBootstrap 미연결 — 인스펙터에서 할당하세요.");
                return;
            }
            if (!game.EnterDockFromSurface()) {
                Debug.LogWarning("[SurfaceBootstrap] 잠수 요청이 거부됨");
            }
        }
    }
}
