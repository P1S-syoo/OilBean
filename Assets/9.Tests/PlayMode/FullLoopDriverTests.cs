using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Core;
using Game.Player;
using Game.Stage;
using Game.Craft;
using Game.UI;

namespace Game.Tests {
    // 전체 게임플레이 루프 무결성 PlayMode 테스트
    // 수집(시드)→연구(약품 Ⅰ/Ⅱ/Ⅲ 해금)→제작(부유체 1/2/3)→F홀드 설치 3회→Clear 가 끊김 없이 이어짐을 증명.
    // 미니게임 드래그는 별도 테스트 담당 — 여기선 Research.Analyze/Crafting.Craft 직접 호출 + holdOverride 설치 경로.
    public class FullLoopDriverTests {
        GameObject sub;
        GameObject spot;
        GameObject cvGo;
        GameObject clearText;
        GameObject researchGo;
        GameObject craftingGo;
        RunData run;

        [TearDown]
        public void Cleanup() {
            if (sub != null) Object.Destroy(sub);
            if (spot != null) Object.Destroy(spot);
            if (cvGo != null) Object.Destroy(cvGo);
            if (clearText != null) Object.Destroy(clearText);
            if (researchGo != null) Object.Destroy(researchGo);
            if (craftingGo != null) Object.Destroy(craftingGo);
            if (run != null) Object.Destroy(run);
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        // 스팟 진입 + 누적 타이머를 installTime 직전(임계값)으로 시드 — 다음 Update 한 프레임에서 즉시 완료됨.
        // installTime 값이 얼마든, deltaTime > 0인 한 첫 Update에서 t >= installTime 통과.
        // 이렇게 하면 실시간 경과를 기다릴 필요가 없어 도메인 리로드 부하와 완전히 무관해진다.
        static void ArmInstant(PurifyInstaller inst) {
            var installTime = (float)inst.GetType()
                .GetField("installTime", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inst);
            SetField(inst, "inside", true);
            // t를 installTime과 동일하게 시드 → 다음 Update: t += deltaTime → t > installTime → 즉시 완료
            SetField(inst, "t", installTime);
        }

        // 설치 완료(OnPurified) 대기 — 프레임 폴링 방식.
        // yield return null로 프레임을 하나씩 소비해 Update가 OnPurified를 발화할 기회를 준다.
        // 실시간 deadline(10s)은 진짜 데드락만 방어 — 정상 동작 시 1~2 프레임 이내 완료.
        IEnumerator WaitPurified(System.Func<int> count, int target) {
            // 첫 프레임: 도메인 리로드 직후 deltaTime 이상치를 1 프레임 흘려보냄
            yield return null;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (count() < target && Time.realtimeSinceStartup < deadline) {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FullLoop_seed_research_craft_install3x_reaches_clear() {
            run = ScriptableObject.CreateInstance<RunData>();

            // 클리어 연출(카메라 없이 clearText 활성만 검증)
            clearText = new GameObject("ClearText");
            cvGo = new GameObject("ClearView");
            var cv = cvGo.AddComponent<ClearView>();
            SetField(cv, "clearText", clearText);

            // 정화 설치 스팟 — installTime=1f(실제 대기 없음: ArmInstant가 t를 시드해 첫 프레임 완료)
            spot = new GameObject("Spot");
            spot.AddComponent<BoxCollider2D>().isTrigger = true;
            var inst = spot.AddComponent<PurifyInstaller>();
            SetField(inst, "run", run);
            SetField(inst, "installTime", 1f);   // 임의 값 — ArmInstant로 즉시 완료시키므로 실제 경과와 무관
            SetField(inst, "holdOverride", true);   // 키보드 없는 테스트에서 F홀드 대체
            int purifiedCount = 0;
            // 구독은 모든 시드/SetActive 전에 등록 — 이후 어떤 프레임에서 OnPurified가 발화해도 놓치지 않음
            inst.OnPurified += () => purifiedCount++;

            // 연구/제작(직접 호출 경로 — 미니게임 우회)
            researchGo = new GameObject("Research");
            var research = researchGo.AddComponent<Research>();
            SetField(research, "run", run);
            craftingGo = new GameObject("Crafting");
            var crafting = craftingGo.AddComponent<Crafting>();
            SetField(crafting, "run", run);

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
            SetField(gb, "research", research);
            SetField(gb, "crafting", crafting);
            sub.SetActive(true);   // Awake → ResetRun(모든 뱅크/해금 0) + purify.SetArmed(false)(Dock 상태)

            // ── 수집 시드 — 강재 3등급 + 오염수준3 샘플(연구·제작 재료) ──
            // 부유체 레시피: buoy_1=grade0 24kg / buoy_2=grade1 30kg / buoy_3=grade2 40kg
            run.AddSteel(0, 30f);
            run.AddSteel(1, 40f);
            run.AddSteel(2, 50f);
            // 약품 게이트: agent_mild need=3 / agent_mid need=8 / agent_strong need=15 + 최고수준 3 필요
            // 수준3 샘플 8개 → 분석 시 포인트 +3씩, 8회면 24p(≥15) + maxAnalyzedLevel=3
            for (int i = 0; i < 8; i++) {
                run.AddSampleAt(3);
            }

            // ── 연구 — 약품 Ⅰ/Ⅱ/Ⅲ 모두 해금(동기 — Update 불필요) ──
            for (int i = 0; i < 8 && !research.Done; i++) {
                Assert.IsTrue(research.Analyze(3), $"분석 {i + 1}회차 성공(수준3 샘플 소비)");
            }
            Assert.IsTrue(research.IsAgentUnlocked("agent_mild"), "약품 Ⅰ 해금");
            Assert.IsTrue(research.IsAgentUnlocked("agent_mid"), "약품 Ⅱ 해금");
            Assert.IsTrue(research.IsAgentUnlocked("agent_strong"), "약품 Ⅲ 해금");

            // ── 탐사 진입 → SetArmed(true) → Update가 설치를 처리 가능 상태 ──
            gb.StartDive();
            Assert.AreEqual(GameState.Dive, gb.State, "탐사 진입");
            // 첫 yield: 도메인 리로드 직후 이상치 deltaTime 흡수 + armed 상태 안정화
            yield return null;

            // ── 1단계: 부유체 Ⅰ 제작 → ArmInstant(inside+t 시드) → 다음 프레임 즉시 완료 ──
            Assert.IsTrue(crafting.Craft("buoy_1"), "부유체 Ⅰ 제작");
            Assert.AreEqual(1, run.BuoyStage, "부유체 단계 1");
            Assert.IsTrue(run.BuoyReady, "설치 대기");
            ArmInstant(inst);   // inside=true + t=installTime — 다음 Update에서 t+=deltaTime → 즉시 완료
            yield return WaitPurified(() => purifiedCount, 1);
            Assert.AreEqual(1, purifiedCount, "1단계 정화 완료 이벤트");
            Assert.IsFalse(run.BuoyReady, "1단계 부유체 소비됨");
            Assert.AreNotEqual(GameState.Clear, gb.State, "부유체 Ⅲ 전에는 클리어 아님(1단계)");

            // ── 2단계: ReArm이 inside/t를 초기화했으므로 ArmInstant 재시드 후 부유체 Ⅱ 제작 ──
            Assert.IsTrue(crafting.Craft("buoy_2"), "부유체 Ⅱ 제작");
            Assert.AreEqual(2, run.BuoyStage, "부유체 단계 2");
            ArmInstant(inst);
            yield return WaitPurified(() => purifiedCount, 2);
            Assert.AreEqual(2, purifiedCount, "2단계 정화 완료 이벤트");
            Assert.IsFalse(run.BuoyReady, "2단계 부유체 소비됨");
            Assert.AreNotEqual(GameState.Clear, gb.State, "부유체 Ⅲ 전에는 클리어 아님(2단계)");

            // ── 3단계: 부유체 Ⅲ 제작 → 설치 → BuoyStage=3 → OnPurified → Clear 전환 ──
            Assert.IsTrue(crafting.Craft("buoy_3"), "부유체 Ⅲ 제작");
            Assert.AreEqual(3, run.BuoyStage, "부유체 단계 3");
            ArmInstant(inst);
            yield return WaitPurified(() => purifiedCount, 3);
            Assert.AreEqual(3, purifiedCount, "3단계 정화 완료 이벤트");
            // FSM 전환(OnPurified → Fsm.Change(Clear))은 OnPurified와 동일 프레임 동기 처리됨 — 추가 대기 불필요
            Assert.AreEqual(GameState.Clear, gb.State, "부유체 Ⅲ 설치 → STAGE CLEAR");
            Assert.AreEqual(1f, run.Purify, 0.001f, "정화 게이지 가득");
            Assert.IsFalse(run.BuoyReady, "3단계 부유체 소비됨");
            Assert.IsTrue(clearText.activeSelf, "ClearView.Play로 STAGE CLEAR 표시");
        }

        [UnityTest]
        public IEnumerator ForfeitDive_loses_pending_but_CommitDive_keeps_all() {
            run = ScriptableObject.CreateInstance<RunData>();

            sub = new GameObject("Sub");
            sub.SetActive(false);
            var mover = sub.AddComponent<PlayerMove>();
            sub.AddComponent<BoxCollider2D>().isTrigger = true;
            var gb = sub.AddComponent<GameBootstrap>();
            SetField(gb, "run", run);
            SetField(gb, "mover", mover);
            SetField(gb, "sub", sub.transform);
            SetField(gb, "forfeitRatio", 0.5f);   // 결정적 손실 비율(절반)
            sub.SetActive(true);   // Awake → ResetRun

            // ── 강제복귀 정산: 미정착분의 forfeitRatio 만큼 거점 보유분에서 차감 ──
            gb.StartDive();
            Assert.AreEqual(GameState.Dive, gb.State, "탐사 진입");
            run.AddSteel(0, 10f);   // bank=10, pending=10
            run.AddSampleAt(1);
            run.AddSampleAt(1);     // 수준1 샘플 2개(bank=2, pending=2)
            gb.ForceReturn("테스트 강제복귀");   // Dive→Dock → OnReturnedToDock → ForfeitDive(0.5)
            yield return null;
            Assert.AreEqual(GameState.Dock, gb.State, "강제복귀 후 Dock");
            Assert.AreEqual(5f, run.GetSteel(0), 0.001f, "강재 미정착분 절반 유실(10→5)");
            Assert.AreEqual(1, run.GetSampleCount(1), "샘플 미정착분 절반 유실(2→1)");

            // ── 정상복귀 정산: 미정착분 전량 확정(손실 없음) ──
            gb.StartDive();
            Assert.AreEqual(GameState.Dive, gb.State, "재탐사 진입");
            run.AddSteel(0, 8f);   // bank=5+8=13, pending=8(직전 Forfeit가 pending 비움)
            run.AddSampleAt(2);    // 수준2 샘플 1개
            gb.ReturnDock();       // Dive→Dock(정상) → CommitDive(전량 확정)
            yield return null;
            Assert.AreEqual(GameState.Dock, gb.State, "정상복귀 후 Dock");
            Assert.AreEqual(13f, run.GetSteel(0), 0.001f, "정상복귀는 강재 손실 없음");
            Assert.AreEqual(1, run.GetSampleCount(2), "정상복귀는 샘플 손실 없음");
        }
    }
}
