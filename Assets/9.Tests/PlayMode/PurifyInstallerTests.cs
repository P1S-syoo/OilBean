using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;
using Game.Player;
using Game.Stage;

namespace Game.Tests {
    // PurifyInstaller 설치/정화 PlayMode 스모크 테스트
    public class PurifyInstallerTests {
        GameObject sub;
        GameObject spot;
        RunData run;

        [TearDown]
        public void Cleanup() {
            if (sub != null) Object.Destroy(sub);
            if (spot != null) Object.Destroy(spot);
            if (run != null) Object.Destroy(run);
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        void Build(bool buoyReady) {
            run = ScriptableObject.CreateInstance<RunData>();
            run.SetBuoyReady(buoyReady);
            sub = new GameObject("Sub");
            sub.AddComponent<PlayerMove>();                 // Rigidbody2D 자동
            sub.AddComponent<BoxCollider2D>().isTrigger = true;
            spot = new GameObject("Spot");
            spot.AddComponent<BoxCollider2D>().isTrigger = true;
            var p = spot.AddComponent<PurifyInstaller>();
            SetField(p, "run", run);
            SetField(p, "installTime", 0.2f);
            SetField(p, "holdOverride", true);   // E6 hold-to-install — 키보드 없는 테스트에서 홀드 대체
        }

        [UnityTest]
        public IEnumerator Installs_and_purifies_when_buoy_ready() {
            Build(true);
            bool purified = false;
            spot.GetComponent<PurifyInstaller>().OnPurified += () => purified = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.4f);          // 설치 시간 경과
            Assert.IsTrue(purified, "정화 완료 이벤트 발생");
            Assert.AreEqual(1f, run.Purify, 0.001f);
            Assert.IsFalse(run.BuoyReady, "부유체 소비됨");
        }

        [UnityTest]
        public IEnumerator No_install_without_buoy() {
            Build(false);
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, run.Purify, 0.001f, "부유체 없으면 설치 안 됨");
        }

        [UnityTest]
        public IEnumerator Installs_when_buoy_ready_after_enter() {
            Build(false);                                   // 부유체 없이 진입
            bool purified = false;
            spot.GetComponent<PurifyInstaller>().OnPurified += () => purified = true;
            yield return new WaitForFixedUpdate();          // 진입(inside=true)
            yield return null;
            Assert.AreEqual(0f, run.Purify, 0.001f, "부유체 전엔 설치 안 됨");
            run.SetBuoyReady(true);                         // 진입 상태에서 제작 완료
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(purified, "진입 후 부유체 준비돼도 설치 시작(enter-only 아님)");
        }

        [UnityTest]
        public IEnumerator No_reinstall_after_done() {
            Build(true);
            int count = 0;
            spot.GetComponent<PurifyInstaller>().OnPurified += () => count++;
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.4f);          // 1회 완료
            Assert.AreEqual(1, count, "정화 1회 완료");
            run.SetBuoyReady(true);                         // 부유체 다시 준비해도
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1, count, "완료 후 재설치 없음");
        }
    }
}
