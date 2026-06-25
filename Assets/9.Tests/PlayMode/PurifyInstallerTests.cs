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
            spot.SetActive(false);   // Awake가 config를 읽도록 비활성 생성 후 주입 → 활성화
            spot.AddComponent<BoxCollider2D>().isTrigger = true;
            var p = spot.AddComponent<PurifyInstaller>();
            SetField(p, "run", run);
            // 리터럴 직접 주입 대신 config(SO)로 주입 — SetActive(true) 전에 설정해 Awake가 읽게 함
            var c = ScriptableObject.CreateInstance<제작설정>();
            c.설치시간 = 0.2f;
            SetField(p, "config", c);
            SetField(p, "holdOverride", true);   // E6 hold-to-install — 키보드 없는 테스트에서 홀드 대체
            spot.SetActive(true);
        }

        // 설치 완료(OnPurified) 대기 — 이벤트 카운터 폴링 + 실시간 deadline.
        // 첫 프레임은 도메인 리로드 직후 deltaTime 이상치를 흘려보내고,
        // 이후 카운터가 target에 도달하거나 deadline을 넘길 때까지 프레임을 소비한다.
        // 정상 동작 시 installTime(0.2s) 경과 후 즉시 탈출 — deadline(10s)은 진짜 데드락만 방어.
        IEnumerator WaitForCount(System.Func<int> count, int target) {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (count() < target && Time.realtimeSinceStartup < deadline) {
                yield return null;
            }
        }

        // "설치가 일어나지 않음"(부정 검증) 대기 — installTime의 넉넉한 배수만큼 실시간 경과를 보장.
        // 그 사이 Update가 충분히 돌므로, 설치 조건이 충족됐다면 반드시 완료됐어야 한다.
        // 고정 WaitForSeconds(0.4s) 대신 실시간 deadline까지 프레임을 돌려 도메인 리로드 부하에 견고.
        IEnumerator GiveTimeForInstall(float installTime) {
            float deadline = Time.realtimeSinceStartup + installTime * 3f;
            while (Time.realtimeSinceStartup < deadline) {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Installs_and_purifies_when_buoy_ready() {
            Build(true);
            int purifiedCount = 0;
            spot.GetComponent<PurifyInstaller>().OnPurified += () => purifiedCount++;
            yield return WaitForCount(() => purifiedCount, 1);   // 설치 완료(OnPurified) 폴링
            Assert.AreEqual(1, purifiedCount, "정화 완료 이벤트 발생");
            Assert.AreEqual(1f, run.Purify, 0.001f);
            Assert.IsFalse(run.BuoyReady, "부유체 소비됨");
        }

        [UnityTest]
        public IEnumerator No_install_without_buoy() {
            Build(false);
            int purifiedCount = 0;
            spot.GetComponent<PurifyInstaller>().OnPurified += () => purifiedCount++;
            yield return GiveTimeForInstall(0.2f);   // installTime의 3배 실시간 경과 — 설치 기회 충분
            Assert.AreEqual(0, purifiedCount, "부유체 없으면 정화 완료 없음");
            Assert.AreEqual(0f, run.Purify, 0.001f, "부유체 없으면 설치 안 됨");
        }

        [UnityTest]
        public IEnumerator Installs_when_buoy_ready_after_enter() {
            Build(false);                                   // 부유체 없이 진입
            int purifiedCount = 0;
            spot.GetComponent<PurifyInstaller>().OnPurified += () => purifiedCount++;
            yield return new WaitForFixedUpdate();          // 진입(inside=true)
            yield return null;
            Assert.AreEqual(0, purifiedCount, "부유체 전엔 정화 없음");
            Assert.AreEqual(0f, run.Purify, 0.001f, "부유체 전엔 설치 안 됨");
            run.SetBuoyReady(true);                         // 진입 상태에서 제작 완료
            yield return WaitForCount(() => purifiedCount, 1);
            Assert.AreEqual(1, purifiedCount, "진입 후 부유체 준비돼도 설치 시작(enter-only 아님)");
        }

        [UnityTest]
        public IEnumerator No_reinstall_after_done() {
            Build(true);
            int count = 0;
            spot.GetComponent<PurifyInstaller>().OnPurified += () => count++;
            yield return WaitForCount(() => count, 1);      // 1회 완료 폴링
            Assert.AreEqual(1, count, "정화 1회 완료");
            run.SetBuoyReady(true);                         // 부유체 다시 준비해도
            yield return GiveTimeForInstall(0.2f);          // 재설치 기회를 충분히 줘도 done 래치로 차단
            Assert.AreEqual(1, count, "완료 후 재설치 없음");
        }
    }
}
