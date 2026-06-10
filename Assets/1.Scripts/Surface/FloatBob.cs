using System;
using DG.Tweening;
using UnityEngine;

namespace Game.Surface {
    // 부유 모션 — 모델 차일드에만 부착(잠수정 루트에 걸면 덱 위 캐릭터까지 같이 떨림)
    public class FloatBob : MonoBehaviour {
        [SerializeField] float amplitude = 0.15f;   // 상하 진폭(m)
        [SerializeField] float period = 2.6f;       // 한 사이클 시간(초)

        Tween bob;

        void OnEnable() {
            try {
                bob = transform.DOLocalMoveY(transform.localPosition.y + amplitude, period * 0.5f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            } catch (Exception e) {
                Debug.LogError($"[FloatBob] 부유 트윈 시작 실패: {e.Message}");
            }
        }

        void OnDisable() {
            // 트윈 수명 정리 — 파괴된 트랜스폼 참조 방지
            bob?.Kill();
            bob = null;
        }
    }
}
