namespace Game.Core {
    // 게임 모드 상태
    public enum GameState {
        Surface,   // 수상 항해(3D — 잠수정 덱 위)
        Dock,      // 거점(정화선)
        Dive,      // 수중 탐사
        Research,  // 연구
        Craft,     // 제작
        Purify,    // 정화 부유체 설치
        Clear      // 스테이지 클리어
    }
}
