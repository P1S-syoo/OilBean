using UnityEngine;

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

        [Header("미니게임 난이도")]
        public int 미니노드 = 5;            // 연구 미니게임 노드 수
        public float 미니판정 = 46f;        // 노드 통과 판정 반경(px)

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
