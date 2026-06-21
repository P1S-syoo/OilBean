using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.UI;

namespace Game.Tests {
    // 오염체 수집 미니게임(스타포스) 판정 PlayMode 테스트
    public class CollectMinigameTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) Object.Destroy(go);
        }

        static void SetField(object o, string name, object val) {
            typeof(CollectMinigame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, val);
        }
        static float GetFloat(object o, string name) {
            return (float)typeof(CollectMinigame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
        }

        [UnityTest]
        public IEnumerator Judge_at_target_center_hits() {
            go = new GameObject("Mg");
            var mg = go.AddComponent<CollectMinigame>();
            bool? result = null;
            mg.StartGame(0, h => result = h);
            yield return null;
            // 커서를 타겟 중심에 맞춰 명중 강제
            SetField(mg, "t", GetFloat(mg, "targetCenter"));
            mg.Judge();
            Assert.IsTrue(result.HasValue && result.Value, "타겟 중심 멈춤은 명중");
            Assert.IsFalse(mg.Active, "판정 후 비활성");
        }

        [UnityTest]
        public IEnumerator Judge_far_from_target_misses() {
            go = new GameObject("Mg2");
            var mg = go.AddComponent<CollectMinigame>();
            bool? result = null;
            mg.StartGame(2, h => result = h);   // Lv3 — 좁은 타겟
            yield return null;
            float tc = GetFloat(mg, "targetCenter");
            float far = tc > 0.5f ? 0.02f : 0.98f;   // 타겟 반대편 끝
            SetField(mg, "t", far);
            mg.Judge();
            Assert.IsTrue(result.HasValue && !result.Value, "먼 위치 멈춤은 실패");
        }
    }
}
