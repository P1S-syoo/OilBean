using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;
using Game.Player;
using Game.Stage;
using Game.UI;

namespace Game.Tests {
    // 클리어 통합 PlayMode 테스트(부유체 준비 → 스팟 설치 → 정화 → STAGE CLEAR)
    public class ClearFlowTests {
        GameObject sub;
        GameObject spot;
        GameObject cvGo;
        GameObject clearText;
        RunData run;

        [TearDown]
        public void Cleanup() {
            if (sub != null) Object.Destroy(sub);
            if (spot != null) Object.Destroy(spot);
            if (cvGo != null) Object.Destroy(cvGo);
            if (clearText != null) Object.Destroy(clearText);
            if (run != null) Object.Destroy(run);
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        [UnityTest]
        public IEnumerator BuoyReady_install_purifies_and_clears() {
            run = ScriptableObject.CreateInstance<RunData>();
            // 클리어 연출(카메라 없이 clearText 활성만 검증)
            clearText = new GameObject("ClearText");
            cvGo = new GameObject("ClearView");
            var cv = cvGo.AddComponent<ClearView>();
            SetField(cv, "clearText", clearText);
            // 정화 스팟
            spot = new GameObject("Spot");
            spot.AddComponent<BoxCollider2D>().isTrigger = true;
            var inst = spot.AddComponent<PurifyInstaller>();
            SetField(inst, "run", run);
            SetField(inst, "installTime", 0.2f);
            // 탐사 기계 + 코디네이터
            sub = new GameObject("Sub");
            sub.SetActive(false);
            var mover = sub.AddComponent<PlayerMove>();   // Rigidbody2D 자동
            sub.AddComponent<BoxCollider2D>().isTrigger = true;
            var gb = sub.AddComponent<GameBootstrap>();
            SetField(gb, "run", run);
            SetField(gb, "purify", inst);
            SetField(gb, "clearView", cv);
            SetField(gb, "mover", mover);
            SetField(gb, "sub", sub.transform);
            sub.SetActive(true);                  // Awake → ResetRun
            run.SetBuoyReady(true);               // Awake 이후 부유체 준비
            run.SetBuoyStage(3);                  // 부유체 Ⅲ — 설치 시 클리어(그 전 단계는 구역 정화)
            gb.StartDive();                       // Dive 진입
            sub.transform.position = new Vector3(0f, 10f, 0f);  // 수심 게이트 한계(50m=y7) 위
            spot.transform.position = sub.transform.position;   // 트리거 겹침
            yield return new WaitForFixedUpdate();
            // 설치(0.2s) → 정화 → OnPurified → Clear 전환 대기
            for (int i = 0; i < 120; i++) {
                if (gb.State == GameState.Clear) break;
                yield return null;
            }
            Assert.AreEqual(GameState.Clear, gb.State, "정화 완료 → STAGE CLEAR");
            Assert.AreEqual(1f, run.Purify, 0.001f, "정화 게이지 가득");
            Assert.IsFalse(run.BuoyReady, "부유체 소비됨");
            Assert.IsTrue(clearText.activeSelf, "ClearView.Play로 STAGE CLEAR 표시");
        }

        [UnityTest]
        public IEnumerator Buoy_stage_below_3_purifies_but_not_clears() {
            run = ScriptableObject.CreateInstance<RunData>();
            clearText = new GameObject("ClearText");
            cvGo = new GameObject("ClearView");
            var cv = cvGo.AddComponent<ClearView>();
            SetField(cv, "clearText", clearText);
            spot = new GameObject("Spot");
            spot.AddComponent<BoxCollider2D>().isTrigger = true;
            var inst = spot.AddComponent<PurifyInstaller>();
            SetField(inst, "run", run);
            SetField(inst, "installTime", 0.2f);
            sub = new GameObject("Sub");
            sub.SetActive(false);
            var mover = sub.AddComponent<PlayerMove>();
            sub.AddComponent<BoxCollider2D>().isTrigger = true;
            var gb = sub.AddComponent<GameBootstrap>();
            SetField(gb, "run", run);
            SetField(gb, "purify", inst);
            SetField(gb, "clearView", cv);
            SetField(gb, "mover", mover);
            SetField(gb, "sub", sub.transform);
            sub.SetActive(true);
            run.SetBuoyReady(true);
            run.SetBuoyStage(1);   // 부유체 Ⅰ — 구역 정화이되 클리어는 아님
            gb.StartDive();
            sub.transform.position = new Vector3(0f, 14f, 0f);   // 1단계 한계(35m=y12.1) 위
            spot.transform.position = sub.transform.position;
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < 120; i++) {
                if (run.Purify >= 1f) break;
                yield return null;
            }
            Assert.AreEqual(1f, run.Purify, 0.001f, "설치는 완료(구역 정화)");
            Assert.AreNotEqual(GameState.Clear, gb.State, "부유체 Ⅲ 전에는 클리어 아님");
        }

        [UnityTest]
        public IEnumerator ForceReturn_during_install_cancels_and_no_clear() {
            run = ScriptableObject.CreateInstance<RunData>();
            clearText = new GameObject("ClearText");
            cvGo = new GameObject("ClearView");
            var cv = cvGo.AddComponent<ClearView>();
            SetField(cv, "clearText", clearText);
            // 스팟은 거점(원점)과 분리해 강제복귀 시 트리거에서 빠지게
            spot = new GameObject("Spot");
            spot.transform.position = new Vector3(3f, 20f, 0f);   // 수심 게이트 한계(15m=y18.9) 위
            spot.AddComponent<BoxCollider2D>().isTrigger = true;
            var inst = spot.AddComponent<PurifyInstaller>();
            SetField(inst, "run", run);
            SetField(inst, "installTime", 0.5f);   // 설치 도중 개입할 시간 확보
            sub = new GameObject("Sub");
            sub.SetActive(false);
            var mover = sub.AddComponent<PlayerMove>();
            sub.AddComponent<BoxCollider2D>().isTrigger = true;
            var gb = sub.AddComponent<GameBootstrap>();
            SetField(gb, "run", run);
            SetField(gb, "purify", inst);
            SetField(gb, "clearView", cv);
            SetField(gb, "mover", mover);
            SetField(gb, "sub", sub.transform);
            sub.SetActive(true);                   // Awake → dockPos=원점, ResetRun
            run.SetBuoyReady(true);
            gb.StartDive();
            sub.transform.position = spot.transform.position;   // 스팟 진입
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.Greater(run.Purify, 0f, "설치 진행 중");
            gb.ForceReturn("테스트 강제복귀");      // 설치 도중 복귀 → CancelInstall + 거점 복귀
            yield return new WaitForFixedUpdate();  // 트리거 Exit 반영
            Assert.AreEqual(GameState.Dock, gb.State, "복귀 후 Dock 유지");
            Assert.AreEqual(0f, run.Purify, 0.001f, "설치 게이지 리셋");
            yield return new WaitForSeconds(0.7f);  // 원래 installTime 넘겨도
            Assert.AreNotEqual(GameState.Clear, gb.State, "백그라운드 클리어 없음");
        }
    }
}
