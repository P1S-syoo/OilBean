using UnityEngine;
using Game.Core;

namespace Game.Items {
    // 수중 수집물(고철/오염 샘플) — Collector가 트리거로 감지해 획득
    [RequireComponent(typeof(Collider2D))]
    public class Pickup : MonoBehaviour {
        [SerializeField] ResourceKind kind = ResourceKind.Scrap;
        [SerializeField] float weight = 5f;   // 적재 무게(kg)

        public ResourceKind Kind => kind;
        public float Weight => weight;

        void Reset() {
            // 자동 추가된 콜라이더를 트리거로
            var col = GetComponent<Collider2D>();
            if (col != null) {
                col.isTrigger = true;
            }
        }
    }
}
