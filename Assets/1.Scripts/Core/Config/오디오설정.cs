using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Core {
    // 오디오(배경음·효과음) 크기 설정
    [CreateAssetMenu(fileName = "오디오설정", menuName = "게임설정/오디오설정")]
    public class 오디오설정 : ScriptableObject {

        static 오디오설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 오디오설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<오디오설정>());

        [Header("배경음")]
        public float 배경음크기 = 0.35f;    // 배경음 볼륨(0~1)
        [FormerlySerializedAs("배경음전환")]
        public float 배경음전환시간 = 0.8f;     // 배경음 교차 페이드 시간(초)

        [Header("효과음")]
        public float 효과음크기 = 0.9f;     // 효과음 볼륨(0~1)

        [Header("음성")]
        public float 음성크기 = 1f;         // 캐릭터 대사 볼륨(0~1)
    }
}
