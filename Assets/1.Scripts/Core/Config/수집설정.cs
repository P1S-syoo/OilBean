using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Core {
    // 수집(수집물 스폰·적재) 설정
    [CreateAssetMenu(fileName = "수집설정", menuName = "게임설정/수집설정")]
    public class 수집설정 : ScriptableObject {

        static 수집설정 _기본;
        // 미연결 폴백용 기본값 인스턴스 — 숫자의 단일 출처는 이 클래스의 필드 초기값
        public static 수집설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<수집설정>());

        [Header("수집 판정")]
        public float 수집반경 = 1.5f;       // 플레이어 수집 감지 반경
        public float 트리거반경 = 1f;       // 수집물 트리거 월드 반경(m)

        [Header("스폰 기본")]
        [FormerlySerializedAs("셀폭")]
        [FormerlySerializedAs("스폰셀폭")]
        public float 수집물생성간격 = 6f;       // 수집물 생성 간격(u)
        [FormerlySerializedAs("로드반경")]
        [FormerlySerializedAs("스폰로드반경")]
        public int 수집물로드반경 = 6;          // 플레이어 주변 수집물 로드 반경(칸)
        [FormerlySerializedAs("깊이분산")]
        [FormerlySerializedAs("수집물깊이분산")]
        public float 수집물앞뒤흩어짐 = 3f;     // 수집물 앞뒤 시각 분산 폭

        [Header("군집(난파)")]
        public float 군집간격 = 60f;        // 난파 군집 주기(u)
        public float 군집반경 = 13f;        // 군집 밀집 반경(u)
        [FormerlySerializedAs("군집슬롯")]
        public int 군집수집물칸수 = 5;      // 군집 한 칸에 놓을 수집물 후보 수
        [FormerlySerializedAs("군집밀도")]
        public int 군집생성확률 = 90;       // 군집 수집물 생성 확률(%)
        [FormerlySerializedAs("다리기준")]
        [FormerlySerializedAs("양화대교기준X")]
        public float 정화구역중심X = 38f;        // 정화구역 중심 X 좌표(BridgePlacer와 동일해야 함)

        [Header("개방 수역")]
        [FormerlySerializedAs("개방슬롯")]
        public int 일반수집물칸수 = 1;      // 일반 수역 한 칸에 놓을 수집물 후보 수
        [FormerlySerializedAs("개방밀도")]
        public int 일반생성확률 = 26;       // 일반 수역 수집물 생성 확률(%)

        [Header("적재")]
        public float 최대적재 = 70f;        // 최대 적재 무게(kg)
        public float 최소수집무게 = 5f;     // 적재 여유 경고에 쓰는 최소 수집물 무게
        public float 손실비율 = 0.35f;      // 강제 복귀 시 미정착분 손실 비율
    }
}
