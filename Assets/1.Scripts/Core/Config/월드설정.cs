using UnityEngine;

namespace Game.Core {
    // 월드(청크 스트리밍·블록 렌더) 설정
    [CreateAssetMenu(fileName = "월드설정", menuName = "게임설정/월드설정")]
    public class 월드설정 : ScriptableObject {

        static 월드설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 월드설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<월드설정>());

        [Header("청크 스트리밍")]
        public int 청크가로블록수 = 16;      // 청크 가로 블록 수
        public int 청크세로블록수 = 16;      // 청크 세로 블록 수
        public int 청크가로로드반경 = 2;     // 좌우 로드 반경(청크)
        public int 청크세로로드반경 = 2;     // 상하 로드 반경(청크)

        [Header("블록 렌더")]
        public float 블록크기 = 0.95f;       // 블록 렌더 크기
        public float 플레이영역두께 = 4f;   // 플레이 영역 블록 Z 두께
    }
}
