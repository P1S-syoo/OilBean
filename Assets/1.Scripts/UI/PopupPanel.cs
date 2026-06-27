using UnityEngine;
using DG.Tweening;
using Game.Core;

namespace Game.UI {
    // 팝업 패널 연출 — 활성화 시 스케일(살짝 작게→1, OutBack)+페이드 인. 화면을 꽉 채우지 않는 팝업 UI에 부착
    [RequireComponent(typeof(CanvasGroup))]
    public class PopupPanel : MonoBehaviour {
        [SerializeField] float duration = 0.22f;
        [SerializeField] float fromScale = 0.86f;
        [SerializeField] 연출설정 config;   // 연출 설정 — 연결 시 팝업 애니메이션 적용

        CanvasGroup cg;
        RectTransform rt;

        void Awake() {
            cg = GetComponent<CanvasGroup>();
            rt = GetComponent<RectTransform>();
            ApplyConfig();
        }

        // 팝업 연출 설정 적용 — 미연결 시 SO 기본값 사용
        void ApplyConfig() {
            var cfg = config != null ? config : 연출설정.기본;
            duration = cfg.팝업시간;
            fromScale = cfg.팝업시작크기;
        }

        void OnEnable() {
            try {
                if (cg == null) {
                    cg = GetComponent<CanvasGroup>();
                }
                if (rt == null) {
                    rt = GetComponent<RectTransform>();
                }
                DOTween.Kill(cg);
                cg.alpha = 0f;
                rt.localScale = Vector3.one * fromScale;
                // timeScale 무시(메뉴/일시정지에서도 동작) — UI 모듈 없이 본체 To로 페이드+스케일
                var seq = DOTween.Sequence().SetUpdate(true);
                seq.Append(DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, duration));
                seq.Join(DOTween.To(() => rt.localScale, s => rt.localScale = s, Vector3.one, duration).SetEase(Ease.OutBack));
            } catch (System.Exception e) {
                Debug.LogError($"[PopupPanel] 연출 실패: {e.Message}");
                if (cg != null) {
                    cg.alpha = 1f;
                }
                if (rt != null) {
                    rt.localScale = Vector3.one;
                }
            }
        }

        void OnDisable() {
            // 비활성 시 트윈 정리 + 다음 활성화를 위해 스케일 원복
            if (cg != null) {
                DOTween.Kill(cg);
            }
            if (rt != null) {
                rt.localScale = Vector3.one;
            }
        }
    }
}
