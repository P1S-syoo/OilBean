using UnityEngine;

namespace Game.Surface {
    // 실물 명소 높이(m)를 게임 스카이라인에 맞게 압축하는 배치 표준
    // 원칙: 실물 간 위계는 유지(큰 건물이 크게 보임)하되 멱함수로 압축해 씬 한계(MaxY=56)를 넘지 않게 한다
    public static class LandmarkScale {
        public const float Exponent = 0.5f;      // 압축 지수 — 제곱근 곡선(고층일수록 강하게 압축)
        public const float Calibration = 2.7f;   // 세빛섬(실물 20m) ≈ 12u 가 되도록 캘리브레이션
        public const float MaxHeight = 45f;      // 상한 — 안개권/카메라 구도 보호 (롯데타워급 캡)
        public const float MinHeight = 3f;       // 하한 — 너무 작아 안 보이는 소품 방지

        // 실물 높이(m) → 게임 높이(유닛). 예: 세빛섬 20m→12u, 63빌딩 249m→42.6u, 롯데타워 555m→45u(캡)
        public static float GameHeight(float realHeightM) {
            if (realHeightM <= 0f) {
                return MinHeight;
            }
            float h = Calibration * Mathf.Pow(realHeightM, Exponent);
            return Mathf.Clamp(h, MinHeight, MaxHeight);
        }
    }
}
