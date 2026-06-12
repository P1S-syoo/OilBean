using System.Collections.Generic;
using NUnit.Framework;
using Game.World;

namespace Game.Tests {
    // WorldGen 무한 순환 맵 EditMode 테스트 — 순수 함수라 외부 의존 없음
    public class WorldGenTests {

        [Test]
        public void No_ceiling_blocks_above_floor() {
            // 섹션/천장 제거 — 바닥 위부터 해수면까지 전 구간이 비어 있어야(구 터널 x=60~120 구간 포함)
            for (int x = -200; x <= 200; x += 7) {
                for (int y = 12; y <= WorldGen.WaterY; y++) {
                    Assert.IsFalse(WorldGen.IsSolid(1, x, y), $"({x},{y}) Playfield 천장 블록 잔존");
                    Assert.IsFalse(WorldGen.IsSolid(0, x, y), $"({x},{y}) Background 천장 블록 잔존");
                }
            }
        }

        [Test]
        public void Floor_exists_far_from_origin() {
            // 바닥은 X 무한 — 원점에서 아주 먼 곳에도 생성
            Assert.IsTrue(WorldGen.IsSolid(1, 100000, 0), "x=+100000 바닥");
            Assert.IsTrue(WorldGen.IsSolid(1, -100000, 0), "x=-100000 바닥");
            Assert.IsFalse(WorldGen.IsSolid(1, 100000, WorldGen.MaxY + 1), "Y 상한 밖은 비어야");
            Assert.IsFalse(WorldGen.IsSolid(1, 100000, WorldGen.MinY - 1), "Y 하한 밖은 비어야");
        }

        [Test]
        public void Column_generates_specs_far_away() {
            // 청크 스트리머가 쓰는 Column도 먼 좌표에서 스펙 생성
            var specs = new List<WorldGen.Spec>();
            WorldGen.Column(54000, 0, specs);
            Assert.Greater(specs.Count, 0, "먼 좌표 바닥 칼럼에 블록 스펙이 있어야");
        }

        [Test]
        public void Generation_is_deterministic() {
            // 같은 좌표는 항상 같은 결과(시드 고정 절차 생성)
            var a = new List<WorldGen.Spec>();
            var b = new List<WorldGen.Spec>();
            WorldGen.Column(777, 0, a);
            WorldGen.Column(777, 0, b);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) {
                Assert.AreEqual(a[i].role, b[i].role);
                Assert.AreEqual(a[i].variant, b[i].variant);
            }
        }
    }
}
