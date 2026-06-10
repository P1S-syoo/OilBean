using UnityEngine;
using DG.Tweening;

namespace Game.UI {
    // 패널 팝업 트랜지션 유틸 — CanvasGroup 페이드 + 스케일 OutBack(보유 DOTween)
    // 정적 헬퍼: 한 곳에서만 쓰는 연출이라 컴포넌트화 불필요
    // (현재 패널 상시 표시라 미사용 — 거점/모드 전환 UI 도입 시 패널 토글 연출에 사용)
    public static class UiTween {
        // 패널 열기(페이드 인 + 살짝 튀는 스케일)
        public static void Show(GameObject go, float dur = 0.25f) {
            if (go == null) {
                return;
            }
            try {
                go.SetActive(true);
                var cg = GetGroup(go);
                var rt = (RectTransform)go.transform;
                cg.DOKill();
                rt.DOKill();
                cg.alpha = 0f;
                rt.localScale = Vector3.one * 0.85f;
                // CanvasGroup.DOFade는 DOTween UI 모듈(별도 어셈블리) → 본체 DOTween.To로 alpha 트윈
                DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, dur).SetUpdate(true);
                rt.DOScale(1f, dur).SetEase(Ease.OutBack).SetUpdate(true);
            } catch (System.Exception e) {
                Debug.LogError($"[UiTween] Show 오류: {e.Message}");
            }
        }

        // 패널 닫기(페이드 아웃 후 비활성)
        public static void Hide(GameObject go, float dur = 0.18f) {
            if (go == null || !go.activeSelf) {
                return;
            }
            try {
                var cg = GetGroup(go);
                var rt = (RectTransform)go.transform;
                cg.DOKill();
                rt.DOKill();
                DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, dur).SetUpdate(true);
                rt.DOScale(0.85f, dur).SetEase(Ease.InBack).SetUpdate(true)
                  .OnComplete(() => go.SetActive(false));
            } catch (System.Exception e) {
                Debug.LogError($"[UiTween] Hide 오류: {e.Message}");
            }
        }

        // CanvasGroup 확보(없으면 추가)
        static CanvasGroup GetGroup(GameObject go) {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) {
                cg = go.AddComponent<CanvasGroup>();
            }
            return cg;
        }
    }
}
