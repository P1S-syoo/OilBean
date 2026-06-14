using UnityEngine;

namespace Game.World {
    // 수심(미터) ↔ 게임 worldY 변환 — 엑셀 spawnY(0~-50)와 게임 수중 범위(수면 24 ~ 바닥 7) 매핑
    public static class DepthMap {
        public const float SurfaceY = 24f;   // 수면 worldY (= WorldGen.WaterY)
        public const float SeabedY = 7f;     // 바닥 평균 worldY (FloorTop 평균)
        public const float MaxDepthM = 50f;  // 기획 최대 수심(m)

        static float UnitPerMeter => (SurfaceY - SeabedY) / MaxDepthM;   // ≈0.34 u/m

        // 엑셀 수심 Y(0=수면 ~ -50=바닥, 음수) → 게임 worldY
        public static float ExcelToWorld(float excelY) {
            return SurfaceY + excelY * UnitPerMeter;
        }

        // 게임 worldY → 미터 수심(양수, 수면=0)
        public static float WorldToDepth(float worldY) {
            return (SurfaceY - worldY) / UnitPerMeter;
        }

        // 미터 수심(양수) → 게임 worldY
        public static float DepthToWorld(float meters) {
            return SurfaceY - meters * UnitPerMeter;
        }
    }
}
