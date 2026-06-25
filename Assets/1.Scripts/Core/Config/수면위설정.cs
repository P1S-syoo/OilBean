using UnityEngine;

namespace Game.Core {
    // 수면 위(항해·덱·입수 연출) 설정 — 잠수 전 단계의 조작·연출값
    [CreateAssetMenu(fileName = "수면위설정", menuName = "게임설정/수면위설정")]
    public class 수면위설정 : ScriptableObject {

        static 수면위설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 수면위설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<수면위설정>());

        [Header("덱 캐릭터")]
        public float 덱속도 = 1.6f;        // 덱 위 보행 속도
        public float 덱회전 = 12f;         // 덱 캐릭터 회전 보간 속도

        [Header("항해")]
        public float 항해속도 = 4f;         // 잠수정 순항 속도(m/s)
        public float 감속거리 = 8f;         // 목표 앞 감속 시작 거리(m)
        public float 최저속도 = 0.4f;       // 감속 중 최저 속도
        public float 항해회전 = 2f;         // 항해 회전 보간 속도

        [Header("카메라 감도")]
        public float 좌우감도 = 0.15f;      // 카메라 수평 회전 감도
        public float 상하감도 = 0.1f;       // 카메라 수직 회전 감도

        [Header("입수 연출")]
        public float 입수시간 = 2.2f;       // 카메라 블렌드·하강 연출 시간
        public float 하강깊이 = 8f;         // 자동 하강 깊이
        public float 도약시간 = 1.1f;       // 덱 입수 도약 연출 시간
        public float 도약거리 = 6f;         // 입수 시 측면 도약 거리

        [Header("부유 흔들림")]
        public float 부유높이 = 0.15f;      // 수면 부유 상하 진폭(m)
        public float 부유주기 = 2.6f;       // 부유 한 사이클 시간(초)
        public float 기울기 = 0f;           // 좌우 기울기 진폭(도), 0이면 끔
    }
}
