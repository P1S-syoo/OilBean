using System.Collections.Generic;
using NUnit.Framework;
using Game.World;

namespace Game.Tests {
    // WorldGen 무한 순환 맵 EditMode 테스트 — 순수 함수라 외부 의존 없음
    public class WorldGenTests {

        [Test]
        public void Sections_cycle_infinitely() {
            // 시퀀스(일반→터널→통로)가 X 양방향으로 무한 순환
            Assert.AreEqual(SectionType.Open, WorldGen.SectionAt(0), "시작 섹션은 Open");
            Assert.AreEqual(SectionType.Tunnel, WorldGen.SectionAt(60));
            Assert.AreEqual(SectionType.Corridor, WorldGen.SectionAt(120));
            Assert.AreEqual(SectionType.Open, WorldGen.SectionAt(180), "한 바퀴 돌아 다시 Open");
            Assert.AreEqual(SectionType.Corridor, WorldGen.SectionAt(-60), "음수 방향도 순환(왼쪽 이웃=마지막 타입)");
            Assert.AreEqual(SectionType.Open, WorldGen.SectionAt(100000 - (100000 % 180)), "먼 좌표도 주기 일치");
        }

        [Test]
        public void Floor_exists_far_from_origin() {
            // 바닥은 X 무한 — 원점에서 아주 먼 곳에도 생성
            Assert.IsTrue(WorldGen.IsSolid(1, 100000, -30), "x=+100000 바닥");
            Assert.IsTrue(WorldGen.IsSolid(1, -100000, -30), "x=-100000 바닥");
            Assert.IsFalse(WorldGen.IsSolid(1, 100000, WorldGen.MaxY + 1), "Y 상한 밖은 비어야");
            Assert.IsFalse(WorldGen.IsSolid(1, 100000, WorldGen.MinY - 1), "Y 하한 밖은 비어야");
        }

        [Test]
        public void Column_generates_specs_far_away() {
            // 청크 스트리머가 쓰는 Column도 먼 좌표에서 스펙 생성
            var specs = new List<WorldGen.Spec>();
            WorldGen.Column(54000, -25, specs);
            Assert.Greater(specs.Count, 0, "먼 좌표 바닥 칼럼에 블록 스펙이 있어야");
        }

        [Test]
        public void Generation_is_deterministic() {
            // 같은 좌표는 항상 같은 결과(시드 고정 절차 생성)
            var a = new List<WorldGen.Spec>();
            var b = new List<WorldGen.Spec>();
            WorldGen.Column(777, -20, a);
            WorldGen.Column(777, -20, b);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) {
                Assert.AreEqual(a[i].role, b[i].role);
                Assert.AreEqual(a[i].variant, b[i].variant);
            }
        }
    }
}
