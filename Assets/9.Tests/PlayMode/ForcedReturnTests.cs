using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;
using Game.Player;
using Game.Items;

namespace Game.Tests {
    // 강제 복귀 통합 PlayMode 테스트(배터리 방전·오염원 충돌 → Dock 정산 → 수면 부상)
    public class ForcedReturnTests {
        GameObject sub;
        GameObject haz;

        [TearDown]
        public void Cleanup() {
            if (sub != null) Object.Destroy(sub);
            if (haz != null) Object.Destroy(haz);
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        [UnityTest]
        public IEnumerator Battery_empty_resurfaces_and_refills() {
            sub = new GameObject("Sub");
            sub.SetActive(false);
            var bat = sub.AddComponent<Battery>();
            // 리터럴 직접 주입 대신 config(SO)로 주입 — SetActive(true) 전에 설정해 Awake가 읽게 함
            var batCfg = ScriptableObject.CreateInstance<잠수설정>();
            batCfg.배터리최대 = 1f;
            batCfg.배터리소모 = 100f;
            SetField(bat, "config", batCfg);
            var gb = sub.AddComponent<GameBootstrap>();
            SetField(gb, "battery", bat);
            sub.SetActive(true);
            gb.StartDive();                          // 소모 시작
            for (int i = 0; i < 60; i++) {
                if (gb.State == GameState.Surface) break;
                yield return null;
            }
            // 방전 → Dock에서 배터리 충전·정산 후 수면(Surface)으로 부상(재잠수는 E)
            Assert.AreEqual(GameState.Surface, gb.State, "방전 시 정산 후 수면 부상");
            Assert.Greater(bat.Current, 0f, "복귀 시 재충전(이후 소모 off)");
        }

        [UnityTest]
        public IEnumerator Hazard_hit_resurfaces() {
            sub = new GameObject("Sub");
            sub.SetActive(false);
            var rb = sub.AddComponent<Rigidbody2D>(); rb.gravityScale = 0f;
            sub.AddComponent<BoxCollider2D>().isTrigger = true;
            var det = sub.AddComponent<HazardDetector>();
            var gb = sub.AddComponent<GameBootstrap>();
            SetField(gb, "hazard", det);
            sub.SetActive(true);
            gb.StartDive();

            haz = new GameObject("Hazard");
            haz.AddComponent<BoxCollider2D>().isTrigger = true;
            haz.AddComponent<Hazard>();              // points 없음 → 정지(충돌만)
            haz.transform.position = Vector3.zero;   // Sub와 겹침

            for (int i = 0; i < 30; i++) {
                if (gb.State == GameState.Surface) break;
                yield return new WaitForFixedUpdate();
            }
            // 충돌 → Dock 정산 후 수면 부상
            Assert.AreEqual(GameState.Surface, gb.State, "오염원 충돌 시 정산 후 수면 부상");
        }
    }
}
