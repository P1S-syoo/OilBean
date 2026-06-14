using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI {
    // 연구/제작 패널 닫기 버튼 — 클릭 시 GameBootstrap.CloseHub로 연 출발 상태(거점/수상)로 복귀
    [RequireComponent(typeof(Button))]
    public class HubCloseButton : MonoBehaviour {
        GameBootstrap game;

        void Start() {
            try {
                game = FindFirstObjectByType<GameBootstrap>();
                GetComponent<Button>().onClick.AddListener(OnClick);
                if (game == null) {
                    Debug.LogWarning("[HubCloseButton] GameBootstrap 미발견 — 닫기 동작 불가");
                }
            } catch (System.Exception e) {
                Debug.LogError($"[HubCloseButton] 초기화 실패: {e.Message}");
            }
        }

        void OnDestroy() {
            var btn = GetComponent<Button>();
            if (btn != null) {
                btn.onClick.RemoveListener(OnClick);
            }
        }

        void OnClick() {
            if (game != null) {
                game.CloseHub();
            }
        }
    }
}
