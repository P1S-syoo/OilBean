using UnityEngine;

namespace Game.UI {
    // 스테이지 클리어 연출 — 클리어 UI 표시 + 파티클. '맑아짐' 화면 전환은 AtmosphereController의 URP Volume이 처리(C1 근본 해결)
    public class ClearView : MonoBehaviour {
        [SerializeField] GameObject clearText;     // 'STAGE CLEAR' 라벨
        [SerializeField] ParticleSystem fx;        // 정화 파티클(선택)

        bool played;

        // 세션 재시작용 상태 리셋(연출 재생 가능 상태로 — ResetRun과 함께 호출)
        public void ResetState() {
            played = false;
            if (clearText != null) {
                clearText.SetActive(false);
            }
        }

        // 클리어 연출 1회 재생
        public void Play() {
            if (played) {
                return;
            }
            played = true;
            if (clearText != null) {
                clearText.SetActive(true);
            }
            if (fx != null) {
                fx.Play();
            }
            // C1 근본 해결: 정화 전/후 '맑아짐'은 AtmosphereController의 URP Volume(탁함 weight→0)이
            // 화면 전체로 처리. 과거의 카메라 backgroundColor 트릭(LerpBg)은 제거.
        }
    }
}
