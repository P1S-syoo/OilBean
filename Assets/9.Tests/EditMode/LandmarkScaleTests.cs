using NUnit.Framework;
using Game.Surface;

namespace Game.Tests.EditMode {
    // 명소 배치 스케일 표준 검증 — 캘리브레이션·위계 보존·상하한
    public class LandmarkScaleTests {
        [Test]
        public void 세빛섬_캘리브레이션_약12유닛() {
            float h = LandmarkScale.GameHeight(20f);
            Assert.That(h, Is.InRange(11f, 13f));
        }

        [Test]
        public void 실물이_크면_게임에서도_크다_위계보존() {
            float sebit = LandmarkScale.GameHeight(20f);    // 세빛섬
            float b63 = LandmarkScale.GameHeight(249f);     // 63빌딩
            Assert.Greater(b63, sebit);
        }

        [Test]
        public void 초고층은_상한에_캡() {
            float lotte = LandmarkScale.GameHeight(555f);   // 롯데타워 — 실물 비율이면 씬 파괴
            Assert.AreEqual(LandmarkScale.MaxHeight, lotte);
        }

        [Test]
        public void 비정상_입력은_하한으로_방어() {
            Assert.AreEqual(LandmarkScale.MinHeight, LandmarkScale.GameHeight(0f));
            Assert.AreEqual(LandmarkScale.MinHeight, LandmarkScale.GameHeight(-5f));
        }
    }
}
