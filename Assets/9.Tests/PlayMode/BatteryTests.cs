using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;
using Game.Player;

namespace Game.Tests {
    // Battery 소모/고갈 PlayMode 스모크 테스트
    public class BatteryTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) Object.Destroy(go);
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        // 활성화 전에 필드 주입 → OnEnable이 새 값으로 충전되도록 비활성 생성
        Battery Make(float max, float drain) {
            go = new GameObject("Sub");
            go.SetActive(false);
            var b = go.AddComponent<Battery>();
            // 리터럴 직접 주입 대신 config(SO)로 주입 — SetActive(true) 전에 설정해 Awake가 읽게 함
            var c = ScriptableObject.CreateInstance<잠수설정>();
            c.배터리최대 = max;
            c.배터리소모 = drain;
            SetField(b, "config", c);
            go.SetActive(true);
            b.SetDraining(true);   // 기본값이 비소모로 바뀌어 테스트는 명시적으로 소모 활성화
            return b;
        }

        [UnityTest]
        public IEnumerator Drains_over_time() {
            var b = Make(100f, 50f);
            float start = b.Current;
            for (int i = 0; i < 10; i++) {
                yield return null;
            }
            Assert.Less(b.Current, start, "시간이 지나면 배터리가 소모돼야");
        }

        [UnityTest]
        public IEnumerator Empty_fires_and_refill_restores() {
            var b = Make(1f, 100f);   // 빠르게 방전
            bool emptied = false;
            b.OnEmpty += () => emptied = true;
            for (int i = 0; i < 30; i++) {
                yield return null;
            }
            Assert.IsTrue(emptied, "방전 시 OnEmpty 발생");
            Assert.AreEqual(0f, b.Current, 0.001f);
            b.Refill();
            Assert.Greater(b.Current, 0f, "Refill로 재충전");
        }
    }
}
