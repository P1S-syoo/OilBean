using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Core;
using Game.Craft;

namespace Game.Tests {
    // Research 분석→해금 + RunData 샘플 소비 EditMode 테스트
    public class ResearchTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) Object.DestroyImmediate(go);
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        Research Make(RunData run, int need) {
            go = new GameObject("Research");
            var r = go.AddComponent<Research>();
            SetField(r, "run", run);
            SetField(r, "recipeId", "purifier");
            SetField(r, "needSamples", need);
            SetField(r, "sampleUnitWeight", 6f);
            return r;
        }

        [Test]
        public void Consume_sample_reduces_count_and_weight() {
            var run = ScriptableObject.CreateInstance<RunData>();
            run.TryAdd(ResourceKind.Sample, 6f);
            Assert.IsTrue(run.TryConsumeSample(6f));
            Assert.AreEqual(0, run.SampleCount);
            Assert.AreEqual(0f, run.Weight, 0.001f);
        }

        [Test]
        public void Consume_fails_when_no_sample() {
            var run = ScriptableObject.CreateInstance<RunData>();
            Assert.IsFalse(run.TryConsumeSample(6f));
        }

        [Test]
        public void Analyze_unlocks_after_required_samples() {
            var run = ScriptableObject.CreateInstance<RunData>();
            run.TryAdd(ResourceKind.Sample, 6f);
            run.TryAdd(ResourceKind.Sample, 6f);
            var r = Make(run, 2);
            Assert.IsTrue(r.Analyze());          // 1/2
            Assert.IsFalse(r.Done);
            Assert.IsTrue(r.Analyze());          // 2/2 → 해금
            Assert.IsTrue(r.Done);
            Assert.IsTrue(run.IsUnlocked("purifier"));
            Assert.AreEqual(1f, r.Progress, 0.001f);
        }

        [Test]
        public void Analyze_fails_without_samples() {
            var run = ScriptableObject.CreateInstance<RunData>();
            var r = Make(run, 2);
            Assert.IsFalse(r.Analyze());         // 샘플 0 → 실패
            Assert.IsFalse(r.Done);
        }

        [Test]
        public void Reset_clears_progress_no_insta_unlock() {
            var run = ScriptableObject.CreateInstance<RunData>();
            run.TryAdd(ResourceKind.Sample, 6f);
            var r = Make(run, 2);
            r.Analyze();                          // 진행 1/2 (해금 전)
            run.ResetRun();                       // 새 판 — 진행도/해금 초기화
            Assert.AreEqual(0f, r.Progress, 0.001f, "리셋 후 진행도 0");
            run.TryAdd(ResourceKind.Sample, 6f);
            Assert.IsTrue(r.Analyze());           // 1/2 — 아직 해금 안 됨(누수 없음)
            Assert.IsFalse(r.Done, "샘플 1개로 즉시 해금되면 안 됨");
        }

        [Test]
        public void Unlock_event_fires_once() {
            var run = ScriptableObject.CreateInstance<RunData>();
            run.TryAdd(ResourceKind.Sample, 6f);
            var r = Make(run, 1);
            int fired = 0;
            r.OnUnlocked += () => fired++;
            r.Analyze();
            Assert.AreEqual(1, fired);
        }
    }
}
