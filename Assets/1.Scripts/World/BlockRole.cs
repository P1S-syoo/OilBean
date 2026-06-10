namespace Game.World {
    // 블록 역할 — 절벽 구성 부위. VARCO 모델 매핑 키이자 역할별 색/머티리얼 구분 기준
    public enum BlockRole {
        Wall,    // 벽 몸통(채움)
        Top,     // 상단/지붕 마감
        Corner,  // 모서리 전환
        Slope    // 경사/계단 완화
    }
}
