using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Core {
    // 잠수(잠수정 조작·배터리·카메라·시야) 설정 — 탐사 중 핵심값
    [CreateAssetMenu(fileName = "잠수설정", menuName = "게임설정/잠수설정")]
    public class 잠수설정 : ScriptableObject {

        static 잠수설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 잠수설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<잠수설정>());

        [Header("잠수정 조작")]
        public float 가속력 = 30f;          // 잠수정 가속력
        public float 최고속도 = 6f;         // 잠수정 최고 속도
        public float 선회속도 = 360f;       // 기수 선회 각속도(deg/s)

        [Header("카메라")]
        [FormerlySerializedAs("카메라지연")]
        public float 카메라추적지연 = 0.18f;    // 카메라 추적 지연(작을수록 빠름)
        [FormerlySerializedAs("카메라깊이")]
        [FormerlySerializedAs("카메라Z오프셋")]
        public float 카메라거리 = -20f;        // 카메라가 플레이어 뒤로 떨어지는 거리

        [Header("시야 탁함")]
        [FormerlySerializedAs("탁함강도")]
        public float 물탁함강도 = 1f;        // 탐사 중 물 탁함 강도
        [FormerlySerializedAs("탁함전환")]
        [FormerlySerializedAs("탁함전환시간")]
        public float 물탁함전환시간 = 1.3f;  // 탁함↔맑음 전환 시간(초)

        [Header("배터리")]
        public float 배터리최대 = 100f;     // 배터리 최대 용량
        public float 배터리소모 = 3.33f;    // 초당 배터리 소모량

        [Header("수심 경고")]
        public float 경고수심 = 25f;        // 이 수심부터 경고색 표시(m)
        public float 위험수심 = 40f;        // 이 수심부터 위험색 표시(m)
        public float 배터리경고비율 = 0.2f; // 이 비율 이하부터 저배터리 경고

    }
}
