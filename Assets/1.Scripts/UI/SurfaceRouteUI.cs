using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Surface;

namespace Game.UI {
    // 수면 항해 경로 HUD — 아래=출발, 위=목표 방향으로 현재 섹션 진행도를 표시
    public class SurfaceRouteUI : MonoBehaviour {
        [SerializeField] SubNavigator navigator;
        [SerializeField] RectTransform fill;
        [SerializeField] RectTransform marker;
        [SerializeField] RectTransform waypointTick;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text fromText;
        [SerializeField] TMP_Text waypointText;
        [SerializeField] TMP_Text toText;

        void Awake() {
            if (navigator == null) {
                navigator = FindFirstObjectByType<SubNavigator>(FindObjectsInactive.Include);
            }
        }

        void LateUpdate() {
            if (navigator == null) {
                return;
            }
            float p = Mathf.Clamp01(navigator.RouteProgress01);
            UpdateFill(p);
            UpdateMarker(p);
            UpdateWaypoint();
            UpdateText();
        }

        void UpdateFill(float p) {
            if (fill == null) {
                return;
            }
            var max = fill.anchorMax;
            max.y = p;
            fill.anchorMax = max;
        }

        void UpdateMarker(float p) {
            if (marker == null) {
                return;
            }
            var min = marker.anchorMin;
            var max = marker.anchorMax;
            min.y = p;
            max.y = p;
            marker.anchorMin = min;
            marker.anchorMax = max;
        }

        void UpdateWaypoint() {
            if (waypointTick == null && waypointText == null) {
                return;
            }
            float p = Mathf.Clamp01(navigator.FirstLandmarkProgress01);
            SetAnchorY(waypointTick, p);
            SetAnchorY(waypointText != null ? waypointText.rectTransform : null, p);
        }

        void SetAnchorY(RectTransform target, float y) {
            if (target == null) {
                return;
            }
            var min = target.anchorMin;
            var max = target.anchorMax;
            min.y = y;
            max.y = y;
            target.anchorMin = min;
            target.anchorMax = max;
        }

        void UpdateText() {
            if (titleText != null) {
                titleText.text = "한강 정화";
            }
            if (fromText != null) {
                fromText.text = "출발";
            }
            if (waypointText != null) {
                waypointText.text = navigator.FirstLandmarkName;
            }
            if (toText != null) {
                toText.text = navigator.FinalLandmarkName;
            }
        }
    }
}
