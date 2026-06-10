using UnityEngine;
using TMPro;
using DG.Tweening;

namespace Game.UI {
    // 토스트/배너 — 한 줄 메시지를 잠깐 띄우고 페이드 아웃(온보딩·알림)
    [RequireComponent(typeof(CanvasGroup))]
    public class Toast : MonoBehaviour {
        [SerializeField] TMP_Text label;
        [SerializeField] float showTime = 2f;   // 표시 유지(초)
        [SerializeField] float fade = 0.3f;

        CanvasGroup cg;

        void Awake() {
            cg = GetComponent<CanvasGroup>();
            cg.alpha = 0f;
        }

        // 메시지 1회 표시(페이드 인 → 유지 → 페이드 아웃)
        public void Show(string msg) {
            try {
                if (label != null) {
                    label.text = msg;
                }
                DOTween.Kill(cg);
                cg.alpha = 0f;
                // CanvasGroup.DOFade는 UI 모듈 → 본체 DOTween.To로 alpha 트윈
                var seq = DOTween.Sequence().SetUpdate(true);   // timeScale 무시
                seq.Append(DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, fade));
                seq.AppendInterval(showTime);
                seq.Append(DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, fade));
            } catch (System.Exception e) {
                Debug.LogError($"[Toast] 표시 오류: {e.Message}");
            }
        }
    }
}
