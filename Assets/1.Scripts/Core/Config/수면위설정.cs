using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Core {
    // 수면 위(항해·덱·입수 연출) 설정 — 잠수 전 단계의 조작·연출값
    [CreateAssetMenu(fileName = "수면위설정", menuName = "게임설정/수면위설정")]
    public class 수면위설정 : ScriptableObject {

        static 수면위설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 수면위설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<수면위설정>());

        [Header("덱 캐릭터")]
        [FormerlySerializedAs("덱속도")]
        public float 덱보행속도 = 1.6f;        // 덱 위 보행 속도
        [FormerlySerializedAs("덱회전")]
        public float 덱회전속도 = 12f;         // 덱 캐릭터 회전 보간 속도

        [Header("항해")]
        public float 항해속도 = 4f;         // 잠수정 순항 속도(m/s)
        [FormerlySerializedAs("감속거리")]
        public float 항해감속거리 = 8f;         // 목표 앞 감속 시작 거리(m)
        [FormerlySerializedAs("최저속도")]
        public float 항해최저속도 = 0.4f;       // 감속 중 최저 속도
        [FormerlySerializedAs("항해회전")]
        public float 항해회전속도 = 2f;         // 항해 회전 보간 속도

        [Header("수상 리그 생성")]
        public float 잠수정길이 = 15f;           // 수상 리그 잠수정 목표 길이
        public float 잠수정폭배율 = 1.5f;        // 잠수정 좌우 폭 보정 배율
        public float 잠수정흘수보정 = 0.35f;     // 선체가 물 위로 보이도록 올리는 비율
        public float 덱캐릭터키 = 1.7f;          // 수상 덱 캐릭터 목표 키

        [Header("카메라 감도")]
        [FormerlySerializedAs("좌우감도")]
        [FormerlySerializedAs("카메라좌우감도")]
        public float 마우스좌우감도 = 0.15f;      // 마우스 좌우 회전 감도
        [FormerlySerializedAs("상하감도")]
        [FormerlySerializedAs("카메라상하감도")]
        public float 마우스상하감도 = 0.1f;       // 마우스 상하 회전 감도

        [Header("입수 연출")]
        [FormerlySerializedAs("입수시간")]
        public float 잠수전환시간 = 2.2f;       // 수면에서 잠수 화면으로 전환되는 시간
        [FormerlySerializedAs("하강깊이")]
        public float 입수하강거리 = 8f;         // 입수 연출 중 아래로 내려가는 거리
        [FormerlySerializedAs("도약시간")]
        public float 캐릭터점프시간 = 1.1f;    // 덱 캐릭터가 물로 뛰어드는 시간
        [FormerlySerializedAs("도약거리")]
        public float 캐릭터점프거리 = 6f;      // 덱 캐릭터가 옆으로 뛰어드는 거리

        [Header("부유 흔들림")]
        public float 부유높이 = 0.15f;      // 수면 부유 상하 진폭(m)
        public float 부유주기 = 2.6f;       // 부유 한 사이클 시간(초)
        [FormerlySerializedAs("기울기")]
        [FormerlySerializedAs("부유기울기각도")]
        public float 부유기울기 = 0f;       // 좌우 기울기 진폭(도), 0이면 끔

        [Header("덱 발판")]
        public float 발판폭 = 3.6f;          // 캐릭터가 걸을 수 있는 발판 폭
        public float 발판길이 = 2.4f;        // 캐릭터가 걸을 수 있는 발판 길이
        public float 발판앞뒤위치비율 = -0.22f; // 잠수함 길이 대비 발판 앞뒤 위치(-1 뒤, 1 앞)
        public float 발판표시두께 = 0.035f;  // 투명 발판 비주얼 두께

        [Header("덱 모니터")]
        public float 모니터바닥높이 = 1.55f;       // 발판 위 모니터 중심 높이
        public float 모니터앞위치비율 = 0.58f;     // 발판 길이 대비 모니터 앞쪽 위치
        public float 모니터폭 = 3f;                // 모니터 화면 폭
        public float 모니터높이 = 1.8f;            // 모니터 화면 높이

        [Header("인트로 컷신")]
        public float 인트로시간 = 8f;        // 인트로 카메라 이동 시간(초)
        public Vector3 인트로시작오프셋 = new(-26f, 16f, -8f);   // 시작 카메라 상대 위치
        public Vector3 인트로종료오프셋 = new(0f, 5f, -13f);     // 종료 카메라 상대 위치
    }
}
