using UnityEngine;

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
        public float 셀폭 = 6f;             // 스폰 셀 폭(u)
        public int 로드반경 = 6;            // 좌우 로드 반경(셀)
        public float 깊이분산 = 3f;         // z축 시각 분산 폭

        [Header("군집(난파)")]
        public float 군집간격 = 60f;        // 난파 군집 주기(u)
        public float 군집반경 = 13f;        // 군집 밀집 반경(u)
        public int 군집슬롯 = 5;            // 군집 셀 슬롯 수
        public int 군집밀도 = 90;           // 군집 채움 확률(%)
        public float 다리기준 = 38f;        // 양화대교 잔해 X 기준점 (CollectibleStreamer.bridgeAnchorX / BridgePlacer와 동일해야 함)

        [Header("개방 수역")]
        public int 개방슬롯 = 1;            // 개방 수역 셀 슬롯 수
        public int 개방밀도 = 26;           // 개방 수역 채움 확률(%)

        [Header("적재")]
        public float 최대적재 = 70f;        // 최대 적재 무게(kg)
        public float 손실비율 = 0.35f;      // 강제 복귀 시 미정착분 손실 비율
    }
}
