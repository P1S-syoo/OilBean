using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;
using Game.Items;

namespace Game.Tests {
    // Collector 수집/적재한계 PlayMode 스모크 테스트
    public class CollectorTests {
        GameObject subGo;
        GameObject pickGo;
        RunData run;

        [TearDown]
        public void Cleanup() {
            if (subGo != null) Object.Destroy(subGo);
            if (pickGo != null) Object.Destroy(pickGo);
            if (run != null) Object.Destroy(run);
        }

        // private [SerializeField] run 주입
        static void Inject(Collector c, RunData r) {
            typeof(Collector).GetField("run", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c, r);
        }

        Collector MakeCollector() {
            subGo = new GameObject("Sub");
            var rb = subGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            subGo.AddComponent<BoxCollider2D>().isTrigger = true;
            var c = subGo.AddComponent<Collector>();
            run = ScriptableObject.CreateInstance<RunData>();
            Inject(c, run);
            return c;
        }

        Pickup MakePickup(ResourceKind kind, float w) {
            pickGo = new GameObject("Pickup");
            pickGo.AddComponent<BoxCollider2D>().isTrigger = true;
            var p = pickGo.AddComponent<Pickup>();
            typeof(Pickup).GetField("kind", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(p, kind);
            typeof(Pickup).GetField("weight", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(p, w);
            return p;
        }

        [UnityTest]
        public IEnumerator Collects_pickup_in_range() {
            var c = MakeCollector();
            MakePickup(ResourceKind.Scrap, 5f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();   // 트리거 감지
            bool ok = c.TryCollectNearest();
            Assert.IsTrue(ok, "근접 수집물 획득 성공");
            Assert.AreEqual(5f, run.Weight, 0.001f);
            Assert.AreEqual(1, run.ScrapCount);
        }

        [UnityTest]
        public IEnumerator Rejects_when_full() {
            var c = MakeCollector();
            MakePickup(ResourceKind.Scrap, 999f);   // maxWeight(50) 초과
            bool fullFired = false;
            bool collectFired = false;
            c.OnFull += () => fullFired = true;
            c.OnCollect += _ => collectFired = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            bool ok = c.TryCollectNearest();
            Assert.IsFalse(ok, "한계 초과 시 획득 실패");
            Assert.IsTrue(fullFired, "OnFull 이벤트 발생");
            Assert.IsFalse(collectFired, "실패 시 OnCollect 미발화");
            Assert.IsNotNull(pickGo, "실패 시 수집물 유지(파괴 안 됨)");
            Assert.AreEqual(0f, run.Weight, 0.001f);
        }
    }
}
