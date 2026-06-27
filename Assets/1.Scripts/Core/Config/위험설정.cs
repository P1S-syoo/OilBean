using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("화면여유")]
        [FormerlySerializedAs("화면밖스폰여유")]
        public float 화면밖등장거리 = 3f;         // 화면 밖에서 등장할 거리(u)
        public float 공격라인상단여백 = 1.5f;     // 공격 라인이 화면 위쪽에 붙지 않게 하는 여백
        public float 공격라인하단여백 = 1.5f;     // 공격 라인이 화면 아래쪽에 붙지 않게 하는 여백
        public float 경고아이콘안쪽여백 = 1.4f;   // 경고 아이콘을 화면 안쪽으로 들이는 거리

        [Header("표시")]
        public float 오염원본체크기 = 1.4f;        // 오염원 본체 표시 크기
        public float 경고아이콘화면높이비율 = 0.085f;   // 화면 높이 대비 경고 아이콘 크기
        [FormerlySerializedAs("오염원피격박스크기")]
        public Vector2 오염원피격범위 = new(0.88f, 0.48f);   // 오염원 피격 판정 박스 크기
    }
}
