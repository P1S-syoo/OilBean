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
    // 수상 흐름 통합 테스트 — 항해→목표 도달→잠수 요청→Dock 인계·미정화 구역 유지
    public class SurfaceFlowTests {
        GameObject rig;
        GameObject gameGo;
        RunData run;

        [TearDown]
        public void Cleanup() {
            if (rig != null) {
                Object.Destroy(rig);
            }
            if (gameGo != null) {
                Object.Destroy(gameGo);
            }
        }

        static void SetField(object o, string name, object v) {
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
        }

        // 최소 리그: GameBootstrap + 스플라인 항해 잠수정 + SurfaceBootstrap (onSurface=false면 Dock 시작)
        SurfaceBootstrap Make(out SubNavigator nav, bool onSurface = true) {
            run = ScriptableObject.CreateInstance<RunData>();
            gameGo = new GameObject("Game");
            gameGo.SetActive(false);
            var game = gameGo.AddComponent<GameBootstrap>();
            SetField(game, "run", run);
            SetField(game, "startOnSurface", onSurface);
            gameGo.SetActive(true);

            rig = new GameObject("SurfaceRig");
            rig.SetActive(false);

            var riverGo = new GameObject("River");
            riverGo.transform.SetParent(rig.transform, false);
            var container = riverGo.AddComponent<SplineContainer>();
            container.Spline.Add(new BezierKnot(new float3(0f, 24f, 0f)));
            container.Spline.Add(new BezierKnot(new float3(60f, 24f, 0f)));

            var subGo = new GameObject("Sub3D");
            subGo.transform.SetParent(rig.transform, false);
            nav = subGo.AddComponent<SubNavigator>();
            SetField(nav, "river", container);
            // 리터럴 직접 주입 대신 config(SO)로 주입 — rig.SetActive(true) 전에 설정해 Awake가 읽게 함
            var navCfg = ScriptableObject.CreateInstance<수면위설정>();
            navCfg.항해속도 = 200f;
            SetField(nav, "config", navCfg);
            SetField(nav, "targets", new[] { 0.9f });

            var boot = rig.AddComponent<SurfaceBootstrap>();
            SetField(boot, "game", game);
            SetField(boot, "nav", nav);
            // boot는 별도 config 인스턴스 — 잠수전환시간만 테스트값으로
            var bootCfg = ScriptableObject.CreateInstance<수면위설정>();
            bootCfg.잠수전환시간 = 0.05f;
            SetField(boot, "config", bootCfg);
            rig.SetActive(true);
            return boot;
        }

        [UnityTest]
        public IEnumerator Arrival_enables_dive() {
            var boot = Make(out _);
            for (int i = 0; i < 600 && !boot.DiveReady; i++) {
                yield return null;
            }
            Assert.IsTrue(boot.DiveReady, "목표 도달 시 잠수 가능해야");
            Assert.AreEqual(GameState.Surface, ((GameBootstrap)GetGame(boot)).State, "도달만으로는 아직 수상 상태");
        }

        [UnityTest]
        public IEnumerator Dive_hands_off_to_dock_without_advancing_target() {
            var boot = Make(out var nav);
            for (int i = 0; i < 600 && !boot.DiveReady; i++) {
                yield return null;
            }
            int targetBefore = nav.TargetIndex;
            int runTargetBefore = run.SurfaceTargetIdx;
            boot.RequestDive();
            for (int i = 0; i < 120 && boot.enabled; i++) {
                yield return null;
            }
            Assert.IsTrue(nav.gameObject.activeSelf, "모선(3D 잠수정)은 수면 거점으로 유지");
            Assert.IsTrue(rig.activeSelf, "환경(수면·스카이라인)은 2.5D 백드롭으로 유지");
            Assert.IsFalse(boot.enabled, "수상 코디네이터 종료");
            Assert.AreEqual(GameState.Dock, ((GameBootstrap)GetGame(boot)).State, "잠수 후 거점 인계");
            Assert.AreEqual(targetBefore, nav.TargetIndex, "잠수 시작만으로 다음 목표로 이동하면 안 됨");
            Assert.AreEqual(runTargetBefore, run.SurfaceTargetIdx, "정화 완료 전에는 수상 목표 기록을 전진시키면 안 됨");
        }

        [UnityTest]
        public IEnumerator Dive_refused_keeps_surface_rig_alive() {
            // 인계 거부(수상 상태가 아님) 시 리그가 죽지 않고 재시도 가능해야 — 데드 상태 방지(리뷰 Critical)
            var boot = Make(out _, false);   // Dock 시작 → Surface→Dock 인계 거부
            SetField(boot, "diveReady", true);   // 테스트 목적상 잠수 시퀀스까지 강제 진입
            LogAssert.Expect(LogType.Warning, "[SurfaceBootstrap] 잠수 인계 실패 — 수상 상태로 복귀");
            boot.RequestDive();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(rig.activeSelf, "인계 거부 시 수상 리그 유지");
            Assert.AreEqual(GameState.Dock, ((GameBootstrap)GetGame(boot)).State, "상태는 그대로 Dock");
        }

        [UnityTest]
        public IEnumerator Hub_open_close_does_not_advance_voyage() {
            // 정화지점 도착 후 연구/제작 허브를 열었다 닫아도 잠수정이 다음 목표로 전진하면 안 됨(제자리 유지)
            var boot = Make(out var nav);
            for (int i = 0; i < 600 && !boot.DiveReady; i++) {
                yield return null;
            }
            Assert.IsTrue(boot.DiveReady, "선행 조건 — 목표 도달");
            int targetBefore = nav.TargetIndex;
            var game = (GameBootstrap)GetGame(boot);
            game.GoResearch();   // Surface→Research(허브 열기)
            game.CloseHub();     // Research→Surface(허브 닫기) — 항해 재개되면 안 됨
            yield return null;
            Assert.IsFalse(nav.Sailing, "허브 닫기로 항해가 재개되면 안 됨");
            Assert.AreEqual(targetBefore, nav.TargetIndex, "다음 목표로 전진하면 안 됨");
            Assert.IsTrue(boot.DiveReady, "도착 지점 잠수 가능 상태 유지");
        }

        static object GetGame(SurfaceBootstrap boot) {
            return typeof(SurfaceBootstrap)
                .GetField("game", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(boot);
        }
    }
}
