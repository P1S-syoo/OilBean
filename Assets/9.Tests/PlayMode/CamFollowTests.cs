using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;

namespace Game.Tests {
    // CamFollow 추적 PlayMode 스모크 테스트
    public class CamFollowTests {
        GameObject camGo;
        GameObject target;

        [TearDown]
        public void Cleanup() {
            if (camGo != null) {
                Object.Destroy(camGo);
            }
            if (target != null) {
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator Camera_approaches_target() {
            camGo = new GameObject("Cam");
            // zOffset 기본값(-20)과 같은 깊이에서 시작 — z 유지 검증이 수렴 속도에 의존하지 않게
            camGo.transform.position = new Vector3(0f, 0f, -20f);
            var cf = camGo.AddComponent<CamFollow>();
            target = new GameObject("Target");
            target.transform.position = new Vector3(10f, 0f, 0f);
            cf.SetTarget(target.transform);

            float startX = camGo.transform.position.x;
            for (int i = 0; i < 60; i++) {
                yield return null;   // 매 프레임 LateUpdate 누적
            }
            // 대상(x=10)으로 다가가되 z는 zOffset(-20) 유지
            Assert.Greater(camGo.transform.position.x, startX + 1f, "카메라가 대상 쪽으로 이동해야 함");
            Assert.AreEqual(-20f, camGo.transform.position.z, 0.01f, "2D 카메라 z 깊이 유지");
        }
    }
}
