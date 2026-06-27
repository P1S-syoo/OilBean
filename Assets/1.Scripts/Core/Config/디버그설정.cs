using UnityEngine;

namespace Game.Core {
    // 디버그 설정 — 플레이테스트용 초기 자원·진행 보정
    [CreateAssetMenu(fileName = "디버그설정", menuName = "게임설정/디버그설정")]
    public class 디버그설정 : ScriptableObject {

        static 디버그설정 _기본;
        // 미연결 폴백용 기본값 — 실수로 출시 치트가 켜지지 않게 기본 비활성
        public static 디버그설정 기본 => _기본 != null ? _기본 : (_기본 = CreateInstance<디버그설정>());

        [Header("초기 재료")]
        public bool 초기재료지급 = false;     // 켜면 ResetRun 직후 아래 재료를 지급
        public float 일반강재 = 80f;          // 부유체 Ⅰ·적재 확장 테스트용
        public float 합금강재 = 60f;          // 부유체 Ⅱ·배터리 증설 테스트용
        public float 특수강재 = 60f;          // 부유체 Ⅲ·내압 프레임 테스트용

        [Header("초기 샘플")]
        public int 저농도샘플 = 4;            // 약품Ⅰ 연구 테스트용
        public int 중농도샘플 = 4;            // 약품Ⅱ 연구 테스트용
        public int 고농도샘플 = 4;            // 약품Ⅲ 연구 테스트용

        [Header("초기 연구")]
        public bool 약품전체해금 = false;     // 제작 테스트만 빠르게 볼 때 사용
    }
}
