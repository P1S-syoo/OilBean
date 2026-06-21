using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Items;

namespace Game.Tests {
    // Hazard 돌진 공격 PlayMode 스모크 테스트 — 경고 후 횡단 이동
    public class HazardTests {
        GameObject go;

        [TearDown]
        public void Cleanup() {
            if (go != null) Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Dashes_across_after_warning() {
            go = new GameObject("Hazard");
            go.transform.position = Vector3.zero;
            go.AddComponent<BoxCollider2D>();
            var h = go.AddComponent<Hazard>();
            // 좌(0)→우(10) 돌진, 경고 0.2s, 속도 10
            h.Setup(0f, 0f, 10f, 1f, 0.2f, 10f);
            float startX = go.transform.position.x;
            // 경고(0.2) 동안엔 정지 유지
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(startX, go.transform.position.x, 0.01f, "경고 중엔 정지해야");
            // 경고 종료 후 돌진 — +x로 이동
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(go.transform.position.x, startX + 1f, "경고 후 +x로 돌진해야");
        }
    }
}
