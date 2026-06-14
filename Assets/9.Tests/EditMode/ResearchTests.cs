using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;
using Game.Craft;

namespace Game.Tests {
    // Research 2축(분석→포인트/최고수준, 약품 tier·게이트 해금) EditMode 테스트
    public class ResearchTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) {
                Object.DestroyImmediate(go);
            }
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        Research Make(RunData run) {
            go = new GameObject("Research");
            var r = go.AddComponent<Research>();
            SetField(r, "run", run);
            return r;
        }

        [Test]
        public void Analyze_accumulates_points_and_consumes_sample() {
            var run = ScriptableObject.CreateInstance<RunData>();
            run.AddSampleAt(2);   // 오염수준 2 샘플 1개
            var r = Make(run);
            Assert.IsTrue(r.Analyze(2));
            Assert.AreEqual(2, run.ResearchPoints, "분석포인트 = 오염수준");
            Assert.AreEqual(2, run.MaxAnalyzedLevel);
            Assert.AreEqual(0, run.GetSampleCount(2), "샘플 소비");
        }

        [Test]
        public void Analyze_fails_without_sample() {
            var run = ScriptableObject.CreateInstance<RunData>();
            var r = Make(run);
            Assert.IsFalse(r.Analyze(1));
        }

        [Test]
        public void Agent_mild_unlocks_at_3_points() {
            var run = ScriptableObject.CreateInstance<RunData>();
            for (int i = 0; i < 3; i++) {
                run.AddSampleAt(1);
            }
            var r = Make(run);
            r.Analyze(1);
            r.Analyze(1);
            Assert.IsFalse(r.IsAgentUnlocked("agent_mild"), "2pt에선 미해금");
            r.Analyze(1);
            Assert.IsTrue(r.IsAgentUnlocked("agent_mild"), "3pt에서 저농도 해금");
        }

        [Test]
        public void Agent_strong_gated_by_max_level() {
            var run = ScriptableObject.CreateInstance<RunData>();
            for (int i = 0; i < 15; i++) {
                run.AddSampleAt(1);   // Lv1만 15개 → 15pt, maxLevel 1
            }
            var r = Make(run);
            for (int i = 0; i < 15; i++) {
                r.Analyze(1);
            }
            Assert.AreEqual(15, run.ResearchPoints);
            Assert.IsTrue(r.IsAgentUnlocked("agent_mild"));
            Assert.IsTrue(r.IsAgentUnlocked("agent_mid"));
            Assert.IsFalse(r.IsAgentUnlocked("agent_strong"), "최고수준 1이라 고농도 잠김(minLevel 3)");
            // Lv3 샘플 분석 1회 → maxLevel 3 → 고농도 해금
            run.AddSampleAt(3);
            Assert.IsTrue(r.Analyze(3));
            Assert.AreEqual(3, run.MaxAnalyzedLevel);
            Assert.IsTrue(r.IsAgentUnlocked("agent_strong"), "Lv3 분석 후 고농도 해금");
        }

        [Test]
        public void OnUnlocked_fires_on_agent_unlock() {
            var run = ScriptableObject.CreateInstance<RunData>();
            for (int i = 0; i < 3; i++) {
                run.AddSampleAt(1);
            }
            var r = Make(run);
            int fired = 0;
            r.OnUnlocked += () => fired++;
            r.Analyze(1);
            r.Analyze(1);
            r.Analyze(1);
            Assert.GreaterOrEqual(fired, 1, "약품 해금 시 이벤트 발행");
        }

        [Test]
        public void Reset_clears_research() {
            var run = ScriptableObject.CreateInstance<RunData>();
            run.AddSampleAt(1);
            var r = Make(run);
            r.Analyze(1);
            run.ResetRun();
            Assert.AreEqual(0, run.ResearchPoints, "리셋 후 포인트 0");
            Assert.IsFalse(r.IsAgentUnlocked("agent_mild"));
        }
    }
}
