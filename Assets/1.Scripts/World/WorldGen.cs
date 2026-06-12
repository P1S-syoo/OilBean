using System.Collections.Generic;
using UnityEngine;

namespace Game.World {
    // 맵 데이터 정의(절차 생성) — 바닥 지형만 생성(섹션/천장 테마 제거, 해수면까지 전부 개방). X 무한
    public static class WorldGen {
        public const int MinY = -5, MaxY = 56;      // 수중 깊이 1배 환원(바닥 상향) — 해수면 위/X 무한은 유지
        public const int WaterY = 24;               // 해수면(이 위로 못 올라감, 위는 공기/하늘)
        public const int SidewalkY = 32;            // 강변 옹벽 위 인도 높이

        // Z레이어: 0=Background(+4), 1=Playfield(0), 2=Foreground(-1), 3=Shore(+10, 해수면 위 강변/빌딩)
        public static readonly float[] LayerZ = { 4f, 0f, -1f, 10f };

        public struct Spec {
            public int layer;
            public BlockRole role;
            public int variant;
        }

        // 수면(24) 기준 약 17칸 깊이 — 기존 42칸의 1/2.5로 환원(굴곡 진폭도 동일 배율)
        static float FloorTop(int x) => 7f + 1.2f * Mathf.Sin(x * 0.05f) + 0.6f * Mathf.Sin(x * 0.13f);

        // 레이어 의존은 단방향(2→1→0)만 허용 — 역방향 참조 추가 시 무한 재귀
        public static bool IsSolid(int layer, int x, int y) {
            // X는 무한, Y만 수심/하늘 한계로 제한
            if (y < MinY || y > MaxY) {
                return false;
            }
            if (layer == 3) {
                // Shore: 강변 백드롭 전체를 3D 스카이라인(SkylinePlacer)·명소·다리가 대체 — 블록 생성 안 함
                return false;
            }
            float floor = FloorTop(x);
            if (layer == 1) {
                return y <= floor;          // 바닥만 — 위는 해수면까지 전부 개방
            }
            if (layer == 0) {
                return y <= floor + 2f;
            }
            // Foreground
            if (!IsSolid(1, x, y) || !Exposed(x, y)) {
                return false;
            }
            return Hash(x >> 1, y >> 1) % 5 == 0;
        }

        public static bool Exposed(int x, int y) {
            if (!IsSolid(1, x, y)) {
                return false;
            }
            return !IsSolid(1, x + 1, y) || !IsSolid(1, x - 1, y) || !IsSolid(1, x, y + 1) || !IsSolid(1, x, y - 1);
        }

        static int Hash(int a, int b) {
            int h = a * 73856093 ^ b * 19349663;
            return (h % 1000 + 1000) % 1000;
        }

        public static void Column(int x, int y, List<Spec> outList) {
            for (int layer = 0; layer < 4; layer++) {
                if (!IsSolid(layer, x, y)) {
                    continue;
                }
                BlockRole role;
                if (layer == 3) {
                    // Shore 역할: 인도=Top, 옹벽/강변=Slope (빌딩 블록은 3D 명소로 대체)
                    role = y == SidewalkY ? BlockRole.Top : BlockRole.Slope;
                } else if (layer == 2) {
                    role = BlockRole.Wall;
                } else {
                    bool up = IsSolid(layer, x, y + 1);
                    bool l = IsSolid(layer, x - 1, y);
                    bool r = IsSolid(layer, x + 1, y);
                    bool eTop = !up, eSide = (!l || !r);
                    role = (eTop && eSide) ? BlockRole.Corner : eTop ? BlockRole.Top : BlockRole.Wall;
                }
                int variant = Hash(x, y) % 3;
                outList.Add(new Spec { layer = layer, role = role, variant = variant });
            }
        }
    }
}
