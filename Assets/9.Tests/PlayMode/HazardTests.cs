using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Items;

namespace Game.Tests {
    // Hazard waypoint 순회 PlayMode 스모크 테스트
    public class HazardTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Patrols_toward_waypoint() {
            go = new GameObject("Hazard");
            go.transform.position = Vector3.zero;
            go.AddComponent<BoxCollider2D>();
            var h = go.AddComponent<Hazard>();
            typeof(Hazard).GetField("points", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(h, new Vector2[] { new Vector2(3f, 0f) });
            typeof(Hazard).GetField("speed", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(h, 5f);
            float startX = go.transform.position.x;
            yield return new WaitForSeconds(0.5f);   // 실시간 경과(Update는 deltaTime 기반)
            Assert.Greater(go.transform.position.x, startX + 0.5f, "경유점(+x)으로 이동해야");
        }
    }
}
