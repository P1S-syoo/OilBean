using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Game.UI {
    // 버튼 hover/press 스케일 피드백 — UITheme.AddScaleFeedback이 부착한다.
    // hover 시 1.04 확대, press 시 0.96 축소, 떼면 원래 크기로 복귀(ease-out).
    public class UIThemeButtonFeedback : MonoBehaviour,
            IPointerEnterHandler, IPointerExitHandler,
            IPointerDownHandler, IPointerUpHandler {

        Button _btn;
        float _hover = 1.04f;
        float _press = 0.96f;
        Vector3 _baseScale = Vector3.one;
        bool _pointerInside;

        // 대상 버튼과 hover/press 배율 설정(부착 직후 1회 호출)
        public void Setup(Button btn, float hover, float press) {
            _btn = btn;
            _hover = hover;
            _press = press;
            _baseScale = transform.localScale;
        }

        // 비활성 버튼은 피드백 생략
        bool Interactable() {
            return _btn == null || _btn.interactable;
        }

        // 목표 배율로 스케일 트윈(일시정지 영향 없게 SetUpdate(true))
        void ScaleTo(float mul) {
            try {
                transform.DOKill();
                transform.DOScale(_baseScale * mul, 0.12f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .SetTarget(transform);
            } catch (System.Exception e) {
                Debug.LogError($"[UIThemeButtonFeedback] 스케일 트윈 오류: {e.Message}");
            }
        }

        public void OnPointerEnter(PointerEventData e) {
            _pointerInside = true;
            if (Interactable()) {
                ScaleTo(_hover);
            }
        }

        public void OnPointerExit(PointerEventData e) {
            _pointerInside = false;
            ScaleTo(1f);
        }

        public void OnPointerDown(PointerEventData e) {
            if (Interactable()) {
                ScaleTo(_press);
            }
        }

        public void OnPointerUp(PointerEventData e) {
            // 떼면 포인터가 아직 위에 있으면 hover, 아니면 원래 크기
            ScaleTo(_pointerInside && Interactable() ? _hover : 1f);
        }

        // 비활성화 시 트윈 정리 + 스케일 원복(hover 1.04 잔존 방지)
        void OnDisable() {
            transform.DOKill();
            transform.localScale = Vector3.one;
        }
    }
}
