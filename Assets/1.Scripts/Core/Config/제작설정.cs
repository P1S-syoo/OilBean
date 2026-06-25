using UnityEngine;

namespace Game.Core {
    // 제작(레시피 비용·효과·부유체 수심) 설정
    [CreateAssetMenu(fileName = "제작설정", menuName = "게임설정/제작설정")]
    public class 제작설정 : ScriptableObject {

        static 제작설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 제작설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<제작설정>());

        [Header("부유체 제작 비용(강재 kg)")]
        public float 부유체1비용 = 24f;     // 1단계 부유체 강재 비용
        public float 부유체2비용 = 30f;     // 2단계 부유체 강재 비용
        public float 부유체3비용 = 40f;     // 3단계 부유체 강재 비용

        [Header("장비 업그레이드")]
        public float 적재비용 = 16f;        // 적재 한계 업글 강재 비용
        public float 적재증가 = 20f;        // 적재 한계 증가량(kg)
        public float 배터리비용 = 18f;      // 배터리 용량 업글 강재 비용
        public float 배터리증가 = 33f;      // 배터리 용량 증가량
        public float 내압비용 = 30f;        // 내압 프레임 강재 비용

        [Header("정화 설치")]
        public float 설치시간 = 2.5f;       // 정화 부유체 설치 소요(초)

        [Header("부유체 수심 게이트(m)")]
        public float 부유수심1 = 15f;       // 1단계 도달 수심
        public float 부유수심2 = 35f;       // 2단계 도달 수심
        public float 부유수심3 = 50f;       // 3단계(클리어) 도달 수심
    }
}
