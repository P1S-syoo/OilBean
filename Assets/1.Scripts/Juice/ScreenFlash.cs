using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.Juice {
    // 전체 화면 플래시 — 임팩트(수집/충돌/정화/클리어) 순간 색을 번쩍였다 페이드. 카메라 리그와 무관(스크린 스페이스)
    [RequireComponent(typeof(Image))]
    public class ScreenFlash : MonoBehaviour {
        Image img;

        void Awake() {
            img = GetComponent<Image>();
            img.raycastTarget = false;   // 입력 가로채지 않음
            SetAlpha(0f);
        }

        // 지정 색으로 번쩍(짧게 올렸다 길게 내림). timeScale 영향 없이 동작
        public void Flash(Color color, float peak, float dur) {
            try {
                if (img == null) {
                    img = GetComponent<Image>();
                }
                DOTween.Kill(img);
                color.a = 0f;
                img.color = color;
                var seq = DOTween.Sequence().SetUpdate(true);
                seq.Append(DOTween.To(GetAlpha, SetAlpha, peak, dur * 0.25f));
                seq.Append(DOTween.To(GetAlpha, SetAlpha, 0f, dur * 0.75f));
            } catch (System.Exception e) {
                Debug.LogError($"[ScreenFlash] 플래시 실패: {e.Message}");
                SetAlpha(0f);
            }
        }

        float GetAlpha() {
            return img != null ? img.color.a : 0f;
        }

        void SetAlpha(float a) {
            if (img == null) {
                return;
            }
            var c = img.color;
            c.a = a;
            img.color = c;
        }
    }
}
