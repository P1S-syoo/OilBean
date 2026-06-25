using UnityEngine;

namespace Game.Core {
    // 위험(오염원 행동·스폰) 설정
    [CreateAssetMenu(fileName = "위험설정", menuName = "게임설정/위험설정")]
    public class 위험설정 : ScriptableObject {

        static 위험설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 위험설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<위험설정>());

        [Header("오염원 행동")]
        public float 경고시간 = 0.8f;       // 돌진 전 경고(텔레그래프) 시간(초)
        public float 돌진속도 = 14f;        // 돌진 속도(u/s)
        public float 공격간격 = 2.2f;       // 돌진 공격 발생 간격(초)

        [Header("스폰")]
        public float 화면여유 = 3f;         // 화면 밖 스폰 시작/종료 여유(u)
    }
}
