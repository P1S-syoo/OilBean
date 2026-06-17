using UnityEngine;
using Game.Core;

namespace Game.UI {
    // UI 패널 라우터 — GameState에 따라 거점 콘솔/HUD/연구/제작 패널을 토글
    public class UIPanelRouter : MonoBehaviour {
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] GameObject dockConsole;
        [SerializeField] GameObject hudPanel;
        [SerializeField] GameObject researchPanel;
        [SerializeField] GameObject craftPanel;

        void Start() {
            // 미연결 시 자동 탐색
            if (bootstrap == null) {
                bootstrap = FindFirstObjectByType<GameBootstrap>();
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
        }

        void OnStateChanged(GameState from, GameState to) {
            Apply(to);
        }

        // 상태별 패널 표시 — 거점/수상=콘솔(복귀가 Surface 허브로 오므로 콘솔 표시), 탐사=HUD, 연구/제작=각 패널
        void Apply(GameState s) {
            Set(dockConsole, s == GameState.Dock || s == GameState.Surface);
            Set(hudPanel, s == GameState.Dive);
            Set(researchPanel, s == GameState.Research);
            Set(craftPanel, s == GameState.Craft);
        }

        static void Set(GameObject go, bool on) {
            if (go != null && go.activeSelf != on) {
                go.SetActive(on);
            }
        }
    }
}
