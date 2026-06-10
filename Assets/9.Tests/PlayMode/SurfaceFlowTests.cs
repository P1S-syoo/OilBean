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
    // 수상 흐름 통합 테스트 — 항해→목표 도달→잠수 요청→Dock 인계·수상 목표 기록
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

        // 최소 리그: GameBootstrap(수상 시작) + 스플라인 항해 잠수정 + SurfaceBootstrap
        SurfaceBootstrap Make(out SubNavigator nav) {
            run = ScriptableObject.CreateInstance<RunData>();
            gameGo = new GameObject("Game");
            gameGo.SetActive(false);
            var game = gameGo.AddComponent<GameBootstrap>();
            SetField(game, "run", run);
            SetField(game, "startOnSurface", true);
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
            SetField(nav, "speed", 200f);
            SetField(nav, "targets", new[] { 0.9f });

            var boot = rig.AddComponent<SurfaceBootstrap>();
            SetField(boot, "game", game);
            SetField(boot, "nav", nav);
            SetField(boot, "blendTime", 0.05f);
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
        public IEnumerator Dive_hands_off_to_dock_and_records_target() {
            var boot = Make(out var nav);
            for (int i = 0; i < 600 && !boot.DiveReady; i++) {
                yield return null;
            }
            boot.RequestDive();
            for (int i = 0; i < 120 && rig.activeSelf; i++) {
                yield return null;
            }
            Assert.IsFalse(rig.activeSelf, "잠수 후 수상 리그 비활성");
            Assert.AreEqual(GameState.Dock, ((GameBootstrap)GetGame(boot)).State, "잠수 후 거점 인계");
            Assert.AreEqual(nav.TargetIndex + 1, run.SurfaceTargetIdx, "다음 수상 목표 기록(복귀 재개용)");
        }

        static object GetGame(SurfaceBootstrap boot) {
            return typeof(SurfaceBootstrap)
                .GetField("game", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(boot);
        }
    }
}
