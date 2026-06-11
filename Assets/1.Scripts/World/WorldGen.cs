using System.Collections.Generic;
using UnityEngine;

namespace Game.World {
    // 섹션 타입 — X축 구간별 지형 성격(통로/터널/개방)
    public enum SectionType {
        Corridor,   // 통로형: 위아래 구불구불 암벽 + 돌출
        Tunnel,     // 터널형: 평평한 지붕 + 매달린 돌출(다리 밑/구조물)
        Open        // 일반형: 위 트임, 바닥만(해수면까지 개방)
    }

    // 맵 데이터 정의(절차 생성) — 섹션 시퀀스 + 경계 블렌드 + 해수면 위 강변 백드롭. VARCO export도 이걸 샘플링
    public static class WorldGen {
        public const int MinY = -5, MaxY = 56;      // 수중 깊이 1배 환원(바닥 상향) — 해수면 위/X 무한은 유지
        public const int SectionW = 60;
        public const int OriginX = 0;
        public const int BlendW = 8;
        public const int WaterY = 24;               // 해수면(이 위로 못 올라감, 위는 공기/하늘)
        public const int SidewalkY = 32;            // 강변 옹벽 위 인도 높이

        // 섹션 순환 패턴(일반→터널→통로 무한 반복). 시작점(OriginX)이 [0]
        public static readonly SectionType[] Sequence = {
            SectionType.Open,
            SectionType.Tunnel,
            SectionType.Corridor
        };

        // VARCO export(blockmap.json) 샘플링 범위 — 맵 자체는 X 무한, 내보낼 때만 사용
        public static int MinX => OriginX - SectionW;
        public static int MaxX => OriginX + Sequence.Length * SectionW + SectionW;

        // Z레이어: 0=Background(+4), 1=Playfield(0), 2=Foreground(-1), 3=Shore(+10, 해수면 위 강변/빌딩)
        public static readonly float[] LayerZ = { 4f, 0f, -1f, 10f };

        public struct Spec {
            public int layer;
            public BlockRole role;
            public int variant;
        }

        // 섹션 전역 인덱스(무한) — X 방향으로 끝없이 증가/감소
        static int GlobalIndex(int x) {
            return Mathf.FloorToInt((float)(x - OriginX) / SectionW);
        }

        // 전역 인덱스를 시퀀스 길이로 순환(음수 안전)
        static int Cyc(int gi) {
            int n = Sequence.Length;
            return ((gi % n) + n) % n;
        }

        static int SeqIndex(int x) {
            return Cyc(GlobalIndex(x));
        }

        public static SectionType SectionAt(int x) => Sequence[SeqIndex(x)];

        // 수면(24) 기준 약 17칸 깊이 — 기존 42칸의 1/2.5로 환원(굴곡 진폭도 동일 배율)
        static float FloorTop(int x) => 7f + 1.2f * Mathf.Sin(x * 0.05f) + 0.6f * Mathf.Sin(x * 0.13f);

        static float CeilOf(SectionType s, int x) {
            switch (s) {
                case SectionType.Corridor: return 21f + 1.2f * Mathf.Sin(x * 0.06f);
                case SectionType.Tunnel:   return 21.5f;
                default:                    return MaxY + 6f;   // 개방=천장 없음(해수면까지 트임)
            }
        }

        static float CeilBot(int x) {
            // 전역 인덱스로 섹션 시작점을 잡고, 타입 조회만 순환 — 경계 블렌드가 순환 이음새에서도 이어짐
            int gi = GlobalIndex(x);
            float cur = CeilOf(Sequence[Cyc(gi)], x);
            float secStart = OriginX + gi * SectionW;
            float local = (x - secStart) / SectionW;
            if (local * SectionW < BlendW) {
                float prev = CeilOf(Sequence[Cyc(gi - 1)], x);
                float t = 0.5f + 0.5f * (local * SectionW / BlendW);
                return Mathf.Lerp(prev, cur, t);
            }
            if ((1f - local) * SectionW < BlendW) {
                float next = CeilOf(Sequence[Cyc(gi + 1)], x);
                float t = 0.5f + 0.5f * ((1f - local) * SectionW / BlendW);
                return Mathf.Lerp(next, cur, t);
            }
            return cur;
        }

        // 레이어 의존은 단방향(2→1→0)만 허용 — 역방향 참조 추가 시 무한 재귀
        public static bool IsSolid(int layer, int x, int y) {
            // X는 무한(섹션 순환), Y만 수심/하늘 한계로 제한
            if (y < MinY || y > MaxY) {
                return false;
            }
            if (layer == 3) {
                // Shore: 강변 백드롭 전체를 3D 스카이라인(SkylinePlacer)·명소·다리가 대체 — 블록 생성 안 함
                return false;
            }
            float floor = FloorTop(x);
            float ceil = CeilBot(x);
            var sec = SectionAt(x);
            if (layer == 1) {
                if (y <= floor) {
                    return true;
                }
                if (y >= ceil && y <= WaterY) {
                    return true;                  // 천장(해수면 아래만 — 위는 공기)
                }
                if (sec == SectionType.Tunnel) {
                    int local = ((x % 8) + 8) % 8;
                    if ((local == 0 || local == 1) && y <= ceil - 1f && y >= ceil - 2f) {
                        return true;
                    }
                }
                return false;
            }
            if (layer == 0) {
                if (y <= floor + 2f) {
                    return true;
                }
                if (ceil <= MaxY && y >= ceil - 2f && y <= WaterY) {
                    return true;
                }
                return false;
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
