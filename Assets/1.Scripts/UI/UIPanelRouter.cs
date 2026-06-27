using UnityEngine;
using Game.Core;
using Game.Surface;

namespace Game.UI {
    // UI 패널 라우터 — GameState에 따라 거점 콘솔/HUD/연구/제작 패널을 토글
    public class UIPanelRouter : MonoBehaviour {
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] SurfaceBootstrap surface;   // 수상 도착 감지(미연결 시 자동 탐색)
        [SerializeField] GameObject dockConsole;
        [SerializeField] GameObject hudPanel;
        [SerializeField] GameObject researchPanel;
        [SerializeField] GameObject craftPanel;
        [SerializeField] GameObject surfaceRoutePanel;

        bool surfaceArrived;   // 수상에서 정화 지점 도착 여부(콘솔 표시 게이트)

        void Start() {
            // 미연결 시 자동 탐색
            if (bootstrap == null) {
                bootstrap = FindFirstObjectByType<GameBootstrap>();
            }
            if (surface == null) {
                surface = FindFirstObjectByType<SurfaceBootstrap>();
            }
            if (surface != null) {
                surface.OnDiveReadyChanged += OnDiveReadyChanged;
                surfaceArrived = surface.DiveReady;
            }
            if (bootstrap != null) {
                bootstrap.OnStateChanged += OnStateChanged;
                Apply(bootstrap.State);
            } else {
                Debug.LogWarning("[UIPanelRouter] GameBootstrap 미연결 — 패널 토글 불가");
            }
        }

        void OnDestroy() {
            if (bootstrap != null) {
                bootstrap.OnStateChanged -= OnStateChanged;
            }
            if (surface != null) {
                surface.OnDiveReadyChanged -= OnDiveReadyChanged;
            }
        }

        // 수상 도착 상태 변화 — 콘솔 표시 갱신
        void OnDiveReadyChanged(bool arrived) {
            surfaceArrived = arrived;
            if (bootstrap != null) {
                Apply(bootstrap.State);
            }
        }

        void OnStateChanged(GameState from, GameState to) {
            Apply(to);
        }

        // 상태별 패널 표시 — 콘솔은 거점(Dock)이거나 수상에서 정화 지점 도착 시에만(자유 항해 중엔 숨김), 탐사=HUD, 연구/제작=각 패널
        void Apply(GameState s) {
            Set(dockConsole, s == GameState.Dock || (s == GameState.Surface && surfaceArrived));
            Set(hudPanel, s == GameState.Dive);
            Set(researchPanel, s == GameState.Research);
            Set(craftPanel, s == GameState.Craft);
            Set(surfaceRoutePanel, s == GameState.Surface && !(surface != null && surface.ConsoleShown));
        }

        static void Set(GameObject go, bool on) {
            if (go != null && go.activeSelf != on) {
                go.SetActive(on);
            }
        }
    }
}
