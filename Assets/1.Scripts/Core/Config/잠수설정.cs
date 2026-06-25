using UnityEngine;

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
        public float 카메라지연 = 0.18f;    // 카메라 추적 지연(작을수록 빠름)
        public float 카메라깊이 = -20f;     // 카메라 z 위치(프레이밍)

        [Header("시야 탁함")]
        public float 탁함강도 = 1f;          // 탐사 중 물 탁함 강도
        public float 탁함전환 = 1.3f;       // 탁함↔맑음 전환 시간(초)

        [Header("배터리")]
        public float 배터리최대 = 100f;     // 배터리 최대 용량
        public float 배터리소모 = 3.33f;    // 초당 배터리 소모량

        [Header("수심 경고")]
        public float 경고수심 = 25f;        // 이 수심부터 경고색 표시(m)
        public float 위험수심 = 40f;        // 이 수심부터 위험색 표시(m)
    }
}
