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

        bool surfaceConsoleShown;   // 수상에서 E/스크린으로 콘솔을 직접 열었는지

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
                surfaceConsoleShown = surface.ConsoleShown;
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

        // 수상 콘솔 표시 변화 — 도착만으로 자동 표시하지 않고 E/스크린 입력 때만 표시
        void OnDiveReadyChanged(bool shown) {
            surfaceConsoleShown = shown;
            if (bootstrap != null) {
                Apply(bootstrap.State);
            }
        }

        void OnStateChanged(GameState from, GameState to) {
            Apply(to);
        }

        // 상태별 패널 표시 — 수상 콘솔은 직접 열었을 때만 표시(도착 notice와 분리)
        void Apply(GameState s) {
            Set(dockConsole, s == GameState.Dock || (s == GameState.Surface && surfaceConsoleShown));
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
