using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Core {
    // 연출(내레이션·미니게임) 설정
    [CreateAssetMenu(fileName = "연출설정", menuName = "게임설정/연출설정")]
    public class 연출설정 : ScriptableObject {

        static 연출설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 연출설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<연출설정>());

        [Header("내레이션 출력")]
        public float 글자속도 = 0.04f;      // 글자 간 딜레이(초)
        public float 페이드시간 = 0.35f;    // 대사창 페이드 시간(초)
        public float 대사창높이 = 168f;      // 대사창 높이(px)
        public float 대사창하단여백 = 96f;   // 화면 하단에서 대사창까지 여백(px)

        [Header("미니게임 난이도")]
        [FormerlySerializedAs("미니노드")]
        public int 연구미니게임노드수 = 5;            // 연구 미니게임 노드 수
        [FormerlySerializedAs("미니판정")]
        public float 연구미니게임판정반경 = 46f;        // 노드 통과 판정 반경(px)
        public float[] 수집미니게임속도 = { 0.9f, 1.3f, 1.8f };          // 오염 농도별 커서 왕복 속도
        public float[] 수집미니게임타겟반폭 = { 0.16f, 0.11f, 0.07f };  // 오염 농도별 성공 구간 반폭

        [Header("UI 피드백")]
        public float 팝업시간 = 0.22f;       // 팝업 등장 연출 시간(초)
        public float 팝업시작크기 = 0.86f;   // 팝업 시작 스케일
        public float 토스트표시시간 = 2f;    // 토스트 유지 시간(초)
        public float 토스트페이드시간 = 0.3f; // 토스트 페이드 시간(초)
        public float 노티스팁간격 = 5f;      // 상단 notice 게임 팁 변경 간격(초)

        [Header("홀로그램")]
        public float 홀로그램기본알파 = 0.88f;      // 홀로그램 기본 투명도
        public float 홀로그램깜빡임폭 = 0.16f;      // 홀로그램 미세 깜빡임 폭
        public float 홀로그램글리치기준 = 0.93f;    // 이 값 이상이면 강한 글리치

        [Header("인트로 대사(수면 최초 진입)")]
        [TextArea] public string[] 인트로대사 = {
            "2042년, 한강은 죽었다.",
            "오염 물질이 강바닥을 덮고, 물고기는 사라졌다.",
            "당신은 정화선의 마지막 잠수사.",
            "잠수정을 타고 내려가 강을 되살려라.",
        };

        [Header("클리어 대사(정화 완료)")]
        [TextArea] public string[] 클리어대사 = {
            "정화 부유체가 자리를 잡았다.",
            "탁한 물이 조금씩 맑아지기 시작한다.",
            "강은 아직 기억하고 있었다 — 흐르는 법을.",
        };
    }
}
