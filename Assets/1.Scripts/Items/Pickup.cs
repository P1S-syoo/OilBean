using UnityEngine;
using Game.Core;

namespace Game.Items {
    // 수중 수집물(고철/오염 샘플) — Collector가 트리거로 감지해 획득
    // 런타임 스폰 시 ItemDef 주입(등급·오염수준 포함), 에디터 수동 배치는 kind/weight 폴백
    [RequireComponent(typeof(Collider2D))]
    public class Pickup : MonoBehaviour {
        [SerializeField] ResourceKind kind = ResourceKind.Scrap;
        [SerializeField] float weight = 5f;   // 적재 무게(kg)

        ItemDef def;   // 스트리머가 주입(데이터 주도) — 없으면 직렬화 폴백

        public ItemDef Def => def;
        public ResourceKind Kind => def != null ? def.kind : kind;
        public float Weight => def != null ? def.weight : weight;
        public int Grade => def != null ? def.grade : 0;
        public int PollutionLevel => def != null ? def.pollutionLevel : 0;

        // 스폰 시 데이터 주입 — kind/weight도 동기화(폴백 일관)
        public void SetDef(ItemDef d) {
            def = d;
            if (d != null) {
                kind = d.kind;
                weight = d.weight;
            }
        }

        void Reset() {
            var col = GetComponent<Collider2D>();
            if (col != null) {
                col.isTrigger = true;
            }
        }
    }
}
