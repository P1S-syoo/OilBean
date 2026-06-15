using System;
using System.Collections;
using UnityEngine;

namespace Game.UI {
    // 스테이지 클리어 연출 — 카메라 배경색을 맑게 보간 + 클리어 UI 표시(코루틴, DOTween 불필요)
    // DOTween Pro import 시 Lerp 루프를 DOColor 트윈으로 교체 가능
    public class ClearView : MonoBehaviour {
        [SerializeField] Camera cam;
        [SerializeField] Color clearColor = new Color(0.45f, 0.75f, 0.95f);  // 맑은 물
        [SerializeField] float duration = 1.5f;
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

        // 배경색을 탁한 색 → 맑은 색으로 보간
        IEnumerator LerpBg() {
            cam.clearFlags = CameraClearFlags.SolidColor;   // Skybox면 배경색이 안 보임 — 단색으로 전환
            Color from = cam.backgroundColor;
            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                cam.backgroundColor = Color.Lerp(from, clearColor, t / duration);
                yield return null;
            }
            cam.backgroundColor = clearColor;
        }
    }
}
