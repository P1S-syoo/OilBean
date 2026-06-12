using UnityEngine;

namespace Game.Surface {
    // 부유 모션 — 사인파 상하 + 좌우 롤(파도에 흔들리는 느낌). 모델/잔해 차일드에 부착
    public class FloatBob : MonoBehaviour {
        [SerializeField] float amplitude = 0.15f;   // 상하 진폭(m)
        [SerializeField] float period = 2.6f;       // 한 사이클 시간(초)
        [SerializeField] float rollDegrees = 0f;    // 좌우 기울기 진폭(도) — 부유 잔해용, 0이면 끔
        [SerializeField] float phase = 0f;          // 0~1 위상 오프셋 — 개체별 어긋남

        float baseY;
        Quaternion baseRot;

        void OnEnable() {
            baseY = transform.localPosition.y;
            baseRot = transform.localRotation;
        }

        void OnDisable() {
            // 기준 복원 — 재활성 시 y/회전이 누적 표류하지 않도록
            var p = transform.localPosition;
            p.y = baseY;
            transform.localPosition = p;
            transform.localRotation = baseRot;
        }

        void Update() {
            float t = Time.time / Mathf.Max(period, 0.01f) + phase;
            var p = transform.localPosition;
            p.y = baseY + amplitude * Mathf.Sin(t * Mathf.PI * 2f);
            transform.localPosition = p;
            if (rollDegrees > 0f) {
                // 롤은 상하와 살짝 다른 주기/위상 — 기계적 반복감 제거
                float roll = rollDegrees * Mathf.Sin(t * Mathf.PI * 2f * 0.8f + 1.3f);
                transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, roll);
            }
        }
    }
}
