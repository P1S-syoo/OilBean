using UnityEngine;

namespace Game.Surface {
    // 클립별 루트 기준 차이 보정 — 수영/대기 클립은 힙이 원점 아래(+0.55 필요), 보행/입수 클립은 발 기준(0)
    public class ClipHipOffset : MonoBehaviour {
        [SerializeField] Animator animator;
        [SerializeField] float swimOffset = 0.55f;   // 수영 계열 클립 상향 오프셋(m)
        [SerializeField] float lerpSpeed = 4f;       // 상태 전환 시 오프셋 보간 속도(m/s)

        float baseY;

        void Awake() {
            baseY = transform.localPosition.y;
        }

        void LateUpdate() {
            if (animator == null || !animator.isInitialized) {
                return;
            }
            var s = animator.GetCurrentAnimatorStateInfo(0);
            bool feetBased = s.IsName("Walk") || s.IsName("RunToDive") || s.IsName("IdleDeck");
            float target = baseY + (feetBased ? 0f : swimOffset);
            var p = transform.localPosition;
            p.y = Mathf.MoveTowards(p.y, target, lerpSpeed * Time.deltaTime);
            transform.localPosition = p;
        }
    }
}
