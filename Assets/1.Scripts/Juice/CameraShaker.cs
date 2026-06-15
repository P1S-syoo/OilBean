using UnityEngine;

namespace Game.Juice {
    // 카메라 셰이크 — CamFollow/Cinemachine이 위치를 쓴 뒤(LateUpdate 후순위)에 감쇠 오프셋을 덧붙임
    [DefaultExecutionOrder(300)]
    public class CameraShaker : MonoBehaviour {
        float amplitude;
        float duration;
        float timeLeft;

        // 셰이크 시작/누적 — 더 센 진폭·긴 지속이면 갱신
        public void Shake(float amp, float dur) {
            amplitude = Mathf.Max(amplitude, amp);
            duration = Mathf.Max(duration, dur);
            timeLeft = Mathf.Max(timeLeft, dur);
        }

        void LateUpdate() {
            if (timeLeft <= 0f) {
                return;
            }
            timeLeft -= Time.unscaledDeltaTime;
            if (timeLeft <= 0f) {
                amplitude = 0f;
                duration = 0f;
                return;
            }
            // 감쇠(제곱) — 끝으로 갈수록 잦아듦. CamFollow가 매 프레임 위치를 다시 써서 누적 드리프트 없음
            float k = duration > 0f ? timeLeft / duration : 0f;
            float a = amplitude * k * k;
            transform.localPosition += new Vector3((Random.value * 2f - 1f) * a, (Random.value * 2f - 1f) * a, 0f);
        }
    }
}
