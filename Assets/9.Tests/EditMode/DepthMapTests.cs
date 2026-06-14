using NUnit.Framework;
using Game.World;

namespace Game.Tests {
    // DepthMap 수심↔worldY 매핑 EditMode 테스트 — 스폰·수심게이트 정합의 기준
    public class DepthMapTests {
        [Test]
        public void Surface_and_seabed_anchors() {
            // 수면(엑셀 0) = SurfaceY, 최대수심(엑셀 -50) = SeabedY
            Assert.AreEqual(DepthMap.SurfaceY, DepthMap.ExcelToWorld(0f), 0.01f);
            Assert.AreEqual(DepthMap.SeabedY, DepthMap.ExcelToWorld(-DepthMap.MaxDepthM), 0.01f);
        }

        [Test]
        public void Depth_roundtrip() {
            // 미터 수심 → worldY → 미터 수심 왕복 일치
            foreach (float m in new[] { 0f, 12f, 35f, 50f }) {
                float y = DepthMap.DepthToWorld(m);
                Assert.AreEqual(m, DepthMap.WorldToDepth(y), 0.01f, $"{m}m 왕복");
            }
        }

        [Test]
        public void Deeper_is_lower_worldY() {
            // 깊을수록 worldY가 작아야(아래)
            Assert.Greater(DepthMap.DepthToWorld(10f), DepthMap.DepthToWorld(40f));
        }
    }
}
