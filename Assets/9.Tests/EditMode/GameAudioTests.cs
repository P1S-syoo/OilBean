using NUnit.Framework;
using Game.Audio;
using Game.Core;

namespace Game.Tests.EditMode {
    // 상태 → BGM 매핑 규칙 검증
    public class GameAudioTests {
        [Test]
        public void 탐사_중에만_수중_BGM() {
            Assert.AreEqual(GameAudio.BgmKind.Dive, GameAudio.BgmFor(GameState.Dive));
        }

        [Test]
        public void 비탐사_상태는_전부_수상_BGM() {
            Assert.AreEqual(GameAudio.BgmKind.Surface, GameAudio.BgmFor(GameState.Dock));
            Assert.AreEqual(GameAudio.BgmKind.Surface, GameAudio.BgmFor(GameState.Surface));
            Assert.AreEqual(GameAudio.BgmKind.Surface, GameAudio.BgmFor(GameState.Research));
            Assert.AreEqual(GameAudio.BgmKind.Surface, GameAudio.BgmFor(GameState.Craft));
            Assert.AreEqual(GameAudio.BgmKind.Surface, GameAudio.BgmFor(GameState.Purify));
            Assert.AreEqual(GameAudio.BgmKind.Surface, GameAudio.BgmFor(GameState.Clear));
        }
    }
}
