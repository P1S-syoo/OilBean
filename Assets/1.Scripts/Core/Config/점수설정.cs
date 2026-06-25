using UnityEngine;

namespace Game.Core {
    // 점수(콤보·등급) 설정
    [CreateAssetMenu(fileName = "점수설정", menuName = "게임설정/점수설정")]
    public class 점수설정 : ScriptableObject {

        static 점수설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 점수설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<점수설정>());

        [Header("콤보")]
        public float 콤보시간 = 3f;         // 콤보 유지 시간(초)
        public int 기본점수 = 100;          // 수집 1회 기본 점수

        [Header("등급 임계값")]
        public int S등급 = 5000;            // S 등급 점수 임계값
        public int A등급 = 2500;            // A 등급 점수 임계값
    }
}
