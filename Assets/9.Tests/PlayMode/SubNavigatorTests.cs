using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.TestTools;
using Game.Core;
using Game.Surface;

namespace Game.Tests {
    // SubNavigator 항해/도달/재출발 PlayMode 테스트 — 코드로 만든 직선 스플라인 사용
    public class SubNavigatorTests {
        GameObject riverGo;
        GameObject subGo;

        [TearDown]
        public void Cleanup() {
            if (subGo != null) {
                Object.Destroy(subGo);
            }
            if (riverGo != null) {
                Object.Destroy(riverGo);
            }
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        // 도착 조건 충족 또는 실시간 deadline까지 프레임 폴링 — 조건 도달 즉시 탈출.
        // 고정 프레임 상한(600) 대신 WaitForSecondsRealtime 기반 deadline으로 도메인 리로드 부하에 견고.
        static IEnumerator WaitUntil(System.Func<bool> cond, float timeout = 10f) {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < deadline) {
                yield return null;
            }
        }

        // x=0→100 직선 강 + 목표 {0.5, 1.0} 잠수정 생성
        SubNavigator Make(float speed) {
            riverGo = new GameObject("River");
            var container = riverGo.AddComponent<SplineContainer>();
            container.Spline.Add(new BezierKnot(new float3(0f, 24f, 0f)));
            container.Spline.Add(new BezierKnot(new float3(50f, 24f, 0f)));
            container.Spline.Add(new BezierKnot(new float3(100f, 24f, 0f)));

            subGo = new GameObject("Sub");
            subGo.SetActive(false);
            var nav = subGo.AddComponent<SubNavigator>();
            SetField(nav, "river", container);
            // 리터럴 직접 주입 대신 config(SO)로 주입 — SetActive(true) 전에 설정해 Awake가 읽게 함
            var c = ScriptableObject.CreateInstance<수면위설정>();
            c.항해속도 = speed;
            SetField(nav, "config", c);
            SetField(nav, "targets", new[] { 0.5f, 1f });
            subGo.SetActive(true);
            return nav;
        }

        [UnityTest]
        public IEnumerator Arrives_at_first_target_and_stops() {
            var nav = Make(120f);
            bool arrived = false;
            nav.OnArrived += () => arrived = true;
            yield return WaitUntil(() => arrived);
            Assert.IsTrue(arrived, "첫 목표 도달 시 OnArrived 발생");
            Assert.IsFalse(nav.Sailing, "도달 후 정지");
            Assert.AreEqual(50f, subGo.transform.position.x, 1.5f, "첫 목표(스플라인 50%) 부근 정지");
        }

        [UnityTest]
        public IEnumerator Resume_sails_to_next_target() {
            var nav = Make(120f);
            int arrivals = 0;
            nav.OnArrived += () => arrivals++;
            yield return WaitUntil(() => arrivals >= 1);
            nav.Resume();
            Assert.IsTrue(nav.Sailing, "재출발 후 항해 재개");
            yield return WaitUntil(() => arrivals >= 2);
            Assert.AreEqual(2, arrivals, "두 번째 목표 도달");
            Assert.AreEqual(100f, subGo.transform.position.x, 1.5f, "종점 부근 정지");
        }
    }
}
