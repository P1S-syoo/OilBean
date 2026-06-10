using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Tests {
    // GameBootstrap 코디네이터(강제 복귀 + Dock 리필) EditMode 테스트
    public class GameBootstrapTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) Object.DestroyImmediate(go);
        }

        // 주의: EditMode는 Awake/OnChanged 구독이 실행되지 않으므로 fsm 직접 로직만 검증.
        // 이벤트→복귀→Refill 통합은 PlayMode(ForcedReturnTests)에서 검증.
        [Test]
        public void ForceReturn_changes_to_dock() {
            go = new GameObject("Game");
            var gb = go.AddComponent<GameBootstrap>();
            gb.StartDive();
            Assert.AreEqual(GameState.Dive, gb.State);
            gb.ForceReturn("테스트");
            Assert.AreEqual(GameState.Dock, gb.State);
        }
    }
}
