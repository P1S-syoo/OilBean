using UnityEngine;

namespace Game.World {
    // 블록 1개 — 역할·변형을 보유한 절벽 구성 단위(IBlock 구현). 직렬화되어 BlockMap이 수집
    public class BlockTile : MonoBehaviour, IBlock {
        [SerializeField] BlockRole role;
        [SerializeField] int variant;

        public Vector3 WorldPos => transform.position;
        public BlockRole Role => role;
        public int Variant => variant;

        // 생성 시 역할/변형 주입(절차 생성기가 호출)
        public void Set(BlockRole r, int v) {
            role = r;
            variant = v;
        }
    }
}
