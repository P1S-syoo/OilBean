using UnityEngine;

namespace Game.Surface {
    // 부유 모션 — 사인파 상하 + 좌우 롤(파도에 흔들리는 느낌). 모델/잔해 차일드에 부착
    public class FloatBob : MonoBehaviour {
        [SerializeField] float amplitude;           // 상하 진폭(m) — 기본값은 수면위설정.부유높이
        [SerializeField] float period;              // 한 사이클 시간(초) — 기본값은 수면위설정.부유주기
        [SerializeField] float rollDegrees;         // 좌우 부유 기울기 진폭(도) — 기본값은 수면위설정.부유기울기(0이면 끔)
        [SerializeField] float phase = 0f;          // 0~1 위상 오프셋 — 개체별 어긋남(config 비대상)
        [SerializeField] Game.Core.수면위설정 config;   // 수면위 설정 — 연결 시 부유 수치 덮어씀(미연결 시 위 기본값 유지)

        float baseY;
        Quaternion baseRot;
        bool configApplied;   // 부유 수치 1회 주입 가드

        void Awake() {
            try {
                // 수면위 설정 적용 — 미연결 시 SO 기본값 사용(중복 제거, OnEnable 이전 1회)
                if (configApplied) {
                    return;
                }
                var cfg = config != null ? config : Game.Core.수면위설정.기본;
                amplitude = cfg.부유높이;
                period = cfg.부유주기;
                rollDegrees = cfg.부유기울기;
                configApplied = true;
            } catch (System.Exception e) {
                Debug.LogError($"[FloatBob] 설정 적용 실패: {e.Message}");
            }
        }

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
